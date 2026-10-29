# Deployment

Deploying a localized application is at least three deployments: the application and its
packages, the catalog content or database schema, and the configuration and secrets that tell the
process where the catalog lives. The library keeps those concerns separate. This chapter covers
package placement, the committed schema scripts, EF Core versus SQL, seeding, multi-instance
concerns, cache warm-up, secrets, and rollback.

## What goes where

A server-side host needs the core pipeline, exactly one message provider, and only the
integrations it uses.

| Target | Packages | Notes |
|---|---|---|
| Server or desktop app | `Jeninnet.Localization.Core` plus one provider | Core supplies the localizer, formatter, selector, and store bridge; the provider supplies the catalog. |
| Administered store | `Jeninnet.Localization.Providers.Database.EntityFrameworkCore` | An `ILocalizationStore`, not a message provider; it does not feed `IMessageLocalizer`. |
| ASP.NET Core MVC | `Integrations.Mvc.Validation` | Validation metadata, display metadata, views, `<loc>` tag helper. |
| Request cultures | `Integrations.RequestLocalization` | Populates the framework middleware from the store's served cultures. |
| Blazor | `Integrations.Blazor` | Circuit-scoped or static-scope culture management, with a WASM (WebAssembly) guard. |
| Browser client | `Integrations.JavaScript` or its static export | Allow-listed templates only; never a database connection. |

The browser is a separate trust boundary. `AddBlazorWasmLocalization` rejects a provider that does
not implement `IWasmCompatibleMessageProvider`; the memory provider is the in-box implementation.
A browser build cannot reach a SQL Server catalog directly. Export approved keys through the
JavaScript integration or move the lookup to the server.

The EF Core store is the one runtime package intentionally outside the Native AOT (Ahead-of-Time)/trimming
contract. Fully trimmed or Native AOT deployments should use a non-EF provider. See
[AOT and trimming](../aot-and-trimming.md).

## Content layout

`dotnet publish` copies assemblies, not files the project knows only as source content. For file
providers, make the catalog part of the publish output explicitly:

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
  <None Update="Localization\*.xml" CopyToOutputDirectory="PreserveNewest" />
  <None Update="Localization\*.po" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Resolve paths from `AppContext.BaseDirectory`, not the current working directory. A path that
works under `dotnet run` can fail under a service, a container entry point, or a scheduled task.
The convention overloads expect `{baseDirectory}/Localization/{culture}.json` (or `.xml`/`.po`).
`AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"])` registers one provider per
culture and wraps them in a single `CultureAwareMessageProvider`; XML and PO (Portable Object) follow the same shape.
A single-culture registration reads one flat map and ignores the culture argument.

ResX and satellite content is embedded or deployed as satellite packs. Both need the manifest
resource base name and the owning assembly:

```csharp
services.AddResXLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
services.AddSatelliteLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
```

A wrong base name cannot be validated at registration; it surfaces at the first lookup as
`MissingManifestResourceException`. Exercise one lookup per provider during release verification.
SQLite is a file-backed database: treat its path as state, put it on a writable volume, back it up,
and do not bake it into a read-only image. SQL Server, MySQL, and PostgreSQL belong to the
database release, not the application image.

## Database schema for message providers

The four database message providers read the canonical table:

```text
LocalizationMessages
    Id
    MessageKey
    Culture
    Template
    UNIQUE (MessageKey, Culture)
```

`DatabaseLocalizationOptions.TableName` defaults to `LocalizationMessages` and accepts a valid SQL
identifier of up to three dot-separated segments, so `dbo.LocalizationMessages` is valid and
arbitrary text is rejected when the option is set.

The committed rename migration is
[0001_localization_messages.sql](../../database/0001_localization_messages.sql). It is a **guarded
rename migration**, not a create script:

1. If `dbo.ValidationMessages` exists and `dbo.LocalizationMessages` does not, rename the table.
2. If it still has `Key` and no `MessageKey`, rename the column.
3. If it still has `MessageTemplate` and no `Template`, rename that column.

SQL Server carries constraints and identities through `sp_rename`, so rows are preserved, and the
guards make the script safe to re-run on an already-migrated database. A fresh database has neither
table, so run the matching creation script from the [database guide](../../database/README.md):
[SQL Server](../../database/schema/0001_create_localization_messages.sqlserver.sql),
[PostgreSQL](../../database/schema/0001_create_localization_messages.postgresql.sql),
[MySQL](../../database/schema/0001_create_localization_messages.mysql.sql), or
[SQLite](../../database/schema/0001_create_localization_messages.sqlite.sql). Each script is
idempotent, creates the four canonical columns and the unique `(MessageKey, Culture)` pair, and
leaves an existing table untouched. Then seed it. Apply the schema change as a reviewed database
step, not from application startup, after a backup or restore point.

### The rename is a breaking change

The old application names `ValidationMessages`, `Key`, and `MessageTemplate`; the new application
names `LocalizationMessages`, `MessageKey`, and `Template`. An old process that has not been
redeployed will fail its queries after the forward migration. Deploy in this order:

1. Stop writers to the message table or enter a read-only maintenance mode.
2. Back up the database and apply the forward script.
3. Deploy the application version that uses the canonical names and the same single provider.
4. Verify one lookup per served culture and one deliberate miss, then resume writers.

