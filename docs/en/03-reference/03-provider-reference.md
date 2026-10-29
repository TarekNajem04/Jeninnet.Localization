# Provider Reference

Every message source implements `IInnerMessageProvider`. Exactly one resolved provider serves a
composition. This chapter groups the provider packages by family, records the option names and
defaults that actually exist, and states what each package does not do.

Package identity is the project name: `Jeninnet.Localization.Providers.*` for runtime providers,
`Jeninnet.Localization.Translation.*` for machine translation, and the EF Core store package for the
resource-set store. `Jeninnet.Localization.Providers.Database.Abstractions` is a shared base library,
not a registrable source.

Generated provider contracts: <xref:Jeninnet.Localization.Abstractions.IInnerMessageProvider> and
<xref:Jeninnet.Localization.Abstractions.IMessageProvider>.

## Summary

| Package | Source | Culture model | Reload | Main limit |
|---|---|---|---|---|
| `Jeninnet.Localization.Providers.Memory` | In-memory dictionary | One flat map; ignores culture | None | No external editing |
| `Jeninnet.Localization.Providers.Json` | One JSON file | One file per culture; dispatcher for multi-culture | Opt-in watch + `RefreshAsync` | Flat strings only |
| `Jeninnet.Localization.Providers.Xml` | One XML file | One file per culture; dispatcher for multi-culture | Opt-in watch + `RefreshAsync` | Flat strings, XML-name keys |
| `Jeninnet.Localization.Providers.Po` | PO (Portable Object) text or MO (Machine Object) binary | One catalog per culture | Opt-in watch + `RefreshAsync` | Tooling discipline required |
| `Jeninnet.Localization.Providers.ResX` | Compiled `.resources` via `ResourceManager` | Exact culture, no parent probing | None | Strings only; missing base throws |
| `Jeninnet.Localization.Providers.Satellite` | Neutral resources + satellite packs | Exact culture, no parent probing | None | Strings only; packs are external |
| `Jeninnet.Localization.Providers.Pseudo` | Transformed in-memory source | Serves one synthetic culture | None | QA aid, not a translation |
| `Jeninnet.Localization.Providers.Database.SqlServer` | SQL Server table | Exact `(key, culture)` rows | None | DB round trip per step |
| `Jeninnet.Localization.Providers.Database.MySql` | MySQL table | Exact `(key, culture)` rows | None | DB round trip per step |
| `Jeninnet.Localization.Providers.Database.PostgreSQL` | PostgreSQL table | Exact `(key, culture)` rows | None | DB round trip per step |
| `Jeninnet.Localization.Providers.Database.SQLite` | SQLite table | Exact `(key, culture)` rows | None | DB round trip per step |
| `Jeninnet.Localization.Providers.Database.EntityFrameworkCore` | SQL Server resource-set store | Set + exact culture | Store-owned reload | Not a message provider; not AOT (Ahead-of-Time) |
| `Jeninnet.Localization.Translation.Azure` | Azure Translator v3 | Target culture; key is source text | `RefreshAsync` | Network, cost, one-provider rule |
| `Jeninnet.Localization.Translation.Google` | Google Translate v2 | Target culture; key is source text | None | Network, cost, no cache/batch |

## Memory

`AddMemoryLocalization` copies the supplied `Messages` dictionary into an immutable construction
snapshot. Lookups are dictionary reads with ordinal, case-sensitive comparison. The provider ignores
the culture argument, so one instance serves one flat map. Multi-culture compositions use
`CultureAwareMessageProvider` with one map per culture.

- **Options:** `MemoryLocalizationOptions.Messages` (`IReadOnlyDictionary<string,string>`, default
  empty; must not be null).
- **Reload:** none. Changes require a new registration or process restart.
- **WASM (WebAssembly):** declares `IWasmCompatibleMessageProvider`.
- **Choose it for:** tests, prototypes, small static catalogs, and browser-compatible hosts.

## JSON

`AddJsonLocalization` registers one `JsonMessageProvider`. The file is loaded lazily on the first
lookup under synchronized one-shot initialization. The table is immutable for the provider's
lifetime unless hot reload is enabled.

- **Options:** `FilePath` (required; must exist at registration), `WatchForChanges` (default
  `false`), `ReloadDebounce` (default 500 ms; must be positive when watching).
- **Shape:** a flat JSON object mapping string keys to string templates. The provider deserializes
  into a dictionary; it does not use nested objects for keys.
- **Reload:** opt-in file watch. A change swaps the whole table atomically. A failed reload keeps the
  last-good table and raises `AcquisitionFailed`; a success raises `TranslationAcquired`.
  `RefreshAsync` reloads immediately and throws on failure.
