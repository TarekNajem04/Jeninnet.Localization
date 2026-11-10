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
| Run date | 2026-10-21 |
| Runtime | .NET 10.0.13 |
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

Reproduce from the repository root:

```powershell
dotnet build tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj -c Release
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj `
    -c Release --no-build `
    --filter "TestCategory=Performance" `
    --logger "console;verbosity=detailed"
```

The `RESULT`, `ENV`, and `NOTE` lines are written through the test context. Run the command twice and
compare medians; do not compare against another machine or a run made while the machine was busy.

## Core, store, database, and diagnostics

| Scenario | Run A median | Run B median | Alloc median (A / B) |
|---|---:|---:|---:|
| Memory provider, direct exact lookup, 1,000 keys | 59.2 ns | 77.8 ns | 0 / 0 B |
| `IMessageLocalizer`, exact present, 1,000 keys | 557.2 ns | 662.4 ns | 88 / 88 B |
| `IMessageLocalizer`, missing key, 1,000 keys | 455.7 ns | 313.5 ns | 208 / 208 B |
| `IMessageLocalizer`, `fr-FR` fallback to `en`, 1,000 keys | 278.6 ns | 181.5 ns | 88 / 88 B |
| ResX provider, repeated exact lookup | 115.8 ns | 197.3 ns | 0 / 0 B |
| Store bridge, `IStringLocalizerFactory.Create` | 57.4 ns | 48.3 ns | 40 / 40 B |
| Store bridge, `IStringLocalizer` indexer lookup | 234.4 ns | 251.4 ns | 144 / 144 B |
| SQLite provider, in-memory DB, per-call connection + query | 90.2 µs | 114.0 µs | 2,576 / 2,576 B |
| Diagnostics absent, no listeners | 138.1 ns | 170.1 ns | 88 / 88 B |
| Diagnostics observed, activity + meter listeners recording | 5.76 µs | 8.85 µs | 1,849 / 1,849 B |

The diagnostics run used 5,000 operations per block, 5 blocks, and 1,000 keys. The observed run
recorded 51,000 activities and 76,500 measurements in each pass. The observed/absent median ratio was
41.70 in run A and 52.02 in run B; listeners pay the recording cost, and a host with no listeners
pays only the listener-guard checks.

## File providers

First-load measures provider construction, file parse/load, and one exact lookup. Steady state
measures repeated exact lookups after the table is loaded. JSON and XML use the same key/template
fixtures.

| Scenario | Run A median | Run B median | Alloc median (A / B) |
|---|---:|---:|---:|
| JSON first load, 10 keys | 130.2 µs | 185.3 µs | 4,152 / 4,152 B |
| XML first load, 10 keys | 276.2 µs | 187.6 µs | 11,424 / 11,424 B |
| JSON first load, 1,000 keys | 795.6 µs | 764.0 µs | 333,664 / 333,664 B |
| XML first load, 1,000 keys | 1.348 ms | 1.640 ms | 482,336 / 482,336 B |
| JSON first load, 10,000 keys | 6.904 ms | 8.339 ms | 3,327,312 / 3,327,312 B |
| XML first load, 10,000 keys | 22.892 ms | 26.948 ms | 4,658,064 / 4,657,864 B |
| JSON steady state, 1,000 keys | 51.1 ns | 64.4 ns | 0 / 0 B |
| XML steady state, 1,000 keys | 54.1 ns | 62.4 ns | 0 / 0 B |
| JSON steady state, 10,000 keys | 46.5 ns | 56.2 ns | 0 / 0 B |
| XML steady state, 10,000 keys | 45.2 ns | 56.2 ns | 0 / 0 B |

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
| Run dates | 2026-11-10 (run A), 2026-11-10 (run B) |
| Runtime | .NET 10.0.14, X64 RyuJIT x86-64-v3, Concurrent Workstation GC |
| SDK | 10.0.204 |
| OS | Microsoft Windows 10.0.26300 |
| CPU | Intel Core i7-8850H @ 2.60 GHz |
| Configuration | Release, `ShortRun` job (1 launch, 3 warmup + 3 target iterations per benchmark) |
| Tool | BenchmarkDotNet v0.15.8, memory diagnoser on |

Reproduce from the repository root (about 3 minutes per pass on the machine above):

```powershell
dotnet run --project benchmarks/Jeninnet.Localization.Benchmarks -c Release -- --filter "*"
```

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
| INI hit | 20.9 ns | 33.3 ns | 0 / 0 B |
| INI miss | 19.0 ns | 42.1 ns | 0 / 0 B |
| TOML hit | 22.4 ns | 34.7 ns | 0 / 0 B |
| TOML miss | 18.2 ns | 28.1 ns | 0 / 0 B |

Decorator overhead, steady-state hits over the same warmed key (ratio against the direct
lookup in the same run):

| Scenario | Run A mean (ratio) | Run B mean (ratio) | Allocated (A / B) |
|---|---:|---:|---:|
| Direct inner hit | 22.7 ns (1.01) | 23.3 ns (1.00) | 0 / 0 B |
| Memory-cached hit | 76.2 ns (3.40) | 106.0 ns (4.57) | 0 / 0 B |
| Distributed-cached hit (in-memory stub) | 951.0 ns (42.42) | 976.4 ns (42.05) | 800 / 800 B |

DeepL provider-side batching, fake transport, caching disabled (mean per batch; the 50-key
batch is one full 50-text chunk):

| Scenario | Run A mean | Run B mean | Allocated (A / B) |
|---|---:|---:|---:|
| Batch, 1 key | 11.39 µs | 8.87 µs | 7.7 / 7.7 KB |
| Batch, 10 keys | 18.20 µs | 34.0 µs | 20.2 / 20.2 KB |
| Batch, 50 keys | 60.39 µs | 84.3 µs | 76.6 / 76.6 KB |

What the numbers show:

- **INI/TOML steady state matches the memory/JSON shape.** Hits and misses sit in the
  tens of nanoseconds with zero allocation, like the loaded JSON/XML lookups above: the
  file parse is a cold-path cost, the hot path is a dictionary lookup.
- **The memory decorator costs a few dictionary probes.** A warmed hit runs ~3–5× the
  direct lookup in these runs and allocates nothing.
- **The distributed decorator pays serialization plus a store round trip.** A warmed hit
  over the in-memory stub runs ~42× the direct lookup and allocates 800 B per call
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
- **SQLite in-memory is still a database path.** Per-call connection and query cost about 90–114 µs
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

- These are **two runs on one machine**, not a distribution. The second run was noisier for several
  scenarios (for example, `IMessageLocalizer` exact 557 → 662 ns and SQLite 90 → 114 µs). Use the
  medians as an order-of-magnitude shape, not as a guarantee.
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
