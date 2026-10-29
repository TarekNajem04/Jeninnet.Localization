# Machine Translation

A translation provider is an ordinary message provider with an unusual cost model. The key you pass
is the source text, the requested culture is the target language, and the value comes from a remote
service. Every cache miss is a network call that may cost money, fail, or return something a human
must review.

That makes machine translation a product decision, not a drop-in replacement for a catalog. This
chapter covers the two providers, the Azure resilience and batching model, and the review workflow
that keeps machine output from becoming unreviewed production copy.

## What the providers actually do

`Jeninnet.Localization.Translation.Azure` targets Azure Translator v3 REST.
`Jeninnet.Localization.Translation.Google` targets the legacy Google Cloud Translation v2 REST
`translate` method. Both treat the message key as the source text and translate it into the requested
culture. An invariant-culture lookup returns `null` immediately without an HTTP request.

```csharp
services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.Region = configuration["AzureTranslator:Region"];
    options.CachingEnabled = true;
});
```

```csharp
services.AddGoogleTranslationLocalization(options => {
    options.ApiKey = configuration["GoogleTranslate:ApiKey"]!;
});
```

Both registrations validate immediately when the delegate overload is used: a blank `ApiKey` or an
invalid `Endpoint` fails at registration. The no-argument overloads read through `IOptions<T>` and
throw `InvalidOperationException` when the provider is constructed with invalid options.

**Pitfall:** registering a translation provider alongside a file or database provider. The
exactly-one rule throws at resolution. A translation provider cannot be layered behind a catalog, and
a catalog cannot be used as a fallback behind it. See
[The mental model](../01-getting-started/04-mental-model.md).

## Azure capabilities

Azure is the capable provider. It implements the optional capability contracts and reports them
honestly per instance:

- **Batch lookup** — `GetMessageTemplatesAsync` sends up to 100 keys per request.
- **Caching** — a bounded TTL (Time To Live) cache of 4096 entries, keyed by `(key, culture)`, default one hour.
  `Supports(Caching)` is false when `CachingEnabled` is false.
- **Single-flight coalescing** — concurrent lookups of the same key and culture share one acquisition.
  Batch calls join keys already in flight instead of starting a second request.
- **Preload** — `PreloadAsync` warms the cache for caller-supplied keys when caching is enabled.
- **Refresh** — `RefreshAsync` clears the cache and advances a generation so a pre-refresh completion
  cannot repopulate invalidated state. In-flight requests are not cancelled.
- **Events** — `TranslationAcquired` and `AcquisitionFailed` report external acquisition outcomes.
- **Translation memory** — `ITranslationMemory` is consulted before any paid call. A hit performs zero
  API calls and raises no acquisition event.
- **Glossary** — `MessageGlossary` forces per-culture term translations by replacing matched terms
  with placeholder tokens before the request and restoring the forced targets afterwards.

The resolution order for a cache miss is: translation memory, glossary-protected machine translation,
then TTL cache write-back. The glossary is snapshotted when the provider is constructed; later
registration on the same `MessageGlossary` does not change an existing provider. The translation
memory store reference is captured at construction, and its content may change; refresh the provider
to drop cached results that shadow updated entries.

Machine translations are never written into the translation memory. The memory holds approved
translations only. That is the line between a draft and a reviewed result.

## Azure resilience

Every chunk exchange runs through a provider-owned pipeline, outermost to innermost:
`Timeout → Retry → Breaker → RateLimit → HTTP`. The policies are Base Class Library (BCL)-only state machines, and all
timing uses an injectable clock so tests are deterministic.

| Option | Default | Behavior |
|---|---|---|
| `RequestTimeout` | 30 seconds | Total per-chunk budget covering attempts, backoff, and rate-limiter waits. Expiry surfaces as `OperationCanceledException`. |
| `RetryMaxAttempts` | 3 | HTTP attempts per chunk, including the first. Only network failures, timeouts, HTTP 408/429/5xx are retried. |
| `RetryBaseDelay` | 200 ms | Exponential backoff base (`base × 2^(n-1)`), capped by `RetryMaxDelay`, with up to 25% jitter. |
| `RetryMaxDelay` | 5 seconds | Cap for one backoff delay before jitter. |
| `BreakerFailureThreshold` | 5 | Consecutive transient attempt failures that open the breaker. |
| `BreakerOpenDuration` | 30 seconds | Open duration before a single half-open probe. Success closes; failure re-opens. |
| `RateLimitRequestsPerSecond` | 0 (disabled) | Client-side token-bucket refill rate. Positive values pace every HTTP attempt. |
| `RateLimitBurstSize` | 1 | Immediate burst capacity when rate limiting is enabled. |

A timeout is terminal and is never retried. A genuine caller cancellation is never retried, never
recorded by the breaker, and raises neither acquisition event. While the breaker is open, acquisitions
fail fast with `BrokenCircuitException` and no HTTP request is made.

Resilience state is per provider instance: one breaker and one token bucket shared by all keys,
cultures, and lookup paths. That is deliberate. A throttled subscription should slow the whole
provider, not one key.

## Batch administration

`TranslationBatchJob` backfills catalogs through the provider pipeline rather than bypassing it. It
takes the concrete `AzureTranslationProvider`, translates distinct keys across cultures sequentially,
and reports progress per 100-key window. The DI (Dependency Injection) registration exposes the provider as
`IInnerMessageProvider`; cast the single resolved instance, or register/construct the concrete type
yourself.

