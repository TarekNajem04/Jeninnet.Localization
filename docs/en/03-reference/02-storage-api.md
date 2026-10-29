# Storage API

The storage layer is the second domain of the framework. It manages named, mutable resource sets and
their governed administration, audit trail, and versioned history. It is not the message pipeline and
does not implement `IMessageProvider`.

Contracts live in `...Abstractions.Store`; the EF implementation is in `...Database.EntityFrameworkCore`; cache decorators live in Core.

Generated member pages: <xref:Jeninnet.Localization.Abstractions.Store.ILocalizationStore> and
<xref:Jeninnet.Localization.Abstractions.Store.IResourceAdminStore>.

## `ILocalizationStore`

```csharp
public interface ILocalizationStore {
    Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture,
        CancellationToken cancellationToken = default);
    Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture,
        CancellationToken cancellationToken = default);
    bool TryGetResourceSet(string resourceSet, string culture,
        [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet);
    Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet,
        string culture, CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture,
        CancellationToken cancellationToken = default);
    bool TryGetString(string resourceSet, string culture, string name,
        [NotNullWhen(true)] out LocalizedStringEntry? entry);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default);
}
```

- A store resolves an **exact** `(resourceSet, culture)` pair; there is no parent-culture walk.
- `GetResourceSetAsync` returns `null` when the set does not exist. A set exists only while it has
  persisted entries.
- `GetStringAsync` never returns `null` for a missing key. It returns
  `LocalizedStringEntry.Missing(...)`, with `Value = "[MISSING:key]"` and `ResourceNotFound = true`.
- `GetAllStringsForCultureAsync` is a flat name-to-entry map across all sets; a duplicate name is
  merged, and the EF store lets later entries overwrite earlier ones. It is not set-qualified.
- `TryGetResourceSet` and `TryGetString` are synchronous probes. The `IStringLocalizer` bridge uses
  `TryGetResourceSet`; test doubles must implement it.
- `ReloadAsync` re-acquires the source; cache decorators forward it and invalidate cached state.
- `GetSupportedCulturesAsync` feeds request-culture selection and the store health check.

## `ILocalizationResourceSet`

`ILocalizationResourceSet` exposes `ResourceSet`, `Culture`, `GetCultureInfo`, lookups, and
`AddAsync`/`UpdateAsync`/`DeleteAsync` in single-entry and enumerable overloads.

This is the low-level lifecycle surface for seeding, bulk import, and tests. It is **not** the
governed path: raw CRUD (Create, Read, Update, Delete) does not validate through `ResourceAdminValidation`, does not report conflicts,
does not raise audit events, and is not versioned.

`EmptyLocalizationResourceSet` is a read-only sentinel for a temporary miss; lookups return missing
entries and mutations throw `NotSupportedException`.

## `LocalizedStringEntry`

`LocalizedStringEntry` carries required `Id`, `ResourceSet`, `Culture`, `Name`, and settable `Value`,
plus `ResourceNotFound` and `Missing(resourceSet, culture, name)`.

`Id` is `Guid.Empty` for a missing sentinel; equality is by `Id`. `ToString()` adds a `[MISSING] `
prefix only when the value does not already contain `MISSING`; the implicit conversion returns `Value`.

`ResourceAdminValidation` owns shape rules: resource-set names and keys are non-whitespace; cultures
must resolve through `CultureInfo.GetCultureInfo` and are stored verbatim; templates and notes may be
empty but not null. Validation failures throw in the `ArgumentException` family.

## `IResourceAdminStore`

```csharp
public interface IResourceAdminStore : ILocalizationStore {
    bool SupportsAdministration { get; }
    event EventHandler<ResourceAdminAuditEventArgs>? Audited;

    // Entry CRUD and renames:
    Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name,
        string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name,
        string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    // Notes: reads are un-gated; mutations take ResourceAdminOptions and a token.
    Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
    Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name,
        ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
}
```

`ResourceAdminOptions` has `DryRun` (default `false`) and `Actor` (default empty). A dry run computes
the result without changing anything and raises no audit entries. The actor is recorded on every
audit entry.

- **Expected conflicts are returned, not thrown.** `ResourceAdminResult` exposes `Applied`, `DryRun`,
  `AffectedEntries`, and `Conflict`. The kind is `AlreadyExists`, `NotFound`, or `TargetExists`.
- **Validation failures throw** in the `ArgumentException` family before a result exists.
- **A read-only store throws `StoreReadOnlyException`** before producing a result.
- **Renames are cross-culture atomic.** A key or set rename either commits in every affected culture
  or reports a conflict and changes nothing.
- **Notes are key-level and culture-agnostic.** A note requires the key to exist in at least one
  culture; there are no orphan notes. Renaming moves the note; deleting the key's last culture row
  deletes it.

`store.RequireAdmin()` is the capability gate and throws `StoreReadOnlyException` when unsupported;
`store.SupportsAdministration()` answers the boolean without throwing.

## Audit and history

`Audited` is the live, synchronous event. It fires once per mutated row after commit, in ordinal
culture order for multi-row operations. A dry run or conflict raises nothing. A throwing handler
propagates even though the mutation committed; keep handlers fast and non-throwing.

