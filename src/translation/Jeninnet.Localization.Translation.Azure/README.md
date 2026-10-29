# Jeninnet.Localization.Translation.Azure

Azure Translator v3 REST provider for `Jeninnet.Localization`. The provider treats the message key as the source text and translates it into the requested culture. It is the capable translation provider: multi-key lookup, provider-owned caching, preload, refresh, acquisition events, a glossary, translation memory, and a resilience pipeline all participate through the canonical `IInnerMessageProvider`/`IMessageProvider` contract.

Machine translation is a product decision, not a drop-in catalog. The provider returns text without judging it; review and application belong to the translator workflow. See [Machine translation](../../../docs/en/02-guides/12-machine-translation.md) and the [translator workflow](../../../docs/en/02-guides/11-translator-workflow.md).

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.Azure
```

## Registration

The delegate overload validates the configured options immediately:

```csharp
using Jeninnet.Localization.Translation.Azure.Extensions;

services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.Region = configuration["AzureTranslator:Region"];
});
```

The no-argument overload leaves options to the standard Options pipeline; the provider validates the resolved options at construction:

```csharp
services.AddAzureTranslationLocalization();
services.Configure<AzureTranslationOptions>(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.Endpoint = "https://api.cognitive.microsofttranslator.com";
});
```

A blank `ApiKey`, an invalid endpoint, or an inconsistent option combination fails before the first translation call. The provider is registered as a singleton `IInnerMessageProvider` through `IHttpClientFactory`, so cache and in-flight state persist across resolutions. Core's exactly-one provider selector delegates to it when it is the single registered provider.

## Configuration

`AzureTranslationOptions` exposes the following settings:

| Option | Default | Meaning |
|----|----|----|
| `ApiKey` | required | Azure Translator subscription key, sent in `Ocp-Apim-Subscription-Key`. |
| `Endpoint` | `https://api.cognitive.microsofttranslator.com` | Base endpoint; must be an absolute `http`/`https` URI. |
| `Region` | empty | Sent in `Ocp-Apim-Subscription-Region` only when non-blank. |
| `FromLanguage` | empty | Source language sent only when non-blank; otherwise Azure auto-detects. |
| `CachingEnabled` | `true` | Enables the provider-owned TTL (Time To Live) cache. |
| `CacheExpiration` | 1 hour | Cache TTL; must be positive when caching is enabled. |
| `Glossary` | `null` | `MessageGlossary` of forced term translations. |
| `TranslationMemory` | `null` | `ITranslationMemory` of approved exact-match translations. |
| `RequestTimeout` | 30 seconds | Total per-chunk budget covering every attempt, backoff, and rate-limiter wait. |
| `RetryMaxAttempts` | 3 | HTTP attempts per chunk, including the first. |
| `RetryBaseDelay` | 200 ms | Exponential backoff base. |
| `RetryMaxDelay` | 5 seconds | Cap for one backoff delay before jitter. |
| `BreakerFailureThreshold` | 5 | Consecutive transient failures that open the breaker. |
| `BreakerOpenDuration` | 30 seconds | Open duration before one half-open probe. |
| `RateLimitRequestsPerSecond` | 0 (disabled) | Client-side token-bucket refill rate. |
| `RateLimitBurstSize` | 1 | Immediate burst capacity when rate limiting is enabled. |

Credentials are ordinary options, not built-in environment-variable names. Bind them from the host's configuration; environment variables follow the standard `__` nesting, for example `AzureTranslator__ApiKey`. Use User Secrets locally, a platform secret store in production, one key per environment, and never commit a key to `appsettings.json`.

## Usage

A reviewed backfill uses the provider pipeline rather than bypassing it. The job windows distinct keys at 100 per request, culture by culture, and produces a ledger:

