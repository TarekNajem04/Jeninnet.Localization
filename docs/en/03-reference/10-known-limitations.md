# Known Limitations and Support Boundaries

This page is the honest boundary sheet for the library. It is not a bug list: each item is a
deliberate design choice, a current scope limit, or a responsibility that belongs to the host or the
consumer. Read it before adopting the library, and follow the linked chapter for the full behavior.

The short version: the library owns message resolution, formatting, provider selection, the store
contracts, and the tooling around catalogs. It does not own layout, translation management, database
provisioning, or application authorization.

## Architecture and pipeline

- **Exactly one message provider per composition.** Zero providers is valid and makes lookups return
  `null`; more than one throws when the provider selector is constructed. There is no chaining,
  ordering, priority, aggregation, or provider-level fallback. See
  [The mental model](../01-getting-started/04-mental-model.md).
- **Providers are exact-match.** A provider resolves one key for one culture. It never walks parent
  cultures and never applies the default culture. That walk belongs only to
  `DefaultMessageLocalizer`. See [Cultures and fallback](../02-guides/03-cultures-and-fallback.md).
- **Fallback is fixed.** The requested culture, its parent cultures, then the configured default
  culture. The invariant culture is never queried. The default culture is captured when the localizer
  is constructed; mutating options later has no effect. See
  [Core API](01-core-api.md).
- **A miss is not an error.** A complete miss returns `null`; the formatter is not called. Provider
  and formatter exceptions, including cancellation, propagate. Cancellation is never converted into
  a miss. See [Core API](01-core-api.md).
- **There is no second localization engine.** Translation providers and file/database providers are
  all ordinary providers. Registering one does not add a fallback layer behind another.

## Culture and consumer APIs

- **`IStringLocalizer` is a bridge, not the fallback owner.** The bridge captures
  `CurrentUICulture` when the localizer is created, probes one exact resource set per lookup, and
  does not walk parent cultures. A missing value is `[MISSING:key]` with `ResourceNotFound = true`.
  See [Core API](01-core-api.md).
- **No root `IStringLocalizer` registration.** The bridge is intended for `IStringLocalizer<T>` and
  the framework's typed/localizer factory paths; see the Core reference before assuming a root
  localizer exists.
- **A registered store does not feed `IMessageLocalizer`.** The message pipeline reads an
  `IInnerMessageProvider`; the store feeds `IStringLocalizer` through the bridge. These are separate
  paths. See [Resource sets and the store](../02-guides/06-resource-sets-and-store.md).
- **Culture selection and direction are host code.** Request localization, `CurrentUICulture`,
  cookies, and layout direction are application responsibilities. The library provides strings and
  fallback, not request middleware or layout behavior outside the integrations that explicitly own
  it. See [RTL (Right-to-Left) and bidirectional text](../02-guides/16-rtl-and-bidi.md).

## Providers and content sources

- **Memory is culture-agnostic.** It serves one flat map and ignores the culture argument. Use a
  per-culture file/database provider or a culture-aware composition when cultures differ. See
  [Memory provider](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md).
- **File providers load at registration.** JSON, XML, and PO (Portable Object) validate and read their content during
  registration; a missing or malformed file fails there. Hot reload is opt-in
  (`WatchForChanges = true`), debounced, and whole-table, not per culture.
- **Compiled resources do not watch files.** ResX and satellite providers read compiled resources;
  replacing a `.resx` or satellite assembly at runtime requires a new deployment, not a file watch.
- **Database changes are visible on the next lookup, but there is no provider cache.** Each fallback
  step opens a connection and executes one scalar query. If that cost matters, cache above the
  provider with a deliberate invalidation strategy. See [Performance](../04-operations/03-performance.md).
- **Case sensitivity follows the database collation.** The database providers compare
  `MessageKey`/`Culture` with the column collation. Use a case-sensitive/bin collation when keys must
  match ordinally. See the provider README for your vendor.

## Formatting and encoding

- **The default formatter is deliberately small.** It substitutes named `{...}` placeholders; missing
  parameters stay literal, `null` becomes empty, and there is no format-string handling. Use the
  opt-in ICU (International Components for Unicode) formatter for plural/select/number/date patterns. See
  [Formatting and plurals](../02-guides/04-formatting-and-plurals.md).
