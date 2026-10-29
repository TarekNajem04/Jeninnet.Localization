//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The outcome of one governed resource-administration mutation: whether it applied,
/// whether it was a dry-run preview, how many persisted rows it touched (or would
/// touch), and the reported conflict when it did not apply. Produced by the
/// administering store. Expected domain conflicts are reported here, never thrown;
/// validation failures throw <see cref="ArgumentException"/> and read-only stores throw
/// <see cref="StoreReadOnlyException"/> before any result is produced.
/// </summary>
public sealed class ResourceAdminResult {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAdminResult"/> class.
    /// </summary>
    /// <param name="operation">The attempted operation.</param>
    /// <param name="applied">Whether the mutation was persisted.</param>
    /// <param name="dryRun">Whether the result is a dry-run preview.</param>
    /// <param name="affectedEntries">The number of rows touched (or that would be touched).</param>
    /// <param name="conflict">The reported conflict, or <see langword="null"/> when the mutation applied or would apply.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="affectedEntries"/> is negative.</exception>
    public ResourceAdminResult(
        ResourceAdminOperation operation,
        bool applied,
        bool dryRun,
        int affectedEntries,
        ResourceAdminConflict? conflict) {
        ArgumentOutOfRangeException.ThrowIfNegative(affectedEntries);

        Operation = operation;
        Applied = applied;
        DryRun = dryRun;
        AffectedEntries = affectedEntries;
        Conflict = conflict;
    }

    /// <summary>
    /// Gets the attempted operation.
    /// </summary>
    public ResourceAdminOperation Operation { get; }

    /// <summary>
    /// Gets whether the mutation was persisted. <see langword="false"/> for conflicts
    /// and for dry-run previews.
    /// </summary>
    public bool Applied { get; }

    /// <summary>
    /// Gets whether the result is a dry-run preview that changed nothing.
    /// </summary>
    public bool DryRun { get; }

    /// <summary>
    /// Gets the number of persisted rows the mutation touched, or — for a
    /// conflict-free dry-run — the number it would have touched.
    /// </summary>
    public int AffectedEntries { get; }

    /// <summary>
    /// Gets the reported conflict when <see cref="Applied"/> is <see langword="false"/>
    /// for a domain reason; otherwise <see langword="null"/>.
    /// </summary>
    public ResourceAdminConflict? Conflict { get; }
}
