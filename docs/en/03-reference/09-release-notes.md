# Release Notes

This page is the user-facing release record. `CHANGELOG.md` remains the development journal; this page
summarizes what a release contains and what a consumer can rely on.

## Current status

**`1.0.0-preview.1` — first public preview, published 2026-10-31.** All 35 packages are available
on NuGet at version `1.0.0-preview.1`. It is a preview version, not a stable release: pin the exact
version and review `CHANGELOG.md` before upgrading.

- [GitHub release](https://github.com/TarekNajem04/Jeninnet.Localization/releases/tag/v1.0.0-preview.1)
- [NuGet packages](https://www.nuget.org/packages/Jeninnet.Localization.Core)
- [Migration guide](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/MIGRATION.md)

## 1.0.0-preview.1 — first public release

The first release is intended to include the complete library described by this book:

- **Core pipeline and contracts** — `Jeninnet.Localization.Abstractions`, `Core`, and `Storage`:
  exact-match providers, single-owner culture fallback, formatting, plural and ICU (International Components for Unicode) support, the
  store-backed `IStringLocalizer` bridge, and registration.
- **Message providers** — Memory, JSON, XML, ResX, Satellite, PO (Portable Object)/MO (Machine Object), Pseudo, SQL Server, PostgreSQL,
  MySQL, SQLite, and the EF Core resource-set store.
- **Translation providers** — Azure Translator v3 and Google Cloud Translation, with caching,
  batching, glossaries, translation memory, and resilience on the Azure side.
- **Framework integrations** — Avalonia, MAUI, and WinUI host culture, ASP.NET Core request localization, MVC validation and view
  localization, DataAnnotations, Blazor (circuit and static scope), JavaScript delivery, and
  diagnostics/health checks.
- **Interchange** — CSV, PO, ResX, and XLIFF (XML Localization Interchange File Format) catalog formats with shared keys, statuses, and
  translator notes.
- **Tooling** — the compile-time key generator, the `JLNLOC001` analyzer and code fix, and the
  `jeninnet-localization-keys` parity tool.
- **Samples and reference application** — the BookStore reference application across six hosts and
  the classic `ResourceManager`-style sample.

The complete package inventory, dependencies, and "which package do I need?" guidance are in the
[package map](../appendix/02-package-map.md).

## Installation

The published workflow is:

```powershell
dotnet add package Jeninnet.Localization.Core --version 1.0.0-preview.1
dotnet add package Jeninnet.Localization.Providers.Json --version 1.0.0-preview.1
```

For local development against the source tree, pack the solution into the local feed and add
`--source`:

```powershell
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
```

Package READMEs document the registration, options, and behavior for each package. The
[installation chapter](../01-getting-started/02-installation.md) covers project setup and the
first registration.

## Supported frameworks and versions

- Runtime libraries target `net8.0`, `net9.0`, and `net10.0`; C# language version is 14.0.
- Build-time Roslyn tooling targets `netstandard2.0` because it runs in the compiler.
- The parity tool targets `net8.0` and `net10.0` and is packaged as a .NET tool.
- There is no multi-targeting and no support claim for older .NET versions.
- AOT (Ahead-of-Time) and trimming posture, including the EF Core exclusion and annotated reflection surfaces, is
  documented in [AOT and trimming](../aot-and-trimming.md) and
  [Versions and support](07-compatibility-and-versions.md).

## Verification posture

The release is gated by repository evidence, not by a version number:

- the full test suite, including architecture, contract, provider, integration, interchange, and
  analyzer tests;
- 35 committed public-API baselines, one per packable assembly;
- package validation and the consumer solution, which restores from the packed feed rather than
  project references;
- the Native AOT smoke consumer in CI;
- the reference application across API, MVC, Blazor Server, WinForms, WPF, and Blazor Hybrid.

The exact counts change as the suite grows; `CHANGELOG.md` and the test project are the source of
truth.

## Change policy for preview releases

`1.0.0-preview.1` is the first published package set, so there is no earlier Jeninnet package to
upgrade from. Between previews the project may still adjust the public surface; every intentional
public change is recorded in `CHANGELOG.md` and the committed API baselines are regenerated in the
same change. After the stable `1.0.0`, the baselines become the release gate described in
[Versions and support](07-compatibility-and-versions.md).

## Known limitations

The boundaries an evaluator should read before adopting the library are consolidated in
[Known limitations](10-known-limitations.md). They are deliberate design choices, not a bug list.

## Related documentation

- [Installation and first registration](../01-getting-started/02-installation.md)
- [Versions and support](07-compatibility-and-versions.md)
- [Known limitations](10-known-limitations.md)
- [Package map](../appendix/02-package-map.md)
- [Deployment](../04-operations/01-deployment.md)
- [Migration guide](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/MIGRATION.md)
- [Repository changelog](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CHANGELOG.md)
- [Repository README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
