//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Composite;

/// <summary>
/// <para>An opt-in ordered composition of inner message providers.</para>
/// <para>
/// Resolution order: each inner is tried in construction order and the first exact
/// (non-null) template wins. The composite invents nothing: no translation, no
/// fallback chain, no merging of templates. The requested culture is passed to
/// every inner unchanged; culture fallback stays owned by
/// <c>DefaultMessageLocalizer</c>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// At the selector boundary the composite IS the single <see cref="IInnerMessageProvider"/>:
/// registering it plus another provider still throws, exactly as with any other
/// single provider.
/// </para>
/// <para>
/// Failure policy: an inner throwing is a failure, not a miss. The first exception
/// surfaces through <see cref="AcquisitionFailed"/> and propagates; remaining
/// inners are skipped. A broken catalog therefore fails loudly instead of silently
/// falling through to the next catalog. Cancellation is observed before any source
/// access and never becomes a missing-resource result.
/// </para>
/// <para>
/// Capability aggregation is honest forwarding: <see cref="Supports"/> reports the
/// OR over inners that declare the capability interface, and every capability
/// method fans out only to inners that implement it (neutral when none does).
/// Plural lookups forward to the first inner implementing
/// <see cref="IPluralFormMessageProvider"/>.
/// </para>
/// <para>
/// Events are composite-owned: one <see cref="TranslationAcquired"/> per resolved
/// lookup or batch, never per inner call; inner-provider events are not forwarded
/// (subscribe to the inner instance directly when its own signals are needed).
/// A genuine caller cancellation raises neither event.
/// </para>
/// <para>
/// The composite owns no file watcher and no timer, so it implements no disposal
/// abstraction. Inner providers keep the lifetimes their owners gave them: dispose
/// a watching file provider directly when the composite is no longer needed.
/// </para>
/// </remarks>
public class CompositeMessageProvider : MessageProviderBase, IInnerMessageProvider,
    IProviderCapabilities, IMultiKeyMessageProvider, IPluralFormMessageProvider,
    IPreloadableMessageProvider, IRefreshableMessageProvider, IProviderEventSource {
    private readonly IInnerMessageProvider[] _inners;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeMessageProvider"/> class with the ordered inner providers.
    /// </summary>
    /// <param name="inners">The inner providers in resolution order. The first exact (non-null) template wins.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inners"/> or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="inners"/> is empty or contains the same instance twice.</exception>
    public CompositeMessageProvider(IReadOnlyList<IInnerMessageProvider> inners) {
        ArgumentNullException.ThrowIfNull(inners);

        if(inners.Count == 0) {
            throw new ArgumentException("The composite provider requires at least one inner provider.", nameof(inners));
        }

        var seen = new HashSet<IInnerMessageProvider>(ReferenceEqualityComparer.Instance);
        foreach(var inner in inners) {
            ArgumentNullException.ThrowIfNull(inner);

            if(!seen.Add(inner)) {
                throw new ArgumentException("The composite provider must not contain the same inner provider instance twice.", nameof(inners));
            }
        }

        _inners = [.. inners];
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeatures feature) => feature switch {
        ProviderFeatures.MultiKeys => _inners.Any(static inner => inner is IMultiKeyMessageProvider),
        ProviderFeatures.EventsListening => true,
        ProviderFeatures.Preload => _inners.Any(static inner => inner is IPreloadableMessageProvider),
        ProviderFeatures.Refresh => _inners.Any(static inner => inner is IRefreshableMessageProvider),
        ProviderFeatures.Caching => false,
        _ => false,
    };

    /// <inheritdoc />
    public override async ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        foreach(var inner in _inners) {
            string? template;
            try {
                template = await inner.GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch(Exception exception) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(culture, exception));
                throw;
            }

            if(template is not null) {
                TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(
                    new Dictionary<string, string?> { [key] = template }, culture));
                return template;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var distinct = keys.Distinct().ToArray();
        var result = distinct.ToDictionary(static key => key, static _ => (string?)null, StringComparer.Ordinal);
        var missing = new HashSet<string>(distinct, StringComparer.Ordinal);

        foreach(var inner in _inners) {
            if(missing.Count == 0) {
                break;
            }

            IReadOnlyDictionary<string, string?> batch;
            try {
                batch = inner is IMultiKeyMessageProvider multi
                    ? await multi.GetMessageTemplatesAsync([.. missing], culture, cancellationToken).ConfigureAwait(false)
                    : await FanOutAsync(inner, missing, culture, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch(Exception exception) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(culture, exception));
                throw;
            }

            var winners = batch
                .Where(static pair => pair.Value is not null)
                .Select(static pair => pair.Key)
                .Where(missing.Remove)
                .ToArray();
            foreach(var key in winners) {
                result[key] = batch[key];
            }
        }

        if(result.Values.Any(static template => template is not null)) {
            TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(result, culture));
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetPluralTemplateAsync(
        string singularKey,
        string pluralKey,
        decimal count,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        foreach(var inner in _inners) {
            if(inner is not IPluralFormMessageProvider plurals) {
                continue;
            }

            string? template;
            try {
                template = await plurals.GetPluralTemplateAsync(singularKey, pluralKey, count, culture, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch(Exception exception) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(culture, exception));
                throw;
            }

            if(template is not null) {
                TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(
                    new Dictionary<string, string?> { [singularKey] = template }, culture));
                return template;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetPluralTemplateWithContextAsync(
        string singularKey,
        string pluralKey,
        string context,
        decimal count,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        foreach(var inner in _inners) {
            if(inner is not IPluralFormMessageProvider plurals) {
                continue;
            }

            string? template;
            try {
                template = await plurals.GetPluralTemplateWithContextAsync(singularKey, pluralKey, context, count, culture, cancellationToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) {
                throw;
            }
            catch(Exception exception) {
                AcquisitionFailed?.Invoke(this, new ProviderAcquisitionFailedEventArgs(culture, exception));
                throw;
            }

            if(template is not null) {
                TranslationAcquired?.Invoke(this, new ProviderAcquisitionEventArgs(
                    new Dictionary<string, string?> { [singularKey] = template }, culture));
                return template;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        foreach(var inner in _inners) {
            if(inner is IPreloadableMessageProvider preloadable) {
                await preloadable.PreloadAsync(keys, culture, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        foreach(var inner in _inners) {
            if(inner is IRefreshableMessageProvider refreshable) {
                await refreshable.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Fans out one key at a time for an inner without batch support.
    /// </summary>
    /// <param name="inner">The inner provider.</param>
    /// <param name="missing">The still-missing keys.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolved templates.</returns>
    private static async Task<Dictionary<string, string?>> FanOutAsync(
        IInnerMessageProvider inner,
        HashSet<string> missing,
        CultureInfo culture,
        CancellationToken cancellationToken) {
        var batch = new Dictionary<string, string?>(missing.Count, StringComparer.Ordinal);
        foreach(var key in missing) {
            batch[key] = await inner.GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
        }

        return batch;
    }
}