- **Markdown is a documented subset, not a full CommonMark engine.** The Markdown formatter renders
  that subset and never parses arguments as Markdown or HTML. Raw HTML is inert unless explicitly
  allowed. See [Formatting and plurals](../02-guides/04-formatting-and-plurals.md).
- **The HTML-safe formatter is not a sanitizer.** It encodes string arguments; it does not clean
  untrusted template markup. Templates are trusted content, and the host remains responsible for
  output encoding and content review. See [Security](../04-operations/02-security.md).
- **No automatic translation of fragments.** Translating concatenated fragments produces wrong word
  order and broken grammar. Translate complete sentences and preserve placeholders. See
  [Translator workflow](../02-guides/11-translator-workflow.md).

## Store, administration, and history

- **The store is a separate contract.** `ILocalizationStore` and its resource-set model do not
  replace the message provider. A store registration alone does not make `IMessageLocalizer` serve
  messages. See [Storage API](02-storage-api.md).
- **EF Core store targets SQL Server.** The shipped store implementation is SQL Server-specific. It
  is not the same schema as the ADO.NET `LocalizationMessages` table, and one does not migrate the
  other.
- **No EF Core migrations ship with the package.** A new database can be created with
  `EnsureCreated`, but only when the database is empty; schema management is consumer-owned. See
  [EF Core store schema](../04-operations/06-ef-core-store-schema.md) for the tables, provisioning
  checks, and coexistence rules.
- **Governed mutations require authorization and cooperation.** The store serializes mutations per
  instance; cross-process writers are not coordinated by that in-process gate. Add database
  constraints or serialize writers externally. Administration also requires the host to authorize
  callers; the library does not provide an authorization policy.
- **History is audit evidence, not a backup.** The audit/history records and hash chain detect
  tampering and reproduce versions; they do not replace database backups or access control. See
  [Audit and history](../02-guides/07-audit-and-history.md).
