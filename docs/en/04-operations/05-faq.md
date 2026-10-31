# Frequently Asked Questions

These are the questions that recur in design reviews, onboarding, and production incidents. Each
answer is short, but links point to the chapter that develops the topic properly.

## Why does the framework allow only one provider?

Because provider chaining is a second fallback engine, and this library deliberately has one.
`LocalizationProviderSelector` resolves the registered `IInnerMessageProvider` instances and
throws `InvalidOperationException` if more than one is present. The localizer owns culture
fallback; providers are exact-match sources. Two providers would duplicate that ownership and make
the lookup order ambiguous. If two sources must contribute content, merge them before runtime or
make one authoritative. See [The mental model](../01-getting-started/04-mental-model.md).

## Can I combine a JSON file with a database?

Not by registering both. Export one source into the other, generate a merged catalog at build
time, or make one the system of record. Different hosts may use different providers; the
exactly-one rule applies per composition, not per repository. If you need runtime-editable content
and a file snapshot, pick one for the message pipeline and use the store for the separately
administered resource sets. See
[Choosing a provider](../02-guides/01-choosing-a-provider.md).

## What is the minimum package set?

For `IMessageLocalizer`: `Jeninnet.Localization.Core` (which brings
`Jeninnet.Localization.Abstractions` transitively) plus exactly one provider package. For the
`IStringLocalizer` bridge, add an `ILocalizationStore`; the EF Core store package is one option.
For ASP.NET Core validation and views, add
`Jeninnet.Localization.Integrations.Mvc.Validation`. The nominal "core trio"
is `Abstractions`, `Core`, and `Storage`; application code usually gets `Storage` transitively and
only references `Core` plus a provider. See the
[package map](../appendix/02-package-map.md).

## Do I need the resource-set store to use `IMessageLocalizer`?

No. The message pipeline and the store are separate domains. `IMessageLocalizer` reads a message
provider and never reads `ILocalizationStore`. The store is for named, mutable resource sets,
`IStringLocalizer`, administration, audit, and history. Registering it does not give the message
pipeline a source, and registering a provider does not populate the store. See
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

## How do I migrate from RESX?

Two different migrations are often conflated. If the `.resx` files are embedded in an assembly and
you want `ResourceManager` to resolve them, use `Jeninnet.Localization.Providers.ResX` with the
manifest base name and the owning assembly. If the `.resx` files are translator interchange, use the ResX interchange
package to import them into JSON/XML catalogs. The runtime ResX provider does not read loose
`.resx` files, and the interchange package does not become a runtime provider. See the
[ResX provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.ResX/README.md) and the
[interchange README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md).

## How do I handle plurals?

Three options, in increasing order of fidelity:

1. **Key pair** — `LocalizePluralAsync(singularKey, pluralKey, count, ...)` selects the singular
   for `PluralCategory.One` and the plural key for every other category. It is simple and works
   for two-form languages.
2. **Per-form provider tables** — the PO (Portable Object) provider implements `IPluralFormMessageProvider` when the
   catalog carries a `Plural-Forms` header, so it can select the exact `msgstr[n]` form.
3. **ICU (International Components for Unicode) plural** — `IcuMessageFormatter` with `{count, plural, ...}` branches selects CLDR (Common Locale Data Repository)
   categories inside the template and supports `=0`, `offset`, and `#`.

Use ICU or per-form tables for Arabic, Slavic, and other languages with more than two rendered
forms. The key-pair API is a documented degradation there. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

## When should I use ICU MessageFormat?

Use it when a template needs plural categories, `select`, `selectordinal`, or culture-aware number
and date rendering. It is opt-in: register `IcuMessageFormatter` as `IMessageFormatter` before
`AddLocalization`, or replace the descriptor. Templates without an ICU construct take the default
formatter path, so enabling it does not change existing content. It is stricter than the default
formatter and throws `FormatException` for malformed constructs. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

## How do I distinguish "Open" as a button from "Open" as an order state?

Use `LocalizeWithContextAsync(key, context, parameters, culture)`. The localizer builds the
provider key as `context + '\u0004' + key`, the gettext EOT (End of Transmission) convention, so PO `msgctxt` entries
interoperate without a second lookup path. Context is part of the identity: changing it later is a
catalog migration. An empty context behaves like a plain key. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

## How do I gate localization in CI?

Use several layers, not one:

- the key generator turns malformed catalogs and identifier collisions into build errors;
- the localizability analyzer suggests moving hardcoded user-facing strings;
- `jeninnet-localization-keys` reports missing, unused, template-drift, format-gap, and
  placeholder-drift findings, plus a served/fallback/missing matrix, with a non-zero exit code;
- unit tests assert fallback sequences and formatter behavior;
- the AOT (Ahead-of-Time) smoke consumer verifies the publish path when trimming is in scope.

Exit code `1` from the parity tool is a finding; `2` is a usage or environment error. See
[Strongly typed keys](../02-guides/05-strongly-typed-keys.md) and the
[tool README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md).

## How do I translate at scale?

Export one culture's catalog to an interchange format (CSV for spreadsheets, XLIFF (XML Localization Interchange File Format) for vendors, PO
for gettext tooling, ResX for ResX editors), send it out, then import it with validation and a dry
run. Keep the source culture as the key authority: interchange never invents keys. For machine
translation, prefer an offline batch into the runtime catalog; use the Azure translation provider
at request time only when dynamic translation is an explicit product decision. Its batching,
cache, glossary, and translation-memory features exist for that case. See the
[Azure provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Azure/README.md)
for the supported surface.

