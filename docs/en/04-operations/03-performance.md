# Performance

Localization performance is mostly the performance of whatever the provider does. The localizer
adds a small amount of work around it: argument checks, a fallback walk, a formatter call, and
optional diagnostics. That is good news for capacity planning, because the provider choice and
the catalog shape, not the pipeline, determine the cost of a lookup. It is also a warning: a fast
formatter cannot rescue a per-request database round trip or a remote translation call.

This chapter separates structural facts (what the code does, verified in source) from guidance
(what to try). It does not turn benchmark numbers into a capacity promise. The repository contains
evidence-style performance tests that record measurements, but they assert validity, not thresholds,
so their numbers are only meaningful for the machine and run that produced them. One reproducible
snapshot is published in [Performance evidence](07-performance-evidence.md).

## The hot path

A `LocalizeAsync` call performs:

1. null checks and cancellation checks;
2. provider selection through `LocalizationProviderSelector`;
3. one provider query per fallback step, stopping at the first template;
4. a miss report to the optional sink, or a formatter call on a hit;
5. listener-guarded diagnostics.

Providers are exact-match, so the fallback walk is the only loop. The invariant culture is never
queried, and the default culture is queried once after the parent walk. A miss is therefore bounded
by the culture depth plus one; a hit is bounded by the position of the first match. In the common
case where the requested culture has the key, there is one provider query.

Exceptions are not converted into misses. Database failures, malformed catalogs, cancellation, and
formatter errors propagate, so the cost of an unhealthy dependency is visible to the caller rather
than silently becoming a fallback. See [The mental model](../01-getting-started/04-mental-model.md).

## Provider costs

| Provider | Steady-state lookup | First-use cost | Notes |
|---|---|---|---|
| Memory | Ordinal dictionary lookup | Copies the dictionary at construction | No I/O; one flat map, culture ignored. |
| JSON / XML / PO (Portable Object) | Ordinal dictionary lookup after load | Parses the file on the first lookup | The loaded table is immutable; a failed load is not cached. |
| ResX / satellite | `ResourceManager` resource-set lookup | Resource set load on first use | Exact culture, parent probing disabled. |
| Database (SQLite/SQL Server/MySQL/PostgreSQL) | Open connection, execute scalar, close | Connection pool warm-up | One round trip per fallback step; no in-box message cache. |
| EF Core store | Query per call through a context factory | Context and query compilation warm-up | Administration writes add transaction, journal, audit, and version work. |
| Azure Translator | Cache hit, otherwise HTTP | Network, JSON, cache fill | TTL (Time To Live) cache, single-flight, batching up to 100 keys, preload, refresh. |
| Google Translate | HTTP per lookup | Network, JSON | No provider cache, batching, preload, or refresh. |

The file providers load lazily and publish the completed table atomically. After that, lookup is a
dictionary operation and does not touch the filesystem. Watching is a separate cost: a file-system
watcher and a debounce timer exist only when `WatchForChanges` is enabled.

The database providers open a connection per lookup through the ADO.NET provider. Connection
pooling is the vendor's, not the library's; the library does not keep a connection or a command
alive between calls. There is no message cache in the box. If a page performs twenty lookups in a
request and the provider is a database, that can be twenty round trips before fallback steps are
counted.

The EF Core store is a different path. Reads go through `IDbContextFactory` and a fresh context per
call. Governed mutations are more expensive by design: they write the catalog row, append replay
journal rows, append hash-chained audit records, and advance the catalog version in one
transaction. History reads are never cached. Budget for administration separately from runtime
lookups.

Translation is network-bound. Azure's default cache expiration is one hour (`CachingEnabled` is
true by default); the cache is per provider instance, and concurrent lookups of the same
key/culture share one in-flight acquisition. `PreloadAsync` warms caller-supplied keys, and
`RefreshAsync` drops cache and in-flight state. Google has none of those facilities, so every
lookup is a remote call unless the host adds its own cache.

## Caching layers that exist

Do not assume a cache that is not in the source. The in-box layers are:

- **File-provider snapshots.** JSON, XML, and PO load once and serve an immutable table. This is a
  snapshot, not a TTL cache; `RefreshAsync` or a watched reload replaces it.
- **Memory-provider snapshot.** The dictionary is copied at construction and never refreshed.
- **Azure translation cache.** A BCL (Base Class Library) TTL cache with single-flight coalescing, default one hour,
  disabled only by `CachingEnabled = false`.
- **Azure translation memory and glossary.** An exact-match translation memory short-circuits the
  external call; a glossary forces terms before machine translation. Neither is a general cache.
- **`CachedLocalizationStoreDecorator`.** Wraps an `ILocalizationStore` with `IMemoryCache`.
  Resource-set entries have a thirty-minute cache duration; a successful `ReloadAsync` and applied
  governed mutations invalidate the affected state.
- **`DistributedLocalizationStoreDecorator`.** Wraps a store with `IDistributedCache`, default
  thirty-minute expiration, per-node single-flight, and generation-stamp invalidation. Synchronous
  `TryGet*` members bypass the distributed cache by design because the abstraction is async-only.

The store decorators are not registered by an `Add...` extension; wire them explicitly. Cache
decorators are per process unless you use the distributed one. A cache outage falls back to the
inner store, and cancellation is never swallowed. The database message providers, ResX, satellite,
and Google translation have no message cache.

## Allocation notes

These are structural observations from the source, not measured allocation numbers:

- The memory and loaded-file lookup paths allocate no per-lookup collection; they call
  `Dictionary.TryGetValue` and return the existing string.
- `LocalizeWithContextAsync` builds a composite `context + '\u0004' + key` string when the context
  is non-empty. That allocation is per call.
