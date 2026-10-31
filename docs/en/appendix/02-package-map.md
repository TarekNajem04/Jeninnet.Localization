# Package Map

There are 35 packable projects in the repository. Every one has the same identity rule: project
name equals assembly name equals root namespace equals package id, and the solution version is
`1.0.0-preview.1`. No package has been published to
NuGet; local packages come from `dotnet pack` and the `artifacts/` folder.

Two rules decide most package choices:

1. **Choose any message provider, exactly one.** The pipeline resolves one `IInnerMessageProvider`.
   Adding a second provider is a configuration error, not a fallback layer.
2. **The core trio is the contracts baseline.** `Abstractions`, `Core`, and `Storage` are the
   shared foundation. A working message pipeline needs `Core` plus one provider; `Abstractions`
   arrives transitively, and `Storage` arrives transitively through the JSON, XML, and PO (Portable Object)
   providers. Reference `Storage` directly only when you implement a provider or tool.

The groups below list direct dependencies only, using short names after the first mention; the
package graph is small, but NuGet resolves the transitive closure.

## Contracts and pipeline (3)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md) | Contracts for providers, localizers, formatters, plural rules, diagnostics, and the store. | None (BCL (Base Class Library) only). |
| [`Jeninnet.Localization.Storage`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md) | Canonical message data models shared by file and database providers. | None (BCL only). |
| [`Jeninnet.Localization.Core`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) | The runtime pipeline, default localizer and formatter, provider selector, store bridge, and DI (Dependency Injection) registration. | `Abstractions`; `Microsoft.Extensions.Localization`, `Caching.Abstractions`, `AspNetCore.Http.Abstractions`, `Logging.Abstractions`. |

