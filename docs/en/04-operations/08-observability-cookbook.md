# Observability Cookbook

The [diagnostics guide](../02-guides/14-diagnostics-and-observability.md) explains the signals the
library emits. This page turns those signals into an operator setup: OpenTelemetry wiring, a
collector sketch, dashboard panels, alert candidates, and an investigation flow.

The library has no OpenTelemetry package dependency. Everything here is host-side configuration. The
instrument and activity names are stable; the exporter and backend are yours to choose.

## What to collect

| Signal | Kind | Tags / labels | Use |
|---|---|---|---|
| `localization.lookup` | Activity | `localization.key`, `localization.culture`, `localization.found` | Per-key diagnosis and fallback chain |
| `localization.fallback.step` | Child activity | `localization.culture`, `localization.found` | Which culture in the chain answered or missed |
| `localization.lookup.hits` | Counter | `localization.culture` | Aggregate hit volume |
| `localization.lookup.misses` | Counter | `localization.culture` | Coverage gaps and fallback pressure |
| `localization.lookup.duration` | Histogram (ms) | `localization.culture` | Lookup latency and tail |
| `localization.fallback.steps` | Counter | `localization.culture` | Provider queries per lookup |
| `localization.translation.requests` | Counter | `localization.culture` | Azure outbound request volume |
| `localization.translation.cache.hits` | Counter | `localization.culture` | Azure cache effectiveness |
| `localization.translation.cache.misses` | Counter | `localization.culture` | Azure cache misses |
| `localization.translation.duration` | Histogram (ms) | `localization.culture` | Azure request latency |
| `jeninnet-localization-provider` | Health check | Status + data | Provider reachability and probe result |
| `jeninnet-localization-store` | Health check | Status + data | Store reachability and served cultures |
| Missing-key sink | Log / snapshot | Key, culture, call site | Closed-loop gap collection |

Keys appear only on traces. Metrics carry the culture only, which keeps cardinality bounded and
keeps content out of the metrics backend. Template text is never emitted by the framework.

## Minimal OpenTelemetry wiring

Add the OpenTelemetry SDK and exporter packages to the host; the library does not reference them:

```csharp
using Jeninnet.Localization.Abstractions.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("checkout"))
    .WithTracing(tracing => tracing
        .AddSource(LocalizationDiagnostics.SOURCE_NAME)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(LocalizationDiagnostics.SOURCE_NAME)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

Prefer the constants (`LocalizationDiagnostics.SOURCE_NAME`,
`LOOKUP_ACTIVITY_NAME`, `FALLBACK_STEP_ACTIVITY_NAME`, and the instrument-name constants) over string
literals when the host references Abstractions. The literal names are stable too.

Add the health checks after the provider and store registrations:

```csharp
services.AddLocalizationHealthChecks(options => {
    options.ProviderProbeKey = "App.Title";
    options.ProviderProbeCulture = "en";
});

app.MapHealthChecks("/health");
```

The provider check registers only when a probe key is configured. The store check registers only
when an `ILocalizationStore` is already registered.

## Collector sketch

This is a starting point, not a supported deployment artifact. Replace the backend endpoint and the
exporter choices with the ones your platform provides.

```yaml
receivers:
  otlp:
    protocols:
      grpc:
      http:

processors:
  memory_limiter:
    check_interval: 1s
    limit_mib: 256
  batch:

exporters:
  prometheus:
    endpoint: "0.0.0.0:9464"
  otlp/backend:
    endpoint: "backend:4317"
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [otlp/backend]
    metrics:
      receivers: [otlp]
      processors: [memory_limiter, batch]
      exporters: [prometheus]
