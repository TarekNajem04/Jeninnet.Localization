# The Jeninnet.Localization Book

Jeninnet.Localization is a modular localization framework for .NET applications: key-driven
message lookup, resource-set storage, file and database providers, ASP.NET Core integration, and
optional translation providers. This book explains how the pieces fit together, why the
architecture makes the choices it does, and how to use the framework without fighting it.

**Languages:** English (current) | [العربية (النسخة النهائية)](../ar/README.md) |
[language selector](../languages.md)

## Preface

### What this book is

This is a reference book, not a class-by-class API dump. Each chapter explains a problem, the
model the library uses to solve it, the workflows that follow from that model, and the limits you
should plan around. Code examples are intentionally short and use the real public API; package
names and option names are verified against the source tree at the revision this book was written
for.

The book assumes you know C# and the basics of `Microsoft.Extensions.DependencyInjection`. It does
not assume prior experience with localization tooling, gettext, ICU (International Components for Unicode) MessageFormat, or translation
management systems.

### Who it is for

- **Application developers** adding localization to a new or existing .NET application.
- **Tech leads and architects** evaluating whether the framework fits an existing platform,
  provider strategy, or translation workflow.
- **Operators and release engineers** responsible for deployment, security, performance, and
  troubleshooting of localized systems.
- **Localization engineers** who need to understand keys, catalogs, plural forms, audit history,
  and the interchange formats the framework can read and write.

### How it is organized

The book has four parts and an appendix.

- **Part I — Getting started** builds the conceptual model. Read this part in order if the library
  is new to you.
- **Part II — Guides** covers recurring jobs: choosing a provider, configuring it, handling
  cultures, formatting, strongly typed keys, the resource-set store, audit history, host
  integrations, translator workflows, quality gates, diagnostics, testing, and right-to-left /
  bidirectional text.
- **Part III — Reference** is the lookup material: core API, storage API, provider reference,
  interchange formats, tooling, diagnostics, compatibility, release notes, known limitations, the
  generated API guide, and migration from `Microsoft.Extensions.Localization`.
- **Part IV — Operations** addresses deployment, security, performance and its evidence,
  observability operations, troubleshooting, frequently asked questions, and the EF Core store schema.
- The **Appendix** contains a glossary and a package map.

### How to read it

- **Newcomers** start at Part I, chapter 1, and read through the mental model before writing code.
  Then work through the quick start and the provider guide.
- **Evaluators** read Part I chapter 1 for the model and the positioning, then
  [Compatibility and versions](03-reference/07-compatibility-and-versions.md) for the support and
  migration posture. The [package map](appendix/02-package-map.md) answers "which package do I
  need?" quickly.
- **Operators** start at Part IV, especially deployment and security, and use the diagnostics and
  troubleshooting chapters as runbooks. The audit and history chapter is required reading if the
  catalog is edited in production.

### Conventions

- Target framework is **.NET 8, .NET 9, and .NET 10**; runtime libraries target `net8.0`, `net9.0`,
  and `net10.0` except the build-time Roslyn tooling, which targets `netstandard2.0`.
- **Status:** the repository is unreleased and under active development. No NuGet packages have
  been published. Installation examples show the intended package workflow and the local-feed
  workflow that works today.
- Code uses the canonical names from the source: `IMessageLocalizer`, `IMessageProvider`,
  `ILocalizationStore`, and the per-package `Add*` extension methods.
- Relative links connect chapters. Links to chapters that are still being written are part of the
  planned table of contents and are marked by their position, not by a placeholder page.

## Table of contents

### Part I — Getting started

1. [Introduction: localization as an engineering workflow](01-getting-started/01-introduction.md) —
   the problem space, the two layers, who the framework is for, and honest positioning.
2. [Installation and first registration](01-getting-started/02-installation.md) — target framework,
   package selection, local-feed installation, project setup, and the first working registration.
3. [Quick start](01-getting-started/03-quick-start.md) — a complete console walkthrough: catalogs,
   registration, lookup, culture switching, and fallback.
4. [The mental model](01-getting-started/04-mental-model.md) — provider selection, the exactly-one
   rule, the fallback algorithm, keys, resource sets, formatting, and cancellation.

### Part II — Guides

1. [Choosing a provider](02-guides/01-choosing-a-provider.md) — scenario-driven selection among
   memory, files, ResX, satellite assemblies, gettext catalogs, databases, pseudo-localization,
   and translation providers.
2. [Configuring providers](02-guides/02-configuring-providers.md) — options snapshots,
   registration-time validation, reload behavior, and per-family configuration.
3. [Cultures and fallback](02-guides/03-cultures-and-fallback.md) — culture names, parent chains,
   default culture, request culture selection, and tests.
4. [Formatting and plurals](02-guides/04-formatting-and-plurals.md) — the placeholder pipeline, ICU
   MessageFormat, CLDR (Common Locale Data Repository) plural categories, the Markdown formatter, escaping, and performance.
5. [Strongly typed keys](02-guides/05-strongly-typed-keys.md) — the key generator, the
   localizability analyzer, generated API shape, diagnostics, and build-time behavior.
6. [Resource sets and the store](02-guides/06-resource-sets-and-store.md) — `ILocalizationStore`,
   resource-set CRUD (Create, Read, Update, Delete), the governed administration path, synchronization, and concurrency.
