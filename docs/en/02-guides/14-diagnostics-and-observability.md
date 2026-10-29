# Diagnostics and Observability

Localization failures are quiet by default. A missing key returns `null`, a fallback resolves the
wrong language, and a translator's placeholder disappears. By the time a user reports the wrong text,
the request is long gone. Observability exists to make those failures visible while they are still
cheap to fix.

The framework exposes three complementary surfaces:

- **Traces and metrics** through BCL (Base Class Library) `ActivitySource` and `Meter`, named `Jeninnet.Localization`.
- **Missing-key reporting** through the opt-in `IMissingKeySink`.
- **Health checks** for the provider and the store, in the Diagnostics integration.

There is no OpenTelemetry package dependency. The hooks are listener-guarded, so a host without
diagnostics pays only a guard check and behaves exactly as it would otherwise.

## Traces

`LocalizationDiagnostics.Source` emits two activity names:

- `localization.lookup` — one activity per localizer call.
- `localization.fallback.step` — one child activity per provider query along the canonical fallback
  chain.

The root activity carries:

| Tag | Meaning |
|---|---|
| `localization.key` | The lookup key. For plural lookups, the singular key anchors the activity. For context lookups, the composite key may contain the EOT (End of Transmission) separator. |
| `localization.culture` | The requested culture. |
| `localization.found` | Whether a template resolved. Set on completion; absent on a faulted lookup. |

Each fallback step carries `localization.culture` and `localization.found`. The step's parent is the
lookup activity, so the trace shows the exact sequence `de-CH`, `de`, `en` without guessing.

A lookup that throws leaves the activity in error state (`ActivityStatusCode.Error`) and records no
hit or miss. That matches the runtime contract: a provider failure is not a miss, and the exception
propagates.

Template text is never tagged. Keys and cultures are considered developer identifiers; templates may
contain personal data or secrets. Do not encode personal data into keys either, because keys are
tagged.

Sampling behaves as expected: when a listener samples out an activity, no activity is created and
lookups still succeed. The pipeline does not force sampling.

## Metrics

`LocalizationDiagnostics.Meter` exposes five pipeline instruments and four Azure translation
instruments:

| Instrument | Unit | Meaning |
|---|---|---|
| `localization.lookup.hits` | `{lookup}` | Lookups that resolved a template. |
| `localization.lookup.misses` | `{lookup}` | Lookups that resolved nothing. |
| `localization.lookup.duration` | `ms` | Duration of a completed lookup. |
| `localization.fallback.steps` | `{step}` | Provider queries along the fallback chain. |
| `localization.translation.requests` | `{request}` | Outbound Azure translation requests. |
| `localization.translation.cache.hits` | `{lookup}` | Azure cache hits. |
| `localization.translation.cache.misses` | `{lookup}` | Azure cache misses. |
| `localization.translation.duration` | `ms` | Outbound Azure request duration. |

Metrics are tagged with the culture only. Keys never appear in metric tags; that keeps cardinality
bounded and avoids leaking content into a metrics backend. The translation instruments are emitted by
the Azure provider, not by Core.

`localization.fallback.steps` counts provider queries, not distinct cultures. A miss that queries the
requested culture and then the default culture contributes two steps. The duplicated default query
when the default equals the requested culture is visible here.

A faulted lookup records no hit, no miss, and no duration. The activity carries the error; the
counters stay clean so a provider outage is not mistaken for a coverage gap.

## Enabling traces and metrics

Consumers opt in on the SDK side with the stable names:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Jeninnet.Localization"))
    .WithMetrics(metrics => metrics.AddMeter("Jeninnet.Localization"));
```

The literal names are also available as `LocalizationDiagnostics.SOURCE_NAME`,
`LOOKUP_ACTIVITY_NAME`, `FALLBACK_STEP_ACTIVITY_NAME`, and the instrument-name constants. Prefer the
constants when the consuming project references Abstractions.

With no listener, `ActivitySource.HasListeners()` is false and no activity is created. With no meter
listener, each instrument's `Enabled` flag is false and no measurement is written. This is why the
hooks are safe to leave in a library.

The operator-side wiring, collector sketch, dashboard queries, and alert candidates are in the
[Observability cookbook](../04-operations/08-observability-cookbook.md).

## Missing keys

The localizer reports a miss only after the complete fallback chain resolves nothing. Intermediate
fallback steps never report, so a key that resolves through a parent culture is not a miss. A
`null` sink is the default and costs one null check.

`MissingKeyCollector` aggregates by `(key, culture)` with a hit count and the first call site. It is
bounded: `maxEntries` defaults to 1000, and misses beyond the cap increment `DroppedCount` instead of
growing memory. `GetSnapshot()` returns entries in ordinal order; `Clear()` resets both entries and
the dropped count.

`LoggerMissingKeySink` logs one structured warning per miss, with event id `1001` and the message
`Localization miss for key '{Key}' in culture '{Culture}' at '{CallSite}'.` Only identifiers are
logged; template text never crosses the boundary.

`CompositeMissingKeySink` fans out to several sinks in order, so a collector and a logger can share
one localizer:

```csharp
var collector = new MissingKeyCollector(maxEntries: 500);

