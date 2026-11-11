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
| Run date | 2026-11-11 (runs A and B on each runtime, back to back) |
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
| Memory provider, direct exact lookup, 1,000 keys | 110.2 ns | 80.2 ns | 58.5 ns | 70.1 ns | 0 / 0 B | 0 / 0 B |
| `IMessageLocalizer`, exact present, 1,000 keys | 988.3 ns | 669.5 ns | 521.6 ns | 648.0 ns | 88 / 88 B | 80 / 80 B |
| `IMessageLocalizer`, missing key, 1,000 keys | 990.0 ns | 344.4 ns | 263.5 ns | 994.5 ns | 208 / 208 B | 208 / 208 B |
| `IMessageLocalizer`, `fr-FR` fallback to `en`, 1,000 keys | 727.5 ns | 193.7 ns | 144.5 ns | 256.0 ns | 88 / 88 B | 80 / 80 B |
| ResX provider, repeated exact lookup | 178.2 ns | 179.5 ns | 150.3 ns | 206.6 ns | 0 / 0 B | 0 / 0 B |
| Store bridge, `IStringLocalizerFactory.Create` | 58.6 ns | 57.5 ns | 49.5 ns | 58.1 ns | 40 / 40 B | 40 / 40 B |
| Store bridge, `IStringLocalizer` indexer lookup | 412.9 ns | 325.3 ns | 268.2 ns | 360.7 ns | 144 / 144 B | 144 / 144 B |
| SQLite provider, in-memory DB, per-call connection + query | 127.6 µs | 118.4 µs | 94.8 µs | 220.2 µs | 2,576 / 2,576 B | 2,512 / 2,512 B |
| Diagnostics absent, no listeners | 716.4 ns | 187.3 ns | 160.6 ns | 240.8 ns | 88 / 88 B | 80 / 80 B |
| Diagnostics observed, activity + meter listeners recording | 8.28 µs | 5.98 µs | 8.14 µs | 11.85 µs | 1,849 / 1,849 B | 1,841 / 1,841 B |

The diagnostics run used 5,000 operations per block, 5 blocks, and 1,000 keys. The observed run
recorded 51,000 activities and 76,500 measurements in each pass. On .NET 10 the observed/absent
median ratio was 11.56 in run A and 31.93 in run B; on .NET 11 it was 50.69 in run A and
49.23 in run B; listeners pay the recording cost, and a host with no listeners
pays only the listener-guard checks.

## File providers

First-load measures provider construction, file parse/load, and one exact lookup. Steady state
measures repeated exact lookups after the table is loaded. JSON and XML use the same key/template
fixtures.

| Scenario | net10 Run A | net10 Run B | net11 Run A | net11 Run B | Alloc net10 (A / B) | Alloc net11 (A / B) |
|---|---:|---:|---:|---:|---:|---:|
| JSON first load, 10 keys | 197.4 µs | 256.4 µs | 152.6 µs | 222.3 µs | 4,400 / 4,400 B | 4,384 / 4,384 B |
| XML first load, 10 keys | 225.8 µs | 235.4 µs | 171.1 µs | 265.4 µs | 12,616 / 12,616 B | 12,440 / 12,440 B |
| JSON first load, 1,000 keys | 917.4 µs | 843.5 µs | 531.5 µs | 858.0 µs | 333,912 / 333,912 B | 333,896 / 333,896 B |
| XML first load, 1,000 keys | 1.907 ms | 1.938 ms | 1.633 ms | 2.600 ms | 562,728 / 562,728 B | 562,544 / 562,544 B |
| JSON first load, 10,000 keys | 9.043 ms | 10.327 ms | 6.103 ms | 8.154 ms | 3,327,560 / 3,327,560 B | 3,327,544 / 3,327,544 B |
| XML first load, 10,000 keys | 30.912 ms | 33.688 ms | 25.604 ms | 28.739 ms | 5,458,456 / 5,458,456 B | 5,458,272 / 5,458,272 B |
| JSON steady state, 1,000 keys | 117.0 ns | 72.6 ns | 60.3 ns | 82.6 ns | 0 / 0 B | 0 / 0 B |
| XML steady state, 1,000 keys | 118.6 ns | 76.2 ns | 61.6 ns | 81.4 ns | 0 / 0 B | 0 / 0 B |
| JSON steady state, 10,000 keys | 99.1 ns | 73.6 ns | 56.1 ns | 72.6 ns | 0 / 0 B | 0 / 0 B |
| XML steady state, 10,000 keys | 99.9 ns | 113.5 ns | 58.5 ns | 78.1 ns | 0 / 0 B | 0 / 0 B |

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
| Run dates | 2026-11-11 (run A), 2026-11-11 (run B) |
| Runtime | .NET 10.0.15, X64 RyuJIT x86-64-v3, Concurrent Workstation GC |
| SDK | 11.0.100 |
| OS | Microsoft Windows 10.0.26300 |
| CPU | Intel Core i7-8850H @ 2.60 GHz |
| Configuration | Release, `ShortRun` job (1 launch, 3 warmup + 3 target iterations per benchmark) |
| Tool | BenchmarkDotNet v0.15.8, memory diagnoser on |

