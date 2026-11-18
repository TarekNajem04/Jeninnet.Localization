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
/// options object. All lookup flow lives in <see cref="CoalescingCacheEngine"/> over a
/// generation-keyed distributed store; this class owns only the public surface.
/// <para>
/// Cache identity: entries are keyed by decorator identity, cache generation, culture,
/// and a SHA-256 hash of the key, so keys of any shape stay within store key limits
/// and two decorators over the same inner provider never share entries. Refresh
/// semantics: cancels nothing that is in flight; it advances the generation so that
/// earlier entries become unreachable (they expire naturally) and completions begun
/// before the refresh cannot repopulate the cache. The refresh is forwarded to
/// the inner provider when it is refreshable.
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
    private readonly CoalescingCacheEngine _engine;

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
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _engine = new CoalescingCacheEngine(
            inner,
            new DistributedCacheStore(cache, value.CacheExpiration),
            acquired => TranslationAcquired?.Invoke(this, acquired),
            failed => AcquisitionFailed?.Invoke(this, failed));
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeatures feature) => feature switch {
        ProviderFeatures.MultiKeys => true,
        ProviderFeatures.EventsListening => true,
        ProviderFeatures.Preload => true,
        ProviderFeatures.Refresh => true,
        ProviderFeatures.Caching => true,
        _ => false,
    };

    /// <inheritdoc />
    public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => _engine.GetMessageTemplateAsync(key, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default)
        => _engine.GetMessageTemplatesAsync(keys, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default)
        => _engine.PreloadAsync(keys, culture, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Refresh semantics: cancels nothing that is in flight; it advances the generation so that
    /// earlier entries become unreachable (they expire naturally) and completions begun before the
    /// refresh cannot repopulate the cache. Active in-flight entries remain provider-owned and live
    /// until terminal completion. The refresh is forwarded to the inner provider when it is
    /// refreshable.
    /// </remarks>
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => _engine.RefreshAsync(cancellationToken);
}
