# Jeninnet.Localization

[![CI](https://img.shields.io/github/actions/workflow/status/TarekNajem04/Jeninnet.Localization/ci.yml?branch=main&label=CI)](https://github.com/TarekNajem04/Jeninnet.Localization/actions/workflows/ci.yml)
[![Documentation](https://img.shields.io/github/actions/workflow/status/TarekNajem04/Jeninnet.Localization/docs-build.yml?branch=main&label=docs)](https://tareknajem04.github.io/Jeninnet.Localization/)
[![License: MIT](https://img.shields.io/github/license/TarekNajem04/Jeninnet.Localization)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/TarekNajem04/Jeninnet.Localization)](https://github.com/TarekNajem04/Jeninnet.Localization/stargazers)

![api stability: baselines ok](badges/api-stability.svg) ![architecture: invariants ok](badges/architecture.svg) ![target framework: net10.0/net11.0](badges/frameworks.svg) ![obsolete members: 6 deliberate](badges/obsolete.svg)

- **Status:** `1.0.0-preview.2` is the latest published preview (2026-11-18); `1.0.0-preview.3` is in hardening and not yet published. Pin the exact preview version; see the [release notes](https://github.com/TarekNajem04/Jeninnet.Localization/releases).
- **Try it now:** [Browse the live documentation](https://tareknajem04.github.io/Jeninnet.Localization/) — also available in [Arabic](https://tareknajem04.github.io/Jeninnet.Localization/ar/README.html).
- **Target frameworks:** `net10.0` (LTS) and `net11.0` (STS); MAUI `net10.0`; WinUI `net10.0-windows10.0.19041.0`
- **Security:** report privately through GitHub Security Advisories; see [SECURITY.md](.github/SECURITY.md).
- **Support:** see [SUPPORT.md](.github/SUPPORT.md) and the [documentation](docs/en/README.md).

Jeninnet.Localization is a modular localization framework for .NET applications. It provides key-driven message localization, resource-set storage, file and database message providers, ASP.NET Core integration (including DataAnnotations and MVC validation messages), optional machine-translation providers (Azure Translator, DeepL, Google Translate), and TMS connectors (Crowdin, Weblate).

## Conceptual model

- **Providers** — every message source implements the canonical `IMessageProvider` contract (`IInnerMessageProvider` for provider implementations): Memory, JSON, INI, TOML, XML, ResX, PO, satellite assemblies, pseudo-localization, SQLite, SQL Server, MySQL, PostgreSQL, the EF Core resource-set store, the caching and composite decorators, and the translation/TMS adapters. Registrations are capability-named: `AddMemoryLocalization(...)`, `AddJsonLocalization(...)`, `AddSqlServerLocalization(...)`, `AddAzureTranslationLocalization(...)`.
- **Exactly one provider** — the pipeline resolves exactly one registered provider. Provider chaining, merging, and provider-level fallback are not part of the architecture.
- **Culture fallback** — `DefaultMessageLocalizer` owns the single fallback algorithm: requested culture → parent cultures (invariant culture is never queried) → optional default culture. Providers are exact-match only.
- **Resource sets** — named, mutable string collections accessed through `ILocalizationStore`. This is a separate concept from the message-provider pipeline.
- **ASP.NET Core bridge** — `CustomStringLocalizerFactory` implements `IStringLocalizerFactory` over `ILocalizationStore`; each created localizer resolves the store per lookup, so a temporary store miss is never permanent.
- **Cancellation** — canonical async lookups accept an optional final `CancellationToken` that is propagated to cancellable I/O. Cancellation is never converted into a missing message.

For the public contracts, dependency rules, package layout, and quality gates, see [Architecture](docs/en/03-reference/00-architecture.md). For a short orientation, see [Architectural Overview](src/Jeninnet.Localization.Core/Architectural%20Overview.md).

## Repository layout

| Area | Projects |
|----|----|
| Core (`src/`) | `Jeninnet.Localization.Abstractions`, `Jeninnet.Localization.Core`, `Jeninnet.Localization.Storage`, `Jeninnet.Localization` (metapackage) |
| Providers (`src/providers/`) | `Memory`, `Json`, `Ini`, `Toml`, `Xml`, `ResX`, `Po`, `Pseudo`, `Satellite`, `Caching`, `Composite`, `Database.Abstractions`, `Database.SqlServer`, `Database.MySql`, `Database.PostgreSQL`, `Database.SQLite`, `Database.EntityFrameworkCore` |
| Translation (`src/translation/`) | `Translation.Azure`, `Translation.DeepL`, `Translation.Google`, `Translation.Tms`, `Translation.Tms.Crowdin`, `Translation.Tms.Weblate` |
| Interchange (`src/interchange/`) | `Interchange.Abstractions`, `Interchange.Csv`, `Interchange.Po`, `Interchange.ResX`, `Interchange.Xliff` |
| Shared sources (`src/shared/`) | Compiled into the consuming packages (no runtime assembly): translation pipeline, single-flight coalescing, TMS flow, file-provider shell, flat-catalog driver, placeholder scanner, provider registration, host culture, resource management, PO text parsing |
| Integrations (`integrations/`) | `Integrations.Avalonia`, `Integrations.Maui`, `Integrations.WinUI`, `Integrations.DataAnnotations`, `Integrations.Mvc.Validation`, `Integrations.Blazor`, `Integrations.Diagnostics`, `Integrations.JavaScript`, `Integrations.RequestLocalization` |
| Tests (`tests/`) | `Jeninnet.Localization.Tests` (MSTest) and `Jeninnet.Localization.HostIntegrations.Tests` (Windows-only MSTest) |
| Testing infrastructure | `Jeninnet.Assertions` (external NuGet package from its standalone repository; consumed by the test project) |
| Samples (`samples/`) | `Jeninnet.Localization.Docs` (documentation snippet verification, non-packable), `Jeninnet.Localization.Reference.*` (BookStore reference application: Shared, nine hosts, three test projects, shared content), `Jeninnet.Localization.HostSamples.Shared`, and `Jeninnet.Localization.Smoke.Aot` (Native AOT publish/run) — all non-packable |
| Consumers (`consumers/`) | `Jeninnet.Localization.Consumer` + `Jeninnet.Localization.Consumer.Tests` — package validation against the packed local feed (own solution; pack first) |

The 44 packable projects can be packaged locally with `dotnet pack Jeninnet.Localization.slnx -c Release` and `dotnet pack Jeninnet.Localization.Windows.slnx -c Release`; the previews are published to nuget.org and local packs are for validation. Before deleting generated directories such as `docs/_site/`, `docs/api/`, `artifacts/`, or `TestResults/`, see the cleanup guidance in [scripts/clean/README.md](scripts/clean/README.md).

## Solutions

[Jeninnet.Localization.slnx](Jeninnet.Localization.slnx) is the authoritative cross-platform solution. [Jeninnet.Localization.Windows.slnx](Jeninnet.Localization.Windows.slnx) is a focused Windows companion for the WinUI/MAUI host integrations, Windows samples, and Windows-only host tests. See [README-SOLUTIONS.md](README-SOLUTIONS.md) for the project inventory, the differences between the two solutions, and why the Windows solution exists.

## Reference application

If you want to **see the library in action**, start at the [Reference Application](samples/README.md): one BookStore application demonstrated through six hosts (API, MVC, Blazor Server, Blazor Hybrid, WinForms, WPF) and the full Provider matrix (build-safe local Providers, infrastructure-gated server databases, and credential-gated translation Providers). It covers host and Provider selection, User Secrets setup, culture switching, verified run commands, expected behavior, and troubleshooting.

## Quick start

```csharp
// Register the base pipeline (localizer, formatter, provider selector).
services.AddLocalization();

// Register exactly one message source, for example:
services.AddMemoryLocalization(options => options.Messages = messages);
// services.AddJsonLocalization(options => options.FilePath = "Localization/messages.json");
// services.AddXmlLocalization(options => options.FilePath = "Localization/messages.xml");
// services.AddSqlServerLocalization(options => options.ConnectionString = connectionString);
```

Optional registrations:

- `services.AddMvcLocalizedValidation()` — localized MVC validation metadata.
- `services.AddEntityFrameworkLocalizationStore(connectionString)` — EF Core resource-set store.

Inject `IMessageLocalizer` for canonical message lookups, or `IStringLocalizer<T>` after registering the Core `AddLocalization()` bridge and an `ILocalizationStore`. Create a non-generic `IStringLocalizer` from the registered `IStringLocalizerFactory` when a resource type is known only at runtime.

## Building and testing

```bash
dotnet build Jeninnet.Localization.slnx -c Release
dotnet test  Jeninnet.Localization.slnx -c Release

# Windows-only host packages, samples, and tests
dotnet build Jeninnet.Localization.Windows.slnx -c Release
dotnet test  tests/Jeninnet.Localization.HostIntegrations.Tests -c Release
```

Database runtime tests are environment-gated and run only when their connection string is provided: `JENINNET_TEST_SQLSERVER_CONNECTION`, `JENINNET_TEST_MYSQL_CONNECTION`, `JENINNET_TEST_POSTGRESQL_CONNECTION`. Without them they are reported as inconclusive. Repository maintenance tooling lives under [`scripts/`](scripts/README.md).

Package validation (`consumers/`) restores the **packed** packages from the local feed, so it runs after a pack and through its own solution; the tag-driven NuGet workflow runs it before publication:

```bash
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet pack Jeninnet.Localization.Windows.slnx -c Release
dotnet test consumers/Jeninnet.Localization.Consumers.slnx -c Release
```

## Documentation

The [live documentation site](https://tareknajem04.github.io/Jeninnet.Localization/) is the fastest way to explore the project in a browser, with English and [Arabic](https://tareknajem04.github.io/Jeninnet.Localization/ar/README.html) editions. The [Jeninnet.Localization reference book](docs/en/README.md) is the narrative guide to the model, providers, store, tooling, and operations. Start with:

- [Introduction](docs/en/01-getting-started/01-introduction.md) — the problem space and the two-layer model.
- [Quick start](docs/en/01-getting-started/03-quick-start.md) — a complete working walkthrough.
- [Package map](docs/en/appendix/02-package-map.md) — which package to reference.
- [Glossary](docs/en/appendix/01-glossary.md) — terms, abbreviations, and traceability IDs.
- [Release notes](docs/en/03-reference/09-release-notes.md) — publication status and 1.0 preview scope.
- [Migration guide](MIGRATION.md) — adopting the framework from Microsoft.Extensions.Localization, ResX, or a hand-rolled layer.
- [Known limitations](docs/en/03-reference/10-known-limitations.md) — design boundaries to read before adopting.
- [AOT (Ahead-of-Time) and trimming](docs/en/aot-and-trimming.md) — Native AOT/trimming posture and evidence.

Also see the [Architectural Overview](src/Jeninnet.Localization.Core/Architectural%20Overview.md), the [Reference Application guide](samples/README.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [CHANGELOG.md](CHANGELOG.md).

## License

MIT — see [LICENSE](LICENSE). Third-party notices and license attributions are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