services.AddMemoryLocalization(messages);
services.AddLocalization(options => {
    options.DefaultCulture = "en";
    options.MissingKeySink = new CompositeMissingKeySink(
        collector,
        new LoggerMissingKeySink(loggerFactory.CreateLogger("Localization")));
});
```

Sink failures are contained. A throwing sink does not break the lookup, by design: diagnostic
observers must never become a production dependency.

## Provider events and request-localization logging

Providers that declare `IProviderEventSource` raise:

- `TranslationAcquired` with the resolved templates and the target culture.
- `AcquisitionFailed` with the culture and the exception.

File providers raise these for initial load and reload. Azure raises them for external acquisition;
cache and translation-memory hits raise nothing. A caller cancellation raises no failure event.

The request-localization bridge logs one warning when the store is unreachable and the served set
falls back to the default culture alone. It uses event id `1` and the event name
`LocalizationStoreUnreachable`. That log line is the quickest explanation for "the site ignores
`Accept-Language` in this environment."

## Health checks

`AddLocalizationHealthChecks` registers up to two checks:

- `jeninnet-localization-provider` — resolves an explicit `ProviderProbeKey` for
  `ProviderProbeCulture` through the canonical `IMessageProvider`. Resolved is `Healthy`; reachable
  but unserved is `Degraded`; throwing is `Unhealthy` with the exception type in data.
- `jeninnet-localization-store` — probes `ILocalizationStore.GetSupportedCulturesAsync`. Reachable is
  `Healthy`; reachable with zero cultures is `Degraded`; throwing is `Unhealthy`.

```csharp
services.AddLocalizationHealthChecks(options => {
    options.ProviderProbeKey = "App.Title";
    options.ProviderProbeCulture = "en";
});

app.MapHealthChecks("/health");
```

The provider check registers only when a probe key is configured. The probe never dumps undefined
keys and never returns template text. The store check registers only when an `ILocalizationStore` is
already registered, so call the composition after the store registration.

Health check descriptions and data carry identifiers, counts, durations, and exception type names.
They do not carry templates or exception messages. That makes the endpoint safe to expose to
operators.

## Worked example: diagnosing a production miss

A user reports an untranslated checkout button. The trace shows a `localization.lookup` for
`Checkout.Pay` in `fr-CA` with `localization.found=false`, plus fallback steps for `fr-CA`, `fr`, and
the default `en`, all misses.

The missing-key collector, scraped from a diagnostic endpoint in staging, shows the same key and
culture with a call site in `CheckoutController`. That rules out a provider outage and points at a
missing `en` entry. The parity tool confirms `Checkout.Pay` is missing from the English catalog, so
the fix is a catalog entry, not a fallback configuration.

The metrics dashboard shows `localization.lookup.misses` rising for `fr-CA`; the trace explains why
the chain cannot recover.

## Pitfalls

**Pitfall:** tagging templates or parameter values. The framework never does; custom instrumentation
should not either. Keys and cultures are enough.

**Pitfall:** assuming metrics carry keys. They do not. Use traces for per-key diagnosis and metrics
for aggregate rates.

**Pitfall:** treating a faulted lookup as a miss. It records no hit or miss; the activity is in error
state and the exception propagates.

**Pitfall:** registering the store health check before the store. The check is simply not registered;
there is no error at startup.

**Pitfall:** pointing the provider probe at a key that is intentionally absent. A `Degraded` result
then means the probe is wrong, not the provider.

**Pitfall:** expecting `AcquisitionFailed` for caller cancellation. Cancellation is not a provider
failure and raises no event.

**Pitfall:** exposing health-check data without reviewing it. The framework keeps templates out, but
your own custom checks may not.

## Where to go next

- [Observability cookbook](../04-operations/08-observability-cookbook.md) for collector, dashboard,
  and alert recipes.
- [Diagnostics reference](../03-reference/06-diagnostics-reference.md) for every name, id, and
  severity in one place.
- [Troubleshooting](../04-operations/04-troubleshooting.md) for symptom-driven diagnosis.
- [Testing your localization](15-testing-your-localization.md) for activity and meter assertions.
- [Machine translation](12-machine-translation.md) for the translation-specific instruments.
