# Jeninnet.Localization.Translation.DeepL

Machine translation provider that serves localized templates through the DeepL API v2 `translate` method: one paid request per cache miss, with batching, caching, preload, refresh, glossary, translation memory, retries, a circuit breaker, and rate limiting.

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.DeepL --version 1.0.0-preview.1
```

## Registration

```csharp
services.AddDeepLTranslationLocalization(options => {
    options.ApiKey = "<DeepL authentication key>";
});
```

Free-tier authentication keys must set `Endpoint` to `https://api-free.deepl.com`:

```csharp
services.AddDeepLTranslationLocalization(options => {
    options.ApiKey = "<DeepL authentication key>";
    options.Endpoint = "https://api-free.deepl.com";
});
```

## Configuration

`DeepLTranslationOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `ApiKey` | `string` | `""` | Required. Sent as the `Authorization: DeepL-Auth-Key` header. |
| `Endpoint` | `string` | `https://api.deepl.com` | Paid endpoint. Free-tier keys must use `https://api-free.deepl.com`. Must be an absolute http/https URI. |
| `FromLanguage` | `string` | `""` | Optional DeepL source language code (for example `EN`). Blank means auto-detect. |
| `CachingEnabled` | `bool` | `true` | Enables the provider-owned TTL cache. |
| `CacheExpiration` | `TimeSpan` | 1 hour | Must be positive when caching is enabled. |
| `Glossary` | `MessageGlossary?` | `null` | Forced term translations; snapshotted at construction. |
| `TranslationMemory` | `ITranslationMemory?` | `null` | Exact-match store consulted before machine translation. |
| `RequestTimeout` | `TimeSpan` | 30 seconds | Total budget per chunk, covering attempts, backoffs, and limiter waits. |
| `RetryMaxAttempts` | `int` | 3 | Attempts per chunk, including the first. Only transient faults retry. |
| `RetryBaseDelay` | `TimeSpan` | 200 ms | Exponential backoff base with up to 25% jitter. |
| `RetryMaxDelay` | `TimeSpan` | 5 seconds | Backoff cap before jitter. |
| `BreakerFailureThreshold` | `int` | 5 | Consecutive transient failures that open the breaker. |
| `BreakerOpenDuration` | `TimeSpan` | 30 seconds | Open duration before a half-open probe. |
| `RateLimitRequestsPerSecond` | `int` | 0 | Disabled by default; set to protect a throttled subscription. |
| `RateLimitBurstSize` | `int` | 1 | Burst capacity when rate limiting is enabled. |

Mutating the options object after registration does not affect the provider.

## Usage

A translation provider is the single message provider, not a layer behind one. The key is the source text; the target culture selects the DeepL target language (the upper-cased culture name, for example `de` becomes `DE`):

```csharp
var text = await localizer.LocalizeAsync(
    "Hello world",
    null,
    CultureInfo.GetCultureInfo("de"));
```

The result is the DeepL translation of `Hello world` into German. Invariant-culture lookups return `null` without an HTTP request. For genuinely dynamic content only; for normal products, translate catalogs offline and serve the result from a file or database provider.

## Behavior and limits

- **Capabilities.** Multi-key batching (up to 50 texts per request), caching when enabled, preload when caching is enabled, refresh, and acquisition events. `Supports` returns `false` for every other feature.
- **Lookup order.** Translation memory (exact match) short-circuits the paid call; otherwise glossary-protected machine translation; then TTL cache write-back. A memory-only hit raises no event.
- **Glossary.** Matched source terms are replaced by opaque `[[gN]]` tokens before the request and restored to the forced targets in the translated result. Terms are snapshotted at construction.
- **Batching.** `GetMessageTemplatesAsync` groups cache-missed keys into 50-text chunks; keys already in flight are joined instead of re-acquired. There is no governed batch-ledger job on this provider; use the multi-key path for windowed backfills.
- **Resilience.** Hand-rolled BCL-only pipeline: per-chunk timeout, idempotent-safe retries (network failures, timeouts, HTTP 408/429/5xx), a circuit breaker with half-open probes (`BrokenCircuitException` on fail-fast rejection), and a client-side token bucket. A genuine caller cancellation raises neither acquisition event.
- **Language codes.** The target language is the upper-cased culture name. Cultures outside the DeepL language list fail with the service error (`DeepL Translate returned ...`); the provider performs no silent code mapping. `FromLanguage` is sent as `source_lang` when set, otherwise the service auto-detects.
- **Request shape.** One JSON request per chunk (`text` array, `target_lang`, optional `source_lang`); response translations align with request texts by index. Only `text`, `target_lang`, and `source_lang` are sent: no formality, split-sentence, or server-side glossary options.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. Request/response payloads serialize through a source-generated `JsonSerializerContext`. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Translation.Azure](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md) — same capability model over Azure Translator, with a governed batch-ledger job
- [Jeninnet.Localization.Translation.Google](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Google/README.md) — minimal single-key translation without cache or batching

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [Machine translation](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/12-machine-translation.html)
- [Provider reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/03-provider-reference.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
