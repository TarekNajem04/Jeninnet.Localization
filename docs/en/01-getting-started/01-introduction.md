# 1. Introduction: Localization as an Engineering Workflow

Localization is usually introduced as a lookup problem: given a key and a culture, return a string.
That description is accurate but incomplete. In a real application, localization is a workflow that
spans authoring, review, translation, packaging, deployment, runtime lookup, and quality assurance.
The lookup is the easy part. The expensive failures happen around it: a key exists in the default
catalog but not in a translated one, a culture falls back in a way nobody expected, a translator
edits a file that the deployed application never reads, or an administrator changes a production
catalog with no record of who changed what.

Jeninnet.Localization is designed around that larger workflow. It keeps runtime lookup small and
predictable, and it treats the catalog as an asset with a lifecycle rather than as a dictionary
that happens to be loaded at startup.

## The problem space

Consider the ordinary life of one message:

1. A developer writes user-facing text and gives it a stable key.
2. The default-language catalog receives the key and its template.
3. A translator receives the key, the template, context, and possibly plural variants.
4. The translated catalog is reviewed, validated, and packaged.
5. The application deploys the catalog and resolves the key for the current culture.
6. Someone changes the text in production, and the change must be attributable and reversible.
7. A QA engineer verifies that no screen still shows the default language.

Every step has failure modes that a simple `Dictionary<string, string>` does not address. Keys
drift between code and catalogs. Cultures are not a flat list: `de-CH` has a parent `de`, and the
invariant culture is not a language. Translators need context to avoid translating "Open" as an
adjective when it is a button. Plural rules differ by language. Production edits need audit
records. Several hosts (web, desktop, mobile, background jobs) may consume the same catalog.

The framework addresses those concerns with a small number of concepts and a strict separation
between them.

## What the library is

Jeninnet.Localization is a modular localization framework for .NET. It provides:

- **Key-driven message lookup** through `IMessageLocalizer`, with an optional default-culture
  fallback.
- **Message providers** for memory, JSON, XML, ResX, satellite assemblies, gettext PO (Portable Object)/MO (Machine Object),
  SQLite, SQL Server, MySQL, PostgreSQL, pseudo-localization, Azure Translator, and Google
  Translate.
- **A resource-set store** for named, mutable string collections, with a governed administration
  path, translator notes, audit history, and versioned reads.
- **ASP.NET Core interoperability**: a store-backed `IStringLocalizerFactory`, MVC validation and
  display metadata, Razor tag helpers and view localization, request-culture selection, Blazor
  culture management, and JavaScript resource delivery.
- **Tooling**: a source generator for strongly typed keys, a localizability analyzer and code fix,
  a catalog parity tool, and interchange codecs for CSV, PO, ResX, and XLIFF (XML Localization Interchange File Format).

The library is not a translation management system. It does not host translator discussions, run a
marketplace, or own a translation memory service. It provides the runtime, the catalog contracts,
and the file formats that let such systems connect to a .NET application.

## The two layers

The most important design decision in the library is that it separates two concepts that are often
conflated: the **message pipeline** and the **resource-set store**.

| Concern | Message pipeline | Resource-set store |
|---|---|---|
| Primary contract | `IMessageLocalizer` / `IMessageProvider` | `ILocalizationStore` / `ILocalizationResourceSet` |
| Unit of lookup | Message key plus culture | Resource-set name, culture, and name |
| Culture behavior | Localizer-owned fallback chain | Exact requested set and culture per call |
| Formatting | Formatter runs after a template resolves | Values are strings; no message formatter |
| Mutation | Providers are read-only | CRUD (Create, Read, Update, Delete) and a governed administration path |
| Typical consumer | Application code, MVC tag helpers, JavaScript export | `IStringLocalizer` bridge, editors, synchronizer |

The message pipeline answers "what template should this key render to for this culture?" The store
answers "what strings does this named resource set contain, and how are they managed?" They can be
used together, but they are not the same lookup path. In particular, registering a store does not
make `IMessageLocalizer` read it, and registering a message provider does not populate
`IStringLocalizer`.

That separation is deliberate. Message lookup has a fallback algorithm and a formatter. Resource
management has CRUD, validation, conflicts, audit, and history. Forcing both through one interface
would make each one worse.

## Design decisions and their consequences

A few rules shape nearly every experience described in this book.

**Exactly one message provider is resolved.** `LocalizationProviderSelector` resolves the
registered `IInnerMessageProvider` services. Zero providers is a valid but empty configuration;
more than one is an invalid configuration and throws when the provider selector is constructed.
There is no provider chaining, merging, priority, or provider-level fallback. If two data sources
must contribute messages, they must be combined before runtime, or one of them must become the
system of record. See [The mental model](04-mental-model.md) and
[Choosing a provider](../02-guides/01-choosing-a-provider.md).

**Providers are exact-match.** A provider is asked for one key and one culture. It does not walk
parents, apply a default culture, or consult another provider. The single fallback algorithm lives
in `DefaultMessageLocalizer`: requested culture, then parent cultures, with the invariant culture
excluded, then the optional default culture. Exact-match providers keep their behavior simple and
make fallback observable in one place.