```

For a smaller setup, send both signals directly to the backend and skip the collector. A collector is
useful when you need batching, sampling, redaction, or a single egress point.

## Dashboard panels

The queries below assume the OpenTelemetry .NET Prometheus exporter's normal name normalization:
dots become underscores and counters gain a `_total` suffix. Check the actual names in your backend
before copying them into alerts.

| Panel | PromQL starting point | Why it matters |
|---|---|---|
| Hit ratio by culture | `sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) / (sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) + sum by (localization_culture) (rate(localization_lookup_misses_total[5m])))` | Coverage and fallback pressure |
| Miss rate by culture | `sum by (localization_culture) (rate(localization_lookup_misses_total[5m])) / (sum by (localization_culture) (rate(localization_lookup_hits_total[5m])) + sum by (localization_culture) (rate(localization_lookup_misses_total[5m])))` | Same data, easier to alert on |
| p95 lookup duration | `histogram_quantile(0.95, sum by (le, localization_culture) (rate(localization_lookup_duration_bucket[5m])))` | Tail latency; duration is in milliseconds |
| Fallback steps per lookup | `sum by (localization_culture) (rate(localization_fallback_steps_total[5m])) / sum by (localization_culture) (rate(localization_lookup_hits_total[5m]) + rate(localization_lookup_misses_total[5m]))` | Values above 1 show incomplete catalogs or misses |
| Azure cache hit ratio | `sum by (localization_culture) (rate(localization_translation_cache_hits_total[5m])) / (sum by (localization_culture) (rate(localization_translation_cache_hits_total[5m])) + sum by (localization_culture) (rate(localization_translation_cache_misses_total[5m])))` | Whether translation cache is earning its cost |
| p95 translation duration | `histogram_quantile(0.95, sum by (le, localization_culture) (rate(localization_translation_duration_bucket[5m])))` | External service latency |
| Health status | Your health-check exporter's status series | Provider/store availability |

`localization.fallback.steps` counts provider queries, so a normal exact hit should be `1` per lookup.
Values above `1` mean the requested culture did not answer: `2` is one extra step (a parent or the
default), and `3` is a deeper parent chain or a repeated default query. The exact shape depends on
the requested culture and the configured default.

Faults are not counted as hits or misses. Use the trace backend to query activities with
`ActivityStatusCode.Error`, or use your HTTP/database instrumentation for the dependency failure. The
metrics deliberately stay clean so an outage is not reported as a coverage gap.

## Alert candidates

These are starting points, not universal thresholds. Baseline them against your own traffic and
catalog completeness.

| Alert | Starting condition | Interpretation |
|---|---|---|
| Miss ratio regression | Miss ratio by culture is above its 7-day baseline for 15 minutes, or above 5% in a culture that is expected to be complete | A catalog entry, deployment, or provider path regressed |
| Fallback depth regression | Fallback steps per lookup is above 1.5 for 15 minutes | A culture is missing keys and is falling back more often |
| Lookup latency | p95 duration is above the service's latency budget for 15 minutes | Provider I/O, fallback depth, or a cold catalog |
| Translation latency | p95 translation duration is above the provider's expected range for 15 minutes | External service or network degradation |
| Translation cache regression | Cache hit ratio drops sharply against its baseline | Cache size, key churn, or restart behavior changed |
| Provider/store health | Either health check is `Unhealthy` or remains `Degraded` for 10 minutes | Dependency unavailable, or the probe key/culture is wrong |
| Missing-key log spike | Event id `1001` rate rises sharply | New untranslated keys, wrong culture, or a missing catalog |
| Missing-key collector drops | `DroppedCount` rises | The miss volume exceeds the collector cap; increase it or fix the source |

A `Degraded` provider health check with a deliberately absent probe key is a probe bug, not a provider
outage. Use a key that is expected to resolve in the probe culture.

## Investigation flow

1. **Start with the miss rate, not the raw hit count.** A traffic drop lowers both hits and misses; a
   coverage regression raises the ratio. Break it down by `localization_culture`.
2. **Check fallback depth.** If steps per lookup rises with misses, the requested culture is
   incomplete. If misses rise without a step change, the provider itself is returning nothing.
3. **Open a trace for one key/culture.** `localization.lookup` shows the key, culture, and `found`
   tag; each `localization.fallback.step` child shows which culture missed or answered. The trace is
   the fastest way to distinguish fallback from provider failure.
4. **Check the missing-key sink.** The collector or event id `1001` gives the key, culture, and call
   site. After the fix, clear or reset the collector and confirm the rate drops.
5. **Confirm with the parity tool.** If the trace says the key is missing from the default culture,
   run the parity check and add the catalog entry; do not change fallback configuration to mask it.
6. **For latency, separate provider time from pipeline time.** A lookup with one step and a high
   duration points at the provider. A lookup with several steps points at fallback depth or catalog
   completeness. Translation latency is a dependency cost, not a Core cost.
7. **For faults, use the trace error status and dependency instrumentation.** A faulted lookup has no
   hit, miss, or duration; the exception propagates. Do not page on a missing metric when the trace
   already says the dependency threw.

## Cardinality, sampling, and cost

- **Keys stay on traces.** Do not add key, user, tenant, or request-id labels to metrics. The built-in
  metrics carry only `localization.culture`, so cardinality is bounded by the supported cultures.
- **Culture cardinality is bounded by design.** If a host accepts arbitrary culture tags, validate
  them against the supported set before lookup; otherwise the culture label can grow.
- **Missing-key collection is capped.** `MissingKeyCollector` drops beyond `maxEntries` and increments
  `DroppedCount`; watch both. Do not encode personal data into keys, because keys are tagged on traces
  and may appear in logs.
- **Sampling affects traces, not counters.** A sampled-out activity is not created; metrics still
  record hits, misses, duration, and fallback steps. Use metrics for aggregate alerts and traces for
  per-key diagnosis.
- **Observers pay recording cost.** With listeners attached, activity and meter recording is more
  expensive than the listener-guarded no-op path; the measured snapshot is in
  [Performance evidence](07-performance-evidence.md). Budget for it when enabling high-cardinality
  tracing in production.

## Security and privacy

- The framework never tags template text. Keep it that way in custom instrumentation and logs.
- Keys are developer identifiers, not a safe place for user input. Do not build keys from names,
  emails, or account data.
- Health-check descriptions and data carry identifiers, counts, durations, and exception type names,
  not templates or exception messages. Review custom checks before exposing the endpoint.
- The missing-key logger records key, culture, and call site. Treat those as operational metadata and
  apply the same access controls as other application logs.

## Related documentation

- [Diagnostics and observability](../02-guides/14-diagnostics-and-observability.md)
- [Diagnostics reference](../03-reference/06-diagnostics-reference.md)
- [Performance](03-performance.md)
- [Performance evidence](07-performance-evidence.md)
- [Troubleshooting](04-troubleshooting.md)
- [Security](02-security.md)
- [Testing your localization](../02-guides/15-testing-your-localization.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
