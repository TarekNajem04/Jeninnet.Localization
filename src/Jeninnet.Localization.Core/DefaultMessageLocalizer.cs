//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core;

/// <summary>
/// Default implementation of <see cref="IMessageLocalizer"/>.
/// </summary>
public class DefaultMessageLocalizer : IMessageLocalizer {
    /// <summary>
    /// The gettext-canonical separator joining context and key in a composite
    /// provider key (; NGettext-compatible,  PO (Portable Object)
    /// <c>msgctxt</c> interop).
    /// </summary>
    private const char CONTEXT_SEPARATOR = '\u0004';

    private readonly IMessageProvider _provider;
    private readonly IMessageFormatter _formatter;
    private readonly string? _defaultCulture;
    private readonly IMissingKeySink? _sink;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultMessageLocalizer"/> class.
    /// </summary>
    /// <remarks>
    /// The effective default-culture name is captured at construction time, so the localizer does
    /// not observe later mutations of the supplied options instance.
    /// </remarks>
    /// <param name="provider">The message provider.</param>
    /// <param name="formatter">The message formatter.</param>
    /// <param name="options">The localization options, or <see langword="null"/> when no default-culture fallback is desired.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or <paramref name="formatter"/> is null.</exception>
    public DefaultMessageLocalizer(
        IMessageProvider provider,
        IMessageFormatter formatter,
        Abstractions.Options.LocalizationOptions? options) {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(formatter);

        _provider = provider;
        _formatter = formatter;
        _defaultCulture = options is null || string.IsNullOrEmpty(options.DefaultCulture) ? null : options.DefaultCulture;
        _sink = options?.MissingKeySink;
    }

