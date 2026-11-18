//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// The cache backend behind <see cref="CoalescingCacheEngine"/>: read-through, write-back,
/// and invalidation. The memory backend bounds entries and expires them on read; the
/// distributed backend keys entries by decorator identity and cache generation with
/// store-enforced TTL.
/// </summary>
internal interface ICacheStore {
    /// <summary>
    /// Attempts to get a cached template.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The target culture name.</param>
    /// <param name="generation">The cache generation observed at acquisition start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The hit flag and the template (<see langword="null"/> on miss).</returns>
    ValueTask<(bool Hit, string? Template)> TryGetAsync(string key, string culture, int generation, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes resolved templates. The write lands only when <paramref name="generation"/>
    /// still matches <paramref name="getCurrentGeneration"/> under <paramref name="syncRoot"/>,
    /// so a stale pre-refresh completion can never repopulate invalidated state.
    /// </summary>
    /// <param name="result">The resolved templates.</param>
    /// <param name="culture">The target culture name.</param>
    /// <param name="generation">The cache generation observed at acquisition start.</param>
    /// <param name="getCurrentGeneration">Reads the current generation.</param>
    /// <param name="syncRoot">The publication boundary shared with refresh invalidation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task WriteAsync(
        IReadOnlyDictionary<string, string?> result,
        string culture,
        int generation,
        Func<int> getCurrentGeneration,
        Lock syncRoot,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops locally held state on refresh. Backends that key entries by generation
    /// have nothing local to drop.
    /// </summary>
    void Invalidate();
}
