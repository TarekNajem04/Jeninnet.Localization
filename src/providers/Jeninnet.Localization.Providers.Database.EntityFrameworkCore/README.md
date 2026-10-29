# Jeninnet.Localization.Providers.Database.EntityFrameworkCore

`Jeninnet.Localization.Providers.Database.EntityFrameworkCore` is the EF Core resource-set store for Jeninnet.Localization. It provides `LocalizationDataContext` (the `DbContext`), `ILocalizationDataContext`, and `SqlDbLocalizationStore`, which implements `ILocalizationStore`, `IResourceAdminStore`, and `IResourceHistoryStore` over SQL Server.

This package is a store, not a message provider. It does not implement `IInnerMessageProvider` and does not feed `IMessageLocalizer`; it backs the ASP.NET Core `IStringLocalizer` bridge and governed catalog administration. Its schema (`LocalizedStrings`, `AuditRecords`, `StringVersions`, `CatalogState`, `StringNotes`) is separate from the ADO.NET providers' `LocalizationMessages` table. See [Resource sets and the store](../../../docs/en/02-guides/06-resource-sets-and-store.md) and [Storage API](../../../docs/en/03-reference/02-storage-api.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Providers.Database.EntityFrameworkCore
```

Add [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) for the `IStringLocalizer` bridge or the message pipeline.

## Registration

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;

// Direct connection string.
services.AddEntityFrameworkLocalizationStore(
    "Server=.;Database=Localization;Trusted_Connection=True;TrustServerCertificate=True;");

// Named connection string from IConfiguration (default name: "LocalizationDb").
services.AddEntityFrameworkLocalizationStore(configuration, "LocalizationDb");

// Optional: the store-backed Microsoft IStringLocalizer bridge.
services.AddLocalization();
```

Both overloads register `AddDbContextFactory<LocalizationDataContext>` and a singleton `ILocalizationStore` mapped to `SqlDbLocalizationStore`. The context factory is the store's only dependency, so the store can also be constructed directly with an `IDbContextFactory<LocalizationDataContext>`.

Registration validates the connection string (null or empty throws `ArgumentException` / `ArgumentNullException`) and the configuration name, but does not connect. A missing named connection string throws `ArgumentException`.

## Configuration

There is no store options class; configuration is the connection string passed to the registration method. The context model owns five sets:

| DbSet | Table | Purpose |
|----|----|----|
| `LocalizedStrings` | `LocalizedStrings` | Live catalog: `Id`, `ResourceSet`, `Culture`, `Name`, `Value`. |
| `AuditRecords` | `AuditRecords` | Redacted, hash-chained audit trail. |
| `StringVersions` | `StringVersions` | Replay journal for as-of-version reads. |
| `CatalogState` | `CatalogState` | Single-row version counter (`Id = 1`). |
| `StringNotes` | `StringNotes` | Key-level, culture-agnostic translator notes. |

Per-mutation administration options are `ResourceAdminOptions`:

| Property | Type | Default | Meaning |
|----|----|----|----|
| `DryRun` | `bool` | `false` | Compute the result without changing data or raising audit events. |
| `Actor` | `string` | `""` | Caller identity recorded on every audit record. |

## Usage

```csharp
using Jeninnet.Localization.Abstractions.Store;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalization();
services.AddEntityFrameworkLocalizationStore(connectionString);

await using var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<ILocalizationStore>();

// Governed create through the capability gate.
var admin = store.RequireAdmin();
var created = await admin.CreateEntryAsync(
    "Bookstore", "en", "App.Title", "Bookstore",
    new ResourceAdminOptions { Actor = "seed" });

// Exact read: returns the value or the canonical missing sentinel.
var entry = await store.GetStringAsync("Bookstore", "en", "App.Title");
Console.WriteLine(entry.Value); // Bookstore

// The governed path is versioned and audited.
var history = store.RequireHistory();
var version = await history.GetCurrentVersionAsync();
var trail = await history.GetHistoryAsync(sinceVersion: 0);
```

With `AddLocalization()` and the store registered, the Microsoft bridge uses the resource-set name as identity:

```csharp
using Microsoft.Extensions.Localization;

var factory = provider.GetRequiredService<IStringLocalizerFactory>();
var strings = factory.Create("Bookstore", "Docs");
Console.WriteLine(strings["App.Title"]); // Bookstore
Console.WriteLine(strings["Missing.Key"]); // [MISSING:Missing.Key]
```

The bridge captures `CurrentUICulture` when the localizer is created and re-probes that exact set and culture on every lookup. Create a new localizer after changing the UI culture.

## Behavior and limits

