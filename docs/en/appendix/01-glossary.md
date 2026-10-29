# Glossary

The vocabulary of the book, in alphabetical order. Definitions are intentionally narrow: a term
means the same thing everywhere in this repository, even where ordinary usage is broader.

## Abbreviations and acronyms

| Short form | Full form | Notes |
|---|---|---|
| AD | Architecture Decision | |
| API | Application Programming Interface | |
| AOT | Ahead-of-Time compilation | Native AOT compiles IL to machine code before execution. |
| BCL | Base Class Library | The standard .NET class library. |
| BOM | Byte Order Mark | Optional Unicode signature at the start of a text stream. |
| CLDR | Common Locale Data Repository | Unicode Consortium data used for plural rules and locale data. |
| CLI | Command-Line Interface | |
| CLR | Common Language Runtime | The .NET execution engine. |
| CRLF | Carriage Return + Line Feed | The Windows line-ending sequence `\r\n`. |
| CRUD | Create, Read, Update, Delete | |
| CSV | Comma-Separated Values | |
| DI | Dependency Injection | |
| DOC | Documentation | |
| DOM | Document Object Model | |
| DTO | Data Transfer Object | |
| EF Core | Entity Framework Core | Microsoft's object-relational mapper for .NET. |
| EOT | End of Transmission | The `\u0004` character that gettext uses to separate a context from a key. |
| GUID | Globally Unique Identifier | |
| HTML | HyperText Markup Language | |
| i18n | internationalization | Numeronym: `i` + 18 letters + `n`. |
| ICU | International Components for Unicode | Unicode's formatting and message-format specification/library. |
| IDE | Integrated Development Environment | |
| IL | Intermediate Language | .NET's portable instruction set (CIL/MSIL). |
| ISO | International Organization for Standardization | |
| JIT | Just-in-Time compilation | |
| JLN | Jeninnet Localization | Prefix of diagnostic identifiers (`JLNLOC`, `JLNKEY`). |
| JSON | JavaScript Object Notation | |
| l10n | localization | Numeronym: `l` + 10 letters + `n`. |
| LTR | Left-to-Right | |
| MO | Machine Object | Compiled GNU gettext catalog format (`.mo`). |
| MSTest | Microsoft Test Framework | The test framework used by this repository. |
| MVC | Model-View-Controller | |
| ORM | Object-Relational Mapping | |
| OTel | OpenTelemetry | The observability framework. |
| OTLP | OpenTelemetry Protocol | |
| PO | Portable Object | GNU gettext text catalog format (`.po`). |
| R8 | Requirement 8 | The cancellation and async-contract requirement. |
| RFC | Request for Comments | |
| ResX | `.resx` resource format | Not an abbreviation: the XML-based resource-file format name. |
| RTL | Right-to-Left | |
| SHA-256 | Secure Hash Algorithm, 256-bit | |
| SQL | Structured Query Language | |
| TFM | Target Framework Moniker | For example `net10.0` or `netstandard2.0`. |
| TLS | Transport Layer Security | |
| TM | Translation Memory | |
| TTL | Time To Live | |
| URI | Uniform Resource Identifier | |
| URL | Uniform Resource Locator | |
| UTC | Coordinated Universal Time | |
| UTF-8 | Unicode Transformation Format, 8-bit | |
| WASM | WebAssembly | |
| XAML | Extensible Application Markup Language | |
| XLIFF | XML Localization Interchange File Format | |
| XML | Extensible Markup Language | |

**Actor.** The caller-supplied label recorded on a governed mutation and its audit entry, usually
the authenticated principal. It is evidence only if the host supplies it from a trusted identity,
not from a request body. See [Audit and history](../02-guides/07-audit-and-history.md).