## How do I test localization?

Use a recording provider that records every `(key, culture)` query and returns controlled
templates. Assert the exact culture sequence, not only the returned string. Add tests for invariant
exclusion, default-culture reachability, a complete miss returning `null`, cancellation, and
formatter behavior. For the store bridge, use a test store and assert `[MISSING:key]` and
`ResourceNotFound`. Pseudo-localization is for visual QA, not a substitute for a parity check. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

## How do I find missing translations in production?

Enable the missing-key sink. `LoggerMissingKeySink` writes one structured warning per miss after
the full fallback chain fails, with the key, culture, and call site. Watch
`localization.lookup.misses` and `localization.fallback.steps`; a fallback step is not necessarily
a failure, but a rising miss rate is. Do not infer coverage from successful page renders: fallback
can hide a gap. Use the parity tool and pseudo-localization for coverage. See
[Troubleshooting](04-troubleshooting.md).

## How does culture fallback actually work?

Requested culture, then each parent, stopping before invariant culture, then the configured
default culture once. The provider is exact-match at every step. If nothing resolves, the result
is `null`. The default culture is captured when the localizer is constructed. An empty default
culture disables the final step. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

## Why is my Arabic page showing English?

Either the Arabic catalog does not contain the key and fallback reached the default culture, or
the request culture never became Arabic. Check the request-culture configuration first: without
the request-localization bridge, a provider's Arabic files do not make `ar` a served culture. If
the culture is correct, check the catalog for the exact key and whether an empty value is
suppressing fallback. A successful fallback is expected behavior, not an error. See
[Troubleshooting](04-troubleshooting.md).

## How do I let administrators edit strings at runtime?

Use the EF Core resource-set store and the governed `IResourceAdminStore` path. Do not use raw
`ILocalizationResourceSet` CRUD (Create, Read, Update, Delete) for administrative edits: it bypasses validation, conflict
reporting, audit, and versioning. Remember that the store does not feed `IMessageLocalizer`; if
the runtime pipeline must see the edit, you need a synchronization strategy or a different
architecture. See [Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

## How do I audit who changed a template?

Use the governed admin path and `IResourceHistoryStore`. Each applied mutation produces an audit
record with actor, timestamp, operation, key set, and before/after SHA-256 (Secure Hash Algorithm, 256-bit) hashes. Versions are
monotonic per mutation batch, and `GetStringsAsOfVersionAsync` replays the catalog at a version.
The hash chain is tamper evidence, but it is unanchored; a fully rewrite-capable attacker can
recompute it. See [Audit and history](../02-guides/07-audit-and-history.md).

## Does it work with trimming and Native AOT?

Yes for the runtime pipeline and every message provider. The repository builds its `net8.0`, `net9.0`, and `net10.0`
runtime projects with `IsAotCompatible=true`, and the smoke consumer publishes and runs Native
AOT. The EF Core store is the one runtime package excluded because EF Core itself is not fully
AOT-compatible. Some reflection-based convenience surfaces are annotated
`RequiresUnreferencedCode`. See [AOT and trimming](../aot-and-trimming.md).

## What is the release status and versioning posture?

The repository is unreleased and under active development; no NuGet packages have been published.
The build declares version `1.0.0-preview.1`, and a local `dotnet pack` produces packages versioned
`1.0.0-preview.1` in `artifacts/`. Package identity is project name equals assembly name equals package id.
Do not mix a locally packed package with a hypothetical future feed version in one solution. See
the [repository README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md).

## What license applies?

MIT. The repository ships the license file and the packages declare `PackageLicenseExpression` as
MIT. See [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).

## Can I use this in Blazor WebAssembly and desktop apps?

Desktop apps can use the normal server-side packages, including file and SQLite providers. Browser
WebAssembly is constrained: `AddBlazorWasmLocalization` requires providers that implement
`IWasmCompatibleMessageProvider`; the memory provider is the in-box implementation, and file and
database providers are rejected. Blazor Hybrid is a desktop process and may use file or database
I/O. See the
[Blazor integration README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/integrations/Jeninnet.Localization.Integrations.Blazor/README.md).

## How should I render HTML or Markdown messages?

`HtmlSafeMessageFormatter` encodes argument values while leaving templates trusted;
`MarkdownMessageFormatter` renders a CommonMark subset and then formats the result. Both produce
HTML that must be emitted raw, not HTML-encoded again. Neither sanitizes an untrusted template:
templates are content, and content review is part of the translation workflow. See
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

## How do I handle right-to-left languages?

The library returns strings; it does not set text direction or layout. The host sets `dir` and the
appropriate CSS for the culture. Arabic catalogs can be served like any other culture, and the
fallback rules are language-neutral. Translate complete sentences rather than concatenating
fragments, because word order and grammar differ. See
[RTL (Right-to-Left) and bidirectional text](../02-guides/16-rtl-and-bidi.md) for host examples, logical CSS, bidi
isolation, and a testing checklist.

## How do I deploy catalog changes?

For file providers, publish the catalog files with the application and keep the content path
relative to `AppContext.BaseDirectory`. For database providers, apply the schema and seed or update
rows before or with the application release. For the store, use `ResourceSynchronizer` with a dry
run and keep the report as release evidence. See [Deployment](01-deployment.md).

## Where do I start if something is broken?

Classify the symptom first: miss, wrong culture, startup exception, or provider exception. Then use
the symptom table in [Troubleshooting](04-troubleshooting.md). The most common root causes are a
second provider, a culture that was never served, a catalog that was not deployed, and a store
lookup mistaken for the message pipeline.