- **Multi-culture:** `AddJsonLocalizationProviders(baseDirectory, cultures)` expects
  `{baseDirectory}/{subdirectory}/{culture}.json`, or use the builder overload for explicit paths.
  Each provider is wrapped in `CultureAwareMessageProvider`.
- **Limits:** no contexts, no plural tables, no XML-name restrictions to worry about, but also no
  metadata or notes.

JSON is the canonical format for the key generator (`*.loc.json`). The convention overload treats the
file stem as the culture code, so a file named `en.loc.json` is not culture `en`.

## XML

`AddXmlLocalization` has the same lifecycle and option names as JSON, with
`XmlLocalizationOptions`. The file is a flat XML document whose direct child elements are entries:
the element local name is the key and the element value is the template.

- **Options:** `FilePath` (required; exists at registration), `WatchForChanges` (default `false`),
  `ReloadDebounce` (default 500 ms; positive when watching).
- **Shape:** `<messages><App.Title>Bookstore</App.Title></messages>`. Keys must be valid XML names.
  Duplicate element names fail the load.
- **Reload:** identical to JSON, including the events and the throwing `RefreshAsync`.
- **Multi-culture:** `AddXmlLocalizationProviders` uses `{culture}.xml` under the subdirectory.
- **Limits:** flat strings only; no contexts or plural tables. XML element-name rules constrain keys.

## PO and MO

`AddPoLocalization` serves gettext catalogs. A catalog is exact-match templates plus plural-form
tables and contexts. The file may be PO text or a GNU MO binary.

- **Options:** `FilePath` (required; exists at registration), `Format` (`PoCatalogFormat.Auto`
  default; `Auto` selects MO by `.mo` extension, otherwise PO), `WatchForChanges` (default `false`),
  `ReloadDebounce` (default 500 ms).
- **Multi-culture:** `AddPoLocalizationProviders(baseDirectory, cultures)` expects `{culture}.po`.
  The default location provider does not probe for `.mo`; use explicit paths, a custom
  `IPoFileLocationProvider`, or the builder.
- **Plural forms:** implements `IPluralFormMessageProvider`. When the catalog carries a
  `Plural-Forms` header, the provider selects the exact `msgstr[n]` index. Otherwise it uses the
  CLDR (Common Locale Data Repository) pair mapping.
- **Contexts:** `msgctxt` entries are addressed through the EOT (End of Transmission) composite key produced by
  `LocalizeWithContextAsync`.
- **Reload:** opt-in watch; `RefreshAsync` throws on failure. MO catalogs reload the same way.
- **Limits:** translator tooling and file discipline are required. Plural semantics depend on the
  header the catalog carries; the framework does not fabricate gettext expressions.

Choose JSON/XML for repository-owned, code-deployed catalogs; PO for gettext tools and plural or context needs.

## ResX and satellite

`AddResXLocalization(baseName, assembly)` and `AddSatelliteLocalization(baseName, assembly)` both use
`ResourceManager`. They differ in what they expect on disk.

- **ResX options:** `BaseName` (required, non-empty), `ResourceAssembly` (default is the provider
  assembly, not the application assembly; set it explicitly).
- **Satellite options:** the same two names with the same default. The base name is the manifest
  resource name without culture or extension; satellite packs are probed next to the assembly.
- **Culture:** both call `GetResourceSet(culture, createIfNotExists: true, tryParents: false)`.
  Parent probing is disabled so the localizer remains the only fallback owner.
- **Missing behavior:** ResX throws `MissingManifestResourceException` when the resource base itself
  is missing. Satellite throws for a missing base but treats a missing satellite as a clean `null`.
  Satellite converts a non-string resource to `null`.
- **Reload:** none. Rebuild and redeploy.
- **Choose ResX** when the `.resx` files are authored in the same project; choose **Satellite** when
  the strings arrive as precompiled packs, including packs authored elsewhere.

## Pseudo-localization

`AddPseudoLocalization` serves a synthetic culture by transforming source templates. It accents Latin
letters through a fixed map, copies balanced `{...}` spans verbatim, wraps the result in
`[!! ... !!]`, and appends `~` padding.

- **Options:** `SourceTemplates` (required, non-null), `PseudoCulture` (default `qps-ploc`),
  `ExpansionPercent` (default 35; valid 0–100).
- **Culture:** only the configured synthetic culture is served; every other culture returns `null`.
- **Determinism:** the transform is a pure function of template and percentage. There are no
  timestamps, randomness, or host-culture dependencies.
- **WASM:** declares `IWasmCompatibleMessageProvider`.
- **Choose it for:** layout overflow, hard-coded-string leaks, and demos. It is not a translation.

## Database message providers

`AddSqlServerLocalization`, `AddMySqlLocalization`, `AddPostgreSqlLocalization`, and
`AddSqliteLocalization` register ADO.NET providers that query one table per lookup.

