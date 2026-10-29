//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Code-first resource synchronization: applies a set of
/// <see cref="ResourceDeclaration"/> values to a store — every mutation through the
/// governed <see cref="IResourceAdminStore"/> path (validation, conflicts, audit,
/// actor), never a parallel write mechanism, never a delete, never an implicit
/// background add. The callable service is explicit; hosts schedule it.
/// </summary>
public interface IResourceSynchronizer {
    /// <summary>
    /// Synchronizes the declarations into the store and returns the per-entry report.
    /// Validate-then-apply: invalid input fails before any store call; a read-only
    /// store rejects with the typed <see cref="StoreReadOnlyException"/>.
    /// </summary>
    /// <param name="declarations">The declarations to synchronize.</param>
    /// <param name="options">The synchronization options (default culture required).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The synchronization report.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an option or a declaration is invalid, or a declaration duplicates another.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support governed administration.</exception>
    Task<ResourceSynchronizationReport> SynchronizeAsync(IReadOnlyList<ResourceDeclaration> declarations, ResourceSynchronizationOptions options, CancellationToken cancellationToken = default);
}
