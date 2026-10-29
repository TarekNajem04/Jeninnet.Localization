//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Represents a caching decorator for an <see cref="ILocalizationStore"/>.
/// This decorator forwards calls to the underlying store while caching results to improve performance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CachedLocalizationStoreDecorator"/> class.
/// </remarks>
/// <param name="innerStore">The underlying <see cref="ILocalizationStore"/> to be decorated.</param>
/// <param name="cacheService">The memory cache instance used for caching.</param>
/// <exception cref="ArgumentNullException">Thrown if either argument is null.</exception>
public class CachedLocalizationStoreDecorator(ILocalizationStore innerStore, IMemoryCache cacheService) : IResourceAdminStore, IResourceHistoryStore {
    /// <summary>
    /// Gets the underlying localization store.
    /// </summary>
    protected ILocalizationStore LocalizationStore { get; } = innerStore ?? throw new ArgumentNullException(nameof(innerStore));

    /// <summary>
    /// Gets the memory cache instance.
    /// </summary>
    protected IMemoryCache CacheService { get; } = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
#if NET9_0_OR_GREATER

    private readonly Lock _sync = new();
#else

    private readonly object _sync = new();
#endif
    private readonly Dictionary<StoreCacheKey, ILocalizationResourceSet> _resourceSets = [];
    private readonly HashSet<string> _populatedCultures = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        var key = new StoreCacheKey(resourceSet, culture);

        lock(_sync) {
            if(_resourceSets.TryGetValue(key, out var cachedResourceSet)) {
                return cachedResourceSet;
            }
        }

        var resourceSetResult = await LocalizationStore.GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

        if(resourceSetResult is null) {
            return null;
        }

        resourceSetResult = CreateDecorator(resourceSetResult);

        lock(_sync) {
            if(_resourceSets.TryGetValue(key, out var existingResourceSet)) {
                resourceSetResult = existingResourceSet;
            } else {
                _resourceSets[key] = resourceSetResult;
            }
        }