```csharp
var job = new TranslationBatchJob(azureProvider);

var estimate = await job.EstimateAsync(keys, cultures, cancellationToken);
// estimate.TotalKeys, FreeKeys, BillableKeys, BillableCalls

var result = await job.RunAsync(keys, cultures, progress, cancellationToken);
// result.TranslatedCount, CachedCount, FailedCount, Entries
```

`EstimateAsync` is a side-effect-free dry run. It simulates the real run's chunk windows against the
provider's local layers: cache and translation-memory hits count as free. The billable-call count
assumes a quiescent provider and fault-free transport; retries, concurrent writers, and cache expiry
change the real number. An immediate re-run of a completed backfill estimates zero calls while the
cache is warm.

`RunAsync` produces one ledger entry per distinct key per culture:

- `Translated` — a fresh service result. A `null` template means the service resolved the key as
  missing.
- `Cached` — served from the provider cache or translation memory.
- `Failed` — the chunk acquisition failed. The reason is recorded as `ExceptionType: message`.

Chunk failures never abort the batch. Duplicate keys are translated once, invariant cultures are
rejected up front, and an empty key set is a no-op. Cancellation throws
`TranslationBatchCancelledException` (an `OperationCanceledException`) carrying the partial ledger on
`PartialResult`, so completed work is not lost. The job performs no writes; applying the ledger to a
store or catalog is a separate reviewed step.

## Google's minimal surface

Google implements only the mandatory single-key lookup. `Supports` returns false for every feature:
no batching, no cache, no preload, no refresh, and no acquisition events. The canonical lookup passes
the caller's cancellation token to `SendAsync` and `ReadFromJsonAsync`; a cancellation surfaces as
`OperationCanceledException`, not as a missing message. A non-success response throws
`HttpRequestException`. A successful response without a translation resolves to `null`.

Use Google when the volume is low, the content is genuinely dynamic, and an external cache is not
needed. Do not use it as a high-traffic catalog source; every lookup is a paid network call.

## Cost and quality controls

Machine translation is cheapest when it never runs. Apply controls in this order:

1. **Do not translate at request time when a catalog can hold the result.** Translate in a batch,
   review the output, and serve it from a file or database provider. This also removes the remote
   service from the request path.
2. **Estimate before running.** Use `EstimateAsync` to see free keys and billable calls. A surprising
   number usually means the cache or translation memory is cold.
3. **Put approved terminology in a glossary.** Forced terms survive paraphrase through placeholder
   protection. A token the machine translation drops cannot be restored; the term is then absent
   rather than silently wrong, which is the safer failure.
4. **Put approved translations in translation memory.** Exact-match reuse is deterministic and free.
   The memory is not a machine-translation archive; curate it.
5. **Bound the cache.** Azure's TTL cache is bounded at 4096 entries; the oldest expiry is evicted
   under pressure. Do not treat it as a durable translation store.
6. **Review the ledger, not just the counts.** `FailedCount` tells you a chunk failed; `Entries` tells
   you which keys and why.

Quality review is a process, not a provider feature. The provider returns text without judging it.
A workable review loop is: batch-translate to a staging catalog, run the parity tool to catch missing
keys and placeholder drift, have a native reviewer edit the staged strings, then apply the approved
values through the governed store or the normal catalog release process.

**Pitfall:** assuming placeholders survive machine translation. They often do, but not reliably.
Run the placeholder check after every machine-translation batch. A translated template that lost
`{0}` will throw or render wrong at runtime.

**Pitfall:** writing machine output directly into translation memory. The memory is the approved
baseline; polluting it makes every future lookup trust unreviewed text.

**Pitfall:** enabling a high request rate against a throttled subscription. The rate limiter is
disabled by default to preserve throughput. Enable it deliberately and set a burst size that matches
the subscription's limits.

**Pitfall:** treating `RequestTimeout` as a per-attempt timeout. It bounds the whole chunk, including
retries and waits. A long retry chain can exhaust the budget before the last attempt starts.

## Worked example: a reviewed backfill

A product has 800 English keys and wants Arabic and Polish drafts. The team does not want request-time
translation.

1. A one-off job resolves the Azure provider and constructs `TranslationBatchJob`.
2. `EstimateAsync` reports 800 keys × 2 cultures, with 120 cache hits from an earlier run and 40
   billable calls. The job windows keys at 100 per request.
3. `RunAsync` writes a ledger. One Polish window fails with a timeout; 18 entries are marked
   `Failed`, the rest are `Translated` or `Cached`. The batch completes.
4. The team retries only the failed keys after the breaker closes. The successful entries are cached
   and cost nothing the second time.
5. The ledger's templates are written to a staging catalog. The parity tool reports 12 placeholder
   drifts and 3 missing keys. Reviewers fix them.
6. Approved strings are applied to the runtime catalog through the normal release process. The
   translation memory is updated only with reviewed values.
7. Production serves the released catalog from a JSON provider. The translation provider is not
   registered in production at all.

That last point is the important one. The provider was a build-time tool, not a runtime dependency.

## Where to go next

- [Provider reference](../03-reference/03-provider-reference.md) for the complete option matrix.
- [Formatting and plurals](04-formatting-and-plurals.md) for placeholder and plural behavior in
  translated templates.
- [Parity and quality gates](13-parity-and-quality-gates.md) for the post-translation checks.
- [Translation provider source](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md)
  for the provider-level contract notes.
