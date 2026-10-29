//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Abstractions.Diagnostics;

/// <summary>
/// The stable observability surface of the localization pipeline:
/// one <see cref="ActivitySource"/> and one <see cref="Meter"/>, both named
/// <c>Jeninnet.Localization</c>, plus the shared instruments.
/// </summary>
/// <remarks>
/// <para>
/// The hooks use BCL (Base Class Library) <c>System.Diagnostics.*</c> only — no OpenTelemetry package is
/// referenced. Consumers opt in on the SDK side with the stable names, for example
/// <c>AddOpenTelemetry().WithTracing(b =&gt; b.AddSource("Jeninnet.Localization"))</c> and
/// <c>WithMetrics(b =&gt; b.AddMeter("Jeninnet.Localization"))</c>. With no listeners every
/// emission is skipped (activities are only created when
/// <see cref="ActivitySource.HasListeners"/> is true; instruments are only written when
/// their <c>Enabled</c> flag is set), so hosts without diagnostics pay nothing and
/// behavior is identical.
/// </para>
/// <para>
/// Traces carry developer identifiers only: the lookup key and culture are tags, the
/// fallback steps are child activities, and <b>template text is never tagged</b> (PII
/// caution: hosts must not encode personal data into keys). Metrics carry the culture tag
/// only — keys never appear in metrics (bounded cardinality).
/// </para>
/// </remarks>
public static class LocalizationDiagnostics {
    /// <summary>The name of the localization <see cref="ActivitySource"/> and <see cref="Meter"/>.</summary>
    public const string SOURCE_NAME = "Jeninnet.Localization";

    /// <summary>The root activity name for one localizer lookup.</summary>
    public const string LOOKUP_ACTIVITY_NAME = "localization.lookup";

    /// <summary>The child activity name for one provider query along the canonical fallback chain.</summary>
    public const string FALLBACK_STEP_ACTIVITY_NAME = "localization.fallback.step";

    /// <summary>The tag name carrying the lookup key (traces only).</summary>
    public const string KEY_TAG_NAME = "localization.key";

    /// <summary>The tag name carrying the culture of a lookup or fallback step.</summary>
    public const string CULTURE_TAG_NAME = "localization.culture";

    /// <summary>The tag name indicating whether a lookup or fallback step resolved a template.</summary>
    public const string FOUND_TAG_NAME = "localization.found";

    /// <summary>The instrument name of the resolved-lookup counter.</summary>
    public const string LOOKUP_HITS_INSTRUMENT_NAME = "localization.lookup.hits";

    /// <summary>The instrument name of the unresolved-lookup counter.</summary>
    public const string LOOKUP_MISSES_INSTRUMENT_NAME = "localization.lookup.misses";

    /// <summary>The instrument name of the lookup duration histogram (milliseconds).</summary>
    public const string LOOKUP_DURATION_INSTRUMENT_NAME = "localization.lookup.duration";

    /// <summary>The instrument name of the fallback-step counter.</summary>
    public const string FALLBACK_STEPS_INSTRUMENT_NAME = "localization.fallback.steps";

    /// <summary>The instrument name of the outbound translation-request counter.</summary>
    public const string TRANSLATION_REQUESTS_INSTRUMENT_NAME = "localization.translation.requests";

    /// <summary>The instrument name of the translation cache-hit counter.</summary>
    public const string TRANSLATION_CACHE_HITS_INSTRUMENT_NAME = "localization.translation.cache.hits";

    /// <summary>The instrument name of the translation cache-miss counter.</summary>
    public const string TRANSLATION_CACHE_MISSES_INSTRUMENT_NAME = "localization.translation.cache.misses";

    /// <summary>The instrument name of the outbound translation duration histogram (milliseconds).</summary>
    public const string TRANSLATION_DURATION_INSTRUMENT_NAME = "localization.translation.duration";

    /// <summary>The OpenTelemetry unit of the item-counting instruments (UCUM annotation).</summary>
    private const string LOOKUP_COUNT_UNIT = "{lookup}";

    /// <summary>Gets the activity source for pipeline traces.</summary>
    public static ActivitySource Source { get; } = new(SOURCE_NAME);

    /// <summary>Gets the meter for pipeline metrics.</summary>
    public static Meter Meter { get; } = new(SOURCE_NAME);

    /// <summary>Gets the resolved-lookup counter (tagged with the culture).</summary>
    public static Counter<long> LookupHits { get; } = Meter.CreateCounter<long>(LOOKUP_HITS_INSTRUMENT_NAME, LOOKUP_COUNT_UNIT, "Lookups that resolved a template.");

    /// <summary>Gets the unresolved-lookup counter (tagged with the culture).</summary>
    public static Counter<long> LookupMisses { get; } = Meter.CreateCounter<long>(LOOKUP_MISSES_INSTRUMENT_NAME, LOOKUP_COUNT_UNIT, "Lookups that resolved no template.");

    /// <summary>Gets the lookup duration histogram in milliseconds (tagged with the culture).</summary>
    public static Histogram<double> LookupDuration { get; } = Meter.CreateHistogram<double>(LOOKUP_DURATION_INSTRUMENT_NAME, "ms", "Duration of a localizer lookup.");

    /// <summary>Gets the fallback-step counter (tagged with the culture).</summary>
    public static Counter<long> FallbackSteps { get; } = Meter.CreateCounter<long>(FALLBACK_STEPS_INSTRUMENT_NAME, "{step}", "Provider queries performed along the canonical fallback chain.");

    /// <summary>Gets the outbound translation-request counter (tagged with the culture).</summary>
    public static Counter<long> TranslationRequests { get; } = Meter.CreateCounter<long>(TRANSLATION_REQUESTS_INSTRUMENT_NAME, "{request}", "Outbound machine-translation requests.");

    /// <summary>Gets the translation cache-hit counter (tagged with the culture).</summary>
    public static Counter<long> TranslationCacheHits { get; } = Meter.CreateCounter<long>(TRANSLATION_CACHE_HITS_INSTRUMENT_NAME, LOOKUP_COUNT_UNIT, "Translation lookups served from the provider cache.");

    /// <summary>Gets the translation cache-miss counter (tagged with the culture).</summary>
    public static Counter<long> TranslationCacheMisses { get; } = Meter.CreateCounter<long>(TRANSLATION_CACHE_MISSES_INSTRUMENT_NAME, LOOKUP_COUNT_UNIT, "Translation lookups that were not served from the provider cache.");

    /// <summary>Gets the outbound translation duration histogram in milliseconds (tagged with the culture).</summary>
    public static Histogram<double> TranslationDuration { get; } = Meter.CreateHistogram<double>(TRANSLATION_DURATION_INSTRUMENT_NAME, "ms", "Duration of an outbound machine-translation request.");
}
