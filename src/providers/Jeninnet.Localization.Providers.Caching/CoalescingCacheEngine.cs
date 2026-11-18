//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// Shared single-flight caching pipeline for the caching decorators: cache read-through,
/// coalesced batch acquisition over the inner provider, write-back guarded by a refresh
/// generation, and decorator-owned acquisition events. The cache backend
/// (<see cref="ICacheStore"/>) is the only per-decorator seam.
/// </summary>
/// <remarks>
/// Canonical single-key acquisitions follow the Model C lifecycle: the shared acquisition is
/// provider-owned and detached from caller tokens, each caller awaits with its own token, and
/// an in-flight entry lives from installation until terminal completion, so one caller
/// cancelling never cancels the shared work nor affects other callers.
/// </remarks>
internal sealed class CoalescingCacheEngine {
    private readonly IInnerMessageProvider _inner;
    private readonly ICacheStore _store;

    private readonly ConcurrentDictionary<(string key, string culture), Lazy<Task<string?>>> _inFlight = new();

    /// <summary>
    /// Serializes cache publication against refresh invalidation so a stale pre-refresh completion
    /// can never repopulate state that a concurrent refresh has invalidated (the refresh/invalidation rule).
    /// </summary>
    private readonly Lock _cacheLock = new();

    private int _generation;

    private readonly Action<ProviderAcquisitionEventArgs> _onAcquired;
    private readonly Action<ProviderAcquisitionFailedEventArgs> _onFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoalescingCacheEngine"/> class.
    /// </summary>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="store">The cache backend.</param>
    /// <param name="onAcquired">Invoked for every externally acquired batch (the decorator re-raises its public event).</param>
    /// <param name="onFailed">Invoked for every terminal acquisition failure (the decorator re-raises its public event).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/>, <paramref name="store"/>, <paramref name="onAcquired"/>, or <paramref name="onFailed"/> is null.</exception>
    internal CoalescingCacheEngine(
        IInnerMessageProvider inner,
        ICacheStore store,
        Action<ProviderAcquisitionEventArgs> onAcquired,
        Action<ProviderAcquisitionFailedEventArgs> onFailed) {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _onAcquired = onAcquired ?? throw new ArgumentNullException(nameof(onAcquired));
        _onFailed = onFailed ?? throw new ArgumentNullException(nameof(onFailed));
    }

