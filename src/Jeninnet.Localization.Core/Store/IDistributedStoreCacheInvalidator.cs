//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Documented invalidation contract for the distributed store cache.
/// Invalidation is stamp-based, never "clear the cluster and pray":
/// <list type="bullet">
/// <item>Per-key: <see cref="InvalidateEntryAsync"/> removes one resource-set/culture payload.</item>
/// <item>Per-culture: <see cref="InvalidateCultureAsync"/> removes every payload indexed under a culture.</item>
/// <item>Per-generation: <see cref="BumpGenerationAsync"/> advances the shared generation stamp so
/// payloads written before the bump are ignored on every node without deleting them.</item>
/// </list>
/// <see cref="DistributedLocalizationStoreDecorator"/> implements this interface; callers holding
/// only the decorator can invalidate through it directly.
/// </summary>
public interface IDistributedStoreCacheInvalidator {
    /// <summary>
    /// Removes the cached payload for one resource-set/culture pair on every node
    /// (the entry is deleted from the shared cache; nodes refetch from the backend on next miss).
    /// </summary>
    /// <param name="resourceSet">The resource-set name (case-sensitive, as stored).</param>
    /// <param name="culture">The culture code (matched case-insensitively, as stored).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task InvalidateEntryAsync(string resourceSet, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every cached payload indexed under <paramref name="culture"/> (all resource sets).
    /// Entries written but never indexed (unknown set names) still expire via TTL (Time To Live) or fall to
    /// <see cref="BumpGenerationAsync"/> for immediate global invalidation.
    /// </summary>
    /// <param name="culture">The culture code (matched case-insensitively, as stored).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task InvalidateCultureAsync(string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the shared generation stamp and returns the new generation.
    /// Payloads stamped with an older generation are ignored cluster-wide on next read —
    /// the cross-node equivalent of the Azure translation provider's local generation
    /// refresh (<c>RefreshAsync</c> discards completions begun before the refresh).
    /// Last-writer-wins under concurrent bumps; every bump still invalidates everything
    /// stamped before it, so concurrent operator reloads stay safe. Redis-atomic
    /// compare-and-swap tuning is explicitly out of scope (deferred finding).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The new generation stamp.</returns>
    Task<long> BumpGenerationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current shared generation stamp (<c>0</c> when never bumped or when the
    /// stamp entry is missing or corrupt — a corrupt stamp never fails a read).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The current generation stamp.</returns>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken = default);
}