- **Exact lookup.** Every resource or entry lookup resolves an exact `(resourceSet, culture)` pair. There is no parent-culture walk, default culture, or formatter in the store.
- **Set existence.** `GetResourceSetAsync` returns `null` when no entries exist for the pair. A set exists only while it has persisted rows; creating the first row creates it, and deleting the last row removes it.
- **Missing entries.** `GetStringAsync` never returns `null` for a missing key. It returns `LocalizedStringEntry.Missing(...)`, whose `Value` is `[MISSING:name]` and whose `ResourceNotFound` is `true`. The message pipeline returns `null` instead.
- **Live reads.** Each call uses an independent context from the factory. `ReloadAsync` is a no-op because reads already reflect the current database state; there is no store-level cache.
- **Governed versus raw.** `IResourceAdminStore` mutations validate through `ResourceAdminValidation`, report expected conflicts in `ResourceAdminResult`, raise `Audited`, and are versioned. Raw `ILocalizationResourceSet` CRUD (Create, Read, Update, Delete) is the low-level lifecycle path: it is not gated, not validated the same way, not audited, and not versioned. Do not mix the paths.
- **Conflicts and failures.** Expected domain conflicts (`AlreadyExists`, `NotFound`, `TargetExists`) are returned, not thrown. Validation failures throw in the `ArgumentException` family. A read-only store throws `StoreReadOnlyException` through `RequireAdmin()`.
- **Audit redaction.** Audit rows carry actor, timestamp, key set, and lowercase SHA-256 (Secure Hash Algorithm, 256-bit) hashes of the UTF-8 (Unicode Transformation Format, 8-bit) template bytes (`ResourceValueHash.Compute`, unsalted). Template text never lands in audit storage in plain text. The `StringVersions` replay journal stores actual values and has the same confidentiality class as the live catalog.
- **History.** Each governed batch advances one monotonic version shared by all its rows. `GetHistoryAsync` uses an exclusive lower bound (`0` reads everything). `GetStringsAsOfVersionAsync` is replay-faithful; version `0` is the empty pre-history state. `VerifyHistoryAsync` detects ordinary modification, insertion, or removal of records, but the chain is unanchored: a rewrite-capable attacker who recomputes every subsequent hash is only detectable against an externally retained anchor.
- **Notes.** One note per `(resourceSet, key)`, culture-agnostic. Notes require the key to exist in at least one culture, move with a key rename, and are deleted when the key's last culture row is deleted. Notes are audited but do not enter the replay journal.
- **Concurrency.** The store is a singleton. Governed mutations are serialized per store instance in process, and the version counter uses a bounded-retry conditional update so in-process writers cannot share a version. Cross-process writers are not coordinated; retry `DbUpdateConcurrencyException`, add database constraints, or serialize writers externally.
- **Schema ownership.** No EF Core migrations ship with the package. `EnsureCreated` includes the current model, including accountability tables and notes, but only in an empty database; a database with any existing table needs an explicit schema step. See [EF Core store schema](../../../docs/en/04-operations/06-ef-core-store-schema.md) for the table inventory and provisioning checks. The committed `0001_localization_messages.sql` script covers the ADO.NET table only and does not touch these tables.
- **Cancellation.** Cancellation tokens are forwarded to EF Core operations and surface as `OperationCanceledException`; a cancelled lookup is never converted into a miss.

## AOT and trimming

**Excluded from the AOT (Ahead-of-Time)/trimming contract.** The project sets `IsAotCompatible=false` because EF Core is not fully compatible with trimming or Native AOT. Use a non-EF database provider for trimmed or Native AOT deployments. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract and the provider guidance.

## Related packages

| Package | Relationship |
|----|----|
| [`Jeninnet.Localization.Abstractions`](../../Jeninnet.Localization.Abstractions/README.md) | Store, admin, history, and entry contracts. |
| [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) | `IStringLocalizer` bridge, caching decorators, synchronizer, and message pipeline. |
| [`Database.SqlServer`](../Jeninnet.Localization.Providers.Database.SqlServer/README.md) | ADO.NET message provider with a separate `LocalizationMessages` schema. |
| [`Database.SQLite`](../Jeninnet.Localization.Providers.Database.SQLite/README.md) | File-backed message provider, not a store. |
| [`Database.MySql`](../Jeninnet.Localization.Providers.Database.MySql/README.md) | MySQL message provider, not a store. |
| [`Database.PostgreSQL`](../Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | PostgreSQL message provider, not a store. |

## Documentation

- [Resource sets and the store](../../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Storage API](../../../docs/en/03-reference/02-storage-api.md)
- [Audit and history](../../../docs/en/02-guides/07-audit-and-history.md)
- [Deployment](../../../docs/en/04-operations/01-deployment.md)
- [Troubleshooting](../../../docs/en/04-operations/04-troubleshooting.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../../LICENSE).
