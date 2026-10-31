# Release Notes

This page is the user-facing release record. `CHANGELOG.md` remains the development journal; this page
summarizes what a release contains and what a consumer can rely on.

## Current status

**Unreleased.** No NuGet packages have been published. The product version declared by the build is
`1.0.0-preview.1` (package version `1.0.0-preview.1`), which is a preview version and not a stable
release. Until then, use the local-feed workflow in
[Installation](../01-getting-started/02-installation.md).

## 1.0 — first public release (planned)

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

The intended published workflow is:

```powershell
dotnet add package Jeninnet.Localization.Core
dotnet add package Jeninnet.Localization.Providers.Json
```

While the repository is unreleased, pack the solution into the local feed and add `--source`:

```powershell
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
```

Package READMEs document the registration, options, and behavior for each package. The
[installation chapter](../01-getting-started/02-installation.md) covers project setup and the
first registration.

## Compatibility

- Runtime libraries target `net8.0`, `net9.0`, and `net10.0`; C# language version is 14.0.
- Build-time Roslyn tooling targets `netstandard2.0` because it runs in the compiler.
- The parity tool targets `net8.0` and `net10.0` and is packaged as a .NET tool.
- There is no multi-targeting and no support claim for older .NET versions.
- AOT (Ahead-of-Time) and trimming posture, including the EF Core exclusion and annotated reflection surfaces, is
  documented in [AOT and trimming](../aot-and-trimming.md) and
  [Compatibility and versions](07-compatibility-and-versions.md).

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

## Breaking changes before 1.0

No public API has been released, so there is no consumer migration burden yet. The repository still
records every intentional public change in `CHANGELOG.md` with a **Breaking:** prefix and updates
the committed API baselines in the same change. Until the first package is published, canonical
renames are allowed; after publication, the baseline becomes the compatibility gate described in
[Compatibility and versions](07-compatibility-and-versions.md).

## Known limitations

The boundaries an evaluator should read before adopting the library are consolidated in
[Known limitations](10-known-limitations.md). They are deliberate design choices, not a bug list.

## Related documentation

- [Installation and first registration](../01-getting-started/02-installation.md)
- [Compatibility and versions](07-compatibility-and-versions.md)
- [Known limitations](10-known-limitations.md)
- [Package map](../appendix/02-package-map.md)
- [Deployment](../04-operations/01-deployment.md)
- [Repository changelog](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CHANGELOG.md)
- [Repository README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
