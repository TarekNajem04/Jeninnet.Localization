# Diagnostics Reference

This chapter collects the stable observability and build-diagnostic names in one place. The names are
contracts: changing an ID, activity name, instrument name, or metric tag is a breaking change for
dashboards, alerts, and suppressions.

## Roslyn diagnostics

### Generator: `Jeninnet.Localization.Generators.Keys`

All six are build errors with a file and line where the input supplies one. There is no silent skip.

| ID | Meaning | Fix |
|---|---|---|
| `JLNKEY001` | The catalog is not well-formed JSON, or the root is not a JSON object. | Correct the JSON syntax or the root shape. |
| `JLNKEY002` | An entry does not map a string key to a string template. | Use only string keys and string values. |
| `JLNKEY003` | The same key occurs more than once in one catalog file. | Remove the duplicate; the message names the first line. |
| `JLNKEY004` | Two distinct keys sanitize to the same C# identifier. | Rename one key or change the boundary that causes the collision. |
| `JLNKEY005` | A key contains no identifier characters. | Rename the key to include a letter, digit, or underscore. |
| `JLNKEY006` | The configured namespace is not a legal dotted C# identifier. | Correct `LocalizationKeysNamespace` or remove the override. |

### Analyzer: `Jeninnet.Localization.Analyzers.Localizability`

| ID | Default severity | Meaning | Fix |
|---|---|---|---|
| `JLNLOC001` | Hidden | A hardcoded user-facing string sits outside the message pipeline. | Move the text to a catalog and resolve it through `IMessageLocalizer`; when the literal is intentional, suppress with justification. |

`JLNLOC001` can be raised to warning or error through `LocalizabilitySeverity` (declared visible with
`CompilerVisibleProperty`) or `dotnet_diagnostic.JLNLOC001.severity`. `.editorconfig` wins on
conflict. Suppress with `#pragma warning disable JLNLOC001`, `[SuppressMessage]`, or a folder
section.

The code fix is offered only when the literal exactly equals a `const string` in a type whose name
ends in `Keys`. It replaces the literal with a fully qualified constant reference. There is no
fix-all provider and no automatic extraction into a catalog.

### Parity tool findings

The parity tool is not a Roslyn analyzer, so these are finding kinds, not diagnostic IDs. They are
stable strings in the JSON report:

| Kind | Meaning |
|---|---|
| `missing-key` | A referenced key is absent from a culture and its fallback chain. |
| `unused-key` | A catalog key is never referenced in source. |
| `template-drift` | One culture/key has different templates across formats. |
| `format-gap` | A key is served in one format but absent from another serving format. |
| `placeholder-drift` | A culture's placeholder set differs from the reference culture. |
| `unresolved-reference` | A `*Keys` reference has no constant definition. |
| `definition-conflict` | One constant name has different values. |
| `catalog-error` | A catalog cannot be read or parsed. |

The tool exits `0` clean, `1` with findings, and `2` on usage or environment errors. It has no SARIF
emitter.

## Tracing

Source name: `Jeninnet.Localization`. Type: `System.Diagnostics.ActivitySource`. No OpenTelemetry
package is referenced.

| Activity name | Parent | Tags | Meaning |
|---|---|---|---|
| `localization.lookup` | Current activity | `localization.key`, `localization.culture`, `localization.found` | One localizer call. |
| `localization.fallback.step` | The lookup activity | `localization.culture`, `localization.found` | One provider query along the fallback chain. |

Tag semantics:

- `localization.key` — the requested key. A plural lookup uses the singular key. A context lookup
  may carry the EOT (End of Transmission) composite key.
- `localization.culture` — the culture of the lookup or the step.
- `localization.found` — set on completion; absent while running and on a faulted lookup.

Template text is never tagged. A faulted lookup sets `ActivityStatusCode.Error` and records no hit,
miss, or duration. Sampling is respected: a sampled-out activity is not created.

Opt in with `AddSource("Jeninnet.Localization")`.

## Metrics

Meter name: `Jeninnet.Localization`. Type: `System.Diagnostics.Metrics.Meter`.

| Instrument | Type | Unit | Tags | Meaning |
|---|---|---|---|---|
| `localization.lookup.hits` | Counter\<long\> | `{lookup}` | culture | Completed lookups that resolved a template. |
| `localization.lookup.misses` | Counter\<long\> | `{lookup}` | culture | Completed lookups that resolved nothing. |
| `localization.lookup.duration` | Histogram\<double\> | `ms` | culture | Duration of a completed lookup. |
| `localization.fallback.steps` | Counter\<long\> | `{step}` | culture | Provider queries along the fallback chain. |
| `localization.translation.requests` | Counter\<long\> | `{request}` | culture | Outbound Azure translation requests. |
| `localization.translation.cache.hits` | Counter\<long\> | `{lookup}` | culture | Azure cache hits. |
| `localization.translation.cache.misses` | Counter\<long\> | `{lookup}` | culture | Azure cache misses. |
| `localization.translation.duration` | Histogram\<double\> | `ms` | culture | Outbound Azure request duration. |