    /// <inheritdoc />
    public async ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        //: the hooks are inert without listeners (null activity, disabled instruments).
        var activity = StartLookupActivity(key, culture);
        var stopwatch = StartLookupMeasurement();
        bool? found = null;
        try {
            var template = await GetTemplateWithFallbackAsync(key, culture, cancellationToken).ConfigureAwait(false);
            found = template is not null;
            if(template is null) {
                ReportMiss(key, culture);
                return null;
            }

            return await FormatResolvedAsync(template, parameters, culture, cancellationToken).ConfigureAwait(false);
        }
        catch {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally {
            CompleteLookup(activity, stopwatch, culture, found);
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => LocalizeAsync(key, parameters, CultureInfo.CurrentUICulture, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var activity = StartLookupActivity(singularKey, culture);
        var stopwatch = StartLookupMeasurement();
        bool? found = null;
        try {
            //: when the resolved provider serves per-form plural tables
            // (PO catalogs with a Plural-Forms header), the count-aware path selects the
            // exact msgstr index through the same culture-fallback chain; otherwise the
            //  pair path below applies. A full plural miss falls through to the pair
            // path so exact-match pair entries (e.g. non-plural providers behind a
            // CultureAware dispatcher) keep their documented degraded fallback.
            if(_provider is IPluralFormMessageProvider pluralProvider) {
                var pluralTemplate = await GetPluralTemplateWithFallbackAsync(pluralProvider, singularKey, pluralKey, count, culture, cancellationToken).ConfigureAwait(false);
                if(pluralTemplate is not null) {
                    found = true;
                    return await FormatResolvedAsync(pluralTemplate, parameters, culture, cancellationToken).ConfigureAwait(false);
                }
            }

            var category = PluralRules.GetCardinalCategory(culture, count);
            var preferredKey = category == PluralCategory.One ? singularKey : pluralKey;
            var fallbackKey = category == PluralCategory.One ? pluralKey : singularKey;
            var template = await GetTemplateWithFallbackAsync(preferredKey, culture, cancellationToken).ConfigureAwait(false);
            if(template is null && !string.Equals(preferredKey, fallbackKey, StringComparison.Ordinal)) {
                template = await GetTemplateWithFallbackAsync(fallbackKey, culture, cancellationToken).ConfigureAwait(false);
            }

            found = template is not null;
            if(template is null) {
                ReportMiss(preferredKey, culture);
                if(!string.Equals(preferredKey, fallbackKey, StringComparison.Ordinal)) {
                    ReportMiss(fallbackKey, culture);
                }

                return null;
            }

            return await FormatResolvedAsync(template, parameters, culture, cancellationToken).ConfigureAwait(false);
        }
        catch {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally {
            CompleteLookup(activity, stopwatch, culture, found);
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => LocalizePluralAsync(singularKey, pluralKey, count, parameters, CultureInfo.CurrentUICulture, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        var compositeKey = context.Length == 0 ? key : context + CONTEXT_SEPARATOR + key;
        var activity = StartLookupActivity(compositeKey, culture);
        var stopwatch = StartLookupMeasurement();
        bool? found = null;
        try {
            var template = await GetTemplateWithFallbackAsync(compositeKey, culture, cancellationToken).ConfigureAwait(false);
            found = template is not null;
            if(template is null) {
                ReportMiss(compositeKey, culture);
                return null;
            }

            return await FormatResolvedAsync(template, parameters, culture, cancellationToken).ConfigureAwait(false);
        }
        catch {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally {
            CompleteLookup(activity, stopwatch, culture, found);
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => LocalizeWithContextAsync(key, context, parameters, CultureInfo.CurrentUICulture, cancellationToken);

    /// <summary>
    /// Formats resolved.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A value task whose result is resolved.</returns>
    private ValueTask<string?> FormatResolvedAsync(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        // Culture-aware formatters (for example ICU (International Components for Unicode) templates) format in the requested
        // culture; every other formatter keeps the culture-unaware contract unchanged.
        if(_formatter is ICultureAwareMessageFormatter cultureAware) {
            return ValueTask.FromResult<string?>(cultureAware.Format(template, parameters, culture));
        }

        return ValueTask.FromResult<string?>(_formatter.Format(template, parameters));
    }

    /// <summary>
    /// Reports a lookup miss to the opt-in sink. The sink is consulted only
    /// after the full fallback chain resolved nothing, so intermediate steps never
    /// report. A <see langword="null"/> sink (the default) costs one null check and
    /// no allocations: off-state behavior is identical to today. Sink failures are
    /// contained — diagnostic observers must never break lookups,
    /// mirroring the debounced file-reloader callback contract.
    /// </summary>
    /// <param name="key">The message key that was not found.</param>
    /// <param name="culture">The requested culture.</param>
    private void ReportMiss(string key, CultureInfo culture) {
        var sink = _sink;
        if(sink is null) {
            return;
        }

#pragma warning disable CA1031 // Containment is the documented design: a failing diagnostic sink must not break localization lookups.
        try {
            sink.ReportMiss(key, culture.Name, MissingKeys.MissingKeyCallSite.Capture());
        }
        catch(Exception exception) {
            // Contained by design (see above): the failure is deliberately discarded.
            _ = exception;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Gets plural template with fallback.
    /// </summary>
    /// <param name="pluralProvider">The plural provider.</param>
    /// <param name="singularKey">The singular key.</param>
    /// <param name="pluralKey">The plural key.</param>
    /// <param name="count">The count.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A value task whose result is plural template with fallback.</returns>
    private async ValueTask<string?> GetPluralTemplateWithFallbackAsync(IPluralFormMessageProvider pluralProvider, string singularKey, string pluralKey, decimal count, CultureInfo culture, CancellationToken cancellationToken) {
        var currentCulture = culture;
        while(currentCulture is not null && currentCulture != CultureInfo.InvariantCulture) {
            cancellationToken.ThrowIfCancellationRequested();
            var template = await QueryPluralProviderAsync(pluralProvider, singularKey, pluralKey, count, currentCulture, cancellationToken).ConfigureAwait(false);
            if(template is not null) {
                return template;
            }

            currentCulture = currentCulture.Parent;
        }

        if(_defaultCulture is not null) {
            cancellationToken.ThrowIfCancellationRequested();
            var defaultCulture = new CultureInfo(_defaultCulture);
            return await QueryPluralProviderAsync(pluralProvider, singularKey, pluralKey, count, defaultCulture, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Gets template with fallback.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A value task whose result is template with fallback.</returns>
    private async ValueTask<string?> GetTemplateWithFallbackAsync(string key, CultureInfo culture, CancellationToken cancellationToken) {
        var currentCulture = culture;
        while(currentCulture is not null && currentCulture != CultureInfo.InvariantCulture) {
            cancellationToken.ThrowIfCancellationRequested();
            var template = await QueryProviderAsync(key, currentCulture, cancellationToken).ConfigureAwait(false);
            if(template is not null) {
                return template;
            }

            currentCulture = currentCulture.Parent;
        }

        if(_defaultCulture is not null) {
            cancellationToken.ThrowIfCancellationRequested();
            var defaultCulture = new CultureInfo(_defaultCulture);
            return await QueryProviderAsync(key, defaultCulture, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Queries the provider for one fallback step, emitting the step activity and the
    /// fallback-step counter when observed. With no listeners this is a single
    /// listener check followed by the plain provider call.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture of this fallback step.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The template served by this step, or <see langword="null"/> on a miss.</returns>
    private async ValueTask<string?> QueryProviderAsync(string key, CultureInfo culture, CancellationToken cancellationToken) {
        var stepActivity = StartFallbackStepActivity(culture);
        try {
            var template = await _provider.GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
            RecordFallbackStep(stepActivity, culture, template is not null);
            return template;
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            stepActivity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally {
            stepActivity?.Dispose();
        }
    }

    /// <summary>
    /// Queries a plural-form provider for one fallback step, emitting the step activity and
    /// the fallback-step counter when observed.
    /// </summary>
    /// <param name="pluralProvider">The plural-form provider.</param>
    /// <param name="singularKey">The singular key of the plural pair.</param>
    /// <param name="pluralKey">The plural key of the plural pair.</param>
    /// <param name="count">The count selecting the plural form.</param>
    /// <param name="culture">The culture of this fallback step.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The template served by this step, or <see langword="null"/> on a miss.</returns>
    private static async ValueTask<string?> QueryPluralProviderAsync(IPluralFormMessageProvider pluralProvider, string singularKey, string pluralKey, decimal count, CultureInfo culture, CancellationToken cancellationToken) {
        var stepActivity = StartFallbackStepActivity(culture);
        try {
            var template = await pluralProvider.GetPluralTemplateAsync(singularKey, pluralKey, count, culture, cancellationToken).ConfigureAwait(false);
            RecordFallbackStep(stepActivity, culture, template is not null);
            return template;
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            stepActivity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally {
            stepActivity?.Dispose();
        }
    }

    /// <summary>
    /// Starts lookup activity.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The start lookup activity result.</returns>
    private static Activity? StartLookupActivity(string key, CultureInfo culture) {
        if(!LocalizationDiagnostics.Source.HasListeners()) {
            return null;
        }

        var activity = LocalizationDiagnostics.Source.StartActivity(LocalizationDiagnostics.LOOKUP_ACTIVITY_NAME);
        activity?.SetTag(LocalizationDiagnostics.KEY_TAG_NAME, key);
        activity?.SetTag(LocalizationDiagnostics.CULTURE_TAG_NAME, culture.Name);
        return activity;
    }

    /// <summary>
    /// Starts lookup measurement.
    /// </summary>
    /// <returns>The start lookup measurement result.</returns>
    private static Stopwatch? StartLookupMeasurement() =>
        LocalizationDiagnostics.LookupDuration.Enabled ? Stopwatch.StartNew() : null;

    /// <summary>
    /// Starts fallback step activity.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The start fallback step activity result.</returns>
    private static Activity? StartFallbackStepActivity(CultureInfo culture) {
        if(!LocalizationDiagnostics.Source.HasListeners()) {
            return null;
        }

        var activity = LocalizationDiagnostics.Source.StartActivity(LocalizationDiagnostics.FALLBACK_STEP_ACTIVITY_NAME);
        activity?.SetTag(LocalizationDiagnostics.CULTURE_TAG_NAME, culture.Name);
        return activity;
    }

    /// <summary>
    /// Records fallback step.
    /// </summary>
    /// <param name="stepActivity">The step activity.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="found">The found.</param>
    private static void RecordFallbackStep(Activity? stepActivity, CultureInfo culture, bool found) {
        stepActivity?.SetTag(LocalizationDiagnostics.FOUND_TAG_NAME, found);
        if(LocalizationDiagnostics.FallbackSteps.Enabled) {
            LocalizationDiagnostics.FallbackSteps.Add(1, new KeyValuePair<string, object?>(LocalizationDiagnostics.CULTURE_TAG_NAME, culture.Name));
        }
    }

    /// <summary>
    /// Completes a lookup's diagnostics: the found tag, the duration histogram,
    /// and the hit/miss counter are recorded only for completed lookups (an exception
    /// leaves the activity in error state and records no hit/miss). Template text is never
    /// tagged — only the key and culture (PII caution).
    /// </summary>
    /// <param name="activity">The lookup activity, or <see langword="null"/> when unobserved.</param>
    /// <param name="stopwatch">The duration measurement, or <see langword="null"/> when unobserved.</param>
    /// <param name="culture">The requested culture.</param>
    /// <param name="found">Whether a template was found, or <see langword="null"/> for an errored lookup.</param>
    private static void CompleteLookup(Activity? activity, Stopwatch? stopwatch, CultureInfo culture, bool? found) {
        if(found is not null) {
            activity?.SetTag(LocalizationDiagnostics.FOUND_TAG_NAME, found.Value);

            var cultureTag = new KeyValuePair<string, object?>(LocalizationDiagnostics.CULTURE_TAG_NAME, culture.Name);
            if(stopwatch is not null) {
                stopwatch.Stop();
                LocalizationDiagnostics.LookupDuration.Record(stopwatch.Elapsed.TotalMilliseconds, cultureTag);
            }

            var counter = found.Value ? LocalizationDiagnostics.LookupHits : LocalizationDiagnostics.LookupMisses;
            if(counter.Enabled) {
                counter.Add(1, cultureTag);
            }
        }

        activity?.Dispose();
    }
}