```csharp
using Jeninnet.Localization.Translation.Azure;

var provider = (AzureTranslationProvider)services.GetRequiredService<IInnerMessageProvider>();
var job = new TranslationBatchJob(provider);

var keys = sourceCatalog.Keys.ToArray();          // scanned source keys = source text
var cultures = new[] { CultureInfo.GetCultureInfo("ar"), CultureInfo.GetCultureInfo("pl") };

var estimate = await job.EstimateAsync(keys, cultures, cancellationToken);
// estimate.TotalKeys, FreeKeys, BillableKeys, BillableCalls

var result = await job.RunAsync(keys, cultures, progress, cancellationToken);

foreach (var entry in result.Entries.Where(e => e.Status == TranslationBatchStatus.Failed)) {
    Console.Error.WriteLine($"{entry.Culture.Name}/{entry.Key}: {entry.FailureReason}");
}
```

The job performs no writes. Treat the ledger and resolved templates as input to the review step: compare placeholders with the source, have a native reviewer edit the drafts, then apply approved values through the governed store or the normal catalog release process. An empty key set is a no-op; invariant cultures are rejected.

For request-time lookups, inject `IMessageLocalizer` or resolve the canonical provider:

```csharp
var text = await localizer.LocalizeAsync("Checkout.Pay", parameters, culture, cancellationToken);
```

## Behavior and limits

**Lookup semantics.** The key is the source text and the requested culture is the target language. A translation-memory exact match short-circuits the paid call, then glossary- protected machine translation runs, and resolved results are written back to the TTL cache when caching is enabled. An invariant-culture lookup returns `null` immediately without an HTTP request. Concurrent lookups of the same `(key, culture)` share one in-flight acquisition. A batch sends at most 100 keys per request and joins keys already in flight; there is no whole-batch coalescing.

**Glossary and memory.** Glossary terms are snapshotted at provider construction; later registration on the same `MessageGlossary` does not change an existing provider. Matched terms are replaced with placeholder tokens before the request and restored to the forced targets afterward. Conflicting registrations are rejected through `GlossaryRegistrationResult.Conflicts`. Translation memory is consulted by exact source text and target culture; machine output is never written back into it. The store reference is captured at construction; call `RefreshAsync` to drop cached results that shadow updated entries.

**Resilience.** Every chunk runs through `Timeout → Retry → Breaker → RateLimit → HTTP`. Only idempotent-safe transient faults are retried: network failures, timeouts, HTTP 408/429/5xx. A total request timeout is terminal and is never retried; it surfaces as `OperationCanceledException`. While the breaker is open, acquisitions fail fast with `BrokenCircuitException` without an HTTP request. Resilience state is per provider instance: one breaker and one token bucket shared by every key, culture, and lookup path. A genuine caller cancellation is never retried, never recorded by the breaker, and raises no acquisition event.

**Caching and events.** The TTL cache is bounded at 4096 entries; expired entries are removed first, then the entry closest to expiry is evicted. `Supports` reports `Caching` and `Preload` as enabled only when `CachingEnabled` is true, and always reports `MultiKeys`, `Refresh`, and `EventsListening`. `TranslationAcquired` and `AcquisitionFailed` cover external acquisitions; cache and memory hits raise nothing. `RefreshAsync` clears the cache and advances a generation so pre-refresh completions cannot repopulate it; in-flight requests are not canceled. Handler exceptions propagate to the caller.

**Batch limits.** `EstimateAsync` is a side-effect-free simulation against the local layers; billable calls assume a quiescent provider and fault-free transport. `RunAsync` reports cumulative progress after every window, records chunk failures in the ledger without aborting the batch, and throws `TranslationBatchCancelledException` (an `OperationCanceledException` carrying `PartialResult`) when cancelled. The job writes nothing, so cancellation never leaves orphaned writes.

**Security.** The API key is sent as a request header, and the key text is sent to the remote service as source content. Pin the endpoint to the expected host, require HTTPS, restrict outbound access, and never put personal data or secrets into message keys. Do not log options or full requests. See the [security chapter](../../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Translation.Google](../Jeninnet.Localization.Translation.Google/README.md)
- [Jeninnet.Localization.Integrations.Diagnostics](../../../integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md)
- [Jeninnet.Localization.Abstractions](../../Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [Machine translation](../../../docs/en/02-guides/12-machine-translation.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Diagnostics and observability](../../../docs/en/02-guides/14-diagnostics-and-observability.md)
- [Security](../../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../../LICENSE).