- `DefaultMessageFormatter` runs a compiled regex replace and allocates the result string. The ICU (International Components for Unicode)
  and Markdown formatters allocate parser/builder state per call; none of them cache rendered
  templates.
- Diagnostics are listener-guarded. With no `ActivityListener` and no enabled instruments, the
  pipeline skips activity and measurement creation. When listeners are present, recording costs are
  paid by the observer.
- The missing-key sink is optional. With a null sink, the miss path is a null check. With a sink,
  call-site capture walks the stack.
- The database and translation paths allocate commands, requests, and response objects per call;
  those costs belong to the dependency boundary.

## Scaling guidance

**Memory and file providers.** Scale out by deploying the same content to every instance. Each
instance loads its own snapshot; there is no cross-instance cache to coordinate. File watching is
per instance and should usually be off in production, where the image or mounted volume is the
deployment unit.

**Database providers.** Scale reads with connection pooling, a correct unique index on
`(MessageKey, Culture)`, and enough database capacity for one scalar query per fallback step. Use a
read replica if replica lag is acceptable; otherwise read from the primary. If the query volume is
too high, add `CachedLocalizationStoreDecorator` only for store reads; the message providers do not
accept a decorator. For message lookups, reduce round trips by resolving the key once per request
or by caching at the application layer with a deliberate invalidation strategy.

**EF Core store.** Reads and writes share the database. The store serializes governed mutations per
instance and retries the version-counter update up to five times before throwing
`DbUpdateConcurrencyException`; cross-process writers are not coordinated by that in-process gate.
Add constraints or serialize writers externally, and keep administration traffic off read
replicas. The distributed decorator is the in-box way to share read cache across nodes.

**Translation providers.** Prefer offline or batch translation for catalog content. At request
time, Azure's cache, single-flight, batching, and rate limiter are the controls you have; Google
offers none. Set a request timeout and a rate limit appropriate to the subscription, and expect a
cache miss to cost a network round trip. A translation provider is the single provider, so there is
no file catalog behind it to absorb failures.

**Formatting.** The default formatter is cheap relative to I/O but not free. For a very hot
template family, cache the formatted result at the call site keyed by culture and arguments. Do not
add a cache inside a shared formatter: it has no invalidation signal and parameters vary per call.

## Measure before optimizing

Use a measurement loop that matches the production path:

1. **Define the scenario.** Key mix, culture chain, hit/miss ratio, provider, cache state, and
   concurrency. A benchmark of an exact hit in the requested culture does not predict a page that
   misses into the default culture twenty times.
2. **Separate cold and warm.** File parsing, resource-set loading, connection-pool warm-up, and
   translation cache fills belong to the cold path. Measure them separately from steady state.
3. **Measure the distribution.** Record p50, p95, and p99, not only an average. Fallback depth and
   database latency are tail-heavy.
4. **Count the work.** Provider queries per lookup, fallback steps, database round trips, cache
   hits and misses, and translation requests explain a latency change better than a single elapsed
   time.
5. **Measure allocations.** `GC.GetAllocatedBytesForCurrentThread` around repeated blocks is the
   same technique the repository's evidence tests use. Allocation rate matters under load even when
   a single call looks fast.
6. **Change one thing.** Re-run the same scenario after one change. Do not compare runs on
   different hardware, runtime versions, or data sets.

The repository's performance tests are non-gating evidence, not a benchmark suite: they are marked
`TestCategory=Performance`, run without parallelization, print an environment snapshot, and assert
only that measurements are positive and finite. They cover memory lookup, file first-load and
steady state, ResX, the store bridge, SQLite, and diagnostics overhead. Use them as a harness shape
and as a regression conversation, not as a capacity promise. The measured snapshot and reproduction
commands are in [Performance evidence](07-performance-evidence.md).

Useful runtime signals are already defined: `localization.lookup.duration`,
`localization.lookup.hits`, `localization.lookup.misses`, `localization.fallback.steps`, and the
translation request/cache/duration instruments under the `Jeninnet.Localization` meter. Activity
tags include the key and culture, but template text is never tagged.

## Worked example: a slow catalog page

A page that renders a product grid is slow only for Arabic. The provider is SQL Server. The
default culture is `en`, and the Arabic catalog is incomplete.

The measurement shows twenty lookups, each missing in `ar` and then hitting `en`: forty provider
queries, not twenty. The fix is not a faster formatter; it is to complete the Arabic catalog or
resolve the values once per request. If the catalog is intentionally allowed to fall back, accept
the doubled query count and cache the resolved page data at the application layer with an
invalidation strategy tied to catalog changes. If the data must be fresh, add database capacity or
move the hot catalog to a file provider that matches the deployment model.

## Pitfalls

**Pitfall:** optimizing the formatter when the provider is a database. Measure provider queries
first.

**Pitfall:** treating a file provider's first lookup as steady state. The first request pays the
parse; warm it if cold start matters.

**Pitfall:** assuming the database providers cache. They do not; every fallback step is a round
trip.

**Pitfall:** caching missing values indefinitely. A catalog edit or a new deployment will not
invalidate a host-level dictionary unless you build that invalidation.

**Pitfall:** enabling file watching on immutable content. It adds a watcher and timer for no
benefit.

**Pitfall:** comparing benchmark runs across machines. The repository's tests print an environment
snapshot for a reason.

## Where to go next

- [Performance evidence](07-performance-evidence.md) for the measured snapshot and reproduction
  commands.
- [Observability cookbook](08-observability-cookbook.md) for dashboards and alerts over the measured
  signals.
- [Troubleshooting](04-troubleshooting.md) for latency and failure symptoms.
- [Deployment](01-deployment.md) for cold start, cache warm-up, and read replicas.
- [Formatting and plurals](../02-guides/04-formatting-and-plurals.md) for formatter behavior.
- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md) for the store cache
  decorators.