    /// <inheritdoc cref="IMessageProvider.GetMessageTemplateAsync" />
    internal async ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return null;
        }

        var (hit, cachedTemplate) = await _store.TryGetAsync(key, culture.Name, Volatile.Read(ref _generation), cancellationToken).ConfigureAwait(false);
        if(hit) {
            return cachedTemplate;
        }

        // Model C: the shared acquisition is provider-owned and detached from caller tokens.
        var cacheKey = (key, culture.Name);
        var entry = _inFlight.GetOrAdd(cacheKey, key => SingleFlight.CreateDetachedEntry(key, _inFlight, InFlightSingleAsync));
        return await SingleFlight.AwaitWithCallerTokenAsync(entry.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IMultiKeyMessageProvider.GetMessageTemplatesAsync" />
    internal async ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);

        var distinct = keys.Distinct().ToArray();
        var result = new Dictionary<string, string?>(distinct.Length);

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            foreach(var key in distinct) {
                result[key] = null;
            }

            return result;
        }

        var generation = Volatile.Read(ref _generation);
        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            var (hit, cached) = await _store.TryGetAsync(key, culture.Name, generation, cancellationToken).ConfigureAwait(false);
            if(hit) {
                result[key] = cached;
            } else {
                remaining.Add(key);
            }
        }

        if(remaining.Count > 0) {
            var acquired = await AcquireKeysCoalescedAsync([.. remaining], culture.Name, cancellationToken).ConfigureAwait(false);
            foreach(var pair in acquired) {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <inheritdoc cref="IPreloadableMessageProvider.PreloadAsync" />
    internal async ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return;
        }

        var generation = Volatile.Read(ref _generation);
        var distinct = keys.Distinct().ToArray();
        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            var (hit, _) = await _store.TryGetAsync(key, culture.Name, generation, cancellationToken).ConfigureAwait(false);
            if(!hit) {
                remaining.Add(key);
            }
        }

        await AcquireKeysAsync([.. remaining], culture.Name, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IRefreshableMessageProvider.RefreshAsync" />
    /// <remarks>
    /// Refresh semantics: cancels nothing that is in flight; it invalidates the decorator cache
    /// and advances the generation so that completions begun before the refresh cannot repopulate the
    /// cache. Active in-flight entries remain provider-owned and live until terminal completion.
    /// The refresh is forwarded to the inner provider when it is refreshable.
    /// </remarks>
    internal async ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock(_cacheLock) {
            _store.Invalidate();
            Interlocked.Increment(ref _generation);
        }

        if(_inner is IRefreshableMessageProvider refreshable) {
            await refreshable.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves cache-missed keys through the shared per-(key, culture) single-flight mechanism.
    /// Keys already in flight — whether from the canonical lookup or from another batch — are
    /// joined (awaited) instead of re-acquired; keys this call claims are acquired as one external
    /// batch. Whole-batch coalescing is intentionally not performed. Joining callers observe the
    /// owning acquisition's event; the event payload covers only externally acquired keys.
    /// </summary>
    /// <param name="keys">The cache-missed keys this call is responsible for.</param>
    /// <param name="targetCulture">The culture to acquire the keys for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is translate keys coalesced.</returns>
    private Task<IReadOnlyDictionary<string, string?>> AcquireKeysCoalescedAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken)
        => SingleFlight.CoalesceAsync(keys, targetCulture, _inFlight, AcquireKeysAsync, cancellationToken);


    /// <summary>
    /// Performs the in flight single operation.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="acquisitionToken">The acquisition token.</param>
    /// <returns>A task whose result is in flight single.</returns>
    private async Task<string?> InFlightSingleAsync((string key, string culture) cacheKey, CancellationToken acquisitionToken) {
        var dict = await AcquireKeysAsync([cacheKey.key], cacheKey.culture, acquisitionToken);
        return dict[cacheKey.key];
    }

    /// <summary>
    /// Acquires the keys through the inner provider and publishes the results to the cache
    /// when the generation observed at acquisition start is still current.
    /// </summary>
    /// <param name="keys">The keys to acquire.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved templates for every requested key.</returns>
    private async Task<IReadOnlyDictionary<string, string?>> AcquireKeysAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken) {
        if(keys.Length == 0) {
            return new Dictionary<string, string?>(0);
        }

        var generation = Volatile.Read(ref _generation);
        try {
            var culture = CultureInfo.GetCultureInfo(targetCulture);
            IReadOnlyDictionary<string, string?> acquired;
            if(_inner is IMultiKeyMessageProvider multi) {
                acquired = await multi.GetMessageTemplatesAsync(keys, culture, cancellationToken);
            } else {
                var fanOut = new Dictionary<string, string?>(keys.Length);
                foreach(var key in keys) {
                    fanOut[key] = await _inner.GetMessageTemplateAsync(key, culture, cancellationToken);
                }

                acquired = fanOut;
            }

            var result = new Dictionary<string, string?>(acquired.Count);
            foreach(var pair in acquired) {
                result[pair.Key] = pair.Value;
            }

            await _store.WriteAsync(result, targetCulture, generation, () => Volatile.Read(ref _generation), _cacheLock, cancellationToken).ConfigureAwait(false);
            _onAcquired(new ProviderAcquisitionEventArgs(result, culture));
            return result;
        }
        catch(Exception ex) {
            if(!cancellationToken.IsCancellationRequested) {
                _onFailed(new ProviderAcquisitionFailedEventArgs(CultureInfo.GetCultureInfo(targetCulture), ex));
            }

            throw;
        }
    }
}
