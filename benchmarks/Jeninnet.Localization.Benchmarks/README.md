//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
# Jeninnet.Localization.Benchmarks

Micro-benchmarks for the localization hot paths: pipeline lookup
(`LocalizerLookupBenchmarks`), message formatting (`FormatterBenchmarks`).
Non-shipping: never packed, never referenced by tests.

Run locally with:

```bash
dotnet run --project benchmarks/Jeninnet.Localization.Benchmarks -c Release
```

List benchmarks without running them:

```bash
dotnet run --project benchmarks/Jeninnet.Localization.Benchmarks -c Release -- --list flat
```

Short-run jobs keep local iterations fast; use `--filter` to run a subset.
Results are for relative comparison across changes, not absolute latency claims.