Reproduce from the repository root (about 3 minutes per pass on the machine above;
pass `-f net10.0` explicitly now that the project multi-targets):

```powershell
dotnet run --project benchmarks/Jeninnet.Localization.Benchmarks -c Release -f net10.0 -- --filter "*"
```

Use `--filter "*FileProvider*"` (or `*CacheDecorator*`, `*TranslationBatch*`) to run one class.
The net11 leg is intentionally absent: BenchmarkDotNet v0.15.8 predates .NET 11 and
rejects the net11 runtime (`GetRuntimeVersion ... NotRecognized`), and only preview
BDN builds know it. The net11 runtime shape is covered by the in-repo measurement
harness above instead; BDN coverage on net11 waits for a stable BDN release.

Use `--filter "*FileProvider*"` (or `*CacheDecorator*`, `*TranslationBatch*`) to run one class.
The file fixtures are written to a per-process temp directory in `GlobalSetup` and warmed
before measurement; steady-state benchmarks never touch the filesystem. The DeepL fake
transport parses the request `text` array and answers with one index-aligned translation
per element: no network, no key, no delay. The distributed-cache stub is in-memory and
does not enforce TTL.

### Numbers

INI/TOML steady state, 100-key catalog, warmed (mean per operation; allocated bytes per
operation):

| Scenario | Run A mean | Run B mean | Allocated (A / B) |
|---|---:|---:|---:|
| INI hit | 26.3 ns | 20.8 ns | 0 / 0 B |
| INI miss | 16.3 ns | 17.3 ns | 0 / 0 B |
| TOML hit | 21.1 ns | 22.1 ns | 0 / 0 B |
| TOML miss | 16.6 ns | 18.8 ns | 0 / 0 B |

Decorator overhead, steady-state hits over the same warmed key (ratio against the direct
lookup in the same run):

| Scenario | Run A mean (ratio) | Run B mean (ratio) | Allocated (A / B) |
|---|---:|---:|---:|
| Direct inner hit | 21.3 ns (1.02) | 19.2 ns (1.00) | 0 / 0 B |
| Memory-cached hit | 60.2 ns (2.88) | 65.8 ns (3.44) | 0 / 0 B |
| Distributed-cached hit (in-memory stub) | 770.9 ns (36.87) | 748.4 ns (39.13) | 800 / 800 B |

DeepL provider-side batching, fake transport, caching disabled (mean per batch; the 50-key
batch is one full 50-text chunk):

| Scenario | Run A mean | Run B mean | Allocated (A / B) |
|---|---:|---:|---:|
| Batch, 1 key | 21.97 µs | 7.58 µs | 7.5 / 7.5 KB |
| Batch, 10 keys | 19.59 µs | 17.99 µs | 19.6 / 19.6 KB |
| Batch, 50 keys | 61.19 µs | 58.37 µs | 74.6 / 74.6 KB |

What the numbers show:

- **INI/TOML steady state matches the memory/JSON shape.** Hits and misses sit in the
  tens of nanoseconds with zero allocation, like the loaded JSON/XML lookups above: the
  file parse is a cold-path cost, the hot path is a dictionary lookup.
- **The memory decorator costs a few dictionary probes.** A warmed hit runs ~3–5× the
  direct lookup in these runs and allocates nothing.
- **The distributed decorator pays serialization plus a store round trip.** A warmed hit
  over the in-memory stub runs ~37–39× the direct lookup and allocates 800 B per call
  (the `0x01 + UTF-8` payload plus store bookkeeping). A real network store adds its own
  latency on top; the stub measures the decorator, not Redis.
- **Batching scales sub-linearly in provider-side work.** Ten keys cost well under 10×
  one key and fifty keys (one chunk) well under 50×: one HTTP exchange carries the whole
  chunk. The absolute microseconds are the fake-transport floor (JSON parse plus provider
  machinery), not service latency.

Caveats: one machine, short-run jobs (wide confidence intervals on nanosecond rows: run
B moved several file-provider means by ~10–20 ns), a single pass each way, and stubbed
transports. Compare scenarios within a run, not against another machine.

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
  With activity and meter listeners recording, the cost rises by roughly 40–50× in this workload.
  This is the intended listener-guard design, not a reason to avoid instrumentation in production;
  budget for it when high-cardinality recording is enabled.
- **The store bridge is cheap to create and cheap to read.** Creating a localizer and reading through
  the store bridge are sub-microsecond operations on the measured in-process store. A database-backed
  store pays its own query cost.

## Caveats

- These are **two runs per runtime on one machine**, not a distribution. The second run was noisier for several
  scenarios on both runtimes (for example, on net10 `IMessageLocalizer` exact 988 → 670 ns and SQLite
  128 → 118 µs; on net11 missing-key 264 → 995 ns). Use the medians as an order-of-magnitude shape,
  not as a guarantee. Cross-runtime gaps of the same order as run-to-run gaps are noise, not signal.
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
