//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// Append-only journal row backing replay-faithful as-of-version reads.
/// One row per touched key and batch: an upsert row carries the value at that
/// version; a tombstone (<see cref="Value"/> is <see langword="null"/>) records
/// a deletion or rename source. Unlike audit records, journal rows store template
/// values as-is — they are versioned catalog data, the same confidentiality class
/// as the live catalog; audit storage is the redacted layer.
/// </summary>
public sealed class LocalizedStringVersion {
    /// <summary>
    /// Gets or sets the store-assigned append order (database identity, strictly increasing).
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Gets or sets the catalog version at which this state became effective.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the resource-set name at this version (the new name for set renames).
    /// </summary>
    public string ResourceSet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the culture of the journaled row.
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key at this version (the new name for renames).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the persisted identity of the row (preserved across renames).
    /// </summary>
    public Guid EntryId { get; set; }

    /// <summary>
    /// Gets or sets the template value as of this version, or <see langword="null"/>
    /// for a tombstone (deletion or rename source).
    /// </summary>
    public string? Value { get; set; }
}