7. [Audit and history](02-guides/07-audit-and-history.md) — audit events, persisted history,
   versioned reads, redaction, and hash-chain verification.
8. [ASP.NET Core and MVC](02-guides/08-aspnet-core-and-mvc.md) — request localization, MVC
   validation, display metadata, and Razor views.
9. [Blazor](02-guides/09-blazor.md) — circuit-scoped and static-scope culture management plus the
   WASM (WebAssembly) provider guard.
10. [JavaScript and client delivery](02-guides/10-javascript-and-client.md) — versioned resource
    endpoints, static export, ETags, and allow-lists.
11. [Translator workflow](02-guides/11-translator-workflow.md) — catalogs, notes, contexts, plural
    forms, and handoff to translation tooling.
12. [Machine translation](02-guides/12-machine-translation.md) — Azure Translator, Google
    Translate, caching, batching, glossaries, translation memory, and resilience.
13. [Parity and quality gates](02-guides/13-parity-and-quality-gates.md) — key extraction, missing
    and unused keys, format drift, placeholder drift, and CI enforcement.
14. [Diagnostics and observability](02-guides/14-diagnostics-and-observability.md) — activities,
    metrics, health checks, and missing-key sinks.
15. [Testing your localization](02-guides/15-testing-your-localization.md) — unit tests, fallback
    tests, pseudo-localization, and test doubles for providers and stores.
16. [RTL (Right-to-Left) and bidirectional text](02-guides/16-rtl-and-bidi.md) — direction, logical CSS, bidi
    isolation, and host examples for MVC, Blazor, WinForms, and WPF.

### Part III — Reference

1. [Core API](03-reference/01-core-api.md) — `IMessageLocalizer`, `DefaultMessageLocalizer`,
   formatters, provider selector, and registration.
2. [Storage API](03-reference/02-storage-api.md) — store contracts, entries, resource sets, admin,
   history, and synchronization types.
3. [Provider reference](03-reference/03-provider-reference.md) — every provider package, its
   options, capabilities, and behavior.
4. [Interchange formats](03-reference/04-interchange-formats.md) — CSV, PO (Portable Object), ResX, and XLIFF (XML Localization Interchange File Format)
   catalogs, keys, statuses, and notes.
5. [Tooling reference](03-reference/05-tooling-reference.md) — the key generator, localizability
   analyzer and code fix, and the `jeninnet-localization-keys` tool.
6. [Diagnostics reference](03-reference/06-diagnostics-reference.md) — diagnostic IDs and
   observability names.
7. [Compatibility and versions](03-reference/07-compatibility-and-versions.md) — framework
   support, versioning posture, release status, and migration constraints.
8. [Migration from Microsoft.Extensions.Localization](03-reference/08-migration-from-ms-extensions.md) —
   mapping `IStringLocalizer`, `IStringLocalizerFactory`, and ResX-based code onto this model.
9. [Release notes](03-reference/09-release-notes.md) — current publication status, planned 1.0
   scope, installation, compatibility, and the pre-release change policy.
10. [Known limitations](03-reference/10-known-limitations.md) — the deliberate boundaries of the
    pipeline, providers, store, tooling, AOT (Ahead-of-Time), hosts, and operations, with mitigations.
11. [API reference guide](03-reference/11-api-reference.md) — how the generated API pages are
    organized and which packages are build-time tooling.

### Part IV — Operations

1. [Deployment](04-operations/01-deployment.md) — content layout, database provisioning, schema,
   and release sequencing.
2. [Security](04-operations/02-security.md) — trusted templates, encoding, secrets, store access,
   and audit confidentiality.
3. [Performance](04-operations/03-performance.md) — provider latency, caching, batching, fallback
   cost, and measurement.
4. [Troubleshooting](04-operations/04-troubleshooting.md) — symptom-driven diagnosis of missing
   keys, culture surprises, registration failures, and provider errors.
5. [FAQ](04-operations/05-faq.md) — short answers to recurring design and operational questions.
6. [EF Core store schema](04-operations/06-ef-core-store-schema.md) — tables, `EnsureCreated`
   provisioning, coexistence with `LocalizationMessages`, and schema ownership.
7. [Performance evidence](04-operations/07-performance-evidence.md) — the measured snapshot,
   environment, reproduction commands, and caveats.
8. [Observability cookbook](04-operations/08-observability-cookbook.md) — OpenTelemetry wiring,
   collector sketch, dashboards, alerts, and investigation flow.

### Appendix

1. [Glossary](appendix/01-glossary.md) — provider, resource set, catalog, key, culture fallback,
   and the other terms used throughout the book.
2. [Package map](appendix/02-package-map.md) — all 35 packable projects, their dependencies, and
   when to reference each one.

### Related documentation

- [Repository README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md) — project overview, repository layout, quick start, and build
  commands.
- [Contributing to the documentation](../CONTRIBUTING.md) — chapter structure, snippet verification,
  package README rules, and the review checklist.
- [AOT and trimming](aot-and-trimming.md) — the Native AOT/trimming contract, exclusions, and the
  smoke consumer.
- [Reference Application guide](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/README.md) — the BookStore application across six hosts
  and the full provider matrix.
- [Documentation snippet verification](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Docs/README.md) — the
  compiled and executed examples behind the quick start and registration snippets.
- [Architectural Overview](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/Architectural%20Overview.md) — a short
  orientation to the model.
