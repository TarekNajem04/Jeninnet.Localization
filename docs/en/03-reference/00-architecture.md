# Architecture

This chapter is the public architecture overview for `Jeninnet.Localization`. It describes the
implemented design, the dependency rules enforced by the test suite, and the extension points a
host or provider author should use. It is descriptive, not a second API reference; the generated
[API reference](11-api-reference.md) remains the member-level lookup.

## 1. Design goals

The framework is a modular, key-driven localization system for .NET:

- **Message pipeline** — a key is resolved to a template, with culture fallback owned by one
  localizer and formatting performed by a formatter.
- **Resource sets** — named string collections with administration, audit history, caching
  decorators, and code-first synchronization.
- **Sources** — file providers (JSON, XML, ResX, PO/MO, satellite, pseudo), database providers
  (SQL Server, MySQL, PostgreSQL, SQLite), an EF Core store, and machine-translation providers.
- **Interchange** — CSV, PO, ResX, and XLIFF catalogs for translator workflows.
- **Framework and host integrations** — ASP.NET Core, Blazor, JavaScript delivery, DataAnnotations,
  diagnostics, Avalonia, MAUI, and WinUI culture ownership.
- **Tooling** — a key generator, a parity CLI, and a localizability analyzer with a code fix.

The architecture deliberately keeps the runtime small: the Contracts package is BCL-only, message
providers are exact-match, and fallback, composition, and mutation policy are not duplicated across
providers.

## 2. Package map

The repository contains 35 packable projects. The package map
([English](../appendix/02-package-map.md)) is the authoritative inventory; the groups are:

- Contracts and pipeline: `Abstractions`, `Core`, `Storage`.
- Message providers: Memory, JSON, XML, ResX, Satellite, PO, Pseudo, SQL Server, MySQL, PostgreSQL,
  SQLite, EF Core store, and Database abstractions.
- Translation providers: Azure Translator and Google Cloud Translation.
- Integrations: DataAnnotations, MVC validation, RequestLocalization, Blazor, JavaScript,
  Diagnostics, Avalonia, MAUI, and WinUI.
- Interchange: Abstractions, CSV, PO, ResX, and XLIFF.
- Tooling: key generator, localizability analyzer, and parity CLI.

Runtime libraries target `net8.0`, `net9.0`, and `net10.0`, except the platform packages: MAUI
targets `net10.0` and WinUI targets `net10.0-windows10.0.19041.0`. The generator and analyzer target
`netstandard2.0`; the parity tool targets `net8.0` and `net10.0`.

## 3. Dependency rules

The architecture tests enforce these library-to-library edges:

| Layer | May reference | Must not reference |
|---|---|---|
| `Abstractions` | BCL | Core, providers, integrations, storage implementations |
| `Core` | `Abstractions` | Providers, integrations, EF Core |
| `Storage` | BCL | Core, providers |
| Providers | `Abstractions`; `Storage` for file models; `Database.Abstractions` for database vendors | Core |
| EF Core store | `Abstractions`, EF Core, configuration abstractions | Core |
| Interchange | `Interchange.Abstractions` | Provider or Core runtime types |
| Translation | `Abstractions`, HTTP/Options infrastructure | Core |
| MVC validation | `Core`, ASP.NET Core shared framework | Other integrations |
| Other integrations | `Abstractions` and their platform packages | Core |
| Tooling | BCL, Roslyn | Runtime libraries |

Cross-cutting pins: exactly one registered message provider, no dependency cycles, no provider
`*Entity`/`*Dto` types (except the storage entity), no provider disposal abstractions, construction-
time option snapshots, and `ProjectName == AssemblyName == RootNamespace == PackageId`.

## 4. Contracts and pipeline

The main contracts live in `Jeninnet.Localization.Abstractions`:

- `IMessageProvider.GetMessageTemplateAsync(key, culture, cancellationToken)` returns an exact
  template or `null`.
- `IInnerMessageProvider` marks concrete providers and lets the selector enforce a single provider.
- `IMessageLocalizer` exposes culture-aware and current-culture lookups, plural keys, and
  context-scoped keys.
- `IMessageFormatter` handles substitution; `ICultureAwareMessageFormatter` can use the culture.
- `IProviderCapabilities` and `ProviderFeature` advertise multi-key, preload, refresh, event, and
  caching support through matching interfaces.