- **Options:** `ConnectionString` (required, non-empty) and `TableName` (default
  `LocalizationMessages`). `TableName` accepts up to three dot-separated segments; each segment
  starts with a letter or underscore and is 1–128 characters. `dbo.LocalizationMessages` is valid;
  spaces and punctuation are rejected when the property is set.
- **Schema:** columns `MessageKey`, `Culture`, `Template`. The query is parameterized; identifiers
  are escaped per vendor.
- **Culture:** exact `(key, culture)` rows. Key case sensitivity follows the column collation.
- **Reload:** none. A write is visible on the next lookup; there is no in-box message cache.
- **Cost:** one connection open and one query per fallback step. Add caching above the provider if
  that cost matters.
- **Choose it for:** a central catalog that changes without a deployment. The governed admin and
  history APIs belong to the EF store, not these providers.

`Jeninnet.Localization.Providers.Database.Abstractions` contains `DatabaseMessageProviderBase<TOptions>` and the shared
options type. It has no `Add...` registration and is not a provider on its own.

## EF Core resource-set store

`AddEntityFrameworkLocalizationStore(connectionString)` or the `IConfiguration` overload with a named
connection string (default `LocalizationDb`) registers `SqlDbLocalizationStore` as a singleton
`ILocalizationStore`. It targets SQL Server.

- **Not an `IMessageProvider`.** It does not feed `IMessageLocalizer`; it feeds `IStringLocalizer`
  through `CustomStringLocalizerFactory`.
- **Schema:** `LocalizedStrings`, audit records, replay journal, version row, and notes.
- **Capabilities:** administration and history; see [Storage API](02-storage-api.md).
- **Schema management:** consumer-owned. `EnsureCreated` includes the current model; older databases
  need a migration.
- **AOT:** excluded from the trim/AOT contract because EF Core is not fully trim-safe.
- **Choose it for:** runtime-editable resource sets, audited administration, notes, and code-first
  synchronization.

## Azure Translator

`AddAzureTranslationLocalization` registers the capable translation provider.

- **Required:** `ApiKey`. **Defaults:** `Endpoint`
  `https://api.cognitive.microsofttranslator.com`, `Region` empty, `FromLanguage` empty,
  `CachingEnabled` true, `CacheExpiration` one hour, `RequestTimeout` 30 seconds, `RetryMaxAttempts`
  3, `RetryBaseDelay` 200 ms, `RetryMaxDelay` 5 seconds, `BreakerFailureThreshold` 5,
  `BreakerOpenDuration` 30 seconds, `RateLimitRequestsPerSecond` 0 (disabled),
  `RateLimitBurstSize` 1.
- **Optional:** `Glossary` and `TranslationMemory`; both are captured or snapshotted at construction.
- **Capabilities:** multi-key batching (100 keys), caching, preload when caching is enabled, refresh,
  and acquisition events.
- **Limits:** network and cost on cache miss; one provider only; the key is the source text.
- **Choose it for:** dynamic content, backfill, or a reviewed batch workflow. For normal products,
  materialize the results into a file or database provider.

## Google Translate

`AddGoogleTranslationLocalization` registers the minimal provider.

- **Required:** `ApiKey`. **Default:** `Endpoint` `https://translation.googleapis.com/language/translate/v2`.
- **Capabilities:** `Supports` returns `false` for every feature. There is no batching, cache,
  preload, refresh, or acquisition event.
- **Behavior:** the key is the source text; the target language is the culture name; source language
  is auto-detected. Invariant returns `null` without a request. A remote failure propagates.
- **Choose it for:** low-volume dynamic content where a single paid call per lookup is acceptable.

## Worked example: choosing one source

A repository-owned catalog serves `IMessageLocalizer` from multi-culture JSON. A translator team works
on the same content through PO interchange, but the runtime still reads JSON. An administered catalog
uses the EF store for `IStringLocalizer` and a database provider for the pipeline only if both paths
are intentional. Each host registers one source; the format used for exchange does not change that.

## Pitfalls

**Pitfall:** registering two providers to combine sources. The selector throws; consolidate the data.

**Pitfall:** mutating provider options after registration. Options are construction snapshots.

**Pitfall:** treating file watching as a deployment mechanism. It is opt-in and keeps the last-good
table on failure; deploy content deliberately.

## Where to go next

- [Choosing a provider](../02-guides/01-choosing-a-provider.md) for the scenario-level decision.
- [Configuring providers](../02-guides/02-configuring-providers.md) for validation and snapshots.
- [Machine translation](../02-guides/12-machine-translation.md) for the resilience and batch model.
- [Compatibility and versions](07-compatibility-and-versions.md) for the AOT exclusions.
