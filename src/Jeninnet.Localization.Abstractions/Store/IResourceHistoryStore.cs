//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Persisted audit trail and versioned reads for governed catalog mutations.
/// <para>
/// Stores that implement this interface keep an append-only history of every applied
///  mutation (one record per mutated row, carrying actor, timestamp, key set,
/// and redacted before/after value hashes — never template text in plain text, per
/// <see cref="ResourceValueHash"/>), assign a monotonic catalog version to each
/// mutation batch, expose as-of-version reads that reproduce the catalog exactly,
/// and detect tampering with the trail through a hash chain.
/// </para>
/// <para>
/// Versioning covers the governed  mutation path. Raw resource-set lifecycle
/// writes (<see cref="ILocalizationResourceSet"/> CRUD (Create, Read, Update, Delete)) are not versioned and leave
/// no audit records — the low-level path stays outside the accountability model.
/// </para>
/// <para>
/// Capability advertisement is two-level, mirroring
/// <see cref="IResourceAdminStore"/>: the type-level statement is implementing this
/// interface; the instance-level honest statement is <see cref="SupportsHistory"/>.
/// A store that does not implement this interface — or that honestly reports
/// <see langword="false"/> — rejects every history member through
/// <see cref="StoreHistoryUnsupportedException"/> via
/// <see cref="LocalizationStoreHistory.RequireHistory(ILocalizationStore)"/>,
/// never silently.
/// </para>
/// </summary>
public interface IResourceHistoryStore : ILocalizationStore {
    /// <summary>
    /// Gets whether this store instance currently supports persisted history and
    /// versioned reads. An implementation may honestly report <see langword="false"/>;
    /// every history member then throws <see cref="StoreHistoryUnsupportedException"/>
    /// through <see cref="LocalizationStoreHistory.RequireHistory(ILocalizationStore)"/>.
    /// </summary>
    bool SupportsHistory { get; }

    /// <summary>
    /// Gets the current catalog version: the version of the most recently applied
    /// governed mutation batch, or <c>0</c> when no governed mutation has committed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is the current catalog version.</returns>
    /// <exception cref="StoreHistoryUnsupportedException">Thrown when the store does not support history.</exception>
    Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the audit trail ordered by append sequence. The trail is complete:
    /// every applied governed mutation contributes one record per mutated row, and
    /// records of one batch share the batch version.
    /// </summary>
    /// <param name="sinceVersion">
    /// Exclusive lower bound: only records with <see cref="ResourceHistoryRecord.Version"/>
    /// greater than this value are returned. <c>0</c> reads the entire trail.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is the ordered audit trail.</returns>
    /// <exception cref="StoreHistoryUnsupportedException">Thrown when the store does not support history.</exception>
    Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one resource-set/culture pair exactly as it existed at the end of the
    /// given catalog version (replay-faithful: the result equals the catalog state
    /// after applying every mutation batch up to and including <paramref name="version"/>).
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="version">The catalog version to read; <c>0</c> is the empty pre-history state.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is the catalog state as of the requested version (only existing entries).</returns>
    /// <exception cref="StoreHistoryUnsupportedException">Thrown when the store does not support history.</exception>
    Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(string resourceSet, string culture, long version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the audit trail against its hash chain and reports whether tampering
    /// is detectable. Any modification, insertion, or removal of a persisted record
    /// breaks the chain unless every subsequent record's chain hash is also rewritten;
    /// a fully rewrite-capable attacker against an unanchored chain is only detectable
    /// against an externally retained anchor — anchoring is out of scope (deferred).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result reports validity and, when invalid, the first broken record.</returns>
    /// <exception cref="StoreHistoryUnsupportedException">Thrown when the store does not support history.</exception>
    Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default);
}
