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
/// immutable construction snapshot; the decorator never re-reads the options object. All
/// lookup flow lives in <see cref="CoalescingCacheEngine"/> over an in-memory store;
/// this class owns only the public surface.
/// <para>
/// Refresh semantics: cancels nothing that is in flight; it clears the decorator cache
/// and advances the generation so that completions begun before the refresh cannot repopulate the
/// cache. Invalidation and completion cache publication share a synchronization boundary. A refresh
/// is forwarded to the inner provider when it implements <see cref="IRefreshableMessageProvider"/>.
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
    private readonly CoalescingCacheEngine _engine;

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
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(inner);
        _engine = new CoalescingCacheEngine(
            inner,
            new MemoryCacheStore(value.CacheExpiration, value.MaxEntries, timeProvider),
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
    /// Refresh semantics: cancels nothing that is in flight; it invalidates the decorator cache
    /// and advances the generation so that completions begun before the refresh cannot repopulate the
    /// cache. Active in-flight entries remain provider-owned and live until terminal completion.
    /// The refresh is forwarded to the inner provider when it is refreshable.
    /// </remarks>
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => _engine.RefreshAsync(cancellationToken);
}
