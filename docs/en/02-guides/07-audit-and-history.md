# Audit and History

A production catalog is content with consequences. Someone changes a price label, a legal
disclaimer, or a validation message, and later somebody asks who changed it, when, and what it said
before. Audit and history answer those questions for the governed administration path.

Two surfaces exist:

- **A live audit event** raised once per mutated row after a governed mutation commits.
- **A persisted history** with monotonic catalog versions, as-of-version reads, and hash-chain
  verification.

They share metadata types but serve different purposes: observation versus accountability.

## What is recorded

`ResourceAdminAuditEventArgs` carries:

| Property | Meaning |
|---|---|
| `Timestamp` | When the mutation committed (UTC). |
| `Actor` | The caller-supplied actor, empty when anonymous. |
| `Operation` | The governed operation, for example `CreateEntry` or `RenameKey`. |
| `ResourceSet` | The resource-set name (the new name for a set rename). |
| `Culture` | The culture of the mutated row; empty for note rows. |
| `Key` | The key (the source key for a rename). |
| `NewKey` | The rename target, or `null` for non-renames. |
| `EntryId` | The persisted identity of the row. |
| `OldValue` | The template before the mutation, or `null` for creates. |
| `NewValue` | The template after the mutation, or `null` for deletes and renames. |

The event is raised **once per mutated row**, after the mutation commits. Multi-row mutations are
raised in ordinal culture order. A key rename across two cultures produces two entries. A note
cascade that touches a note row produces an additional entry with an empty culture. Dry-run
previews raise nothing because they mutate nothing. Conflicts raise nothing because nothing
changed.

`ResourceAdminAuditContext` and `ResourceAdminAuditRow` are the shared metadata carriers behind the
event and the persisted record. If you build tooling around audit, depend on the properties rather
than constructing these types; the constructors are an implementation detail grouped by the
architecture.

## Persisted history

`IResourceHistoryStore` extends `ILocalizationStore` with the durable trail. Like administration,
the capability is two-level: implementing the interface is the type-level statement;
`SupportsHistory` is the instance-level honest answer. Use the helper to enter through the
capability check:

```csharp
using Jeninnet.Localization.Abstractions.Store;

var history = store.RequireHistory(); // throws StoreHistoryUnsupportedException when absent
```

The members are:

| Member | Purpose |
|---|---|
| `GetCurrentVersionAsync` | The latest applied batch version, or `0` before the first governed mutation. |
| `GetHistoryAsync(sinceVersion)` | Records with `Version > sinceVersion`, ordered by append sequence. `0` reads the entire trail. |
| `GetStringsAsOfVersionAsync(resourceSet, culture, version)` | Replay-faithful catalog state at the end of a version. `0` is the empty pre-history state. |
| `VerifyHistoryAsync` | Recompute the hash chain and report the first invalid record, if any. |

A persisted `ResourceHistoryRecord` carries the event metadata plus:

- **`Sequence`** — the store-assigned append order. It is strictly increasing across the trail and
  is independent of version; one batch can produce many records with one version.
- **`Version`** — the catalog version of the mutation batch. Versions are monotonic; every row of
  one batch shares the version.
- **`OldValueHash` / `NewValueHash`** — the redacted hashes of the template before and after the
  mutation. Creates have a null old hash; deletes and renames have a null new hash.

### Redaction

Template text is never persisted in the audit record. The record carries the lowercase hexadecimal
SHA-256 (Secure Hash Algorithm, 256-bit) hash of the UTF-8 (Unicode Transformation Format, 8-bit) template bytes, computed by `ResourceValueHash.Compute`. The hash is
unsalted by design so the same template hashes identically across records and stores; the purpose
is equality and change detection, not secrecy against offline brute force.

The replay journal is different. As-of-version reads must reproduce the catalog, so the journal
stores the actual values. Treat the journal with the same confidentiality class as the live
catalog. The redaction rule applies to the audit trail, not to versioned catalog data.

### Renames and notes

A key rename preserves row identities and records the old key as `Key`, the target as `NewKey`, and
the source template hash as `OldValueHash`. The new hash is null because the rename is not a value
change. The replay journal records a tombstone for the old key and an upsert for the new key, so
as-of reads reproduce the rename.

Translator notes are key-level and culture-agnostic, so their audit records carry an empty
`Culture`. Notes are accountability-tracked like templates: a direct note change and each cascaded
note row change bump the catalog version and append an audit record. Notes are not part of
as-of-version template replay; the replay journal covers templates only.

### Hash-chain verification

`VerifyHistoryAsync` recomputes the chain over the persisted records and returns
`ResourceHistoryVerification`:

- `IsValid` — whether the whole trail verifies.
- `VerifiedRecords` — the number of records verified before the first failure, or the total when
  valid.
- `FirstInvalidSequence` — the sequence of the first broken record, or `null` when valid.

