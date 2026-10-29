//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The location and persisted identity of the row a resource-administration mutation
/// produced: resource set, culture, key (or rename source), rename target, and entry
/// identity. Shared by the live audit line (<see cref="ResourceAdminAuditEventArgs"/>)
/// and its persisted counterpart (<see cref="ResourceHistoryRecord"/>).
/// </summary>
public sealed class ResourceAdminAuditRow {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAdminAuditRow"/> class.
    /// </summary>
    /// <param name="resourceSet">The resource-set name of the mutated row (the new name for renames of sets).</param>
    /// <param name="culture">The culture of the mutated row.</param>
    /// <param name="key">The key of the mutated row (the source name for renames).</param>
    /// <param name="newKey">The rename target, or <see langword="null"/> for non-rename operations.</param>
    /// <param name="entryId">The persisted identity of the mutated row.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    public ResourceAdminAuditRow(string resourceSet, string culture, string key, string? newKey, Guid entryId) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(key);

        ResourceSet = resourceSet;
        Culture = culture;
        Key = key;
        NewKey = newKey;
        EntryId = entryId;
    }

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
}
