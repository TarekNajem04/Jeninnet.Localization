//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// <para>An in-memory TTL cache decorator for message providers.</para>
/// <para>
/// Resolution order: decorator cache, then the inner provider. A cache hit returns
/// the stored template and raises no event; a miss acquires through the inner
/// provider under single-flight coalescing (canonical Model C lifecycle: the shared
/// acquisition is provider-owned and detached from caller tokens) and writes the
/// result back with the configured TTL. Multi-key lookups resolve cache hits
/// locally and acquire the remainder through the inner provider — as one batch
/// when it implements <see cref="IMultiKeyMessageProvider"/>, or as fanned-out
/// single-key acquisitions otherwise, joined with in-flight work.
/// </para>
/// </summary>
/// <remarks>
/// File lifecycle model: the inner provider and the option values are captured as an
/// immutable construction snapshot; the decorator never re-reads the options object.
/// <para>
/// Refresh semantics: cancels nothing that is in flight; it clears the decorator
/// cache and advances the generation so that completions begun before the refresh
/// cannot repopulate the cache. Invalidation and completion cache publication share
/// a synchronization boundary. A refresh is forwarded to the inner provider when
/// it implements <see cref="IRefreshableMessageProvider"/>.
/// </para>
/// <para>
/// The decorator owns its <see cref="TranslationAcquired"/> and
/// <see cref="AcquisitionFailed"/> events: they report decorator-level external
/// acquisitions (inner-provider calls), never cache hits. Inner-provider events
/// are not forwarded; subscribe to the inner instance directly when its own
/// reload signals are needed. A genuine caller cancellation raises neither event.
/// </para>
/// <para>
/// The decorator owns no file watcher and no timer: expiry is checked on read
/// against the clock, so it implements no disposal abstraction. It is safe to
/// hold for the process lifetime.
/// </para>
/// </remarks>
public class CachedMessageProvider : IInnerMessageProvider,
    IProviderCapabilities,
    IMultiKeyMessageProvider,
    IPreloadableMessageProvider,
    IRefreshableMessageProvider,
    IProviderEventSource {
    private static readonly CancellationToken _detachedAcquisitionToken = CancellationToken.None;

    private readonly IInnerMessageProvider _inner;
    private readonly TimeSpan _cacheExpiration;
    private readonly int _maxEntries;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<(string key, string culture), CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<(string key, string culture), Lazy<Task<string?>>> _inFlight = new();

    /// <summary>
    /// Serializes cache publication against refresh invalidation so a stale pre-refresh completion
    /// can never repopulate state that a concurrent refresh has invalidated (the refresh/invalidation rule).
    /// </summary>
    private readonly Lock _cacheLock = new();

    private int _generation;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedMessageProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="options">The cache options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    public CachedMessageProvider(IInnerMessageProvider inner, IOptions<MessageProviderCacheOptions> options)
        : this(inner, options, TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedMessageProvider"/> class with an
    /// explicit clock. Test-only: production construction always uses
    /// <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="options">The cache options.</param>
    /// <param name="timeProvider">The clock backing cache expiry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/>, <paramref name="options"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    internal CachedMessageProvider(IInnerMessageProvider inner, IOptions<MessageProviderCacheOptions> options, TimeProvider timeProvider) {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        ArgumentNullException.ThrowIfNull(timeProvider);
        _cacheExpiration = value.CacheExpiration;
        _maxEntries = value.MaxEntries;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeature feature) => feature switch {
        ProviderFeature.MultiKeys => true,
        ProviderFeature.EventsListening => true,
        ProviderFeature.Preload => true,
        ProviderFeature.Refresh => true,
        ProviderFeature.Caching => true,
        _ => false,
    };

    /// <inheritdoc />
    public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return ValueTask.FromResult<string?>(null);
        }

        var cacheKey = (key, culture.Name);
        if(TryGetValidCache(cacheKey, out var cachedTemplate)) {
            return ValueTask.FromResult(cachedTemplate);
        }

        // Model C: the shared acquisition is provider-owned and detached from caller tokens.
        var entry = _inFlight.GetOrAdd(cacheKey, CreateDetachedInFlightEntry);
        return new ValueTask<string?>(AwaitWithCallerTokenAsync(entry.Value, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
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

            return ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(result);
        }

        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            if(TryGetValidCache((key, culture.Name), out var cached)) {
                result[key] = cached;
            } else {
                remaining.Add(key);
            }
        }

        return new ValueTask<IReadOnlyDictionary<string, string?>>(ResolveRemainingAsync(remaining, culture.Name, result, cancellationToken));
    }

    /// <inheritdoc />
    public ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return ValueTask.CompletedTask;
        }

        var distinct = keys.Distinct().ToArray();
        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            if(!TryGetValidCache((key, culture.Name), out _)) {
                remaining.Add(key);
            }
        }

        return new ValueTask(AcquireKeysAsync([.. remaining], culture.Name, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Refresh semantics: cancels nothing that is in flight; it invalidates the decorator cache
    /// and advances the generation so that completions begun before the refresh cannot repopulate the
    /// cache. Active in-flight entries remain provider-owned and live until terminal completion.
    /// The refresh is forwarded to the inner provider when it is refreshable.
    /// </remarks>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock(_cacheLock) {
            _cache.Clear();
            Interlocked.Increment(ref _generation);
        }

        if(_inner is IRefreshableMessageProvider refreshable) {
            await refreshable.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves remaining.
    /// </summary>
    /// <param name="remaining">The remaining.</param>
    /// <param name="targetCulture">The target culture.</param>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is remaining.</returns>
    private async Task<IReadOnlyDictionary<string, string?>> ResolveRemainingAsync(
        List<string> remaining,
        string targetCulture,
        Dictionary<string, string?> result,
        CancellationToken cancellationToken) {
        if(remaining.Count > 0) {
            var acquired = await AcquireKeysCoalescedAsync([.. remaining], targetCulture, cancellationToken);
            foreach(var pair in acquired) {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
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
    private async Task<IReadOnlyDictionary<string, string?>> AcquireKeysCoalescedAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken) {
        var joined = new Dictionary<string, Task<string?>>(keys.Length);
        var pending = new List<(string Key, TaskCompletionSource<string?> Source, (string key, string culture) CacheKey, Lazy<Task<string?>> Entry)>();
        var toAcquire = new List<string>(keys.Length);

        foreach(var key in keys) {
            var cacheKey = (key, targetCulture);
            var source = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownEntry = new Lazy<Task<string?>>(() => source.Task, LazyThreadSafetyMode.ExecutionAndPublication);
            var entry = _inFlight.GetOrAdd(cacheKey, ownEntry);
            if(ReferenceEquals(entry, ownEntry)) {
                pending.Add((key, source, cacheKey, ownEntry));
                toAcquire.Add(key);
            } else {
                joined[key] = entry.Value;
            }
        }

        var results = new Dictionary<string, string?>(keys.Length);
        var batchTask = toAcquire.Count > 0
            ? AcquireKeysAsync([.. toAcquire], targetCulture, cancellationToken)
            : null;

        try {
            foreach(var pair in joined) {
                results[pair.Key] = await pair.Value.WaitAsync(cancellationToken);
            }

            if(batchTask is not null) {
                var batch = await batchTask;
                foreach(var pair in batch) {
                    results[pair.Key] = pair.Value;
                }

                foreach(var (key, source, _, _) in pending) {
                    source.TrySetResult(batch[key]);
                }
            }

            return results;
        }
        catch(Exception ex) {
            foreach(var (_, source, _, _) in pending) {
                source.TrySetException(ex);
            }

            throw;
        }
        finally {
            foreach(var (_, _, cacheKey, entry) in pending) {
                _inFlight.TryRemove(new KeyValuePair<(string key, string culture), Lazy<Task<string?>>>(cacheKey, entry));
            }
        }
    }

    /// <summary>
    /// Creates the provider-owned detached in-flight entry for a canonical single-key lookup.
    /// </summary>
    /// <param name="cacheKey">The cache key representing the key and culture for the acquisition.</param>
    /// <returns>The detached in flight entry result.</returns>
    private Lazy<Task<string?>> CreateDetachedInFlightEntry((string key, string culture) cacheKey) {
        Lazy<Task<string?>> entry = null!;
        entry = new Lazy<Task<string?>>(
            () => {
                var task = InFlightSingleAsync(cacheKey, _detachedAcquisitionToken);
                _ = task.ContinueWith(
                    _ => RemoveInFlightEntry(cacheKey, entry),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return task;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
        return entry;
    }

    /// <summary>
    /// Removes in flight entry.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="entry">The entry.</param>
    private void RemoveInFlightEntry((string key, string culture) cacheKey, Lazy<Task<string?>> entry)
        => _inFlight.TryRemove(new KeyValuePair<(string key, string culture), Lazy<Task<string?>>>(cacheKey, entry));

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
    /// Performs the await with caller token operation.
    /// </summary>
    /// <param name="inFlight">The in flight.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is await with caller token.</returns>
    private static async Task<string?> AwaitWithCallerTokenAsync(Task<string?> inFlight, CancellationToken cancellationToken)
        => await inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);

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

            CacheResolvedKeys(result, targetCulture, generation);
            TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(result, culture));
            return result;
        }
        catch(Exception ex) {
            if(!cancellationToken.IsCancellationRequested) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(CultureInfo.GetCultureInfo(targetCulture), ex));
            }

            throw;
        }
    }

    /// <summary>
    /// Publishes the resolved templates to the TTL cache when the generation observed at
    /// acquisition start is still current.
    /// </summary>
    /// <param name="result">The resolved templates.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="generation">The cache generation observed at acquisition start.</param>
    private void CacheResolvedKeys(Dictionary<string, string?> result, string targetCulture, int generation) {
        lock(_cacheLock) {
            if(Volatile.Read(ref _generation) != generation) {
                return;
            }

            var expiresAt = _timeProvider.GetUtcNow().Add(_cacheExpiration);
            foreach(var pair in result) {
                AddOrReplaceCacheEntry((pair.Key, targetCulture), new CacheEntry(pair.Value, expiresAt));
            }
        }
    }

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

    /// <summary>
    /// Attempts to get valid cache.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="template">The message template.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private bool TryGetValidCache((string key, string culture) cacheKey, out string? template) {
        if(_cache.TryGetValue(cacheKey, out var entry) && entry.ExpiresAt > _timeProvider.GetUtcNow()) {
            template = entry.Template;
            return true;
        }

        _cache.TryRemove(cacheKey, out _);
        template = null;
        return false;
    }

    /// <summary>
    /// Represents a cache entry.
    /// </summary>
    /// <param name="Template">The template.</param>
    /// <param name="ExpiresAt">The expires at.</param>
    private readonly record struct CacheEntry(string? Template, DateTimeOffset ExpiresAt);
}