The [rollback script](../../database/0001_localization_messages.rollback.sql) reverses the renames in
inverse order. It is guarded and preserves rows, but it restores names, not data. Roll the
application back in the same window; the old schema is only meaningful to the old application.

A read replica can serve lookups only after it has applied the same schema change and caught up
with the seed. Replica lag becomes content lag: a key can exist on the primary and be missing from
the replica for a short period. The provider treats that as a miss and the localizer falls back or
returns `null`. If that is unacceptable, route localization reads to the primary or add a cache
with a bounded stale window. Never point the admin store at a replica; governed writes, version
bumps, audit records, and the replay journal require a writable primary.

## EF Core migrations versus SQL scripts

Two schemas are involved, and they are not the same. The ADO.NET providers own
`LocalizationMessages` with `Id`, `MessageKey`, `Culture`, and `Template`. The EF Core store owns
`LocalizedStrings`, `AuditRecords`, `StringVersions`, `CatalogState`, and `StringNotes`.

The repository ships no EF Core migrations. The EF store README states that schema management is
consumer-owned. A new database created with `EnsureCreated` includes the current model, including
the accountability tables and notes; `EnsureCreated` is a no-op when the database already has any
table, so a shared database needs an explicit schema step. See
[EF Core store schema](06-ef-core-store-schema.md) for the tables, provisioning checks, and
coexistence rules. Do not assume the committed `LocalizationMessages` script upgrades the EF store;
it does not touch those tables. The schemas can coexist in one database, but they are not
synchronized. If a consumer uses both `IMessageLocalizer` and `IStringLocalizer`, seed and verify
both paths or use one.

## Seeding

File providers need no runtime seeding: the catalogs ship with the release. Database message
providers have no in-box seeder; use an idempotent upsert or an ETL job that writes `MessageKey`,
`Culture`, and `Template`, and keep the unique pair constraint so a retry cannot create duplicate
rows.

The EF Core store uses `ResourceSynchronizer` with `ResourceDeclaration` values. Preview first,
then apply with identical options except `DryRun`:

```csharp
var declarations = new[] {
    new ResourceDeclaration("Bookstore", "App.Title", "Bookstore"),
    new ResourceDeclaration("Bookstore", "Cart.Items", "You have {count} items.")
};

var synchronizer = new ResourceSynchronizer(admin);
var preview = await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "deploy"
    });

if (preview.Conflicts > 0) {
    throw new InvalidOperationException("Catalog seed conflicts: " + preview.Conflicts);
}

await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], Actor = "deploy"
    });
```

Adds happen by default; changing an existing default-culture value requires `AllowUpdates`.
Target cultures are conservative, and `CreateMissingTargetCultures` creates empty placeholders
that suppress fallback. A second run is idempotent and produces no mutations or audit entries.
Treat the report as release evidence, not as a boolean.

## Cache warm-up, configuration, and release order

File providers load lazily on the first lookup; later requests read an immutable table. To warm
them, probe one key per served culture after the host is built. To force a file load and surface a
malformed catalog immediately, resolve the concrete provider and call `RefreshAsync`; it throws on
failure. A failed lazy load is not cached, so a later lookup may retry it. Database message
providers have no in-box message cache; their warm-up is the database buffer cache and connection
pool. The memory and distributed store decorators are lazy too. Azure translation supports
`PreloadAsync(keys, culture, cancellationToken)` when caching is enabled; Google has no provider
cache. Health checks warm the pipeline only when an explicit probe key is configured.

Provider options are construction snapshots. Reload re-acquires the source; it does not re-read
the options object, so plan a restart for configuration changes. Use .NET User Secrets for local
ASP.NET Core development and the platform secret store or environment variables in production.
Do not put keys in `appsettings.json`, and do not log the options object: the translation options
hold the API key as a plain property. Pin translation endpoints to the expected host and use
HTTPS; an attacker who can change configuration can point the provider at a host that receives
the key.

Release order:

1. Freeze catalog edits and record the current catalog version.
2. Back up the database, or archive the exact content directory for a file provider.
3. Apply schema changes with a reviewed script. For the EF store, apply the consumer-owned
   migration and run `VerifyHistoryAsync` afterward.
4. Publish the application and content together; confirm every configured culture file exists.
5. Start one instance. Resolve a key in each served culture, exercise a deliberate miss, and run
   the provider/store health checks.
6. Roll out the remaining instances. Database catalogs share rows; file content must be identical
   on every instance.
7. Watch misses, fallback steps, database latency, and translation errors for one release window.
   On rollback, revert the application and schema together; as-of-version reads reproduce
   historical state but are not an automatic rollback mechanism.

## Pitfalls

**Pitfall:** treating the committed SQL as a create script. It only renames a legacy table and its
columns; a fresh database needs the canonical table created first.

**Pitfall:** omitting the content copy rule. JSON, XML, and PO registration throws
`FileNotFoundException`, which is early but only if the host starts with that content path.

**Pitfall:** assuming the store and the message table are synchronized. They are separate domains;
align them deliberately or use one path.

## Where to go next

- [Security](02-security.md) and [Performance](03-performance.md).
- [Troubleshooting](04-troubleshooting.md).
- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md).
