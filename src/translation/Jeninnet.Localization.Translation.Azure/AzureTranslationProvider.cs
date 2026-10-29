//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Azure Translator message Provider. Participates through the canonical provider contract
/// (<see cref="IInnerMessageProvider"/>/<see cref="IMessageProvider"/>) and implements the
/// Translation-02 capability model (<see cref="IProviderCapabilities"/>).
/// </summary>
/// <remarks>
/// The Provider's source template is the message key itself; the value is obtained by translating
/// that key into the requested target culture via the Azure Translator v3 REST API (Translation-02).
/// All acquisition complexity — HTTP transport, batching, caching, single-flight coalescing, preload,
/// refresh, and acquisition events — is Provider-owned and does not leak into the canonical contract.
/// Canonical single-key acquisitions follow the  Model C lifecycle: they are provider-owned and
/// detached from caller tokens, each caller awaits with its own token, and an in-flight entry lives
/// from installation until terminal completion, so one caller cancelling never cancels the shared work.
/// Multi-key batch and preload acquisitions are instead scoped to the invoking caller's token: their
/// shared per-key entries observe that caller's cancellation and fault when it cancels.
/// <para>
/// The resolution order for a cache-missed key is: translation memory (exact match) →
/// glossary-protected machine translation → TTL (Time To Live) cache write-back. A translation-memory hit
/// short-circuits the paid external call; a glossary hit replaces the matched source terms with
/// opaque placeholder tokens before the text is sent to the service and restores the forced targets
/// in the translated result, so a forced term survives a machine-translation paraphrase. Both layers
/// are configured through <see cref="AzureTranslationOptions"/> and snapshotted at construction
/// (the glossary terms) or captured at construction (the store reference). An acquisition served
/// fully by the translation memory raises no event, and the acquisition event payload covers only
/// externally acquired keys.
/// </para>
/// <para>
/// Every chunk HTTP exchange runs through the provider-owned resilience pipeline (,
/// hand-rolled BCL-only): <c>Timeout → Retry → Breaker → RateLimit → HTTP</c>. One total
/// per-chunk budget bounds every attempt, backoff, and rate-limiter wait and always surfaces as
/// cancellation; only idempotent-safe transient faults are retried; the breaker fails fast with
/// <see cref="BrokenCircuitException"/> while open; the token bucket paces each attempt.
/// Resilience state is per Provider instance and shared by every lookup path; rejections and
/// terminal failures raise the existing acquisition events, and a genuine caller cancellation
/// raises neither event.
/// </para>
/// </remarks>
public class AzureTranslationProvider : IInnerMessageProvider,
    IProviderCapabilities,
    IMultiKeyMessageProvider,
    IPreloadableMessageProvider,
    IRefreshableMessageProvider,
    IProviderEventSource {
    /// <summary>
    /// Maximum keys per external translation request. Batch administration chunks to the same
    /// size so dry-run estimates and real runs group keys identically.
    /// </summary>
    internal const int MAX_CHUNK_SIZE = 100;

    /// <summary>
    /// Maximum number of entries retained in the provider-owned TTL cache. Bounded to prevent
    /// unbounded memory growth under high key cardinality.
    /// </summary>
    private const int MAX_CACHE_ENTRIES = 4096;

    /// <summary>
    /// The placeholder token family that protects glossary-forced terms from machine translation:
    /// the term is replaced by <c>[[gN]]</c> before the text is sent, and the token is replaced by
    /// the forced target in the translated result. The family is provider-internal and only tokens
    /// absent from the source text are generated.
    /// </summary>
    private const string GLOSSARY_TOKEN_PREFIX = "[[g";

    private const string GLOSSARY_TOKEN_SUFFIX = "]]";

    private static readonly CancellationToken _detachedAcquisitionToken = CancellationToken.None;

    private readonly HttpClient _httpClient;
    private readonly bool _cachingEnabled;
    private readonly TimeSpan _cacheExpiration;
    private readonly string _fromLanguage;
    private readonly string _apiKey;
    private readonly string _region;
    private readonly string _endpoint;

    /// <summary>
    /// The construction snapshot of the configured glossary terms, grouped by target culture name.
    /// Later registration on the caller-owned <see cref="MessageGlossary"/> does not change the
    /// Provider's behavior.
    /// </summary>
    private readonly Dictionary<string, GlossaryTerm[]> _glossaryTermsByCulture;

    private readonly ITranslationMemory? _translationMemory;

    private readonly ResiliencePipeline _resilience;

    private readonly ConcurrentDictionary<(string key, string culture), CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<(string key, string culture), Lazy<Task<string?>>> _inFlight = new();

    /// <summary>
    /// Serializes cache publication against refresh invalidation so a stale pre-refresh completion
    /// can never repopulate state that a concurrent refresh has invalidated (the refresh/invalidation rule).
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly Lock _cacheLock = new();
#else
    private readonly object _cacheLock = new();
#endif

    private int _generation;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTranslationProvider"/> class.
    /// </summary>
    /// <param name="options">The Azure Translator options.</param>
    /// <param name="httpClient">The HTTP client used to call the Azure Translator API.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="AzureTranslationOptions"/> are invalid.</exception>
    public AzureTranslationProvider(IOptions<AzureTranslationOptions> options, HttpClient httpClient)
        : this(options, httpClient, TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTranslationProvider"/> class with an
    /// explicit clock. Test-only: production construction always uses
    /// <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="options">The Azure Translator options.</param>
    /// <param name="httpClient">The HTTP client used to call the Azure Translator API.</param>
    /// <param name="timeProvider">The clock backing resilience timeouts, backoffs, breaker timing, and token refills.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>, <paramref name="httpClient"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="AzureTranslationOptions"/> are invalid.</exception>
    internal AzureTranslationProvider(IOptions<AzureTranslationOptions> options, HttpClient httpClient, TimeProvider timeProvider) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(timeProvider);
        _cachingEnabled = value.CachingEnabled;
        _cacheExpiration = value.CacheExpiration;
        _fromLanguage = value.FromLanguage;
        _apiKey = value.ApiKey;
        _region = value.Region;
        _endpoint = value.Endpoint.TrimEnd('/');
        _glossaryTermsByCulture = SnapshotGlossary(value.Glossary);
        _translationMemory = value.TranslationMemory;
        _resilience = new ResiliencePipeline(
            new ResilienceSettings(
                value.RequestTimeout,
                value.RetryMaxAttempts,
                value.RetryBaseDelay,
                value.RetryMaxDelay,
                value.BreakerFailureThreshold,
                value.BreakerOpenDuration,
                value.RateLimitRequestsPerSecond,
                value.RateLimitBurstSize),
            timeProvider);
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeature feature) => feature switch {
        ProviderFeature.MultiKeys => true,
        ProviderFeature.EventsListening => true,
        ProviderFeature.Preload => _cachingEnabled,
        ProviderFeature.Refresh => true,
        ProviderFeature.Caching => _cachingEnabled,
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

        if(_cachingEnabled) {
            if(TryGetValidCache(cacheKey, out var cachedTemplate)) {
                RecordCacheHit(culture.Name);
                return ValueTask.FromResult(cachedTemplate);
            }

            RecordCacheMiss(culture.Name);
        }

        //  Model C: the shared acquisition is provider-owned and detached from caller tokens.
        // Every caller (including the creator) awaits with its own token, so one caller cancelling
        // never cancels the shared work nor affects other callers. The entry is removed exactly when
        // the shared acquisition reaches terminal completion.
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
            var cacheKey = (key, culture.Name);
            if(_cachingEnabled && TryGetValidCache(cacheKey, out var cached)) {
                result[key] = cached;
                RecordCacheHit(culture.Name);
            } else {
                remaining.Add(key);
                if(_cachingEnabled) {
                    RecordCacheMiss(culture.Name);
                }
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

        if(!_cachingEnabled || culture.Equals(CultureInfo.InvariantCulture)) {
            return ValueTask.CompletedTask;
        }

        var distinct = keys.Distinct().ToArray();
        var remaining = new List<string>(distinct.Length);
        foreach(var key in distinct) {
            if(!TryGetValidCache((key, culture.Name), out _)) {
                remaining.Add(key);
                RecordCacheMiss(culture.Name);
            } else {
                RecordCacheHit(culture.Name);
            }
        }

        return new ValueTask(TranslateKeysAsync([.. remaining], culture.Name, cancellationToken));
    }

    /// <inheritdoc />
    /// <remarks>
    ///  Refresh semantics: cancels nothing that is in flight; it invalidates the provider cache
    /// and advances the generation so that completions begun before the refresh cannot repopulate the
    /// cache. Invalidation and completion cache publication share a synchronization boundary, so a
    /// stale pre-refresh completion can never repopulate invalidated cache state. Active in-flight
    /// entries remain provider-owned and live until terminal completion, so a refresh can never create
    /// a second concurrent acquisition for the same key/culture.
    /// </remarks>
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        lock(_cacheLock) {
            _cache.Clear();
            Interlocked.Increment(ref _generation);
        }

        return ValueTask.CompletedTask;
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
            var acquired = await TranslateKeysCoalescedAsync([.. remaining], targetCulture, cancellationToken);
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
    /// <param name="targetCulture">The culture to translate the keys into.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is translate keys coalesced.</returns>
    private async Task<IReadOnlyDictionary<string, string?>> TranslateKeysCoalescedAsync(
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
            ? TranslateKeysAsync([.. toAcquire], targetCulture, cancellationToken)
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
    /// The acquisition is detached from caller tokens and the entry is removed
    /// exactly when the acquisition reaches terminal completion (success, failure, or cancellation).
    /// </summary>
    /// <param name="cacheKey">The cache key representing the key and culture for the translation.</param>
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
        var dict = await TranslateKeysAsync([cacheKey.key], cacheKey.culture, acquisitionToken);
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
    /// Performs the translate keys operation.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="targetCulture">The target culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is translate keys.</returns>
    private async Task<IReadOnlyDictionary<string, string?>> TranslateKeysAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken) {
        var (result, externallyAcquired) = await TranslateKeysCoreAsync(keys, targetCulture, cancellationToken);
        if(externallyAcquired is { Count: > 0 }) {
            TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(externallyAcquired, CultureInfo.GetCultureInfo(targetCulture)));
        }

        return result;
    }

    /// <summary>
    /// Resolves the requested keys in the documented lookup order: translation memory
    /// (exact match) → glossary-protected machine translation → TTL cache write-back. Externally
    /// acquired pairs are reported separately so an acquisition served fully by the translation
    /// memory — like one served fully from cache — raises no event. Degenerate (empty or whitespace)
    /// keys are never sent to the store; they follow the canonical acquisition path like any other
    /// unresolved key.
    /// </summary>
    /// <param name="keys">The keys to resolve.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The resolved templates for every requested key, plus the externally acquired subset
    /// (the same instance as the result when no key was served by the translation memory, and
    /// <see langword="null"/> when no external acquisition took place).
    /// </returns>
    private async Task<(IReadOnlyDictionary<string, string?> Result, IReadOnlyDictionary<string, string?>? ExternallyAcquired)> TranslateKeysCoreAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken) {
        var generation = Volatile.Read(ref _generation);
        try {
            var result = new Dictionary<string, string?>(keys.Length);
            var remaining = ResolveTranslationMemoryHits(keys, targetCulture, result);
            var externallyAcquired = await TranslateRemainingKeysAsync(remaining, keys.Length, targetCulture, result, cancellationToken);
            CacheResolvedKeys(result, targetCulture, generation);
            return (result, externallyAcquired);
        }
        catch(Exception ex) {
            if(!cancellationToken.IsCancellationRequested) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(CultureInfo.GetCultureInfo(targetCulture), ex));
            }

            throw;
        }
    }

    /// <summary>
    /// Resolves the translation-memory hits for the requested keys and returns the keys that still
    /// require external acquisition.
    /// </summary>
    /// <param name="keys">The requested keys.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="result">The resolved templates sink; translation-memory hits are added here.</param>
    /// <returns>The keys that were not served by the translation memory.</returns>
    private List<string> ResolveTranslationMemoryHits(string[] keys, string targetCulture, Dictionary<string, string?> result) {
        var remaining = new List<string>(keys.Length);
        if(_translationMemory is null) {
            remaining.AddRange(keys);
            return remaining;
        }

        var culture = CultureInfo.GetCultureInfo(targetCulture);
        foreach(var key in keys) {
            if(!string.IsNullOrWhiteSpace(key) && _translationMemory.TryLookup(key, culture, out var remembered)) {
                result[key] = remembered;
            } else {
                remaining.Add(key);
            }
        }

        return remaining;
    }

    /// <summary>
    /// Acquires the remaining keys through chunked machine translation and records each call.
    /// </summary>
    /// <param name="remaining">The keys still requiring external acquisition.</param>
    /// <param name="requestedCount">The total number of requested keys.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="result">The resolved templates sink.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The externally acquired subset (the same instance as <paramref name="result"/> when all
    /// keys were externally acquired), or <see langword="null"/> when nothing was acquired.
    /// </returns>
    private async Task<Dictionary<string, string?>?> TranslateRemainingKeysAsync(
        List<string> remaining,
        int requestedCount,
        string targetCulture,
        Dictionary<string, string?> result,
        CancellationToken cancellationToken) {
        if(remaining.Count == 0) {
            return null;
        }

        var externallyAcquired = remaining.Count == requestedCount ? result : new Dictionary<string, string?>(remaining.Count);
        for(var offset = 0; offset < remaining.Count; offset += MAX_CHUNK_SIZE) {
            var chunk = remaining.Skip(offset).Take(MAX_CHUNK_SIZE).ToArray();
            var measureDuration = LocalizationDiagnostics.TranslationDuration.Enabled;
            var startTimestamp = measureDuration ? Stopwatch.GetTimestamp() : 0L;
            var translated = await TranslateChunkAsync(chunk, targetCulture, cancellationToken);
            RecordTranslationCall(targetCulture, startTimestamp);
            foreach(var pair in translated) {
                result[pair.Key] = pair.Value;
            }

            if(!ReferenceEquals(externallyAcquired, result)) {
                foreach(var pair in translated) {
                    externallyAcquired[pair.Key] = pair.Value;
                }
            }
        }

        return externallyAcquired;
    }

    /// <summary>
    /// Publishes the resolved templates to the TTL cache when caching is enabled and the generation
    /// observed at acquisition start is still current.
    /// </summary>
    /// <param name="result">The resolved templates.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="generation">The cache generation observed at acquisition start.</param>
    private void CacheResolvedKeys(Dictionary<string, string?> result, string targetCulture, int generation) {
        if(!_cachingEnabled) {
            return;
        }

        lock(_cacheLock) {
            if(Volatile.Read(ref _generation) != generation) {
                return;
            }

            var expiresAt = DateTimeOffset.UtcNow.Add(_cacheExpiration);
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
        if(_cache.Count >= MAX_CACHE_ENTRIES && !_cache.ContainsKey(cacheKey)) {
            EvictCacheEntries();
        }

        _cache[cacheKey] = entry;
    }

    /// <summary>
    /// Restores the cache bound: expired entries are removed first; if the cache is still at the
    /// bound, the entry closest to expiry (oldest insertion under the fixed TTL) is evicted.
    /// </summary>
    private void EvictCacheEntries() {
        var now = DateTimeOffset.UtcNow;
        foreach(var pair in _cache) {
            if(pair.Value.ExpiresAt <= now) {
                _cache.TryRemove(pair.Key, out _);
            }
        }

        while(_cache.Count >= MAX_CACHE_ENTRIES) {
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
    /// Performs the translate chunk operation.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="targetCulture">The target culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is translate chunk.</returns>
    private async Task<IReadOnlyDictionary<string, string?>> TranslateChunkAsync(
        string[] keys,
        string targetCulture,
        CancellationToken cancellationToken) {
        var requestItems = new GlossaryRequestItem[keys.Length];
        for(var index = 0; index < keys.Length; index++) {
            requestItems[index] = ProjectGlossary(keys[index], targetCulture);
        }

        var body = requestItems.Select(static item => new TranslationRequestBody { Text = item.RequestText }).ToArray();

        // The resilience pipeline owns the send: per-chunk total timeout, idempotent-safe
        // retries, breaker fail-fast, and token-bucket pacing. Each attempt builds a fresh
        // request message because a sent message can never be re-sent. Terminal client errors
        // return here and are reported exactly as before.
        using var response = await _resilience.ExecuteAsync(
            token => SendChunkAsync(body, targetCulture, token),
            cancellationToken);
        if(!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"Azure Translator returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var items = await response.Content.ReadFromJsonAsync(AzureTranslationJsonContext.Default.TranslationItemArray, cancellationToken);
        var dict = new Dictionary<string, string?>(keys.Length);

        if(items is null) {
            foreach(var key in keys) {
                dict[key] = null;
            }

            return dict;
        }

        var aligned = Math.Min(keys.Length, items.Length);
        for(var index = 0; index < aligned; index++) {
            dict[keys[index]] = RestoreGlossary(
                items[index].Translations.FirstOrDefault(static t => t.To is not null)?.Text,
                requestItems[index].Placeholders);
        }

        for(var index = aligned; index < keys.Length; index++) {
            dict[keys[index]] = null;
        }

        return dict;
    }

    /// <summary>
    /// Sends one chunk HTTP exchange. A fresh request message is built per call because a sent
    /// message can never be re-sent; the resilience pipeline calls this once per attempt.
    /// </summary>
    /// <param name="body">The chunk's request items.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task whose result is send chunk.</returns>
    private async Task<HttpResponseMessage> SendChunkAsync(
        TranslationRequestBody[] body,
        string targetCulture,
        CancellationToken cancellationToken) {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTranslateUri(targetCulture));
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _apiKey);
        if(!string.IsNullOrWhiteSpace(_region)) {
            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", _region);
        }

        request.Content = JsonContent.Create(body, AzureTranslationJsonContext.Default.TranslationRequestBodyArray);

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Builds the construction-time glossary snapshot: the caller-owned terms are copied and
    /// grouped by target culture name, so later registration on the same
    /// <see cref="MessageGlossary"/> does not change the Provider's behavior.
    /// </summary>
    /// <param name="glossary">The caller-owned glossary, possibly <see langword="null"/>.</param>
    /// <returns>The glossary terms grouped by target culture name.</returns>
    private static Dictionary<string, GlossaryTerm[]> SnapshotGlossary(MessageGlossary? glossary) {
        if(glossary is null) {
            return new Dictionary<string, GlossaryTerm[]>(0, StringComparer.Ordinal);
        }

        var byCulture = new Dictionary<string, List<GlossaryTerm>>(StringComparer.Ordinal);
        foreach(var term in glossary.Terms) {
            if(!byCulture.TryGetValue(term.TargetCulture.Name, out var terms)) {
                terms = [];
                byCulture[term.TargetCulture.Name] = terms;
            }

            terms.Add(term);
        }

        return byCulture.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Projects a source text through the glossary for a target culture: every matched term is
    /// replaced by a placeholder token that machine translation cannot paraphrase, and the token is
    /// restored to the forced target in the translated result. Returns the source text unchanged
    /// when the culture has no glossary terms or none match.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <returns>The projected request item, with a <see langword="null"/> placeholder map when no term matched.</returns>
    private GlossaryRequestItem ProjectGlossary(string source, string targetCulture) {
        if(_glossaryTermsByCulture.Count == 0
            || !_glossaryTermsByCulture.TryGetValue(targetCulture, out var terms)
            || terms.Length == 0) {
            return new GlossaryRequestItem(source, null);
        }

        var matches = FindGlossaryMatches(source, terms);
        if(matches.Count == 0) {
            return new GlossaryRequestItem(source, null);
        }

        var text = new StringBuilder(source.Length);
        var placeholders = new Dictionary<string, string>(matches.Count, StringComparer.Ordinal);
        var position = 0;
        var nextIndex = 0;
        foreach(var match in matches) {
            text.Append(source, position, match.Index - position);
            string token;
            do {
                token = GLOSSARY_TOKEN_PREFIX + nextIndex.ToString(CultureInfo.InvariantCulture) + GLOSSARY_TOKEN_SUFFIX;
                nextIndex++;
            } while(source.Contains(token, StringComparison.Ordinal));

            text.Append(token);
            placeholders[token] = match.Term.Target;
            position = match.Index + match.Length;
        }

        text.Append(source, position, source.Length - position);
        return new GlossaryRequestItem(text.ToString(), placeholders);
    }

    /// <summary>
    /// Finds the glossary matches in a source text as a left-to-right, non-overlapping sequence.
    /// Each step takes the earliest match at or after the current position; ties at the same
    /// position resolve to the longest term. Registered conflicts are rejected at registration, so
    /// only undetected partial overlaps need this deterministic resolution.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="terms">The glossary terms registered for the target culture.</param>
    /// <returns>The ordered, non-overlapping matches; empty when nothing matches.</returns>
    private static List<GlossaryMatch> FindGlossaryMatches(string source, GlossaryTerm[] terms) {
        var matches = new List<GlossaryMatch>();
        var position = 0;
        while(position < source.Length) {
            GlossaryMatch? best = null;
            foreach(var term in terms) {
                var index = FindNextTermOccurrence(source, term, position);
                if(index < 0) {
                    continue;
                }

                if(best is null
                    || index < best.Value.Index
                    || (index == best.Value.Index && term.SourceTerm.Length > best.Value.Length)) {
                    best = new GlossaryMatch(term, index, term.SourceTerm.Length);
                }
            }

            if(best is null) {
                break;
            }

            matches.Add(best.Value);
            position = best.Value.Index + best.Value.Length;
        }

        return matches;
    }

    /// <summary>
    /// Finds next term occurrence.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="term">The term.</param>
    /// <param name="startIndex">The start index.</param>
    /// <returns>The next term occurrence value.</returns>
    private static int FindNextTermOccurrence(string source, GlossaryTerm term, int startIndex) {
        var comparison = term.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = source.IndexOf(term.SourceTerm, startIndex, comparison);
        while(index >= 0) {
            if(!term.WholeWord || IsWholeWordMatch(source, index, term.SourceTerm.Length)) {
                return index;
            }

            index = source.IndexOf(term.SourceTerm, index + 1, comparison);
        }

        return -1;
    }

    /// <summary>
    /// Determines whether the value is whole word match.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <param name="length">The length.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWholeWordMatch(string text, int index, int length) =>
        (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
        && (index + length == text.Length || !char.IsLetterOrDigit(text[index + length]));

    /// <summary>
    /// Restores the glossary-forced targets in a translated text by substituting the placeholder
    /// tokens that were present in the request. A token the machine translation dropped or altered
    /// cannot be restored; the term is then absent from the result rather than silently wrong.
    /// </summary>
    /// <param name="translated">The machine-translated text, possibly <see langword="null"/>.</param>
    /// <param name="placeholders">The placeholder-to-forced-target map, possibly <see langword="null"/>.</param>
    /// <returns>The restored text, or the input when there is nothing to restore.</returns>
    private static string? RestoreGlossary(string? translated, IReadOnlyDictionary<string, string>? placeholders) {
        if(translated is null || placeholders is null) {
            return translated;
        }

        foreach(var pair in placeholders) {
            translated = translated.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        return translated;
    }

    /// <summary>
    /// Builds translate uri.
    /// </summary>
    /// <param name="targetCulture">The target culture.</param>
    /// <returns>The translate uri result.</returns>
    private Uri BuildTranslateUri(string targetCulture) {
        var query = $"api-version=3.0&to={Uri.EscapeDataString(targetCulture)}";
        if(!string.IsNullOrWhiteSpace(_fromLanguage)) {
            query += $"&from={Uri.EscapeDataString(_fromLanguage)}";
        }

        return new UriBuilder(new Uri(_endpoint + "/translate", UriKind.Absolute)) { Query = query }.Uri;
    }

    /// <summary>
    /// Records one outbound translation call: the request counter and, when
    /// enabled, the duration histogram. Inert when no metric listener observes the meter.
    /// </summary>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch"/> start timestamp, or 0 when duration recording is disabled.</param>
    private static void RecordTranslationCall(string targetCulture, long startTimestamp) {
        var cultureTag = new KeyValuePair<string, object?>(LocalizationDiagnostics.CULTURE_TAG_NAME, targetCulture);
        if(LocalizationDiagnostics.TranslationRequests.Enabled) {
            LocalizationDiagnostics.TranslationRequests.Add(1, cultureTag);
        }

        if(startTimestamp != 0) {
            LocalizationDiagnostics.TranslationDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, cultureTag);
        }
    }

    /// <summary>
    /// Records cache hit.
    /// </summary>
    /// <param name="culture">The culture.</param>
    private static void RecordCacheHit(string culture) {
        if(LocalizationDiagnostics.TranslationCacheHits.Enabled) {
            LocalizationDiagnostics.TranslationCacheHits.Add(1, new KeyValuePair<string, object?>(LocalizationDiagnostics.CULTURE_TAG_NAME, culture));
        }
    }

    /// <summary>
    /// Records cache miss.
    /// </summary>
    /// <param name="culture">The culture.</param>
    private static void RecordCacheMiss(string culture) {
        if(LocalizationDiagnostics.TranslationCacheMisses.Enabled) {
            LocalizationDiagnostics.TranslationCacheMisses.Add(1, new KeyValuePair<string, object?>(LocalizationDiagnostics.CULTURE_TAG_NAME, culture));
        }
    }

    /// <summary>
    /// Attempts to get valid cache.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="template">The message template.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private bool TryGetValidCache((string key, string culture) cacheKey, out string? template) {
        if(_cache.TryGetValue(cacheKey, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow) {
            template = entry.Template;
            return true;
        }

        _cache.TryRemove(cacheKey, out _);
        template = null;
        return false;
    }

    /// <summary>
    /// Resolves a key from the local layers only (TTL cache, then translation memory) without an
    /// HTTP request. Mirrors the pre-acquisition resolution of the core lookup path so batch
    /// administration classifies and estimates keys exactly as the provider would resolve them.
    /// Degenerate (empty or whitespace) keys are never sent to the store.
    /// </summary>
    /// <param name="key">The key to resolve.</param>
    /// <param name="cultureName">The target culture name.</param>
    /// <param name="template">The resolved template when the local layers answer.</param>
    /// <returns><see langword="true"/> when a local layer resolved the key.</returns>
    internal bool TryPeekLocalTemplate(string key, string cultureName, out string? template) {
        if(_cachingEnabled && TryGetValidCache((key, cultureName), out template)) {
            return true;
        }

        if(!string.IsNullOrWhiteSpace(key) &&
            _translationMemory is not null &&
            _translationMemory.TryLookup(key, CultureInfo.GetCultureInfo(cultureName), out template)) {
            return true;
        }

        template = null;
        return false;
    }

    /// <summary>
    /// Represents a cache entry.
    /// </summary>
    /// <param name="Template">The template.</param>
    /// <param name="ExpiresAt">The expires at.</param>
    private readonly record struct CacheEntry(string? Template, DateTimeOffset ExpiresAt);

    /// <summary>
    /// A glossary match in a source text: the matched term and its position within the text.
    /// </summary>
    /// <param name="Term">The matched glossary term.</param>
    /// <param name="Index">The match start offset within the source text.</param>
    /// <param name="Length">The match length within the source text.</param>
    private readonly record struct GlossaryMatch(GlossaryTerm Term, int Index, int Length);

    /// <summary>
    /// One machine-translation request item: the source text projected through the glossary (the
    /// original text when no term matched) and the placeholder-to-forced-target map used to restore
    /// the result (<see langword="null"/> when no term matched).
    /// </summary>
    /// <param name="RequestText">The glossary-projected source text.</param>
    /// <param name="Placeholders">The placeholder-to-forced-target map, or <see langword="null"/> when no term matched.</param>
    private readonly record struct GlossaryRequestItem(string RequestText, IReadOnlyDictionary<string, string>? Placeholders);
}
