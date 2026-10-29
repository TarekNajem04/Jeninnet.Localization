//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// Persisted, append-only audit record for one mutated row of a governed
/// resource-administration mutation. Template text never lands here:
/// only redacted hashes per the documented rule
/// (<see cref="ResourceValueHash.Compute(string)"/>). Records are hash-chained
/// (<see cref="PreviousHash"/> → <see cref="ChainHash"/>) so tampering is
/// detectable through <c>IResourceHistoryStore.VerifyHistoryAsync</c>.
/// </summary>
public sealed class LocalizationAuditRecord {
    /// <summary>
    /// Gets or sets the store-assigned append order (database identity, strictly increasing).
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Gets or sets the catalog version of the mutation batch; every record of one
    /// batch shares its version.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets when the mutation committed (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets who performed the mutation (empty when anonymous).
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation that produced this record.
    /// </summary>
    public ResourceAdminOperation Operation { get; set; }

    /// <summary>
    /// Gets or sets the resource-set name of the mutated row (the new name for renames of sets).
    /// </summary>
    public string ResourceSet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture of the mutated row.
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key of the mutated row (the source name for renames).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rename target, or <see langword="null"/> for non-rename operations.
    /// </summary>
    public string? NewKey { get; set; }

    /// <summary>
    /// Gets or sets the persisted identity of the mutated row.
    /// </summary>
    public Guid EntryId { get; set; }

    /// <summary>
    /// Gets or sets the redacted hash of the template before the mutation
    /// (<see langword="null"/> for creates).
    /// </summary>
    public string? OldValueHash { get; set; }

    /// <summary>
    /// Gets or sets the redacted hash of the template after the mutation
    /// (<see langword="null"/> for deletes and renames).
    /// </summary>
    public string? NewValueHash { get; set; }

    /// <summary>
    /// Gets or sets the chain hash of the preceding record (the genesis marker for
    /// the first record).
    /// </summary>
    public string PreviousHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chain hash computed over this record and
    /// <see cref="PreviousHash"/>; verification recomputes it.
    /// </summary>
    public string ChainHash { get; set; } = string.Empty;
}
