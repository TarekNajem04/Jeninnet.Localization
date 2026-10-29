# EF Core Store Schema

The EF Core resource-set store is a separate schema from the ADO.NET
`LocalizationMessages` table. This page describes the model a **new** database receives, how to
provision it, and the ownership boundaries a consumer must plan around.

The library is unreleased, so there is no consumer migration or upgrade guide yet. This page covers
fresh-database provisioning only. When the first published model changes, a migration/upgrade page
becomes a release requirement.

The source of truth for the model is
[`LocalizationDataContext`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/LocalizationDataContext.cs)
and the entity types in the same project.

## What `EnsureCreated` does

`Database.EnsureCreatedAsync()` creates the current model **only when the target database has no
tables at all**. If any table already exists, including the ADO.NET `LocalizationMessages` table, it
does nothing and returns `false`; it does not create the missing store tables and it does not run
migrations.

That behavior has three practical consequences:

1. Provision the store against a **dedicated empty database**, or before any other table exists.
2. If the store shares a database with other schemas, create its tables with your own reviewed DDL
   or a migration you own. Do not rely on `EnsureCreated` to add them later.
3. `EnsureCreated` is not a migration tool. There are no EF Core migrations in the package.

## Provision a fresh database

Register the store, resolve the context factory, and run `EnsureCreated` as a deployment step:

```csharp
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEntityFrameworkLocalizationStore(connectionString);

await using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>();

await using var database = await factory.CreateDbContextAsync();
var created = await database.Database.EnsureCreatedAsync();

// created == false means the database already had at least one table.
// EnsureCreated did not add the store tables; verify them explicitly.
```

The expected tables are:

```text
LocalizedStrings
AuditRecords
StringVersions
CatalogState
StringNotes
```

Verify them before starting the application:

```sql
SELECT name
FROM sys.tables
WHERE name IN (
    N'LocalizedStrings',
    N'AuditRecords',
    N'StringVersions',
    N'CatalogState',
    N'StringNotes')
ORDER BY name;
```

The store starts empty. `CatalogState` has no row until the first governed mutation creates row
`Id = 1`; reads before that point report version `0`.

## Table summary

The SQL Server provider materializes the model with `nvarchar(max)` for ordinary strings. Indexed
string properties are materialized as `nvarchar(450)` (900 bytes), which is the provider's current
limit for index keys. The tables have no foreign keys; `EntryId`, `ResourceSet`, `Culture`, and
`Name` are correlation values, not database-enforced relationships.

| Table | Primary key | Indexes | Purpose |
|---|---|---|---|
| `LocalizedStrings` | `Id` (`uniqueidentifier`) | PK only | Live catalog rows |
| `AuditRecords` | `Sequence` (`bigint`, identity) | `Version` (non-unique) | Redacted, hash-chained audit trail |
| `StringVersions` | `Sequence` (`bigint`, identity) | `(ResourceSet, Culture, Version)` (non-unique) | Replay journal for as-of-version reads |
| `CatalogState` | `Id` (`int`, no identity; always `1`) | PK only | Single-row catalog version counter |
| `StringNotes` | `Id` (`uniqueidentifier`) | `(ResourceSet, Name)` unique | Key-level translator notes |

### `LocalizedStrings`

| Column | SQL Server type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | Primary key; governed creates assign `Guid.NewGuid()` |
| `ResourceSet` | `nvarchar(max)` | no | Resource-set name |
| `Culture` | `nvarchar(max)` | no | Exact culture code |
| `Name` | `nvarchar(max)` | no | Message key |
| `Value` | `nvarchar(max)` | no | Template value |

The table has only its primary-key index. Reads filter on `(ResourceSet, Culture, Name)`, so a large
catalog will scan unless the consumer adds an index. Because those columns are `nvarchar(max)` in
the model, an index requires an indexable column shape (finite lengths or a computed key) as part of
consumer-owned schema management.

The model also does not define a unique index on `(ResourceSet, Culture, Name)`. The governed
administration path checks for an existing row in process, but raw resource-set CRUD (Create, Read, Update, Delete) and concurrent
writers are not protected by a database constraint. If database-enforced uniqueness matters, the
consumer must provide the same indexable column shape and the constraint. See
[Known limitations](../03-reference/10-known-limitations.md) and the concurrency notes in the
[EF Core package README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md).

### `AuditRecords`

