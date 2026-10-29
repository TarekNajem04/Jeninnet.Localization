# Jeninnet.Localization.Integrations.Diagnostics

Observability integration for `Jeninnet.Localization`. The package registers ASP.NET Core and `Microsoft.Extensions.Hosting` health checks for the message provider and the localization store. The tracing and metering hooks themselves live in `Jeninnet.Localization.Abstractions` (`LocalizationDiagnostics`) and need no package at all.

The health checks answer two operational questions without dumping catalog content: can the provider resolve a known key, and can the store report its supported cultures? See [Diagnostics and observability](../../docs/en/02-guides/14-diagnostics-and-observability.md) for traces, metrics, missing-key sinks, and the complete observability model.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.Diagnostics
```

The package depends on `Microsoft.Extensions.Diagnostics.HealthChecks` and `Jeninnet.Localization.Abstractions`. It is not coupled to the ASP.NET Core shared framework and works in any `Microsoft.Extensions.Hosting` host.

## Registration

Register the provider and, when it should be probed, the store before the health checks:

```csharp
using Jeninnet.Localization.Integrations.Diagnostics.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
services.AddSingleton<ILocalizationStore>(store);

services.AddLocalizationHealthChecks(options => {
    options.ProviderProbeKey = "App.Title";
    options.ProviderProbeCulture = "en";
});

var app = builder.Build();
app.MapHealthChecks("/health");
```

`AddLocalizationHealthChecks` registers up to two checks:

- `jeninnet-localization-provider` — only when `ProviderProbeKey` is configured.
- `jeninnet-localization-store` — only when an `ILocalizationStore` is already registered in the service collection.

Call it after the store registration when the store should be probed. Registering the composition twice throws.

## Configuration

`LocalizationHealthChecksOptions` exposes:

| Option | Default | Meaning |
|----|----|----|
| `ProviderProbeKey` | unset | Key resolved by the provider check. Unset means the check is not registered. |
| `ProviderProbeCulture` | unset | Culture used for the probe; unset uses the ambient UI culture at check time. |
| `ProviderCheckName` | `jeninnet-localization-provider` | Registered health-check name. |
| `StoreCheckName` | `jeninnet-localization-store` | Registered health-check name. |

Set the probe culture explicitly when the check must be deterministic. The probe key must be a real key the provider serves for that culture; pointing it at an intentionally absent key produces `Degraded`, which means the probe is wrong, not the provider.

## Usage

The endpoint is standard health-check plumbing:

```csharp
app.MapHealthChecks("/health");
```

The checks can also be resolved and run directly for tests or a custom endpoint:

```csharp
var check = new LocalizationProviderHealthCheck(provider, options);
var result = await check.CheckHealthAsync(new HealthCheckContext(), cancellationToken);
```

Health-check descriptions and data carry identifiers, counts, durations, and exception type names. They never carry template text or exception messages, which makes the endpoint safe to expose to operators.

## Behavior and limits

**Provider check.** Resolves `ProviderProbeKey` for `ProviderProbeCulture` through the canonical `IMessageProvider`. A resolved template is `Healthy`; the provider is reachable but does not serve the key is `Degraded`; a thrown exception is `Unhealthy` with the exception type in the result data. The probe never enumerates keys and never returns template text. It performs one exact-match provider lookup per check run.

**Store check.** Probes reachability through `ILocalizationStore.GetSupportedCulturesAsync`. A non-empty culture list is `Healthy`; reachable with zero cultures is `Degraded`; a thrown exception is `Unhealthy`. The result reports only the supported-culture count and duration, never store content.

**Registration rules.** The provider check needs an explicit probe key because the check must not dump undefined keys. The store check is only registered when the store exists at composition time; registering it earlier produces no check and no startup error, which is easy to miss. Registering `AddLocalizationHealthChecks` twice throws.

**Telemetry names.** The stable names live in Abstractions and do not require this package:

- `LocalizationDiagnostics.Source` emits `localization.lookup` and `localization.fallback.step`.
- `LocalizationDiagnostics.Meter` exposes `localization.lookup.hits`, `localization.lookup.misses`, `localization.lookup.duration`, `localization.fallback.steps`, and the Azure translation instruments `localization.translation.requests`, `localization.translation.cache.hits`, `localization.translation.cache.misses`, and `localization.translation.duration`.
- OpenTelemetry opt-in is host-side; this repository references no OpenTelemetry package. With no listener, emissions are skipped.

**Limits.** A health check is a point-in-time probe, not a continuous monitor. It can succeed while a different key or culture is broken. It also shares the provider's normal call path, so a translation provider probe can perform a paid external request. Prefer a cached or catalog-backed key for frequent probes, and keep the endpoint behind the host's operational access controls.

**Security.** Health-check output is designed to be template-free, but custom checks added to the same pipeline may not be. Review every check exposed on a public endpoint. See the [security chapter](../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Translation.Azure](../../src/translation/Jeninnet.Localization.Translation.Azure/README.md)
- [Jeninnet.Localization.Integrations.RequestLocalization](../Jeninnet.Localization.Integrations.RequestLocalization/README.md)
- [Jeninnet.Localization.Abstractions](../../src/Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [Diagnostics and observability](../../docs/en/02-guides/14-diagnostics-and-observability.md)
- [Diagnostics reference](../../docs/en/03-reference/06-diagnostics-reference.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)
- [Security](../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../LICENSE).