- `ILocalizationStore` and its administrative, history, and synchronization companions own
  resource-set reads and governed mutations.
- `PluralRules`, `MessageGlossary`, and translation-memory contracts provide language behavior
  without coupling it to a provider.

A lookup flows through one `DefaultMessageLocalizer`, which owns the complete fallback chain:
requested culture, parent cultures, then the configured default culture. Each step asks the single
resolved provider for an exact match. The selector rejects zero providers (lookups return misses)
or multiple providers (construction fails), so there is no hidden chaining or provider-level
fallback.

Formatting is single-owner: `DefaultMessageFormatter` substitutes values, `IcuMessageFormatter`
adds ICU-compatible plural/select/number/date handling, and `HtmlSafeMessageFormatter` and
`MarkdownMessageFormatter` compose at explicit boundaries. Missing keys are reported only after
fallback exhaustion, and diagnostics never tag template text.

## 5. Providers and storage

Every message provider uses a construction-time snapshot, exact-match lookup, singleton
registration, and no `Core` dependency.

- **File providers** load a synchronized immutable snapshot and optionally hot-reload through the
  shared debounced reloader. Multi-culture builders compose a culture-aware provider that tries the
  full culture name and then the two-letter language without a parent walk.
- **PO** supports PO text and GNU MO, including plural forms, refresh, and events.
- **ResX/Satellite** use `ResourceManager` with `tryParents:false`; satellite packages read compiled
  neutral and satellite assemblies.
- **Database providers** build parameterized single-row queries through a shared base, validate
  table names once, and escape identifiers per vendor.
- **Memory and Pseudo** are deterministic, in-memory providers; both are suitable for
  WebAssembly-compatible compositions where the capability marker requires it.

Storage distinguishes three layers: read-only resource sets (`ILocalizationStore`), governed
administration (`IResourceAdminStore`), and hash-chained history (`IResourceHistoryStore`). Mutations
are validate-then-apply and return conflict results rather than throwing for expected conflicts.
Caching decorators and the EF Core store implement the same contracts without changing lookup
semantics.

## 6. Interchange and tooling

The four catalog formats share a vocabulary and a codec boundary: export/parse/import report
unmodified, updated, skipped, unknown, stale-source, and unsupported keys; imports never invent
keys and dry runs mutate nothing.

Tooling is build-time only:

- the key generator reads `*.loc.json` additional files and emits deterministic constants;
- the parity CLI reports missing, unused, and placeholder-drift keys with stable exit codes;
- the analyzer flags hardcoded user-facing strings and offers a code fix when a matching key
  constant exists.

## 7. Framework and host integrations

ASP.NET Core integrations cover MVC validation and display metadata, Razor `<loc>`, request
cultures, Blazor circuit/static scope, JavaScript delivery with ETags, DataAnnotations, and health
checks. The desktop/mobile integrations own the active culture through a shared host-culture
contract: supported/default cultures, platform persistence, `HostCultureService`, change
notifications, and RTL `FlowDirection` helpers.

| Host | Persistence | Direction helper |
|---|---|---|
| Avalonia | file-backed store | `Avalonia.Media.FlowDirection` |
| MAUI | `Preferences.Default` | `Microsoft.Maui.FlowDirection` |
| WinUI 3 | Windows application data local settings | `Microsoft.UI.Xaml.FlowDirection` |

Platform hosts call `InitializeAsync` during startup and `SetCultureAsync` on the UI thread; the
packages do not marshal to a dispatcher. Each package exposes the same culture contract but keeps
its platform dependency isolated.

## 8. Quality gates

- `ArchitectureTests` enforce package inventory, project references, dependency rules, no cycles,
  provider behavior pins, and platform-package boundaries.
- Public-API baselines cover all 35 packable assemblies: 33 cross-platform baselines are compared
  under each runtime target; MAUI and WinUI are validated by the Windows host test project.
- The unified test suite covers the pipeline, every provider, storage, administration, history,
  interchange, integrations, translation, tooling, and cross-host culture behavior.
- DocFX metadata, repository links, README claims, Arabic text, API coverage, package validation,
  consumer restore/build/test, formatting, vulnerability scanning, and AOT smoke checks run in CI.