| Column | SQL Server type | Null | Notes |
|---|---|---|---|
| `Sequence` | `bigint` identity | no | Primary key, append order |
| `Version` | `bigint` | no | Catalog version shared by a mutation batch |
| `Timestamp` | `datetimeoffset` | no | Commit time (UTC) |
| `Actor` | `nvarchar(max)` | no | Caller identity, empty when anonymous |
| `Operation` | `int` | no | `ResourceAdminOperation` enum |
| `ResourceSet` | `nvarchar(max)` | no | Resource set of the mutated row |
| `Culture` | `nvarchar(max)` | no | Culture of the mutated row; empty for note rows |
| `Key` | `nvarchar(max)` | no | Key of the mutated row |
| `NewKey` | `nvarchar(max)` | yes | Rename target, otherwise `NULL` |
| `EntryId` | `uniqueidentifier` | no | Persisted identity of the mutated row |
| `OldValueHash` | `nvarchar(max)` | yes | Redacted old value hash; `NULL` for creates |
| `NewValueHash` | `nvarchar(max)` | yes | Redacted new value hash; `NULL` for deletes/renames |
| `PreviousHash` | `nvarchar(max)` | no | Previous chain hash or genesis marker |
| `ChainHash` | `nvarchar(max)` | no | Hash over this record and `PreviousHash` |

`AuditRecords` stores hashes, not template text. The replay journal is the layer that stores actual
values.

### `StringVersions`

| Column | SQL Server type | Null | Notes |
|---|---|---|---|
| `Sequence` | `bigint` identity | no | Primary key, append order |
| `Version` | `bigint` | no | Effective catalog version |
| `ResourceSet` | `nvarchar(450)` | no | Resource-set name at this version |
| `Culture` | `nvarchar(450)` | no | Culture at this version |
| `Name` | `nvarchar(max)` | no | Key at this version |
| `EntryId` | `uniqueidentifier` | no | Persisted identity preserved across renames |
| `Value` | `nvarchar(max)` | yes | Value at this version; `NULL` for a deletion/rename tombstone |

`StringVersions` stores actual template values. Treat it with the same confidentiality as the live
catalog.

### `CatalogState`

| Column | SQL Server type | Null | Notes |
|---|---|---|---|
| `Id` | `int` | no | Primary key; always `1` |
| `Version` | `bigint` | no | Last assigned catalog version |

The row is created lazily by the first governed mutation. A missing row means version `0`.

### `StringNotes`

| Column | SQL Server type | Null | Notes |
|---|---|---|---|
| `Id` | `uniqueidentifier` | no | Primary key |
| `ResourceSet` | `nvarchar(450)` | no | Resource-set name |
| `Name` | `nvarchar(450)` | no | Message key |
| `Note` | `nvarchar(max)` | no | Note text; empty is valid |

The unique index on `(ResourceSet, Name)` enforces one note per key. Notes are culture-agnostic and
move or die with the key lifecycle.

## Coexistence with the ADO.NET message table

The EF store tables and the ADO.NET `LocalizationMessages` table can live in the same database, but
they are independent:

- `LocalizationMessages` is created by the vendor scripts described in the
  [database guide](../../database/README.md); the EF store does not create or migrate it.
- The EF store does not feed `IMessageLocalizer`; it backs `ILocalizationStore` and the
  `IStringLocalizer` bridge.
- `EnsureCreated` will not add the store tables to a database that already has
  `LocalizationMessages` (or any other table). Provision the store first, use a dedicated database,
  or manage both schemas with your own reviewed process.
- The schemas are not synchronized. If an application uses both `IMessageLocalizer` and
  `IStringLocalizer`, seed and verify both paths or choose one.

See [Deployment](01-deployment.md) for the full comparison and
[Database schema artifacts](../../database/README.md) for the ADO.NET scripts.

## Backups, reset, and permissions

- **Back up** the database before provisioning or applying a consumer-owned schema change. The
  audit and replay tables are evidence, not a backup substitute.
- **Reset** a disposable environment by dropping the five store tables (or the database) and running
  provisioning again. Do not delete individual audit or journal rows and expect
  `VerifyHistoryAsync` to remain meaningful.
- **Provisioning permissions** require DDL to create the tables (and a database if you create one).
- **Runtime permissions** require `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on the store tables for
  governed administration; a read-only deployment can be narrowed to `SELECT`.
- Do not grant the runtime account `db_owner` or schema-alter rights just to avoid provisioning.

## Schema ownership and future changes

The package ships **no EF Core migrations**. A database created with `EnsureCreated` includes the
current model, including the accountability tables and notes. An older database needs a migration or
an equivalent reviewed script before governed mutations run.

Because the library is unreleased, there is no published older schema and no consumer migration page
yet. When the first published model changes, the release must include an upgrade path, and this page
must be updated with it. Until then, treat the model as a fresh-database contract only.

For the broader operational sequence, see [Deployment](01-deployment.md). For AOT (Ahead-of-Time)/trimming, remember
that this package is a documented exclusion; see [AOT and trimming](../aot-and-trimming.md).

## Related documentation

- [EF Core store package README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md)
- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md)
- [Storage API](../03-reference/02-storage-api.md)
- [Audit and history](../02-guides/07-audit-and-history.md)
- [Deployment](01-deployment.md)
- [Known limitations](../03-reference/10-known-limitations.md)
- [AOT and trimming](../aot-and-trimming.md)
- [Troubleshooting](04-troubleshooting.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
