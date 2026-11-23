# Performance Evidence

This page is a reproducible evidence snapshot for the documented hot paths. It is not a benchmark
suite, a capacity promise, or an SLO. The repository's performance tests assert only that
measurements are positive and finite; the numbers below are the medians from two consecutive runs on
one machine, published so a reader can see the shape of the costs and reproduce them.

For the structural explanation of the pipeline and provider costs, read
[Performance](03-performance.md) first. This page adds measurements, not new behavior.

## Environment and method

| Item | Value |
|---|---|
| Run date | 2026-11-23 (runs A and B on each runtime, back to back) |
| CPU | Intel Core i7-8850H @ 2.60 GHz |
| Runtime | net10 legs: .NET 10.0.15; net11 legs: .NET 11.0.0 (SDK 11.0.100) |
| OS | Microsoft Windows 10.0.26300 |
| Process / OS architecture | X64 / X64 |
| Logical processors | 12 |
| Server GC | Off |
| Stopwatch resolution | High-resolution, 10,000,000 Hz |
| Culture during the run | `en-NL` (formatting), `en-US` (UI) |
| Configuration | Release, no build |
| Test filter | `TestCategory=Performance` |
| Blocks / samples | 7 timed blocks for steady state; 5 for first-load, SQLite, and diagnostics |

The harness warms up each scenario, records elapsed time with `Stopwatch.GetTimestamp()`, and records
allocations with `GC.GetAllocatedBytesForCurrentThread()`. Each result reports min, median, and max
across blocks; the tables below use the median. Allocation is the median bytes per operation.

Reproduce from the repository root (run each runtime separately; compare medians
within one runtime, not across machines or busy runs):

