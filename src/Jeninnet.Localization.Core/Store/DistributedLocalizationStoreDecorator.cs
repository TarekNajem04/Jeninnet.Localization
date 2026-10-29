//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// <see cref="ILocalizationStore"/> decorator that shares store payloads across processes
/// through any <see cref="IDistributedCache"/> implementation. Redis-compatible
/// without a Redis dependency: only the <see cref="IDistributedCache"/> abstraction is
/// referenced, so the in-memory <see cref="IDistributedCache"/> covers tests while Redis
/// (or any compatible backend) covers production.
/// <para>
/// Semantics mirror the in-memory <see cref="CachedLocalizationStoreDecorator"/>:
/// per-key TTL (<see cref="DistributedStoreCacheOptions.Expiration"/>), per-node
/// single-flight coalescing (concurrent misses for one key issue one backend call per
/// node; the shared entry converges after the first write), and a documented
/// invalidation contract — <see cref="IDistributedStoreCacheInvalidator"/> — built on
/// generation stamps rather than cluster-wide clears. The generation pattern reuses the
/// Azure translation provider's refresh model: a payload stamped with an older generation
/// is ignored, and a backend completion that started before a generation bump never
/// repopulates the cache.
/// </para>
/// <para>
/// Resilience rules: the backend store is the source of truth. Distributed read/write
/// failures on the lookup path fall back to the backend (miss/skip) so a cache outage
/// never breaks localization; worst-case staleness after a lost invalidation is bounded
/// by the TTL. <see cref="OperationCanceledException"/> (or a canceled token) is never
/// swallowed and never publishes partial state.
/// </para>
/// <para>
/// Synchronous <c>TryGet*</c> members forward directly to the inner store: the
/// distributed layer is async-only (the abstraction exposes no synchronous API), so
/// synchronous lookups bypass the shared cache by design and stay deadlock-free.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DistributedLocalizationStoreDecorator"/> class.
/// </remarks>
/// <param name="innerStore">The underlying <see cref="ILocalizationStore"/> (backend source of truth).</param>
/// <param name="distributedCache">The shared cache. One instance (one cluster) must be shared by every node.</param>
/// <param name="options">Cache options, or <see langword="null"/> for defaults (validated on construction).</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="innerStore"/> or <paramref name="distributedCache"/> is null.</exception>
/// <exception cref="InvalidOperationException">Thrown when <paramref name="options"/> is invalid.</exception>
public class DistributedLocalizationStoreDecorator(
    ILocalizationStore innerStore,
    IDistributedCache distributedCache,
    DistributedStoreCacheOptions? options = null) : IResourceAdminStore, IDistributedStoreCacheInvalidator, IResourceHistoryStore {
    private readonly ILocalizationStore _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
    private readonly IDistributedCache _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
    private readonly TimeSpan _expiration = SnapshotOptions(options).Expiration;
    private readonly string _keyPrefix = SnapshotOptions(options).KeyPrefix;

    private readonly ConcurrentDictionary<string, Lazy<Task<ILocalizationResourceSet?>>> _resourceSetFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<List<ILocalizationResourceSet>>>> _cultureFlight = new(StringComparer.Ordinal);

    /// <summary>
    /// Performs the snapshot options operation.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <returns>The snapshot options result.</returns>
    private static DistributedStoreCacheOptions SnapshotOptions(DistributedStoreCacheOptions? options) {
        var snapshot = new DistributedStoreCacheOptions();

        if(options is not null) {
            snapshot.Expiration = options.Expiration;
            snapshot.KeyPrefix = options.KeyPrefix;
        }

        snapshot.Validate();
        return snapshot;
    }

    /// <summary>
    /// Gets the generation key.
    /// </summary>
    private string GenerationKey => _keyPrefix + ":gen:v1";

    /// <summary>
    /// Performs the entry key operation.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The entry key text.</returns>
    private string EntryKey(string resourceSet, string culture) =>
        _keyPrefix + ":rs:v1:" + resourceSet + ":" + culture.ToLowerInvariant();

    /// <summary>
    /// Performs the index key operation.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The index key text.</returns>
    private string IndexKey(string culture) =>
        _keyPrefix + ":idx:v1:" + culture.ToLowerInvariant();

    /// <inheritdoc/>
    public async Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var flightKey = "rs:" + resourceSet + ":" + culture.ToLowerInvariant();
        var lazy = _resourceSetFlight.GetOrAdd(
            flightKey,
            _ => new Lazy<Task<ILocalizationResourceSet?>>(
                () => AcquireResourceSetAsync(resourceSet, culture, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try {
            return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally {
            _resourceSetFlight.TryRemove(new KeyValuePair<string, Lazy<Task<ILocalizationResourceSet?>>>(flightKey, lazy));
        }
    }

    /// <inheritdoc/>
    public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) =>
        LocalizationStoreReads.GetStringAsync(GetResourceSetAsync, resourceSet, culture, name, cancellationToken);

    /// <inheritdoc/>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
        LocalizationStoreReads.GetAllStringsAsync(GetResourceSetAsync, resourceSet, culture, cancellationToken);

    /// <inheritdoc/>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(culture);
        var resourceSets = await GetResourceSetsAsync(culture, cancellationToken).ConfigureAwait(false);
        return await BuildCultureSnapshotAsync(resourceSets, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var flightKey = "cult:" + culture.ToLowerInvariant();
        var lazy = _cultureFlight.GetOrAdd(
            flightKey,
            _ => new Lazy<Task<List<ILocalizationResourceSet>>>(
                () => AcquireResourceSetsAsync(culture, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try {
            return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally {
            _cultureFlight.TryRemove(new KeyValuePair<string, Lazy<Task<List<ILocalizationResourceSet>>>>(flightKey, lazy));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Bypasses the shared cache by design: <see cref="IDistributedCache"/> exposes no
    /// synchronous API, and blocking on async cache I/O would risk deadlocks. Forwards
    /// to the inner store without publishing shared state.
    /// </remarks>
    public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) =>
        _innerStore.TryGetResourceSet(resourceSet, culture, out localizationResourceSet);

    /// <inheritdoc/>
    /// <remarks>
    /// Bypasses the shared cache by design (async-only distributed layer). Forwards to
    /// the inner store without publishing shared state.
    /// </remarks>
    public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) =>
        _innerStore.TryGetString(resourceSet, culture, name, out entry);

    /// <inheritdoc/>
    /// <remarks>
    /// Forwards to the inner store first: when the backend reload throws, the exception
    /// propagates and the generation is left untouched, keeping last-known-good shared
    /// state (mirroring the in-memory decorator). Only a successful backend reload bumps
    /// the shared generation; the bump itself is best-effort so a cache outage never
    /// turns a healthy reload into a failure (staleness stays TTL-bounded).
    /// </remarks>
    public async Task ReloadAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await _innerStore.ReloadAsync(cancellationToken).ConfigureAwait(false);

        try {
            await BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            // Best-effort: the backend reloaded; nodes converge via TTL at worst.
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) =>
        await _innerStore.GetSupportedCulturesAsync(cancellationToken).ConfigureAwait(false);

    #region Invalidation (IDistributedStoreCacheInvalidator)

    /// <inheritdoc/>
    public async Task InvalidateEntryAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        await _distributedCache.RemoveAsync(EntryKey(resourceSet, culture), cancellationToken).ConfigureAwait(false);
        await RemoveFromIndexAsync(culture, resourceSet, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task InvalidateCultureAsync(string culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var indexKey = IndexKey(culture);
        var sets = await ReadIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

        foreach(var resourceSet in sets) {
            cancellationToken.ThrowIfCancellationRequested();
            await _distributedCache.RemoveAsync(EntryKey(resourceSet, culture), cancellationToken).ConfigureAwait(false);
        }

        await _distributedCache.RemoveAsync(indexKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> BumpGenerationAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        var current = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);
        var next = current + 1;
        var bytes = BitConverter.GetBytes(next);

        if(!BitConverter.IsLittleEndian) {
            Array.Reverse(bytes);
        }

        await _distributedCache.SetAsync(
            GenerationKey,
            bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
            cancellationToken).ConfigureAwait(false);

        return next;
    }

    /// <inheritdoc/>
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? bytes;

        try {
            bytes = await _distributedCache.GetAsync(GenerationKey, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            return 0;
        }

        if(bytes is null || bytes.Length != sizeof(long)) {
            return 0;
        }

        if(!BitConverter.IsLittleEndian) {
            var copy = (byte[])bytes.Clone();
            Array.Reverse(copy);
            bytes = copy;
        }

        var generation = BitConverter.ToInt64(bytes, 0);
        return generation < 0 ? 0 : generation;
    }

    #endregion Invalidation (IDistributedStoreCacheInvalidator)

    #region Resource Administration forwarding

    /// <inheritdoc/>
    public bool SupportsAdministration =>
        _innerStore is IResourceAdminStore admin && admin.SupportsAdministration;

    /// <inheritdoc/>
    /// <remarks>
    /// Subscriptions forward to the inner store's hook. Subscribing on a read-only
    /// inner store is a no-op: no mutation can commit, so no audit entry can exist.
    /// </remarks>
    public event EventHandler<ResourceAdminAuditEventArgs>? Audited {
        add {
            if(_innerStore is IResourceAdminStore admin) {
                admin.Audited += value;
            }
        }
        remove {
            if(_innerStore is IResourceAdminStore admin) {
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
        await InvalidateWhenAppliedAsync(result, () => InvalidateEntryAsync(resourceSet, culture, cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateValue(value);

        var result = await InnerAdmin().UpdateEntryAsync(resourceSet, culture, name, value, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, () => InvalidateEntryAsync(resourceSet, culture, cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);

        var result = await InnerAdmin().DeleteEntryAsync(resourceSet, culture, name, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, () => InvalidateEntryAsync(resourceSet, culture, cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Renames are cross-culture atomic, so a per-key removal cannot address them: an
    /// applied rename bumps the shared generation instead.
    /// </remarks>
    public async Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(oldName);
        ResourceAdminValidation.ValidateKey(newName);

        var result = await InnerAdmin().RenameKeyAsync(resourceSet, oldName, newName, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, () => BumpGenerationAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);

        var result = await InnerAdmin().DeleteResourceSetAsync(resourceSet, culture, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, () => InvalidateEntryAsync(resourceSet, culture, cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Renames are cross-culture atomic, so per-key removal cannot address them: an
    /// applied rename bumps the shared generation instead.
    /// </remarks>
    public async Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(oldResourceSet);
        ResourceAdminValidation.ValidateResourceSet(newResourceSet);

        var result = await InnerAdmin().RenameResourceSetAsync(oldResourceSet, newResourceSet, options, cancellationToken).ConfigureAwait(false);
        await InvalidateWhenAppliedAsync(result, () => BumpGenerationAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are not cached; reads forward directly. A decorator over a store without
    /// the administration contract reports no notes (the surface does not exist there).
    /// </remarks>
    public Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.GetNoteAsync(_innerStore, resourceSet, name, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are not cached; reads forward directly. A decorator over a store without
    /// the administration contract reports no notes (the surface does not exist there).
    /// </remarks>
    public Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.GetNotesAsync(_innerStore, resourceSet, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes never enter the shared template cache, so an applied note mutation performs
    /// no cache invalidation.
    /// </remarks>
    public Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.SetNoteAsync(_innerStore, resourceSet, name, note, options, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Notes never enter the shared template cache, so an applied note mutation performs
    /// no cache invalidation.
    /// </remarks>
    public Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) =>
        ResourceAdminAccess.DeleteNoteAsync(_innerStore, resourceSet, name, options, cancellationToken);

    /// <summary>
    /// Performs the inner admin operation.
    /// </summary>
    /// <returns>The inner admin result.</returns>
    private IResourceAdminStore InnerAdmin() => ResourceAdminAccess.GetWritableAdmin(_innerStore);

    /// <summary>
    /// Invalidates when is applied.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="invalidate">The invalidate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private static async Task InvalidateWhenAppliedAsync(ResourceAdminResult result, Func<Task> invalidate, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(invalidate);

        if(!result.Applied) {
            return;
        }

        try {
            await invalidate().ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            // Best-effort: the mutation committed; nodes converge via TTL/generation at worst.
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
        _innerStore is IResourceHistoryStore history && history.SupportsHistory;

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and never touch the shared
    /// cache: the audit trail and versioned reads must be live.
    /// </remarks>
    public Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default) =>
        InnerHistory().GetCurrentVersionAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and never touch the shared cache.
    /// </remarks>
    public Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0, CancellationToken cancellationToken = default) =>
        InnerHistory().GetHistoryAsync(sinceVersion, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and never touch the shared cache.
    /// </remarks>
    public Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(string resourceSet, string culture, long version, CancellationToken cancellationToken = default) =>
        InnerHistory().GetStringsAsOfVersionAsync(resourceSet, culture, version, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// History reads forward directly to the inner store and never touch the shared cache.
    /// </remarks>
    public Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default) =>
        InnerHistory().VerifyHistoryAsync(cancellationToken);

    /// <summary>
    /// Performs the inner history operation.
    /// </summary>
    /// <returns>The inner history result.</returns>
    private IResourceHistoryStore InnerHistory() {
        if(_innerStore is IResourceHistoryStore history && history.SupportsHistory) {
            return history;
        }

        throw new StoreHistoryUnsupportedException(
            $"Store '{_innerStore.GetType().FullName}' does not support resource history and rejects history reads.");
    }
    #endregion Resource History forwarding

    /// <summary>
    /// Acquires resource set.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is acquire resource set.</returns>
    private async Task<ILocalizationResourceSet?> AcquireResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken) {
        var generation = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);
        var cached = await ReadPayloadAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

        if(cached is not null && cached.Generation == generation) {
            return CreateSnapshot(resourceSet, culture, cached.Entries);
        }

        var inner = await _innerStore.GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

        if(inner is null) {
            // Null resource sets never touch the shared cache (mirrors the in-memory decorator).
            return null;
        }

        var all = await inner.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);

        // Re-stamp after the backend round-trip: a completion begun before a concurrent
        // generation bump must not repopulate the cache (Azure refresh rule). The caller
        // is still served from backend data; only publication is skipped.
        var stamp = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);

        if(stamp == generation) {
            await WriteThroughAsync(inner.ResourceSet, inner.Culture, all, stamp, cancellationToken).ConfigureAwait(false);
        }

        return CreateSnapshot(inner.ResourceSet, inner.Culture, new Dictionary<string, LocalizedStringEntry>(all, StringComparer.Ordinal));
    }

    /// <summary>
    /// Acquires resource sets.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is acquire resource sets.</returns>
    private async Task<List<ILocalizationResourceSet>> AcquireResourceSetsAsync(string culture, CancellationToken cancellationToken) {
        var generation = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);
        var innerSets = await _innerStore.GetResourceSetsAsync(culture, cancellationToken).ConfigureAwait(false);
        var snapshots = new List<ILocalizationResourceSet>(innerSets.Count);

        foreach(var inner in innerSets) {
            cancellationToken.ThrowIfCancellationRequested();
            var all = await inner.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
            var stamp = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);

            if(stamp == generation) {
                await WriteThroughAsync(inner.ResourceSet, inner.Culture, all, stamp, cancellationToken).ConfigureAwait(false);
            }

            snapshots.Add(CreateSnapshot(inner.ResourceSet, inner.Culture, new Dictionary<string, LocalizedStringEntry>(all, StringComparer.Ordinal)));
        }

        return snapshots;
    }

    /// <summary>
    /// Creates snapshot.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The snapshot result.</returns>
    private DistributedSnapshotResourceSet CreateSnapshot(string resourceSet, string culture, Dictionary<string, LocalizedStringEntry> entries) =>
        new(this, resourceSet, culture, entries);

    /// <summary>
    /// Writes through.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="generation">The generation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task WriteThroughAsync(string resourceSet, string culture, IReadOnlyDictionary<string, LocalizedStringEntry> entries, long generation, CancellationToken cancellationToken) {
        try {
            var bytes = DistributedCachePayload.Serialize(resourceSet, culture, generation, entries);

            await _distributedCache.SetAsync(
                EntryKey(resourceSet, culture),
                bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
                cancellationToken).ConfigureAwait(false);

            await AddToIndexAsync(culture, resourceSet, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            // Best-effort publication: the caller is already served from backend data.
        }
    }

    /// <summary>
    /// Represents a cached payload.
    /// </summary>
    /// <param name="Generation">The generation.</param>
    /// <param name="Entries">The entries.</param>
    private sealed record CachedPayload(long Generation, Dictionary<string, LocalizedStringEntry> Entries);

    /// <summary>
    /// Reads payload.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is payload.</returns>
    private async Task<CachedPayload?> ReadPayloadAsync(string resourceSet, string culture, CancellationToken cancellationToken) {
        byte[]? bytes;

        try {
            bytes = await _distributedCache.GetAsync(EntryKey(resourceSet, culture), cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            return null;
        }

        if(bytes is null) {
            return null;
        }

        // Version skew or corruption yields null: ignored, never misread.
        // TTL bounds the re-parse cost of poison entries.
        var deserialized = DistributedCachePayload.Deserialize(bytes, resourceSet, culture);
        return deserialized is null ? null : new CachedPayload(deserialized.Value.Generation, deserialized.Value.Entries);
    }

    /// <summary>
    /// Adds to index.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task AddToIndexAsync(string culture, string resourceSet, CancellationToken cancellationToken) {
        try {
            var indexKey = IndexKey(culture);
            var sets = await ReadIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

            if(sets.Contains(resourceSet)) {
                return;
            }

            sets.Add(resourceSet);
            await _distributedCache.SetAsync(
                indexKey,
                DistributedCachePayload.SerializeIndex(new DistributedCultureIndex { Sets = sets }),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
                cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            // Best-effort bookkeeping; per-culture invalidation degrades to TTL/generation.
        }
    }

    /// <summary>
    /// Removes from index.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task RemoveFromIndexAsync(string culture, string resourceSet, CancellationToken cancellationToken) {
        try {
            var indexKey = IndexKey(culture);
            var sets = await ReadIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

            if(sets.Remove(resourceSet)) {
                await _distributedCache.SetAsync(
                    indexKey,
                    DistributedCachePayload.SerializeIndex(new DistributedCultureIndex { Sets = sets }),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            // Best-effort bookkeeping.
        }
    }

    /// <summary>
    /// Reads index.
    /// </summary>
    /// <param name="indexKey">The index key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is index.</returns>
    private async Task<List<string>> ReadIndexAsync(string indexKey, CancellationToken cancellationToken) {
        try {
            var bytes = await _distributedCache.GetAsync(indexKey, cancellationToken).ConfigureAwait(false);

            if(bytes is null) {
                return [];
            }

            var distinct = DistributedCachePayload.DeserializeIndex(bytes)?.Sets
                .Where(static name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return distinct ?? [];
        }
        catch(Exception ex) when(!IsCancellation(ex, cancellationToken)) {
            return [];
        }
    }

    /// <summary>
    /// Builds culture snapshot.
    /// </summary>
    /// <param name="resourceSets">The resource sets.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is culture snapshot.</returns>
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
    /// Determines whether the value is cancellation.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException || cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Node-local materialization of one shared payload. Reads serve the local entry map
    /// (missing entries included, mirroring the in-memory decorator); full snapshots
    /// exclude missing entries. Mutations forward to the live inner set and refresh the
    /// shared payload, so a write on any node converges the cluster.
    /// </summary>
    /// <param name="owner">The owning decorator (inner-store access and payload refresh).</param>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The materialized entry map.</param>
    private sealed class DistributedSnapshotResourceSet(
        DistributedLocalizationStoreDecorator owner,
        string resourceSet,
        string culture,
        Dictionary<string, LocalizedStringEntry> entries) : ILocalizationResourceSet {
#if NET9_0_OR_GREATER
        private readonly Lock _sync = new();
#else
        private readonly object _sync = new();
#endif
        private readonly Dictionary<string, LocalizedStringEntry> _entries = entries;

        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => CultureInfo.GetCultureInfo(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(name);
            cancellationToken.ThrowIfCancellationRequested();

            lock(_sync) {
                if(_entries.TryGetValue(name, out var cached)) {
                    return Task.FromResult(cached);
                }
            }

            return GetStringFromInnerAsync(name, cancellationToken);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, LocalizedStringEntry> snapshot;

            lock(_sync) {
                snapshot = _entries
                    .Where(static pair => !pair.Value.IsMissing())
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
            }

            return Task.FromResult(snapshot);
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            lock(_sync) {
                if(_entries.TryGetValue(name, out entry)) {
                    return true;
                }
            }

            if(owner._innerStore.TryGetResourceSet(resourceSet, culture, out var live) &&
               live is not null &&
               live.TryGetString(name, out entry) &&
               entry is not null) {
                lock(_sync) {
                    _entries[entry.Name] = entry;
                }

                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entry);
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.AddAsync(entry, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                _entries[entry.Name] = entry;
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entries);
            var materialized = entries.ToList();
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.AddAsync(materialized, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                foreach(var entry in materialized) {
                    _entries[entry.Name] = entry;
                }
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entry);
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                _entries[entry.Name] = entry;
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entries);
            var materialized = entries.ToList();
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.UpdateAsync(materialized, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                foreach(var entry in materialized) {
                    _entries[entry.Name] = entry;
                }
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entry);
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                _entries.Remove(entry.Name);
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(entries);
            var materialized = entries.ToList();
            var live = await ResolveLiveAsync(cancellationToken).ConfigureAwait(false);
            await live.DeleteAsync(materialized, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                foreach(var entry in materialized) {
                    _entries.Remove(entry.Name);
                }
            }

            await owner.RefreshEntryAsync(resourceSet, culture, SnapshotEntries(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets string from inner.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string from inner.</returns>
        private async Task<LocalizedStringEntry> GetStringFromInnerAsync(string name, CancellationToken cancellationToken) {
            var live = await owner._innerStore.GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

            if(live is null) {
                return LocalizedStringEntry.Missing(resourceSet, culture, name);
            }

            var entry = await live.GetStringAsync(name, cancellationToken).ConfigureAwait(false);

            lock(_sync) {
                _entries[entry.Name] = entry;
            }

            return entry;
        }

        /// <summary>
        /// Resolves live.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is live.</returns>
        private async Task<ILocalizationResourceSet> ResolveLiveAsync(CancellationToken cancellationToken) => await owner._innerStore.GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(
                    $"The underlying resource set '{resourceSet}' ('{culture}') no longer exists; the mutation cannot be forwarded.");

        /// <summary>
        /// Performs the snapshot entries operation.
        /// </summary>
        /// <returns>The snapshot entries result.</returns>
        private Dictionary<string, LocalizedStringEntry> SnapshotEntries() {
            lock(_sync) {
                return new Dictionary<string, LocalizedStringEntry>(_entries, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// Refreshes entry.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task RefreshEntryAsync(string resourceSet, string culture, Dictionary<string, LocalizedStringEntry> entries, CancellationToken cancellationToken) {
        var generation = await GetGenerationAsync(cancellationToken).ConfigureAwait(false);
        await WriteThroughAsync(resourceSet, culture, entries, generation, cancellationToken).ConfigureAwait(false);
    }
}
