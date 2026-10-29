//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// One storage-agnostic audit line for a persisted resource-administration mutation:
/// who performed it, when it committed, and exactly what row changed. The audit hook
/// (<see cref="IResourceAdminStore.Audited"/>) carries these entries; persistence of
/// the audit trail itself is a later operational concern. One entry is raised
/// per mutated row, in ordinal culture order for multi-row mutations. Dry-run previews
/// raise no audit entries because they mutate nothing.
/// </summary>
public sealed class ResourceAdminAuditEventArgs : EventArgs {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAdminAuditEventArgs"/> class.
    /// </summary>
    /// <param name="context">The audit metadata shared by the mutation batch.</param>
    /// <param name="row">The location and persisted identity of the mutated row.</param>
    /// <param name="oldValue">The template before the mutation (<see langword="null"/> for creates).</param>
    /// <param name="newValue">The template after the mutation (<see langword="null"/> for deletes and renames).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="row"/> is <see langword="null"/>.</exception>
    public ResourceAdminAuditEventArgs(
        ResourceAdminAuditContext context,
        ResourceAdminAuditRow row,
        string? oldValue,
        string? newValue) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);

        Timestamp = context.Timestamp;
        Actor = context.Actor;
        Operation = context.Operation;
        ResourceSet = row.ResourceSet;
        Culture = row.Culture;
        Key = row.Key;
        NewKey = row.NewKey;
        EntryId = row.EntryId;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>
    /// Gets when the mutation committed (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets who performed the mutation (the caller-supplied actor; empty when anonymous).
    /// </summary>
    public string Actor { get; }

    /// <summary>
    /// Gets the operation that produced this entry.
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
    /// Gets the template before the mutation (<see langword="null"/> for creates).
    /// </summary>
    public string? OldValue { get; }

    /// <summary>
    /// Gets the template after the mutation (<see langword="null"/> for deletes and renames).
    /// </summary>
    public string? NewValue { get; }
}