Any modification, insertion, or removal breaks the chain unless every later chain hash is rewritten.
The chain is **unanchored**: a fully rewrite-capable attacker is only detectable against an
external anchor, which is out of scope. Treat verification as tamper evidence under normal
operational controls.

## Consuming the live event

Subscribe on the administering store. The event is storage-agnostic and synchronous.

```csharp
using Jeninnet.Localization.Abstractions.Store;

admin.Audited += (_, entry) => {
    logger.LogInformation(
        "Catalog {Operation} on {ResourceSet}/{Culture}/{Key} by {Actor} at {Timestamp}",
        entry.Operation,
        entry.ResourceSet,
        entry.Culture,
        entry.Key,
        entry.Actor,
        entry.Timestamp);
};
```

The handler runs after the transaction commits. If it throws, the exception propagates even though
the mutation is already committed: the catalog is the source of truth, and a failing observer must
not roll back a committed change. Keep handlers fast and non-throwing.

The live event is not durable: no replay, no sequence, no version. For accountability, persist the
information yourself or use `IResourceHistoryStore`; do not treat a log line as a complete audit
system.

## Consuming persisted history

```csharp
var history = store.RequireHistory();

var currentVersion = await history.GetCurrentVersionAsync(cancellationToken);
var records = await history.GetHistoryAsync(sinceVersion: 0, cancellationToken);

foreach (var record in records) {
    Console.WriteLine(
        $"{record.Sequence,6} v{record.Version,-4} {record.Operation,-18} " +
        $"{record.ResourceSet}/{record.Culture}/{record.Key} by {record.Actor}");
}

var asOf = await history.GetStringsAsOfVersionAsync(
    "Bookstore",
    "en",
    version: currentVersion,
    cancellationToken);
```

`GetHistoryAsync(sinceVersion)` uses an exclusive bound: keep the highest version seen and pass it
to append only newer records.

`GetStringsAsOfVersionAsync` is replay-faithful: the result equals the catalog state after applying
every mutation batch up to and including the requested version. It returns only entries that
existed at that version. Version `0` is the empty pre-history state. Use it to investigate a
regression or to export a prior catalog, not as an automatic rollback mechanism.

`VerifyHistoryAsync` belongs in an operational job. Run it on a schedule, alert on an invalid
result, and retain the external evidence you need if the threat model requires anchoring.

## Worked example: a regulated edit

An administrator changes `Legal.Disclaimer` from version A to B with actor `alice`.

1. `UpdateEntryAsync` validates identifiers and culture.
2. One transaction updates the catalog, appends a replay upsert, appends one hash-chained audit
   record with both hashes, and advances the catalog version.
3. After commit, `Audited` fires with `OldValue = A`, `NewValue = B`, and `Actor = "alice"`.
4. A compliance job reads `GetHistoryAsync(lastSeenVersion)` and stores the record.
5. A reviewer calls `GetStringsAsOfVersionAsync("Legal", "en", previousVersion)` to see the prior
   text; the audit record itself carries only hashes.

## Boundaries

- **Governed path only.** History covers `IResourceAdminStore` mutations. Raw
  `ILocalizationResourceSet` CRUD (Create, Read, Update, Delete) is not versioned and leaves no audit records.
- **No dry-run records.** Dry runs mutate nothing and therefore audit nothing.
- **No conflict records.** A refused mutation changes nothing and produces no audit entry; the
  conflict is returned in `ResourceAdminResult`.
- **Notes are template-adjacent, not template replay.** Note changes are audited and versioned, but
  as-of reads replay templates only.
- **History is a store capability.** File providers and read-only stores do not implement it and
  reject through `StoreHistoryUnsupportedException`. The cached store decorator forwards history
  reads to the inner store and never caches them.

## Pitfalls

**Pitfall:** assuming the live event is a complete audit trail. It is in-process and synchronous,
with no replay and no version. Use the persisted history for accountability.

**Pitfall:** letting an audit handler throw. The mutation is already committed; the caller receives
an exception for a change that succeeded. Contain observer failures inside the handler.

**Pitfall:** expecting `OldValue`/`NewValue` on `ResourceHistoryRecord`. The persisted record has
only hashes. To retrieve historical text, use an as-of-version read against the replay journal.

**Pitfall:** assuming the hash chain proves authenticity against a privileged attacker. It detects
ordinary tampering; an unanchored chain can be rewritten by someone who can rewrite every
subsequent record.

**Pitfall:** mixing raw CRUD with governed edits and expecting a complete trail. The raw path
leaves no records, so a later investigation will show unexplained gaps.

## Where to go next

- [Resource sets and the store](06-resource-sets-and-store.md) for the governed operations that
  produce these records.
- [Security](../04-operations/02-security.md) for confidentiality and access control around audit
  storage.
- [Storage API](../03-reference/02-storage-api.md) for the complete history contract.
