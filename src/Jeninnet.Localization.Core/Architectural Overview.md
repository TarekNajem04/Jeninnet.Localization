# Architectural Overview

This is a short orientation to Jeninnet.Localization. The public
[Architecture](../../docs/en/03-reference/00-architecture.md) chapter is the authoritative
description of the implemented model.

## Two domain concepts

Jeninnet.Localization separates two concepts that are often conflated:

1. **Localized messages (key → template)** — the primary capability. `IMessageProvider` resolves
   a message template for a key and culture; `IMessageLocalizer` resolves and formats it.
   Providers are exact-match only and stateless with respect to the caller.
2. **Resource sets (store)** — named, mutable collections of localized strings with CRUD (Create, Read, Update, Delete) through
   `ILocalizationStore` / `ILocalizationResourceSet`. The ASP.NET Core `IStringLocalizer` bridge
   and the EF Core store implement this concept, independently of the provider pipeline.

## The provider model

- Every data source is a **Provider** implementing the canonical `IMessageProvider` contract
  (`IInnerMessageProvider` for implementations): Memory, JSON, XML, ResX, SQLite, SQL Server,
  MySQL, PostgreSQL, Entity Framework Core, and the translation providers.
- **Exactly one provider** is resolved by `LocalizationProviderSelector`. Provider chaining,
  merging, priority, and provider-level fallback are not part of the architecture.
- Providers own their acquisition complexity (file/DB access, batching, caching, preload,
  refresh, translation). None of it leaks into the canonical contract.
- Translation providers are ordinary providers; there is no separate translation abstraction.

## Culture fallback

`DefaultMessageLocalizer` owns the single fallback algorithm: requested culture → parent
cultures (the invariant culture is never queried) → optional default culture. Providers are
exact-match; `CultureAwareMessageProvider` is a pure dispatcher used by file providers that hold
per-culture sources.

## Registration direction

Registration is capability-named and composes through DI (Dependency Injection):

```csharp
services.AddLocalization();                                   // base pipeline (Core)
services.AddJsonLocalization(options => options.FilePath = "Localization/messages.json");
// exactly one provider registration
```

Provider and integration packages expose their own `Add*` methods (for example
`AddMemoryLocalization`, `AddSqlServerLocalization`, `AddEntityFrameworkLocalizationStore`,
`AddMvcLocalizedValidation`). ASP.NET Core applications consume `IStringLocalizer` /
`IStringLocalizer<T>` through the store-backed bridge.

## Behavior guarantees

- **Cancellation:** canonical async lookups accept an optional final `CancellationToken`,
  propagated to cancellable I/O. A cancelled lookup throws `OperationCanceledException`; it is
  never converted into a missing-resource result.
- **Lifecycle:** providers capture their options as immutable construction snapshots, are
  registered as singletons, and expose no disposal/lifecycle abstraction. File providers
  (JSON/XML) initialize once with synchronized, retryable loading. Provider refresh invalidates
  provider-owned caches; store reload is store-owned.
- **Validation integration:** DataAnnotations and MVC validation consume the registered
  `IStringLocalizer`; the MVC validation integration is the only intentional
  integration → Core dependency.

## Where to go deeper

- [Architecture](../../docs/en/03-reference/00-architecture.md) — contracts, dependency rules,
  capability model, lifecycle model, and quality gates.
- The README next to each package documents that package's scope.