```powershell
dotnet build tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj -c Release
foreach ($tfm in @('net10.0', 'net11.0')) {
    dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj `
        -c Release --no-build -f $tfm `
        --filter "TestCategory=Performance" `
        --logger "console;verbosity=detailed"
}
```

The `RESULT`, `ENV`, and `NOTE` lines are written through the test context. Run the command twice and
compare medians; do not compare against another machine or a run made while the machine was busy.

## Core, store, database, and diagnostics

| Scenario | net10 Run A | net10 Run B | net11 Run A | net11 Run B | Alloc net10 (A / B) | Alloc net11 (A / B) |
|---|---:|---:|---:|---:|---:|---:|
| Memory provider, direct exact lookup, 1,000 keys | 75.7 ns | 80.8 ns | 79.2 ns | 74.7 ns | 0 / 0 B | 0 / 0 B |
| `IMessageLocalizer`, exact present, 1,000 keys | 803.1 ns | 704.3 ns | 797.9 ns | 607.2 ns | 88 / 88 B | 80 / 80 B |
| `IMessageLocalizer`, missing key, 1,000 keys | 853.9 ns | 336.9 ns | 754.4 ns | 315.7 ns | 112 / 208 B | 112 / 208 B |
| `IMessageLocalizer`, `fr-FR` fallback to `en`, 1,000 keys | 800.7 ns | 190.6 ns | 640.1 ns | 181.8 ns | 88 / 88 B | 80 / 80 B |
| ResX provider, repeated exact lookup | 204.8 ns | 200.1 ns | 180.8 ns | 174.0 ns | 0 / 0 B | 0 / 0 B |
| Store bridge, `IStringLocalizerFactory.Create` | 58.5 ns | 70.7 ns | 73.6 ns | 58.8 ns | 40 / 40 B | 40 / 40 B |
| Store bridge, `IStringLocalizer` indexer lookup | 467.5 ns | 311.1 ns | 340.6 ns | 443.0 ns | 144 / 144 B | 144 / 144 B |
| SQLite provider, in-memory DB, per-call connection + query | 205.2 µs | 136.2 µs | 119.5 µs | 119.5 µs | 2,576 / 2,576 B | 2,512 / 2,512 B |
| Diagnostics absent, no listeners | 1.48 µs | 219.5 ns | 677.3 ns | 189.7 ns | 88 / 88 B | 80 / 80 B |
| Diagnostics observed, activity + meter listeners recording | 10.2 µs | 8.41 µs | 7.86 µs | 6.58 µs | 1,849 / 1,849 B | 1,841 / 1,842 B |

The diagnostics run used 5,000 operations per block, 5 blocks, and 1,000 keys. The observed run
recorded 51,000 activities and 76,500 measurements in each pass. On .NET 10 the observed/absent
median ratio was 6.90 in run A and 38.33 in run B; on .NET 11 it was 11.61 in run A and
34.70 in run B. Listeners pay the recording cost; a host with no listeners pays only the
listener-guard checks, and the wide spread shows how much the listener cost depends on the run.

## File providers

First-load measures provider construction, file parse/load, and one exact lookup. Steady state
measures repeated exact lookups after the table is loaded. JSON and XML use the same key/template
fixtures.

| Scenario | net10 Run A | net10 Run B | net11 Run A | net11 Run B | Alloc net10 (A / B) | Alloc net11 (A / B) |
|---|---:|---:|---:|---:|---:|---:|
| JSON first load, 10 keys | 159.0 µs | 159.4 µs | 217.6 µs | 141.6 µs | 4,528 / 4,528 B | 4,504 / 4,504 B |
| XML first load, 10 keys | 221.3 µs | 203.0 µs | 201.4 µs | 186.4 µs | 12,744 / 12,744 B | 12,560 / 12,560 B |
| JSON first load, 1,000 keys | 901.7 µs | 819.3 µs | 722.7 µs | 721.4 µs | 334,040 / 334,040 B | 334,016 / 334,016 B |
| XML first load, 1,000 keys | 2.317 ms | 1.910 ms | 1.936 ms | 2.073 ms | 562,856 / 562,856 B | 562,664 / 562,664 B |
| JSON first load, 10,000 keys | 8.247 ms | 8.507 ms | 10.205 ms | 8.410 ms | 3,327,688 / 3,327,688 B | 3,327,888 / 3,327,888 B |
| XML first load, 10,000 keys | 28.261 ms | 33.624 ms | 24.768 ms | 29.124 ms | 5,458,584 / 5,458,584 B | 5,458,392 / 5,458,392 B |
| JSON steady state, 1,000 keys | 125.7 ns | 79.3 ns | 128.8 ns | 81.7 ns | 0 / 0 B | 0 / 0 B |
| XML steady state, 1,000 keys | 124.6 ns | 78.7 ns | 114.4 ns | 80.9 ns | 0 / 0 B | 0 / 0 B |
| JSON steady state, 10,000 keys | 112.7 ns | 76.5 ns | 105.2 ns | 77.3 ns | 0 / 0 B | 0 / 0 B |
| XML steady state, 10,000 keys | 115.5 ns | 76.8 ns | 106.2 ns | 69.7 ns | 0 / 0 B | 0 / 0 B |

The steady-state medians are dictionary lookups and do not touch the filesystem. The first load scales
with file size; XML parses more slowly and allocates more than JSON for the same fixture. A failed load
is not cached, so a broken file pays the failure cost again on the next attempt.

## Wave-2 micro-benchmarks (INI/TOML, caching, DeepL batching)

BenchmarkDotNet micro-benchmarks for the Wave-2 providers and the W2-01 caching layer:
`FileProviderBenchmarks` (INI/TOML steady-state hit and miss over a 100-key catalog),
`CacheDecoratorBenchmarks` (direct inner lookup versus memory-cached versus
distributed-cached steady-state hits), and `TranslationBatchBenchmarks` (DeepL
provider-side batching for 1, 10, and 50 keys over a fake transport with caching
disabled, so every invocation exercises the batch path). Like the rest of this page,
these are evidence, not SLOs: short-run jobs on one machine, reported as means.

### Environment and method

| Item | Value |
|---|---|
| Run dates | 2026-11-11 (runs A and B, net10), 2026-11-23 (two passes, net10) |
| Runtime | .NET 10.0.15, X64 RyuJIT x86-64-v3, Concurrent Workstation GC |
| SDK | 11.0.100 |
| OS | Microsoft Windows 10.0.26300 |
| CPU | Intel Core i7-8850H @ 2.60 GHz |
| Configuration | Release, `ShortRun` job (1 launch, 3 warmup + 3 target iterations per benchmark) |
| Tool | BenchmarkDotNet v0.15.8, memory diagnoser on |

Reproduce from the repository root (about 2–4 minutes per class on the machine above; the
unfiltered run covers all five classes and takes roughly 20 minutes. Pass `-f net10.0` explicitly
now that the project multi-targets):

```powershell
dotnet run --project benchmarks/Jeninnet.Localization.Benchmarks -c Release -f net10.0 -- --filter "*"
```

Use `--filter "*FileProvider*"` (or `*CacheDecorator*`, `*TranslationBatch*`) to run one class.
The net11 leg is intentionally absent: BenchmarkDotNet v0.15.8 predates .NET 11 and
rejects the net11 runtime (`GetRuntimeVersion ... NotRecognized`), and only preview
BDN builds know it. The net11 runtime shape is covered by the in-repo measurement
harness above instead; BDN coverage on net11 waits for a stable BDN release.

The file fixtures are written to a per-process temp directory in `GlobalSetup` and warmed
before measurement; steady-state benchmarks never touch the filesystem. The DeepL fake
transport parses the request `text` array and answers with one index-aligned translation
per element: no network, no key, no delay. The distributed-cache stub is in-memory and
does not enforce TTL.

### Numbers

INI/TOML steady state, 100-key catalog, warmed (mean per operation; allocated bytes per
operation):

| Scenario | Run A mean | Run B mean | Allocated (A / B) | 2026-11-23 pass A mean | 2026-11-23 pass B mean | 2026-11-23 allocated |
|---|---:|---:|---:|---:|---:|---:|
| INI hit | 26.3 ns | 20.8 ns | 0 / 0 B | 26.59 ns | 22.66 ns | 0 B |
| INI miss | 16.3 ns | 17.3 ns | 0 / 0 B | 20.93 ns | 17.60 ns | 0 B |
| TOML hit | 21.1 ns | 22.1 ns | 0 / 0 B | 24.32 ns | 23.34 ns | 0 B |
| TOML miss | 16.6 ns | 18.8 ns | 0 / 0 B | 18.74 ns | 17.33 ns | 0 B |

Decorator overhead, steady-state hits over the same warmed key (ratio against the direct
lookup in the same run):

| Scenario | Run A mean (ratio) | Run B mean (ratio) | Allocated (A / B) | 2026-11-23 pass A mean (ratio) | 2026-11-23 pass B mean (ratio) | 2026-11-23 allocated |
|---|---:|---:|---:|---:|---:|---:|
| Direct inner hit | 21.3 ns (1.02) | 19.2 ns (1.00) | 0 / 0 B | 19.84 ns (1.00) | 20.68 ns (1.00) | 0 B |
| Memory-cached hit | 60.2 ns (2.88) | 65.8 ns (3.44) | 0 / 0 B | 68.15 ns (3.44) | 72.38 ns (3.50) | 0 B |
| Distributed-cached hit (in-memory stub) | 770.9 ns (36.87) | 748.4 ns (39.13) | 800 / 800 B | 795.60 ns (40.18) | 769.22 ns (37.21) | 648 B |

DeepL provider-side batching, fake transport, caching disabled (mean per batch; the 50-key
batch is one full 50-text chunk):

| Scenario | Run A mean | Run B mean | Allocated (A / B) | 2026-11-23 pass A mean | 2026-11-23 pass B mean | 2026-11-23 allocated |
|---|---:|---:|---:|---:|---:|---:|
| Batch, 1 key | 21.97 µs | 7.58 µs | 7.5 / 7.5 KB | 8.358 µs | 8.774 µs | 7.7 KB |
| Batch, 10 keys | 19.59 µs | 17.99 µs | 19.6 / 19.6 KB | 20.005 µs | 20.364 µs | 20.0 KB |
| Batch, 50 keys | 61.19 µs | 58.37 µs | 74.6 / 74.6 KB | 62.226 µs | 65.985 µs | 75.6 KB |

The first 2026-11-23 pass initially measured the memory-cached hit at 99.8 ns (ratio 4.75)
against 60–66 ns in the preview.2 runs; a second pre-fix pass measured 86.0 ns, confirming a
regression above the preview.2 spread. It was traced to the post-preview.2 deduplication
refactor: the cache hit awaited an asynchronous store call and paid state-machine overhead even
when the in-memory store answered synchronously. `CoalescingCacheEngine` now probes the memory
store synchronously and returns a completed `ValueTask`; the two post-fix passes above measure
68.2 and 72.4 ns (ratios 3.44 and 3.50), back inside the preview.2 range. The table reports the
post-fix passes.

What the numbers show:

- **INI/TOML steady state matches the memory/JSON shape.** Hits and misses sit in the
  tens of nanoseconds with zero allocation, like the loaded JSON/XML lookups above: the
  file parse is a cold-path cost, the hot path is a dictionary lookup.
- **The memory decorator costs a few dictionary probes.** A warmed hit runs ~3–3.5× the
  direct lookup in the post-fix passes and allocates nothing.
- **The distributed decorator pays serialization plus a store round trip.** A warmed hit
  over the in-memory stub runs ~37–40× the direct lookup and allocates 648–800 B per call
  (the `0x01 + UTF-8` payload plus store bookkeeping). A real network store adds its own
  latency on top; the stub measures the decorator, not Redis.
- **Batching scales sub-linearly in provider-side work.** Ten keys cost well under 10×
  one key and fifty keys (one chunk) well under 50×: one HTTP exchange carries the whole
  chunk. The absolute microseconds are the fake-transport floor (JSON parse plus provider
  machinery), not service latency.

Caveats: one machine, short-run jobs (wide confidence intervals on nanosecond rows; the
2026-11-23 passes moved several file-provider means by ~5–10 ns against the 2026-11-11
runs), two passes per date, and stubbed transports. Compare scenarios within a run, not
against another machine.

## Formatter and localizer lookup micro-benchmarks

The same BenchmarkDotNet job and net10 host cover the remaining hot-path classes:
`FormatterBenchmarks` (named and positional placeholders plus ICU plural/select) and
`LocalizerLookupBenchmarks` (memory-provider hit, miss, and plural lookup through the
localizer). These classes were not part of the preview.2 page, so the 2026-11-23 passes are
the baseline for them; run them again before treating a later delta as a regression.

| Formatter scenario | 2026-11-23 pass A | 2026-11-23 pass B | Allocated |
|---|---:|---:|---:|
| Named placeholder | 746.2 ns | 700.6 ns | 1,000 B |
| Positional placeholder | 721.3 ns | 759.3 ns | 1,056 B |
| ICU plural | 928.9 ns | 943.0 ns | 1,488 B |
| ICU select | 656.0 ns | 631.0 ns | 1,232 B |

| Localizer lookup scenario | 2026-11-23 pass A | 2026-11-23 pass B | Allocated |
|---|---:|---:|---:|
| Hit | 594.3 ns | 585.2 ns | 560 B |
| Miss | 156.1 ns | 156.9 ns | 112 B |
| Plural | 704.7 ns | 734.8 ns | 544 B |

Formatting costs hundreds of nanoseconds and about 1 KB per operation because it parses the
template and formats into a new string. A hit through the localizer adds allocation on top of
the raw provider lookup; a miss is cheaper because it returns the key path without formatting.

## What the numbers mean

- **In-process lookups are tens of nanoseconds.** Memory and loaded JSON/XML steady-state lookups are
  in the same order of magnitude. The provider's table shape and key distribution dominate.
- **Core adds formatting and fallback, not I/O.** Exact, miss, and fallback paths are hundreds of
  nanoseconds and tens to hundreds of bytes per operation. A database or translation provider is
  thousands of times more expensive than this layer.
- **First load is a cold-path cost.** JSON/XML parsing at 10,000 keys is measured in milliseconds and
  megabytes of allocation; warm the catalog before a latency-sensitive start if cold start matters.
- **SQLite in-memory is still a database path.** Per-call connection and query cost about 95–220 µs
  per lookup in these runs, even with an in-memory database. A network database adds network latency
  on top. Cache above the provider or reduce the number of fallback steps.
- **Diagnostics are observer-paid.** With no listeners, the overhead is in the sub-microsecond range.
  With activity and meter listeners recording, the cost rises by roughly one to one-and-a-half orders
  of magnitude in this workload (7–38× observed across the four legs). This is the intended
  listener-guard design, not a reason to avoid instrumentation in production; budget for it when
  high-cardinality recording is enabled.
- **The store bridge is cheap to create and cheap to read.** Creating a localizer and reading through
  the store bridge are sub-microsecond operations on the measured in-process store. A database-backed
  store pays its own query cost.

## Caveats

- These are **two runs per runtime on one machine**, not a distribution. Run-to-run gaps were as
  large as the cross-runtime gaps in several scenarios (for example, net11 store bridge indexer
  341 → 443 ns, while net10 SQLite fell 205 → 136 µs). Use the medians as an order-of-magnitude
  shape, not as a guarantee.
- The harness reports min/median/max across blocks, not p95/p99. For tail-sensitive planning, measure
  your own production path and record percentiles.
- Server GC was off and the machine had 12 logical processors. Different hardware, power state, GC
  mode, background load, or runtime patch level changes the numbers.
- The SQLite measurement uses an in-memory database and a per-call connection. It does not measure
  file I/O, WAL settings, network databases, or connection-pool warm-up.
- The file measurements use small generated fixtures, not your catalog. Template length, key length,
  encoding, and the number of cultures all affect parse time and allocation.
- The tests are non-gating. They assert positive finite measurements, not thresholds; a green test
  run is not a performance budget.

## Reproducing and interpreting a change

1. Run the command above on a quiet machine with a Release build.
2. Capture `ENV`, `RESULT`, and `NOTE` lines in a file.
3. Run it a second time and compare medians scenario by scenario, not just the fastest row.
4. Change one thing (provider, catalog size, cache, listener) and repeat.
5. Prefer the structural explanation in [Performance](03-performance.md) when a number moves; the
   measurement explains the cost, not the reason.

Useful runtime signals for production are already defined: `localization.lookup.duration`,
`localization.lookup.hits`, `localization.lookup.misses`, `localization.fallback.steps`, and the
translation request/cache/duration instruments under the `Jeninnet.Localization` meter. They are
listener-guarded as well.

## Related documentation

- [Performance](03-performance.md)
- [Diagnostics and observability](../02-guides/14-diagnostics-and-observability.md)
- [Observability cookbook](08-observability-cookbook.md)
- [Deployment](01-deployment.md)
- [EF Core store schema](06-ef-core-store-schema.md)
- [Known limitations](../03-reference/10-known-limitations.md)
- [Testing your localization](../02-guides/15-testing-your-localization.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