- **Synchronization has no delete.** `ResourceSynchronizer` adds by default, can update only when
  explicitly allowed, and never deletes keys or cultures. See
  [Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

## Database providers and schema

- **Read-only by design.** The ADO.NET providers only issue `SELECT`; they do not create tables,
  migrate schemas, seed rows, or reload catalogs.
- **No automatic schema creation.** Create the canonical `LocalizationMessages` table with the
  vendor-specific script described in the [database guide](../../database/README.md) and seed it
  separately. The guide also covers the pre-canonical rename migration.
- **No provider cache.** One connection and one query per fallback step. Use connection pooling and
  application-level caching where appropriate; the store cache decorators do not wrap message
  providers.
- **Database availability is your availability.** Provider exceptions propagate. A database outage
  is not converted into a miss or a fallback, so the host must decide how to degrade.

## Translation providers

- **Network and credentials are required.** Azure Translator and Google Cloud Translation call live
  services and require an API key. The repository defaults to local providers and never commits
  credentials.
- **Placeholders are not guaranteed to survive machine translation.** The providers send the message
  key as the source template and receive translated text; preserve and verify placeholders as part of
  the translation workflow. See [Machine translation](../02-guides/12-machine-translation.md).
- **Google reports no advanced capabilities.** Azure exposes caching, batching, glossary, memory,
  and resilience controls; Google's provider is intentionally minimal. See the
  [provider reference](03-provider-reference.md).
- **A translation provider is the only provider.** There is no file or database catalog behind it to
  absorb a service failure. Choose caching and degradation deliberately.
- **Real end-to-end translation is credential-gated.** Tests use fakes and deterministic behavior;
  only a live run with real keys proves the external service path.

## Tooling and build-time behavior

- **The analyzer covers curated shapes.** `JLNLOC001` flags UI text, exceptions, validation messages,
  view data, `MessageBox`, `Display` attributes, and similar positions. It is not a generic
  hardcoded-string detector. See [Tooling reference](05-tooling-reference.md).
- **The code fix only references existing constants.** It replaces a literal when an exact
  `const string` exists in a type whose name ends in `Keys`; it never extracts text, creates
  catalogs, or offers a fix-all. See
  [Code-fix README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability.CodeFixes/README.md).
- **The parity tool is report-only.** It reports missing/unused keys, template drift, format gaps,
  and placeholder drift; it does not write catalogs back. Applying changes is an explicit governed
  step. See [Parity and quality gates](../02-guides/13-parity-and-quality-gates.md).
- **The key generator requires `*.loc.json` inputs.** It emits constants at compile time; it does not
  discover runtime catalogs or modify them. See
  [Strongly typed keys](../02-guides/05-strongly-typed-keys.md).
- **Tooling packages are build-time assets.** The analyzer, code fix, generator, and parity tool are
  not runtime localization services. Do not reference them as application libraries.

## AOT and trimming

- **The EF Core store is excluded from the AOT (Ahead-of-Time) contract.** EF Core is not fully trim- or AOT-safe;
  use a non-EF provider for trimmed or Native AOT deployments. See
  [AOT and trimming](../aot-and-trimming.md).
- **Reflection-based discovery is annotated.** `LocalizedResourceDiscovery` and reflection-based
  validation surfaces carry `RequiresUnreferencedCode` / `DynamicallyAccessedMembers` annotations.
  Trimmed deployments must supply explicit declarations or preserve the annotated members.
- **The Native AOT smoke covers the exercised paths.** It proves localize/format/cache/translate on
  its own composition; it is not a blanket guarantee for every provider or host combination.

## Hosts, platforms, and UI

- **.NET 8, .NET 9, and .NET 10.** Runtime libraries target `net8.0`, `net9.0`, and `net10.0`;
  there is no support claim for older frameworks. Build-time tooling targets `netstandard2.0`.
- **Desktop hosts require Windows at runtime.** The WinForms, WPF, and Blazor Hybrid samples target
  `net10.0-windows`; they build cross-platform for CI but run on Windows. Blazor Hybrid also needs
  the WebView2 runtime.
- **The Blazor WebAssembly host is deferred.** The Blazor integration provides the WASM (WebAssembly) semantics
  and provider guard, but no browser-WASM host sample ships. See [Blazor](../02-guides/09-blazor.md).
- **No UI, CMS, or translation management system.** The library provides APIs and tooling, not an
  editor, review portal, translator discussion, billing, or terminology management. See
  [Introduction](../01-getting-started/01-introduction.md).
- **No MAUI, Avalonia, or Xamarin sample.** The architecture is host-neutral, but only the documented
  hosts are exercised.

## Operations, performance, and security

- **No capacity promise.** The repository includes non-gating performance evidence tests, not a
  benchmark suite or an SLO. Provider latency and fallback depth dominate cost. See
  [Performance](../04-operations/03-performance.md) and
  [Performance evidence](../04-operations/07-performance-evidence.md).
- **Caching is store-only in the box.** `CachedLocalizationStoreDecorator` and the distributed
  decorator wrap the store. Message providers do not accept a decorator; add application-level
  caching with explicit invalidation. See [Performance](../04-operations/03-performance.md).
- **Templates are trusted content.** The library does not sanitize untrusted catalog files or
  template markup. Validate untrusted interchange files and review translated content. See
  [Security](../04-operations/02-security.md).
- **Audit confidentiality depends on database access control.** Redaction and the hash chain are
  integrity mechanisms; they are not encryption. Protect the database and backups accordingly. See
  [Security](../04-operations/02-security.md).

## Release and versioning

- **The project is unreleased.** No NuGet packages have been published; installation currently uses
  a local feed. See [Release notes](09-release-notes.md).
- **The version is a placeholder until release.** `1.0.0.0` is declared centrally and is not a
  published compatibility promise.
- **No release cadence or LTS window is promised.** The compatibility page states the current
  posture honestly; do not infer a support timeline from the version number. See
  [Compatibility and versions](07-compatibility-and-versions.md).
- **Pre-release API changes are possible.** The API baselines and changelog make them deliberate,
  but until packages are published the surface can still change.

## Related documentation

- [Release notes](09-release-notes.md)
- [Compatibility and versions](07-compatibility-and-versions.md)
- [Core API](01-core-api.md)
- [Provider reference](03-provider-reference.md)
- [Deployment](../04-operations/01-deployment.md)
- [Security](../04-operations/02-security.md)
- [Performance](../04-operations/03-performance.md)
- [Troubleshooting](../04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