        return resourceSetResult;
    }

    /// <inheritdoc/>
    public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) =>
        LocalizationStoreReads.GetStringAsync(GetResourceSetAsync, resourceSet, culture, name, cancellationToken);

    /// <inheritdoc/>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
        LocalizationStoreReads.GetAllStringsAsync(GetResourceSetAsync, resourceSet, culture, cancellationToken);

    /// <inheritdoc/>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
        bool culturePopulated;

        lock(_sync) {
            culturePopulated = _populatedCultures.Contains(culture);
        }

        if(culturePopulated) {
            List<ILocalizationResourceSet> cachedResourceSets;

            lock(_sync) {
                cachedResourceSets = [.. _resourceSets.Values.Where(rs => rs.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))];
            }

            return await BuildCultureSnapshotAsync(cachedResourceSets, cancellationToken).ConfigureAwait(false);
        }

        var localizationResourceSets = await LocalizationStore.GetResourceSetsAsync(culture, cancellationToken)
                                                        .ConfigureAwait(false);

        var localizationResourceSetDecorators = localizationResourceSets.ConvertAll(CreateDecorator);

        lock(_sync) {
            foreach(var localizationResourceSetDecorator in localizationResourceSetDecorators) {
                _resourceSets.TryAdd(new StoreCacheKey(localizationResourceSetDecorator.ResourceSet, localizationResourceSetDecorator.Culture), localizationResourceSetDecorator);
            }

            _populatedCultures.Add(culture);
        }

        return await BuildCultureSnapshotAsync(localizationResourceSetDecorators, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
        lock(_sync) {
            if(_populatedCultures.Contains(culture)) {
                return [.. _resourceSets.Values.Where(rs => rs.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))];
            }
        }

        var localizationResourceSets = await LocalizationStore.GetResourceSetsAsync(culture, cancellationToken)
                                                        .ConfigureAwait(false);

        var localizationResourceSetDecorators = localizationResourceSets.ConvertAll(CreateDecorator);

        lock(_sync) {
            foreach(var localizationResourceSetDecorator in localizationResourceSetDecorators) {
                _resourceSets.TryAdd(new StoreCacheKey(localizationResourceSetDecorator.ResourceSet, localizationResourceSetDecorator.Culture), localizationResourceSetDecorator);
            }

            _populatedCultures.Add(culture);
        }

        return localizationResourceSetDecorators;
    }

    /// <inheritdoc/>
    public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
        var key = new StoreCacheKey(resourceSet, culture);

        lock(_sync) {
            if(_resourceSets.TryGetValue(key, out localizationResourceSet)) {
                return true;
            }
        }

        if(LocalizationStore.TryGetResourceSet(resourceSet, culture, out localizationResourceSet)) {
            localizationResourceSet = CreateDecorator(localizationResourceSet);

            lock(_sync) {
                if(_resourceSets.TryGetValue(key, out var existingResourceSet)) {
                    localizationResourceSet = existingResourceSet;
                } else {
                    _resourceSets[key] = localizationResourceSet;
                }
            }

            return true;
        }

        localizationResourceSet = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
        entry = null;

        if(!TryGetResourceSet(resourceSet, culture, out var cachedResourceSet) || cachedResourceSet is null) {
            return false;
        }

        return cachedResourceSet.TryGetString(name, out entry);
    }

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken cancellationToken = default) {
        await LocalizationStore.ReloadAsync(cancellationToken).ConfigureAwait(false);

        lock(_sync) {
            foreach(var resourceSet in _resourceSets.Values) {
                if(resourceSet is CachedLocalizationResourceSetDecorator cachedResourceSet) {
                    cachedResourceSet.InvalidateCache();
                }
            }

            _resourceSets.Clear();
            _populatedCultures.Clear();
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) =>
        await LocalizationStore.GetSupportedCulturesAsync(cancellationToken).ConfigureAwait(false);

    #region Resource Administration forwarding
    /// <inheritdoc/>
    public bool SupportsAdministration =>
        LocalizationStore is IResourceAdminStore admin && admin.SupportsAdministration;

    /// <inheritdoc/>
    /// <remarks>
    /// Subscriptions forward to the inner store's hook. Subscribing on a read-only
    /// inner store is a no-op: no mutation can commit, so no audit entry can exist.
    /// </remarks>
    public event EventHandler<ResourceAdminAuditEventArgs>? Audited {
        add {
            if(LocalizationStore is IResourceAdminStore admin) {
                admin.Audited += value;
            }
        }
        remove {
            if(LocalizationStore is IResourceAdminStore admin) {
                admin.Audited -= value;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateValue(value);

        var result = await InnerAdmin().CreateEntryAsync(resourceSet, culture, name, value, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateValue(value);

        var result = await InnerAdmin().UpdateEntryAsync(resourceSet, culture, name, value, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);

        var result = await InnerAdmin().DeleteEntryAsync(resourceSet, culture, name, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(oldName);
        ResourceAdminValidation.ValidateKey(newName);

        var result = await InnerAdmin().RenameKeyAsync(resourceSet, oldName, newName, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);

        var result = await InnerAdmin().DeleteResourceSetAsync(resourceSet, culture, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(oldResourceSet);
        ResourceAdminValidation.ValidateResourceSet(newResourceSet);

        var result = await InnerAdmin().RenameResourceSetAsync(oldResourceSet, newResourceSet, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are not cached; reads forward directly. A decorator over a store without
    /// the administration contract reports no notes (the surface does not exist there).
    /// </remarks>
    public Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.GetNoteAsync(LocalizationStore, resourceSet, name, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are not cached; reads forward directly. A decorator over a store without
    /// the administration contract reports no notes (the surface does not exist there).
    /// </remarks>
    public Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.GetNotesAsync(LocalizationStore, resourceSet, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes never enter the template caches, so an applied note mutation performs no
    /// cache invalidation.
    /// </remarks>
    public Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.SetNoteAsync(LocalizationStore, resourceSet, name, note, options, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes never enter the template caches, so an applied note mutation performs no
    /// cache invalidation.
    /// </remarks>
    public Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.DeleteNoteAsync(LocalizationStore, resourceSet, name, options, cancellationToken);

    /// <summary>
    /// Performs the inner admin operation.
    /// </summary>
    /// <returns>The inner admin result.</returns>
    private IResourceAdminStore InnerAdmin() => ResourceAdminAccess.GetWritableAdmin(LocalizationStore);

    /// <summary>
    /// Invalidates when is applied.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task InvalidateWhenAppliedAsync(ResourceAdminResult result, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(result);

        if(result.Applied) {
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion Resource Administration forwarding

    #region Resource History forwarding
    /// <inheritdoc/>
    /// <remarks>
    /// Mirrors the inner store's honest instance-level flag; a decorator over a
    /// history-less store reports the capability as absent, and every history member
    /// rejects with the typed <see cref="StoreHistoryUnsupportedException"/>.
    /// </remarks>
    public bool SupportsHistory =>
        LocalizationStore is IResourceHistoryStore history && history.SupportsHistory;

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and are never cached: the
    /// audit trail and versioned reads must be live.
    /// </remarks>
    public Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default) =>
        InnerHistory().GetCurrentVersionAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and are never cached.
    /// </remarks>
    public Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0, CancellationToken cancellationToken = default) =>
        InnerHistory().GetHistoryAsync(sinceVersion, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and are never cached.
    /// </remarks>
    public Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(string resourceSet, string culture, long version, CancellationToken cancellationToken = default) =>
        InnerHistory().GetStringsAsOfVersionAsync(resourceSet, culture, version, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and are never cached.
    /// </remarks>
    public Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default) =>
        InnerHistory().VerifyHistoryAsync(cancellationToken);

    /// <summary>
    /// Performs the inner history operation.
    /// </summary>
    /// <returns>The inner history result.</returns>
    private IResourceHistoryStore InnerHistory() {
        if(LocalizationStore is IResourceHistoryStore history && history.SupportsHistory) {
            return history;
        }

        throw new StoreHistoryUnsupportedException(
            $"Store '{LocalizationStore.GetType().FullName}' does not support resource history and rejects history reads.");
    }
    #endregion Resource History forwarding

    /// <summary>
    /// Creates a caching decorator for the specified localization resource set.
    /// </summary>
    /// <param name="resourceSetResult">The localization resource set to decorate.</param>
    /// <returns>The decorated localization resource set.</returns>
    [return: NotNull]
    protected ILocalizationResourceSet CreateDecorator([DisallowNull] ILocalizationResourceSet resourceSetResult) =>
        new CachedLocalizationResourceSetDecorator(resourceSetResult, CacheService);

    /// <summary>
    /// Builds a merged dictionary of localized entries for the specified resource sets.
    /// </summary>
    /// <param name="resourceSets">The resource sets to merge.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A dictionary mapping each localized string key to its entry across the resource sets.</returns>
    private static async Task<Dictionary<string, LocalizedStringEntry>> BuildCultureSnapshotAsync(IEnumerable<ILocalizationResourceSet> resourceSets, CancellationToken cancellationToken) {
        var entries = new Dictionary<string, LocalizedStringEntry>();

        foreach(var resourceSet in resourceSets) {
            foreach(var entry in await resourceSet.GetAllStringsAsync(cancellationToken).ConfigureAwait(false)) {
                entries[entry.Key] = entry.Value;
            }
        }

        return entries;
    }

    /// <summary>
    /// Identifies a cached resource set by its resource set name and culture.
    /// The culture comparison is case-insensitive.
    /// </summary>
    /// <param name="resourceSet">The resource set name of the cached entry.</param>
    /// <param name="culture">The culture of the cached entry.</param>
    private readonly struct StoreCacheKey(string resourceSet, string culture) : IEquatable<StoreCacheKey> {
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet { get; } = resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture { get; } = culture;

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        public bool Equals(StoreCacheKey other) =>
            StringComparer.Ordinal.Equals(ResourceSet, other.ResourceSet) &&
            StringComparer.OrdinalIgnoreCase.Equals(Culture, other.Culture);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is StoreCacheKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(ResourceSet),
                StringComparer.OrdinalIgnoreCase.GetHashCode(Culture));
    }
}
