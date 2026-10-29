//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// A caching decorator for <see cref="ILocalizationResourceSet"/> that adds in-memory caching to
/// read operations and invalidates the cache when write operations occur.
/// </summary>
/// <param name="innerResourceSet">The inner localization resource set to decorate.</param>
/// <param name="cache">The memory cache instance.</param>
public class CachedLocalizationResourceSetDecorator(ILocalizationResourceSet innerResourceSet, IMemoryCache cache) : ILocalizationResourceSet {
    /// <inheritdoc/>
    public string ResourceSet => InnerResourceSet.ResourceSet;

    /// <inheritdoc/>
    public string Culture => InnerResourceSet.Culture;

    /// <summary>
    /// Gets the inner localization resource set.
    /// </summary>
    protected ILocalizationResourceSet InnerResourceSet { get; } = innerResourceSet ?? throw new ArgumentNullException(nameof(innerResourceSet));

    /// <summary>
    /// Gets the memory cache instance.
    /// </summary>
    protected IMemoryCache Cache { get; } = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <summary>
    /// Gets the cache duration.
    /// </summary>
    protected TimeSpan CacheDuration { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the cache key.
    /// </summary>
    protected string CacheKey { get; set; } = $"ILRS:Cache::{innerResourceSet.ResourceSet}::{innerResourceSet.Culture}";
#if NET9_0_OR_GREATER

    private readonly Lock _sync = new();
#else

    private readonly object _sync = new();
#endif
    private bool _fullyLoaded;

    /// <inheritdoc/>
    public CultureInfo GetCultureInfo() => InnerResourceSet.GetCultureInfo();

    /// <inheritdoc/>
    public async Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
        var cachedDictionary = GetCache();

        if(cachedDictionary.TryGetValue(name, out var entry)) {
            return entry;
        }

        entry = await InnerResourceSet.GetStringAsync(name, cancellationToken).ConfigureAwait(false);
        cachedDictionary[name] = entry;
        return entry;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
        ConcurrentDictionary<string, LocalizedStringEntry> cachedDictionary;
        bool fullyLoaded;

        lock(_sync) {
            cachedDictionary = GetCache();
            fullyLoaded = _fullyLoaded;
        }

        if(fullyLoaded) {
            return CreateSnapshot(cachedDictionary);
        }

        var allStrings = await InnerResourceSet.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);

        lock(_sync) {
            cachedDictionary = GetCache();

            foreach(var keyValuePair in allStrings) {
                cachedDictionary[keyValuePair.Key] = keyValuePair.Value;
            }

            _fullyLoaded = true;
        }

        return CreateSnapshot(cachedDictionary);
    }

    /// <inheritdoc/>
    public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
        entry = null;
        var cachedDictionary = GetCache();

        if(cachedDictionary.TryGetValue(name, out entry)) {
            return true;
        }

        var found = InnerResourceSet.TryGetString(name, out entry);

        if(found && entry is not null) {
            cachedDictionary[entry.Name] = entry;
        }

        return found;
    }

    /// <inheritdoc/>
    public async Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        await InnerResourceSet.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        GetCache()[entry.Name] = entry;
    }

    /// <inheritdoc/>
    public async Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entries);
        await InnerResourceSet.AddAsync(entries, cancellationToken).ConfigureAwait(false);

        var cachedDictionary = GetCache();

        foreach(var entry in entries) {
            cachedDictionary[entry.Name] = entry;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        await InnerResourceSet.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);

        GetCache()[entry.Name] = entry;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entries);
        await InnerResourceSet.UpdateAsync(entries, cancellationToken).ConfigureAwait(false);

        var cachedDictionary = GetCache();

        foreach(var entry in entries) {
            cachedDictionary[entry.Name] = entry;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entry);
        await InnerResourceSet.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);

        GetCache().TryRemove(entry.Name, out _);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(entries);
        await InnerResourceSet.DeleteAsync(entries, cancellationToken).ConfigureAwait(false);

        var cachedDictionary = GetCache();

        foreach(var entry in entries) {
            cachedDictionary.TryRemove(entry.Name, out _);
        }
    }

    /// <summary>
    /// Retrieves the cache for the current resource set and culture.
    /// </summary>
    /// <returns>
    /// The cached entry map. A fresh map is created when the previous map has expired.
    /// </returns>
    protected ConcurrentDictionary<string, LocalizedStringEntry> GetCache() {
        lock(_sync) {
            var cachedDictionary = Cache.Get<ConcurrentDictionary<string, LocalizedStringEntry>>(CacheKey);

            if(cachedDictionary is null) {
                cachedDictionary = new ConcurrentDictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);
                Cache.Set(CacheKey, cachedDictionary, CacheDuration);
                _fullyLoaded = false;
            }

            return cachedDictionary;
        }
    }

    /// <summary>
    /// Invalidates the cached entries for this resource set.
    /// </summary>
    internal void InvalidateCache() {
        lock(_sync) {
            Cache.Remove(CacheKey);
            _fullyLoaded = false;
        }
    }

    /// <summary>
    /// Creates a point-in-time snapshot of the cached entry map, excluding negative (missing) cache entries.
    /// </summary>
    /// <param name="cachedDictionary">The cached entry map.</param>
    /// <returns>A new dictionary containing only the localized entries that exist in the source.</returns>
    private static Dictionary<string, LocalizedStringEntry> CreateSnapshot(ConcurrentDictionary<string, LocalizedStringEntry> cachedDictionary) {
        var snapshot = new Dictionary<string, LocalizedStringEntry>(cachedDictionary.Count);

        foreach(var keyValuePair in cachedDictionary) {
            if(keyValuePair.Value.IsMissing()) {
                continue;
            }

            snapshot[keyValuePair.Key] = keyValuePair.Value;
        }

        return snapshot;
    }
}