`ResourceHistoryRecord` carries `Sequence` (append order), `Version` (monotonic batch version),
`Timestamp`, `Actor`, `Operation`, `ResourceSet`, `Culture`, `Key`, `NewKey`, `EntryId`,
`OldValueHash`, and `NewValueHash`.

```csharp
public interface IResourceHistoryStore : ILocalizationStore {
    bool SupportsHistory { get; }
    Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0,
        CancellationToken cancellationToken = default);
    Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(
        string resourceSet, string culture, long version, CancellationToken cancellationToken = default);
    Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default);
}
```

- History covers the governed path only. Raw resource-set CRUD is unversioned and unaudited.
- Versions are monotonic and shared by every record of one batch. `Sequence` is independent and
  strictly increasing.
- `GetHistoryAsync` uses an exclusive lower bound: only records with `Version > sinceVersion`. `0`
  reads the whole trail.
- `GetStringsAsOfVersionAsync` is replay-faithful. Version `0` is the empty pre-history state.
- Audit records store lowercase SHA-256 (Secure Hash Algorithm, 256-bit) hashes of the UTF-8 (Unicode Transformation Format, 8-bit) template bytes, never template text. The
  replay journal stores actual values, so protect it as catalog data.
- `VerifyHistoryAsync` recomputes an unanchored hash chain. It detects ordinary modification,
  insertion, or removal; it cannot prove authenticity against an attacker who rewrites the chain.

`store.RequireHistory()` is the capability gate and throws `StoreHistoryUnsupportedException` when
history is absent.

## Synchronization

`IResourceSynchronizer.SynchronizeAsync(declarations, options, token)` returns a
`ResourceSynchronizationReport`. `ResourceDeclaration` carries a resource set, key, default template,
and optional note.
`ResourceSynchronizationOptions` carries `DefaultCulture` (required), `TargetCultures`, `DryRun`,
`AllowUpdates`, `CreateMissingTargetCultures`, `Renames`, and `Actor`.

- Adds only by default; differing default-culture values are `Skipped` unless `AllowUpdates` is set.
  Non-default culture values are never updated.
- Missing target cultures are skipped so fallback applies, unless `CreateMissingTargetCultures`
  creates empty placeholders. Empty values are valid and suppress fallback.
- Renames are explicit through `Renames`; notes follow declarations (applied on create, upserted only
  with `AllowUpdates`).
- No delete. Per-key conflicts do not abort the run. A second run is idempotent and audits nothing.

The report contains `Created`, `Updated`, `Unchanged`, `Skipped`, `Conflict`, and `Renamed` counts plus
per-entry details. Treat it as deployment evidence, not a boolean.

## Cache decorators

`CachedLocalizationStoreDecorator(ILocalizationStore innerStore, IMemoryCache cacheService)` caches
resource sets in process. It implements `IResourceAdminStore` and `IResourceHistoryStore` by
forwarding; reloads invalidate cached resource sets; history reads are forwarded and never cached.

`DistributedLocalizationStoreDecorator(innerStore, distributedCache, options)` mirrors the semantics
across processes. Options: `Expiration` (default 30 minutes; positive) and `KeyPrefix` (default
`Jeninnet.Localization`; no trailing `:`). It implements `IDistributedStoreCacheInvalidator`
(`InvalidateEntryAsync`, `InvalidateCultureAsync`, `BumpGenerationAsync`, `GetGenerationAsync`);
invalidation is stamp-based, so older payloads are ignored cluster-wide. Wire decorators explicitly;
neither has an `Add...` extension.

## EF Core store

`AddEntityFrameworkLocalizationStore(connectionString)` registers a `LocalizationDataContext` through
`AddDbContextFactory` and `SqlDbLocalizationStore` as a singleton `ILocalizationStore`. The
`IConfiguration` overload reads a named connection string, default `LocalizationDb`.

The store implements `ILocalizationStore`, `IResourceAdminStore`, and `IResourceHistoryStore`, with
`LocalizedStrings`, audit records, the replay journal, the version row, and notes. Schema management
is consumer-owned: `EnsureCreated` includes the current model in an empty database, and any existing
database needs an explicit schema step. See
[EF Core store schema](../04-operations/06-ef-core-store-schema.md) for the table inventory and
provisioning behavior.

The EF store is **not** an `IMessageProvider`; database message providers use a different table
(`LocalizationMessages`). The EF store is excluded from the Native AOT (Ahead-of-Time) contract; see
[Compatibility and versions](07-compatibility-and-versions.md).

## Worked example

A dry-run create returns `Applied = false`, `DryRun = true`, and the would-be row count; the applied
call returns `Applied = true`.

## Pitfalls

**Pitfall:** raw resource-set CRUD bypasses validation, audit, and versioning; holding a resource set
as a cache hides reloads.

## Concurrency and versioning

- The store is a singleton; readers use independent contexts per call.
- The EF store serializes governed mutations per store instance and uses a bounded-retry conditional
  update for the version counter, so in-process writers cannot share a version.
- Cross-process writers are not coordinated by that gate; add schema constraints or serialize them.
- Persisted `Sequence` and `Version`, not wall-clock time, are authoritative.
- Raw resource-set CRUD is not gated like the governed path; do not mix the two.

## Where to go next

- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md) for the workflow.
- [Audit and history](../02-guides/07-audit-and-history.md) for the accountability model.
