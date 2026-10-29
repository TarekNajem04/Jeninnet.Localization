# Resource Sets and the Store

The resource-set store is the second domain in the library. It is not a message provider and does
not participate in the message pipeline. It exists for named, mutable string collections that need
CRUD (Create, Read, Update, Delete), validation, audit, and a stable ASP.NET Core bridge.

Read this chapter when you need `IStringLocalizer`, a runtime-editable catalog, translator notes,
or code-first synchronization. If you only need `IMessageLocalizer`, you can skip the store
entirely.

## What a resource set is

A resource set is identified by a set name (`Bookstore`), an exact culture (`en`, `en-US`, `ar`),
and a key (`Name`). The store exposes a whole `ILocalizationResourceSet` or individual
`LocalizedStringEntry` values carrying `Id`, `ResourceSet`, `Culture`, `Name`, `Value`, and
`ResourceNotFound`.

There is no fallback inside the store. `GetResourceSetAsync("Bookstore", "de-CH")` looks for exactly
that pair and returns `null` when only `de` exists. The ASP.NET Core bridge does not walk parents
either.

A set exists only while it has persisted entries: creating the first entry creates it, and deleting
the last entry removes it.

## The store contract

`ILocalizationStore` is the read and reload surface:

| Member | Purpose |
|---|---|
| `GetResourceSetAsync` / `GetResourceSetsAsync` | Return one set or every set for a culture; a missing set is `null`. |
| `TryGetResourceSet` / `TryGetString` | Synchronous probes. |
| `GetStringAsync` | Return an entry, or a missing sentinel. |
| `GetAllStringsAsync` / `GetAllStringsForCultureAsync` | Return one set/culture, or a culture-wide name map. |
| `ReloadAsync` / `GetSupportedCulturesAsync` | Reload the source; list served cultures. |

`GetStringAsync` never returns `null` for a missing key. It returns
`LocalizedStringEntry.Missing(...)`, whose `Value` is `[MISSING:key]` and whose `ResourceNotFound`
is `true`. The canonical message pipeline returns `null` instead.

`GetAllStringsForCultureAsync` returns a flat name-to-entry map. If the same name exists in more
than one set, the implementation merges; the EF store lets later entries overwrite earlier ones. It
is not a set-qualified map.

### The low-level resource-set CRUD

`ILocalizationResourceSet` exposes `GetStringAsync`, `GetAllStringsAsync`, `TryGetString`,
`AddAsync`, `UpdateAsync`, and `DeleteAsync`, with single-entry and enumerable overloads. These are
the low-level lifecycle operations. They are useful for seeding, bulk import, and tests.

They are **not** the governed administration path. Raw CRUD does not validate through
`ResourceAdminValidation`, does not report conflicts in `ResourceAdminResult`, does not raise audit
events, and is not versioned. The EF store documents this boundary explicitly: raw resource-set
writes are outside the accountability model.

Missing sets can be represented by `EmptyLocalizationResourceSet`, a read-only sentinel whose
lookups return missing entries and whose mutation methods throw `NotSupportedException`. The
ASP.NET Core bridge uses it for a temporary miss so that a lookup does not fail.

## The ASP.NET Core bridge

`CustomStringLocalizerFactory` implements `IStringLocalizerFactory` over `ILocalizationStore`.

- `Create(Type)` uses `resourceSource.Name`; `Create(baseName, location)` uses the exact `baseName`
  and treats `location` only as contract validation.
- The localizer captures `CurrentUICulture.Name` at creation and re-probes that exact set and
  culture on every lookup, so a temporary miss is never permanent.

Create a new localizer when the UI culture changes. `IStringLocalizer<T>` is transient and gets a
fresh factory result per resolution; a long-lived localizer keeps its captured culture.

Missing entries surface as `[MISSING:key]` with `ResourceNotFound = true`.
`GetAllStrings(true)` enumerates only the captured exact culture; the bridge does not duplicate the
canonical parent walk. The indexer with arguments uses
`string.Format(CultureInfo.CurrentCulture, ...)`, so store values use composite placeholders
(`{0}`, `{1}`), not the pipeline's named placeholders.

No root `IStringLocalizer` is registered; resolve `IStringLocalizer<T>` or create the non-generic
localizer from `IStringLocalizerFactory`.

## The EF Core store

The `Jeninnet.Localization.Providers.Database.EntityFrameworkCore` package provides
`LocalizationDataContext` and `SqlDbLocalizationStore`. Register it with:

`services.AddEntityFrameworkLocalizationStore(configuration, connectionStringName: "LocalizationDb")`
registers the context through `AddDbContextFactory` and the store as a singleton `ILocalizationStore`.
The store implements `IResourceAdminStore` and `IResourceHistoryStore`; its schema includes
`LocalizedStrings`, audit records, the replay journal, the version row, and notes.

Schema management is consumer-owned; `EnsureCreated` provisions the current model only in an empty
database, and a database with any existing table needs an explicit schema step before governed
mutations. See [EF Core store schema](../04-operations/06-ef-core-store-schema.md) for the tables,
provisioning checks, and coexistence rules. The EF store is not an `IMessageProvider` and does not
feed `IMessageLocalizer`; the database message providers use different tables.

## Governed administration

`IResourceAdminStore` extends `ILocalizationStore` with a governed write path. The capability is
two-level: implementing the interface is the type-level statement; `SupportsAdministration` is the
instance-level honest answer. A store that reports `false` rejects every mutation with
`StoreReadOnlyException`. Use the extension helpers rather than casting by hand:

```csharp
using Jeninnet.Localization.Abstractions.Store;

var admin = store.RequireAdmin(); // throws StoreReadOnlyException when unsupported
```

The operations are entry create/update/delete; `RenameKeyAsync` (atomic across cultures);
`DeleteResourceSetAsync`; `RenameResourceSetAsync` (atomic across cultures); and note get/set/delete.
Notes are key-level, culture-agnostic, and move or die with the key lifecycle.

Each mutation accepts optional `ResourceAdminOptions` with `DryRun` and `Actor`. A dry run changes
nothing and raises no audit entries; the actor is recorded on every audit entry.

Expected domain conflicts are **returned**, not thrown. `ResourceAdminResult` reports
`Applied`, `DryRun`, `AffectedEntries`, and an optional `Conflict` with a kind of `AlreadyExists`,
`NotFound`, or `TargetExists`. Validation failures (invalid identifiers, unknown cultures, null
values) throw in the `ArgumentException` family. A read-only store throws
`StoreReadOnlyException` before producing a result.

```csharp
var result = await admin.CreateEntryAsync("Bookstore", "en", "App.Title", "Bookstore",
    new ResourceAdminOptions { Actor = "seed" });

if (!result.Applied) {
    Console.WriteLine($"{result.Conflict!.Kind}: {result.Conflict.Detail}");
}
```

Translator notes are key-level and culture-agnostic. `SetNoteAsync` requires the key to exist in at
least one culture; there are no orphan notes. Renaming the key moves the note. Deleting the key's
last culture row deletes the note. Notes are visible on the governed surface; they are not part of
the message template.

## Code-first synchronization

`ResourceSynchronizer` applies `ResourceDeclaration` values through the same governed path.

```csharp
var declarations = new[] {
    new ResourceDeclaration("Bookstore", "App.Title", "Bookstore"),
    new ResourceDeclaration("Bookstore", "Cart.Items", "You have {count} items.")
};

var synchronizer = new ResourceSynchronizer(admin);
_ = await synchronizer.SynchronizeAsync(declarations,
    new ResourceSynchronizationOptions {
        DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "deploy"
    });
```

Semantics:

- **Adds only by default.** Differing default-culture values are `Skipped` unless `AllowUpdates` is
  set; non-default culture values are never updated.
- **Target cultures are conservative.** Missing target entries are skipped so fallback still
  applies. `CreateMissingTargetCultures` creates empty placeholders, which suppress fallback.
- **Renames are explicit.** `Renames` applies only where the new key is declared, the old key is not
  declared, and the old key exists, through the cross-culture atomic rename.
- **Notes follow declarations.** Applied on create; upserted only with `AllowUpdates`.
- **No delete, and conflicts do not abort the run.** Per-key conflicts are reported and other keys
  continue. A second run is idempotent (`Unchanged`, no mutations, no audit entries).

The report lists per-entry outcomes and counts. Treat it as deployment evidence, not a boolean.
`LocalizedResourceDiscovery` can find `[LocalizedResource]` attributes, but it is reflection-based
and `RequiresUnreferencedCode`; trimmed/AOT (Ahead-of-Time) deployments pass declarations explicitly.

## Concurrency expectations

- The store is a singleton. Readers use independent contexts per call; do not hold a resource-set
  object as a long-lived cache.
- The EF store serializes governed mutations per store instance and uses a bounded-retry
  conditional update for the version counter, so in-process writers cannot share a version.
- Cross-process writers are not coordinated by that gate. The version counter uses a conditional
  update, but key uniqueness and ordering depend on your schema; add constraints or serialize
  writers externally. Persisted `Sequence` and `Version`, not wall-clock time, are authoritative.
- Raw `ILocalizationResourceSet` CRUD is not gated like the governed path; do not mix them.
- Cache decorators are per process: `CachedLocalizationStoreDecorator` uses `IMemoryCache`,
  `DistributedLocalizationStoreDecorator` uses `IDistributedCache` and
  `DistributedStoreCacheOptions`. Neither has an `Add...` extension; wire them explicitly.

## Worked example: preview, then seed

```csharp
var options = new ResourceSynchronizationOptions {
    DefaultCulture = "en", TargetCultures = ["ar"], Actor = "release-2026-10"
};

var preview = await synchronizer.SynchronizeAsync(declarations, new ResourceSynchronizationOptions {
    DefaultCulture = "en", TargetCultures = ["ar"], DryRun = true, Actor = "release-2026-10"
});

if (preview.Conflicts > 0) {
    throw new InvalidOperationException("Catalog seed conflicts: " + preview.Conflicts);
}

await synchronizer.SynchronizeAsync(declarations, options);
```

The preview creates and audits nothing; the applied run creates only missing entries, and a second
run is idempotent. Keep preview and applied options identical except for `DryRun`.

## Pitfalls

**Pitfall:** expecting the store to feed `IMessageLocalizer`; keep both paths aligned deliberately.

**Pitfall:** using raw `ILocalizationResourceSet` CRUD for administrative edits. It bypasses
validation, conflict reporting, audit, and versioning. Use `IResourceAdminStore` or the
synchronizer for governed changes.

**Pitfall:** creating missing target cultures as empty strings to "complete" the matrix. Empty
values are valid stored values and suppress fallback, so the UI may render blank instead of the
default language. Leave them absent unless an empty placeholder is intentional.

## Where to go next

- [Audit and history](07-audit-and-history.md) for the event and persisted-history model.
- [ASP.NET Core and MVC](08-aspnet-core-and-mvc.md) for the consumers of the bridge.
- [Storage API](../03-reference/02-storage-api.md) for the complete contract surface.