## Message providers (13)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Providers.Memory`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md) | Serves one flat, immutable dictionary; the deterministic choice for tests, prototypes, and WASM (WebAssembly) compositions. | `Abstractions`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Json`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md) | Serves flat JSON catalogs, one file per culture in multi-culture registration, with opt-in hot reload. | `Abstractions`, `Storage`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Xml`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md) | Serves XML message documents, one file per culture in multi-culture registration, with opt-in hot reload. | `Abstractions`, `Storage`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.ResX`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) | Serves embedded `.resx` resources through `ResourceManager` with exact-culture lookup. | `Abstractions`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Satellite`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md) | Serves compiled neutral resources and satellite assemblies, including packs authored elsewhere. | `Abstractions`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Po`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md) | Serves gettext PO text or MO (Machine Object) binaries, including contexts and per-form plural tables. | `Abstractions`, `Storage`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Pseudo`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md) | Produces a synthetic pseudo-localized culture for overflow and leak detection. | `Abstractions`; `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Providers.Database.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/README.md) | Shared base type and options for the ADO.NET database providers; not referenced directly by applications. | `Abstractions`. |
| [`Jeninnet.Localization.Providers.Database.SQLite`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SQLite/README.md) | Reads the `LocalizationMessages` table from SQLite. | `Database.Abstractions`; `Microsoft.Data.Sqlite`, `DependencyInjection`. |
| [`Jeninnet.Localization.Providers.Database.SqlServer`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.SqlServer/README.md) | Reads the `LocalizationMessages` table from SQL Server. | `Database.Abstractions`; `Microsoft.Data.SqlClient`, `DependencyInjection`. |
| [`Jeninnet.Localization.Providers.Database.MySql`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.MySql/README.md) | Reads the `LocalizationMessages` table from MySQL. | `Database.Abstractions`; `MySql.Data`, `DependencyInjection`. |
| [`Jeninnet.Localization.Providers.Database.PostgreSQL`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | Reads the `LocalizationMessages` table from PostgreSQL. | `Database.Abstractions`; `Npgsql`, `DependencyInjection`. |
| [`Jeninnet.Localization.Providers.Database.EntityFrameworkCore`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | The resource-set store over EF Core and SQL Server, with audit, history, notes, and synchronization. | `Abstractions`; `Microsoft.EntityFrameworkCore.SqlServer`, `DependencyInjection.Abstractions`, `Configuration.Abstractions`. |

The EF Core package is a store, not a message provider. It implements `ILocalizationStore`,
`IResourceAdminStore`, and `IResourceHistoryStore`; it does not implement `IInnerMessageProvider`.
The four ADO.NET providers read `LocalizationMessages`; the EF store owns `LocalizedStrings`,
`AuditRecords`, `StringVersions`, `CatalogState`, and `StringNotes`. They are separate schemas.

## Translation providers (2)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Translation.Azure`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md) | Azure Translator v3 provider with TTL (Time To Live) cache, single-flight, batching, preload, refresh, glossary, translation memory, retries, breaker, and rate limiting. | `Abstractions`; `DependencyInjection.Abstractions`, `Microsoft.Extensions.Http`. |
| [`Jeninnet.Localization.Translation.Google`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Google/README.md) | Google Translate v2 provider with the minimal single-key path and no provider cache or batching. | `Abstractions`; `DependencyInjection.Abstractions`, `Microsoft.Extensions.Http`. |

A translation provider is the single message provider, not a layer behind one. Its source text is
the lookup key.

## Integrations (9)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Integrations.Avalonia`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Avalonia/README.md) | Culture service and file-backed persistence for Avalonia hosts, including an RTL direction helper. | `Abstractions`; `Avalonia`, `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Integrations.Maui`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Maui/README.md) | Culture service and MAUI preferences persistence for mobile and desktop MAUI hosts. | `Abstractions`; `Microsoft.Maui.Core`, `Microsoft.Maui.Essentials`, `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Integrations.WinUI`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.WinUI/README.md) | Culture service and Windows application-data persistence for WinUI 3 hosts. | `Abstractions`; `Microsoft.WindowsAppSDK`, `DependencyInjection.Abstractions`. |
| [`Jeninnet.Localization.Integrations.DataAnnotations`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md) | Localized validation attributes that resolve through `IStringLocalizer<>`. | `Microsoft.Extensions.Localization.Abstractions`; no Jeninnet dependency. |
| [`Jeninnet.Localization.Integrations.Mvc.Validation`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Mvc.Validation/README.md) | MVC validation and display metadata, HTML-safe localizers, view localization, and the `<loc>` tag helper. | `Core`; `Microsoft.AspNetCore.App` framework reference. |
| [`Jeninnet.Localization.Integrations.RequestLocalization`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.RequestLocalization/README.md) | Builds request-localization options from the store and narrows results to served cultures. | `Abstractions`; `Microsoft.AspNetCore.App` framework reference. |
| [`Jeninnet.Localization.Integrations.Blazor`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md) | Circuit-scoped and static-scope culture management plus the WASM provider guard. | `Abstractions`; `Microsoft.AspNetCore.App` framework reference. |
| [`Jeninnet.Localization.Integrations.JavaScript`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.JavaScript/README.md) | Versioned endpoint and static export for allow-listed JavaScript resources with ETags. | `Abstractions`; `Microsoft.AspNetCore.App` framework reference. |
| [`Jeninnet.Localization.Integrations.Diagnostics`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md) | Health checks for the provider and store; tracing and metrics live in Abstractions. | `Abstractions`; `DependencyInjection.Abstractions`, `Diagnostics.HealthChecks`. |

`Integrations.DataAnnotations` is deliberately decoupled from the Jeninnet runtime graph. The MVC
and Blazor integrations use framework references rather than package pins for the ASP.NET Core
shared framework.

## Interchange (5)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Interchange.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md) | Shared status vocabulary and catalog file codec for the interchange family. | None (BCL only). |
| [`Jeninnet.Localization.Interchange.Csv`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md) | RFC 4180 CSV export and validate-then-apply import for spreadsheet workflows. | `Interchange.Abstractions`. |
| [`Jeninnet.Localization.Interchange.Po`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md) | PO text export and import for gettext/Poedit workflows. | `Interchange.Abstractions`. |
| [`Jeninnet.Localization.Interchange.ResX`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md) | Canonical ResX 2.0 export and import for ResX editors. | `Interchange.Abstractions`. |
| [`Jeninnet.Localization.Interchange.Xliff`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md) | XLIFF (XML Localization Interchange File Format) 2.0 export and import for translation vendors. | `Interchange.Abstractions`. |

Interchange packages are tooling. They are not runtime providers, and importing a file does not
change a running catalog unless the host applies the result.

## Tooling and build-time assets (3)

| Package | Purpose | Direct dependencies |
|---|---|---|
| [`Jeninnet.Localization.Generators.Keys`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) | Roslyn incremental generator that emits strongly typed key constants from `*.loc.json`. | `Microsoft.CodeAnalysis.CSharp` (private), `System.Collections.Immutable`; `netstandard2.0`. |
| [`Jeninnet.Localization.Analyzers.Localizability`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) | Roslyn analyzer and code fix (`JLNLOC001`) that flags hardcoded user-facing strings. | `Microsoft.CodeAnalysis.CSharp` (private), `System.Collections.Immutable`; `netstandard2.0`. |
| [`Jeninnet.Localization.Tools.Keys`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md) | The `jeninnet-localization-keys` .NET tool for key extraction and catalog parity. | `Microsoft.CodeAnalysis.CSharp`; `net8.0`/`net10.0` tool. |

The generator and analyzer are `DevelopmentDependency` packages with build output disabled. They
ship as analyzer assets, not runtime libraries, and the analyzer package carries both the analyzer
and its code fix under `analyzers/dotnet/cs`. The sibling
`Jeninnet.Localization.Analyzers.Localizability.CodeFixes` project is not packable; its DLL travels
inside the analyzer package.

## Summary

| Group | Count | Reference when |
|---|---|---|
| Contracts and pipeline | 3 | You are starting an application or implementing an extension point. |
| Message providers | 13 | You need exactly one source of message templates; add the store package only for resource-set administration. |
| Translation providers | 2 | Request-time machine translation is an explicit product decision. |
| Integrations | 10 | You need Avalonia, MAUI, WinUI, MVC, request cultures, Blazor, JavaScript delivery, or health checks. |
| Interchange | 5 | You exchange catalogs with translators or other tools. |
| Tooling and build-time assets | 3 | You want compile-time keys, authoring suggestions, or a CI parity gate. |
| **Total** | **35** | |

For the smallest working setup, reference `Core` and one provider. Add `Storage` explicitly only
if you implement a provider or tool, add the store only if you need `IStringLocalizer` or governed
resource sets, and add integrations one at a time as the host needs them. See
[Installation and first registration](../01-getting-started/02-installation.md) for the package
selection walkthrough and [Deployment](../04-operations/01-deployment.md) for where each package
runs.
