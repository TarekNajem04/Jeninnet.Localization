//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// One persisted, append-only audit line for a governed resource-administration
/// mutation: the durable counterpart of
/// <see cref="ResourceAdminAuditEventArgs"/>, carrying the monotonic catalog version
/// of the mutation batch. Template text is never persisted in the audit record —
/// only <see cref="OldValueHash"/>/<see cref="NewValueHash"/>, computed per the
/// documented redaction rule (<see cref="ResourceValueHash"/>), so secret-bearing
/// values never land in audit storage in plain text.
/// </summary>
public sealed class ResourceHistoryRecord {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceHistoryRecord"/> class.
    /// </summary>
    /// <param name="sequence">The store-assigned append order (strictly increasing).</param>
    /// <param name="version">The catalog version of the mutation batch; versions are monotonic and shared by every row of one batch.</param>
    /// <param name="context">The audit metadata shared by the mutation batch.</param>
    /// <param name="row">The location and persisted identity of the mutated row.</param>
    /// <param name="oldValueHash">The redacted hash of the template before the mutation (<see langword="null"/> for creates).</param>
    /// <param name="newValueHash">The redacted hash of the template after the mutation (<see langword="null"/> for deletes and renames).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="row"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sequence"/> is negative or <paramref name="version"/> is less than one.</exception>
    public ResourceHistoryRecord(
        long sequence,
        long version,
        ResourceAdminAuditContext context,
        ResourceAdminAuditRow row,
        string? oldValueHash,
        string? newValueHash) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        Sequence = sequence;
        Version = version;
        Timestamp = context.Timestamp;
        Actor = context.Actor;
        Operation = context.Operation;
        ResourceSet = row.ResourceSet;
        Culture = row.Culture;
        Key = row.Key;
        NewKey = row.NewKey;
        EntryId = row.EntryId;
        OldValueHash = oldValueHash;
        NewValueHash = newValueHash;
    }

    /// <summary>
    /// Gets the store-assigned append order. Strictly increasing across the trail,
    /// independent of version (one batch has one version but many records).
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets the catalog version of the mutation batch. Versions are monotonic —
    /// each applied mutation batch receives the next version — and every record of
    /// one batch shares its version.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets when the mutation committed (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets who performed the mutation (empty when anonymous).
    /// </summary>
    public string Actor { get; }

    /// <summary>
    /// Gets the operation that produced this record.
    /// </summary>
    public ResourceAdminOperation Operation { get; }

    /// <summary>
    /// Gets the resource-set name of the mutated row (the new name for renames of sets).
    /// </summary>
    public string ResourceSet { get; }

    /// <summary>
    /// Gets the culture of the mutated row.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets the key of the mutated row (the source name for renames).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the rename target, or <see langword="null"/> for non-rename operations.
    /// </summary>
    public string? NewKey { get; }

    /// <summary>
    /// Gets the persisted identity of the mutated row.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// Gets the redacted hash of the template before the mutation
    /// (<see cref="ResourceValueHash.Compute(string)"/>; <see langword="null"/> for creates).
    /// </summary>
    public string? OldValueHash { get; }

    /// <summary>
    /// Gets the redacted hash of the template after the mutation
    /// (<see cref="ResourceValueHash.Compute(string)"/>; <see langword="null"/> for deletes and renames).
    /// </summary>
    public string? NewValueHash { get; }
}
