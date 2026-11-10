//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// <para>A distributed-cache decorator for message providers.</para>
/// <para>
/// Resolution order: distributed cache, then the inner provider. A cache hit returns
/// the stored template and raises no event; a miss acquires through the inner
/// provider under single-flight coalescing (canonical Model C lifecycle) and writes
/// the result back with the configured TTL. Multi-key lookups resolve cache hits
/// locally and acquire the remainder through the inner provider — as one batch
/// when it implements <see cref="IMultiKeyMessageProvider"/>, or as fanned-out
/// single-key acquisitions otherwise, joined with in-flight work.
/// </para>
/// </summary>
/// <remarks>
/// File lifecycle model: the inner provider, the distributed cache, and the option values
/// are captured as an immutable construction snapshot; the decorator never re-reads the
/// options object.
/// <para>
/// Cache identity: entries are keyed by decorator instance, cache generation, culture,
/// and a SHA-256 hash of the key, so keys of any shape stay within store key limits
/// and two decorators over the same inner provider never share entries. Refresh
/// semantics: cancels nothing that is in flight; it advances the generation so that
/// earlier entries become unreachable (they expire naturally) and completions begun
/// before the refresh cannot repopulate the cache. The refresh is forwarded to the
/// inner provider when it implements <see cref="IRefreshableMessageProvider"/>.
/// </para>
/// <para>
/// Values are stored as <c>0x01 + UTF-8 bytes</c>; a <see langword="null"/> template
/// is stored as a single <c>0x00</c> byte so cached misses stay distinguishable from
/// absent entries. TTL enforcement belongs to the distributed store.
/// </para>
/// <para>
/// The decorator owns its <see cref="TranslationAcquired"/> and
/// <see cref="AcquisitionFailed"/> events: they report decorator-level external
/// acquisitions (inner-provider calls), never cache hits. Inner-provider events
/// are not forwarded; subscribe to the inner instance directly when its own
/// reload signals are needed. A genuine caller cancellation raises neither event.
/// </para>
/// <para>
/// The decorator owns no file watcher and no timer, so it implements no disposal
/// abstraction. It is safe to hold for the process lifetime.
/// </para>
/// </remarks>
public class DistributedCachedMessageProvider : IInnerMessageProvider,
    IProviderCapabilities,
    IMultiKeyMessageProvider,
    IPreloadableMessageProvider,
    IRefreshableMessageProvider,
    IProviderEventSource {
    private static readonly CancellationToken _detachedAcquisitionToken = CancellationToken.None;

    private static readonly byte[] _nullMarker = [0x00];

    private readonly string _identity = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    private readonly IInnerMessageProvider _inner;
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _cacheExpiration;

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
    /// Initializes a new instance of the <see cref="DistributedCachedMessageProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="cache">The distributed cache backing the decorator.</param>
    /// <param name="options">The cache options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/>, <paramref name="cache"/>, or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="Options.MessageProviderCacheOptions"/> are invalid.</exception>
    public DistributedCachedMessageProvider(IInnerMessageProvider inner, IDistributedCache cache, IOptions<MessageProviderCacheOptions> options) {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _cacheExpiration = value.CacheExpiration;
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

        return new ValueTask<string?>(GetOrAcquireAsync(key, culture.Name, cancellationToken));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
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

        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            var (hit, cached) = await TryGetValidCacheAsync(key, culture.Name, cancellationToken);
            if(hit) {
                result[key] = cached;
            } else {
                remaining.Add(key);
            }
        }

        if(remaining.Count > 0) {
            var acquired = await AcquireKeysCoalescedAsync([.. remaining], culture.Name, cancellationToken);
            foreach(var pair in acquired) {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return;
        }

        var distinct = keys.Distinct().ToArray();
        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            var (hit, _) = await TryGetValidCacheAsync(key, culture.Name, cancellationToken);
            if(!hit) {
                remaining.Add(key);
            }
        }

        await AcquireKeysAsync([.. remaining], culture.Name, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Refresh semantics: cancels nothing that is in flight; it advances the generation so that
    /// earlier entries become unreachable (they expire naturally) and completions begun before the
    /// refresh cannot repopulate the cache. Active in-flight entries remain provider-owned and live
    /// until terminal completion. The refresh is forwarded to the inner provider when it is
    /// refreshable.
    /// </remarks>
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock(_cacheLock) {
            Interlocked.Increment(ref _generation);
        }

        if(_inner is IRefreshableMessageProvider refreshable) {
            await refreshable.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves one key through the distributed cache or a shared acquisition.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolved template.</returns>
    private async Task<string?> GetOrAcquireAsync(string key, string targetCulture, CancellationToken cancellationToken) {
        var (hit, cached) = await TryGetValidCacheAsync(key, targetCulture, cancellationToken);
        if(hit) {
            return cached;
        }

        // Model C: the shared acquisition is provider-owned and detached from caller tokens.
        var cacheKey = (key, targetCulture);
        var entry = _inFlight.GetOrAdd(cacheKey, CreateDetachedInFlightEntry);
        return await AwaitWithCallerTokenAsync(entry.Value, cancellationToken);
    }

    /// <summary>
    /// Resolves cache-missed keys through the shared per-(key, culture) single-flight mechanism.
    /// Keys already in flight are joined (awaited) instead of re-acquired; keys this call claims
    /// are acquired as one external batch. The event payload covers only externally acquired keys.
    /// </summary>
    /// <param name="keys">The cache-missed keys this call is responsible for.</param>
    /// <param name="targetCulture">The culture to acquire the keys for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The externally acquired subset.</returns>
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
    /// Acquires the keys through the inner provider and publishes the results to the distributed
    /// cache when the generation observed at acquisition start is still current.
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

            await CacheResolvedKeysAsync(result, targetCulture, generation, cancellationToken);
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
    /// Publishes the resolved templates to the distributed cache when the generation observed at
    /// acquisition start is still current.
    /// </summary>
    /// <param name="result">The resolved templates.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="generation">The cache generation observed at acquisition start.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task CacheResolvedKeysAsync(Dictionary<string, string?> result, string targetCulture, int generation, CancellationToken cancellationToken) {
        string[] storeKeys;
        byte[][] payloads;
        lock(_cacheLock) {
            if(Volatile.Read(ref _generation) != generation) {
                return;
            }

            storeKeys = new string[result.Count];
            payloads = new byte[result.Count][];
            var index = 0;
            foreach(var pair in result) {
                storeKeys[index] = StoreKey(pair.Key, targetCulture, generation);
                payloads[index] = EncodeTemplate(pair.Value);
                index++;
            }
        }

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheExpiration };
        for(var index = 0; index < storeKeys.Length; index++) {
            await _cache.SetAsync(storeKeys[index], payloads[index], options, cancellationToken);
        }
    }

    /// <summary>
    /// Attempts to get a cached template from the distributed cache.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The hit flag and the template (<see langword="null"/> on miss).</returns>
    private async Task<(bool Hit, string? Template)> TryGetValidCacheAsync(string key, string targetCulture, CancellationToken cancellationToken) {
        var payload = await _cache.GetAsync(StoreKey(key, targetCulture, Volatile.Read(ref _generation)), cancellationToken);
        if(payload is null) {
            return (false, null);
        }

        return (true, DecodeTemplate(payload));
    }

    /// <summary>
    /// Builds the distributed store key: decorator identity, generation, culture, and a SHA-256
    /// hash of the message key, so keys of any shape stay within store key limits.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="generation">The cache generation.</param>
    /// <returns>The store key.</returns>
    private string StoreKey(string key, string targetCulture, int generation) {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(targetCulture + "\n" + key)));
        return $"jloc-mpc:{_identity}:{generation}:{hash}";
    }

    /// <summary>
    /// Encodes a template for the distributed store: a null template becomes a single
    /// <c>0x00</c> byte so cached misses stay distinguishable from absent entries.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The payload.</returns>
    private static byte[] EncodeTemplate(string? template) {
        if(template is null) {
            return [.. _nullMarker];
        }

        var bytes = Encoding.UTF8.GetBytes(template);
        var payload = new byte[bytes.Length + 1];
        payload[0] = 0x01;
        bytes.CopyTo(payload, 1);
        return payload;
    }

    /// <summary>
    /// Decodes a distributed store payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The template.</returns>
    private static string? DecodeTemplate(byte[] payload) {
        if(payload.Length == 0 || payload[0] == 0x00) {
            return null;
        }

        return Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
    }
}
