//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// In-memory <see cref="ICacheStore"/> with TTL expiry and a bounded entry count.
/// </summary>
internal sealed class MemoryCacheStore : ICacheStore {
    /// <summary>
    /// Represents a cache entry.
    /// </summary>
    /// <param name="Template">The template.</param>
    /// <param name="ExpiresAt">The expires at.</param>
    private readonly record struct CacheEntry(string? Template, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<(string key, string culture), CacheEntry> _cache = new();
    private readonly TimeSpan _cacheExpiration;
    private readonly int _maxEntries;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryCacheStore"/> class.
    /// </summary>
    /// <param name="cacheExpiration">The time that resolved messages remain in the cache.</param>
    /// <param name="maxEntries">The maximum number of retained cache entries.</param>
    /// <param name="timeProvider">The clock backing cache expiry.</param>
    internal MemoryCacheStore(TimeSpan cacheExpiration, int maxEntries, TimeProvider timeProvider) {
        _cacheExpiration = cacheExpiration;
        _maxEntries = maxEntries;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public bool TryGet(string key, string culture, out string? template) {
        if(_cache.TryGetValue((key, culture), out var entry) && entry.ExpiresAt > _timeProvider.GetUtcNow()) {
            template = entry.Template;
            return true;
        }

        _cache.TryRemove((key, culture), out _);
        template = null;
        return false;
    }

    /// <inheritdoc />
    public ValueTask<(bool Hit, string? Template)> TryGetAsync(string key, string culture, int generation, CancellationToken cancellationToken) {
        if(TryGet(key, culture, out var template)) {
            return new ValueTask<(bool, string?)>((true, template));
        }

        return new ValueTask<(bool, string?)>((false, null));
    }

    /// <inheritdoc />
    public Task WriteAsync(
        IReadOnlyDictionary<string, string?> result,
        string culture,
        int generation,
        Func<int> getCurrentGeneration,
        Lock syncRoot,
        CancellationToken cancellationToken) {
        lock(syncRoot) {
            if(generation != getCurrentGeneration()) {
                return Task.CompletedTask;
            }

            var expiresAt = _timeProvider.GetUtcNow().Add(_cacheExpiration);
            foreach(var pair in result) {
                AddOrReplaceCacheEntry((pair.Key, culture), new CacheEntry(pair.Value, expiresAt));
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Invalidate()
        => _cache.Clear();

    /// <summary>
    /// Adds or replace cache entry.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="entry">The entry.</param>
    private void AddOrReplaceCacheEntry((string key, string culture) cacheKey, CacheEntry entry) {
        if(_cache.Count >= _maxEntries && !_cache.ContainsKey(cacheKey)) {
            EvictCacheEntries();
        }

        _cache[cacheKey] = entry;
    }

    /// <summary>
    /// Restores the cache bound: expired entries are removed first; if the cache is still at the
    /// bound, the entry closest to expiry (oldest insertion under the fixed TTL) is evicted.
    /// </summary>
    private void EvictCacheEntries() {
        var now = _timeProvider.GetUtcNow();
        foreach(var pair in _cache) {
            if(pair.Value.ExpiresAt <= now) {
                _cache.TryRemove(pair.Key, out _);
            }
        }

        while(_cache.Count >= _maxEntries) {
            var oldestKey = default((string key, string culture));
            var oldestExpiry = DateTimeOffset.MaxValue;
            var found = false;
            foreach(var pair in _cache) {
                if(pair.Value.ExpiresAt < oldestExpiry) {
                    oldestExpiry = pair.Value.ExpiresAt;
                    oldestKey = pair.Key;
                    found = true;
                }
            }

            if(!found || !_cache.TryRemove(oldestKey, out _)) {
                break;
            }
        }
    }
}