**Resource sets are a separate domain.** The store's resource sets are named, mutable collections.
The ASP.NET Core bridge maps a type name or base name to a resource set and resolves that set on
every lookup. A temporary miss is therefore not permanently cached as an empty set.

**Cancellation is not a miss.** Async lookups accept an optional final `CancellationToken` after
argument validation. Cancellation propagates to provider I/O, is observed between fallback steps,
and surfaces as `OperationCanceledException`. It is never converted into `null`, because a caller
that cancelled must be able to distinguish "I asked to stop" from "the message does not exist."

**Formatting is a separate stage.** The localizer resolves a template first. If no template
resolves, it returns `null` and never invokes the formatter. If a template resolves, the formatter
substitutes parameters. This keeps missing-key behavior and formatting behavior independently
testable.

## Who it is for

The framework fits applications that want explicit control over where messages come from and how
culture fallback works, without rebuilding the same provider and catalog plumbing for every host.

- **Product teams** with a single default catalog and a small number of translated cultures.
- **Platform teams** that need one localization model across API, MVC, Blazor, desktop, and worker
  processes.
- **Teams with existing assets**: ResX files, satellite assemblies, gettext PO catalogs, or a
  database table that already holds translations.
- **Regulated or audit-sensitive teams** that need attributable catalog edits and historical reads.
- **Teams preparing for trimming or Native AOT (Ahead-of-Time)**, with one documented exception for the EF Core
  package.

It is a poor fit if you need the library to be a full translation management system, if you need
multiple providers to merge at runtime, or if you expect the resource-set store and the message
pipeline to be interchangeable.

## Honest positioning

The .NET ecosystem already has localization options. Comparing approaches is more useful than
comparing feature checklists.

| Approach | What it does well | Where this library differs |
|---|---|---|
| Framework `IStringLocalizer` with ResX | Familiar, tool-supported, tight ASP.NET Core integration | This library implements the same abstraction over a store, and adds a separate message pipeline with explicit fallback, exact-match providers, and catalog tooling. It does not replace the framework abstraction; it supplies an implementation. |
| Direct `ResourceManager` access | Simple for assemblies that already have resources; satellite probing is built in | The message pipeline makes culture fallback explicit and testable, and lets the same keys be served by files, databases, or translation providers. ResX and satellite providers still use `ResourceManager` underneath. |
| Gettext/PO tooling | Mature translator tooling, contexts, plural forms, MO binaries | The PO provider reads PO text and MO binaries, and exposes gettext-shaped context and plural APIs while keeping the localizer's fallback algorithm. The library does not ship a PO editor. |
| Store-centric admin catalogs | Editable runtime strings, workflow around content | The store is part of this library, but it is deliberately separate from the message provider. The governed path adds validation, conflicts, dry-run, audit, history, and notes. |
| ICU (International Components for Unicode)/MessageFormat libraries | Rich plural, select, number, and date formatting | `IcuMessageFormatter` covers a documented ICU subset with culture-aware formatting. It is opt-in and composes with the default formatter. Project Fluent syntax is a documented non-goal. |
| Translation management systems | Translator collaboration, review, terminology, billing | This library is the runtime and catalog boundary, not the TMS. Interchange codecs and translator notes let a TMS exchange data with it. |

The comparison is about approach, not superiority. A small application can be well served by
plain ResX and the framework localizer. A large application with several hosts, runtime-editable
catalogs, or strict audit requirements gets more value from an explicit model.

## A worked example

Suppose a checkout page resolves the key `Cart.Items` for `de-CH`. The provider holds templates
for `de` and `en`; the configured default culture is `en`.

The localizer asks for `de-CH` first. The provider is exact-match, so it misses unless it has an
entry for that specific culture. The localizer then moves to the parent culture, `de`, and finds
the German template. It formats that template with the caller's parameters and the requested
culture. It does not ask for `en`, because the fallback chain stopped at the first hit.

If the key existed only in `en`, the chain would have continued past `de` to the invariant culture.
The localizer never queries the invariant culture; it stops the parent walk and then queries the
configured default culture. The result is the English template. If the key existed nowhere, the
result would be `null`; the localizer would not fabricate the key or return an empty string.

This example is developed further, including request-culture selection and tests, in
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

**Pitfall:** a successful fallback can hide a missing translation. If `de` is missing a key but
`en` has it, the German page renders English text with no error. That is the intended runtime
behavior, but it is not evidence that the German catalog is complete. Use the parity tool,
pseudo-localization, and tests to find gaps; do not treat "the page renders" as translation
coverage.

## How this book is organized

Part I continues with installation, a complete quick start, and the mental model. Part II turns
the model into workflows: provider selection, configuration, cultures, formatting, keys, the store,
audit, host integrations, translation, quality gates, diagnostics, and testing. Part III is the
reference layer. Part IV covers operations, and the appendix defines terminology and maps every
package.

If you read only one chapter after this one, read
[The mental model](04-mental-model.md). Most surprises in localization come from an inaccurate
model of lookup, fallback, or the boundary between the message pipeline and the store.