**Admin store.** `IResourceAdminStore`, the governed write surface over a resource-set store.
Implementing the interface is the type-level capability; `SupportsAdministration` is the
instance-level answer. See [Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**Audit event.** The live `Audited` event raised once per mutated row after a governed mutation
commits. It is in-process and not durable. See
[Audit and history](../02-guides/07-audit-and-history.md).

**Audit record.** A persisted, hash-chained record of one mutated row, carrying metadata and
before/after SHA-256 hashes rather than template text. See
[Audit and history](../02-guides/07-audit-and-history.md).

**Catalog.** A collection of message keys and templates for one culture, stored in a file, database
table, compiled resource, or translation service. A catalog is not a resource set: the message
pipeline reads catalogs, while the store manages resource sets. See
[Choosing a provider](../02-guides/01-choosing-a-provider.md).

**Catalog version.** The monotonic version assigned to one governed mutation batch. Every audit
record of the batch shares it, and as-of-version reads use it as a boundary. See
[Audit and history](../02-guides/07-audit-and-history.md).

**Chain hash.** The hash of an audit record combined with the previous record's chain hash. A
change to any record breaks the chain unless every later hash is rewritten. See
[Audit and history](../02-guides/07-audit-and-history.md).

**CLDR (Common Locale Data Repository) plural category.** One of the language-specific cardinal
categories used by the plural engine: `Zero`, `One`, `Two`, `Few`, `Many`, or `Other`. `Other`
means "none of the listed categories", not simply "plural". See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

**Context.** A disambiguating namespace for a key, encoded by `LocalizeWithContextAsync` as
`context + '\u0004' + key` (the gettext EOT convention). See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

**Culture fallback.** The single algorithm owned by `DefaultMessageLocalizer`: requested culture,
then parent cultures, stopping before invariant culture, then the optional default culture once.
See [Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Culture-aware provider.** `CultureAwareMessageProvider`, a dispatcher that holds one provider per
culture and selects by exact culture name, then two-letter language name. It is not a fallback
engine; the localizer still performs the parent walk. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Default culture.** The final fallback culture configured through `LocalizationOptions`. It is
captured when the localizer is constructed; null or empty disables the step. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Drift.** Any unintended mismatch between what the code references and what a catalog serves, or
between two representations of the same catalog. The parity tool reports template drift, format
gaps, and placeholder drift. See
[Strongly typed keys](../02-guides/05-strongly-typed-keys.md) and the
[tool README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**Dry run.** A governed preview that validates a mutation and reports what it would do without
changing data or raising audit records. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**EF Core (Entity Framework Core) store.** The resource-set store implemented by `SqlDbLocalizationStore` over
`LocalizationDataContext`. It is an `ILocalizationStore` and `IResourceHistoryStore`, not an
`IMessageProvider`. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**Exact-match provider.** A provider that answers one key and one culture and never walks parents
or consults another source. Every in-box message provider follows this rule. See
[The mental model](../01-getting-started/04-mental-model.md).

**Fallback chain.** The ordered sequence of cultures the localizer queries for one key. The
provider sees each step as an independent exact-match request. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Format drift.** A key served for a culture in one catalog format but absent from another
format's catalog, or served with differing templates across formats. See the
[tool README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**gettext.** The GNU translation toolkit and its file-format family: PO source catalogs, compiled
MO catalogs, `msgctxt` contexts, and `Plural-Forms` headers.

**Hash chain.** The audit-trail construction in which each record's chain hash depends on the
previous record. It is tamper evidence, not authenticity against a fully rewrite-capable attacker.
See [Audit and history](../02-guides/07-audit-and-history.md).

**Interchange.** A file format used to export and import catalog content for translators: CSV,
PO, ResX, or XLIFF. Interchange packages are tooling, not runtime providers. See the
[interchange overview](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md).

**Invariant culture.** The culture whose name is empty and which is never queried by the fallback
walk. It is not a language and should not be a catalog. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Key.** The opaque string a caller passes to the localizer. It is not normalized, trimmed, or
interpreted as hierarchy. See [The mental model](../01-getting-started/04-mental-model.md).

**Key pair.** The singular/plural key shape accepted by `LocalizePluralAsync`. `One` selects the
singular key; every other category selects the plural key. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

**Message pipeline.** The `IMessageLocalizer` path: validate, select one provider, walk fallback,
format on a hit, return `null` on a miss. See
[The mental model](../01-getting-started/04-mental-model.md).

**Message provider.** An implementation of `IInnerMessageProvider`/`IMessageProvider` that returns
templates for an exact key and culture. Exactly one is resolved per composition. See
[Choosing a provider](../02-guides/01-choosing-a-provider.md).

**Missing key sink.** `IMissingKeySink`, the opt-in hook that receives one report after the full
fallback chain misses. The default is null and adds no per-lookup work. See
[Troubleshooting](../04-operations/04-troubleshooting.md).

**MO (Machine Object).** The compiled GNU gettext binary format. The PO provider reads it when `Format` selects the
binary reader or the file extension is `.mo`. See the
[PO provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md).

**Neutral culture.** A language without a region, such as `de`. A specific request such as `de-CH`
falls back to its neutral parent through the localizer. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Parity.** The property that the key set referenced by source, the catalogs, and the configured
cultures agree. The parity tool measures it; it does not write changes. See the
[tool README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**Placeholder.** A `{name}` (or ICU `{name, type, ...}`) span inside a template that the formatter
substitutes. The default formatter uses the text up to the next `}` as the dictionary key. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

**Placeholder drift.** One key/culture whose placeholder identifier set differs from the reference
culture's set. Named-argument reordering is not drift; added or dropped identifiers are. See the
[tool README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

**PO (Portable Object).** The gettext text catalog format: `msgid`, `msgstr`, optional `msgctxt`, plural forms, and a
`Plural-Forms` header. See the
[PO provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md).

**Provider.** Short for message provider. In this book, "provider" never means a translation
management system or a store. See
[Choosing a provider](../02-guides/01-choosing-a-provider.md).

**Pseudo-localization.** A synthetic culture produced by transforming source templates to expose
layout overflow and untranslated strings. It is a provider, not a translation. See the
[pseudo provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md).

**Redaction.** The audit rule that replaces template text with its SHA-256 hash. It applies to audit
records, not to the replay journal. See
[Audit and history](../02-guides/07-audit-and-history.md).

**Replay journal.** The append-only `StringVersions` table that stores actual values and tombstones
so as-of-version reads can reproduce the catalog. It is catalog data, not redacted audit data. See
[Audit and history](../02-guides/07-audit-and-history.md).

**Resource set.** A named, mutable string collection for one exact culture, whose entries are keyed
by name. It is managed through `ILocalizationStore`, not through the message pipeline. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**Resource-set store.** `ILocalizationStore` and its resource-set contracts. It does not participate
in `IMessageLocalizer` lookups. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**ResX (XML-based .resx resource format).** A `.resx` resource format. The runtime provider reads compiled resources through
`ResourceManager`; the interchange package reads and writes ResX text for translators. See the
[ResX provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md).

**Satellite assembly.** A compiled `{culture}/{AssemblyName}.resources.dll` pack probed by
`ResourceManager`. The satellite provider serves it with parent probing disabled. See the
[satellite provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md).

**Sequence.** The store-assigned append order of an audit or journal record. It is strictly
increasing and independent of catalog version; one batch can produce many records with one version.
See [Audit and history](../02-guides/07-audit-and-history.md).

**Source generator.** The Roslyn incremental generator that emits `const string` keys from
`*.loc.json` catalogs. It runs at build time and is not a runtime dependency. See
[Strongly typed keys](../02-guides/05-strongly-typed-keys.md).

**Store decorator.** A wrapper over an `ILocalizationStore` that adds behavior, such as
`CachedLocalizationStoreDecorator` (memory) or `DistributedLocalizationStoreDecorator`
(distributed cache). Decorators are wired explicitly, not by an `Add...` extension. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

**Strongly typed key.** A compile-time constant generated from a catalog key, conventionally
`LocalizationKeys.CART_ITEMS`. It is an ordinary string at runtime. See
[Strongly typed keys](../02-guides/05-strongly-typed-keys.md).

**Translation memory.** An exact-match store of approved translations consulted before machine
translation by the Azure provider. It is not the TTL cache. See the
[Azure provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md).

**Translation provider.** A message provider whose source template is the lookup key and whose
value is fetched from a remote translation service. It is the single provider, not a fallback
layer. See [Choosing a provider](../02-guides/01-choosing-a-provider.md).

**WASM (WebAssembly) guard.** The check in `AddBlazorWasmLocalization` that rejects providers not implementing
`IWasmCompatibleMessageProvider`, enforcing that the registered provider declares no file,
database, or network I/O in a browser process. See the
[Blazor integration README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md).

**XLIFF (XML Localization Interchange File Format).** An XML localization interchange format. The package supports the documented XLIFF 2.0
subset with one file, one segment per unit, and plain-text source/target. See the
[XLIFF README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md).

**Zero providers.** A valid composition in which no `IInnerMessageProvider` is registered. Every
lookup returns `null`; the selector does not throw. See
[The mental model](../01-getting-started/04-mental-model.md).