Keys never appear in metric tags. The translation instruments are emitted by the Azure provider, not
by Core. With no meter listener, instruments are not written.

Opt in with `AddMeter("Jeninnet.Localization")`.

## Events and event IDs

### Log events

| Event ID | Name or message | Level | Source | Meaning |
|---|---|---|---|---|
| `1001` | `Localization miss for key '{Key}' in culture '{Culture}' at '{CallSite}'.` | Warning | `LoggerMissingKeySink` | One lookup miss after the full fallback chain. |
| `1` | `LocalizationStoreUnreachable` | Warning | Request-localization bridge | The store was unreachable; the served set fell back to the default culture alone. |

Only identifiers are logged. Template text never crosses the missing-key boundary.

### Provider events

`IProviderEventSource` declares two .NET events, not ETW/EventSource events:

| Event | Payload | When |
|---|---|---|
| `TranslationAcquired` | `ProviderAcquisitionEventArgs` (`Templates`, `Culture`) | External acquisition succeeded. Cache hits do not raise it. |
| `AcquisitionFailed` | `ProviderAcquisitionFailedEventArgs` (`Culture`, `Exception`) | External acquisition failed. Caller cancellation does not raise it. |

The file providers raise these for initial load and reload. Azure raises them for external
acquisition. Handler exceptions propagate to the caller.

### Missing-key sink

`IMissingKeySink.ReportMiss(key, culture, callSite)` is the extension point. Core provides
`MissingKeyCollector`, `LoggerMissingKeySink`, and `CompositeMissingKeySink`. The localizer contains
sink exceptions.

## Health checks

Package: `Jeninnet.Localization.Integrations.Diagnostics`. Registration:
`AddLocalizationHealthChecks`.

| Check name | Probe | Healthy | Degraded | Unhealthy |
|---|---|---|---|---|
| `jeninnet-localization-provider` | `ProviderProbeKey` for `ProviderProbeCulture` through `IMessageProvider` | Key resolved | Provider reachable, key not served | Probe threw |
| `jeninnet-localization-store` | `ILocalizationStore.GetSupportedCulturesAsync` | One or more cultures | Reachable, zero cultures | Probe threw |

Result data carries identifiers, counts, durations, and exception type names. It never carries
template text or exception messages. The provider check registers only when a probe key is configured;
the store check registers only when a store is already registered.

Options: `ProviderProbeKey`, `ProviderProbeCulture`, `ProviderCheckName` (default
`jeninnet-localization-provider`), `StoreCheckName` (default `jeninnet-localization-store`).

## Diagnostic boundaries

- **A miss is not an error.** No diagnostic ID or unhealthy status is raised for an ordinary miss.
- **A provider failure is not a miss.** It propagates and leaves the trace activity in error state.
- **Cancellation is not a failure event.** It propagates as `OperationCanceledException` and raises
  no acquisition event.
- **A disabled sink costs one null check.** No allocation and no behavior change.
- **There is no EventSource/ETW provider.** Use `ActivitySource`, `Meter`, `ILogger`, health checks,
  and the sink.

## Worked example: reading one trace

A `de-CH` request that resolves through `de` produces a `localization.lookup` activity with
`localization.culture=de-CH` and `localization.found=true`, plus two `localization.fallback.step`
children: `de-CH` with `found=false` and `de` with `found=true`. The metrics show one hit, two fallback
steps, and one duration sample tagged `de-CH`.

## Pitfalls

**Pitfall:** tagging template text or parameter values. Keys and cultures are the intended tags.

**Pitfall:** treating a faulted lookup as a miss. It records no hit or miss; the activity is in error
state and the exception propagates.

**Pitfall:** expecting SARIF from the parity tool. It emits console output and versioned JSON only.

## Where to go next

- [Diagnostics and observability](../02-guides/14-diagnostics-and-observability.md) for usage.
- [Observability cookbook](../04-operations/08-observability-cookbook.md) for collector, dashboard,
  and alert recipes.
- [Tooling reference](05-tooling-reference.md) for the parity CLI and generator configuration.
- [Compatibility and versions](07-compatibility-and-versions.md) for the stability policy behind these
  names.
- [Troubleshooting](../04-operations/04-troubleshooting.md) for symptom-driven diagnosis.
