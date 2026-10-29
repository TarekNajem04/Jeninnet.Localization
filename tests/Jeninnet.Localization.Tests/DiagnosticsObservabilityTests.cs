//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the  observability hooks: stable names,
/// lookup/fallback-step span shape via in-memory activity listeners, hit/miss/latency
/// meters via in-memory meter listeners, and the PII audit (template text is never tagged).
/// </summary>
[TestClass]
public class DiagnosticsObservabilityTests {
    private const string TEMPLATE_MARKER = "SECRET-template-value";
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Verifies that diagnostics names are the stable public contract.
    /// </summary>
    [TestMethod]
    public void DiagnosticsNames_AreTheStablePublicContract() {
        LocalizationDiagnostics.SOURCE_NAME.Should().Be("Jeninnet.Localization");
        LocalizationDiagnostics.Source.Name.Should().Be(LocalizationDiagnostics.SOURCE_NAME);
        LocalizationDiagnostics.Meter.Name.Should().Be(LocalizationDiagnostics.SOURCE_NAME);

        LocalizationDiagnostics.LOOKUP_ACTIVITY_NAME.Should().Be("localization.lookup");
        LocalizationDiagnostics.FALLBACK_STEP_ACTIVITY_NAME.Should().Be("localization.fallback.step");
        LocalizationDiagnostics.LOOKUP_HITS_INSTRUMENT_NAME.Should().Be("localization.lookup.hits");
        LocalizationDiagnostics.LOOKUP_MISSES_INSTRUMENT_NAME.Should().Be("localization.lookup.misses");
        LocalizationDiagnostics.LOOKUP_DURATION_INSTRUMENT_NAME.Should().Be("localization.lookup.duration");
        LocalizationDiagnostics.FALLBACK_STEPS_INSTRUMENT_NAME.Should().Be("localization.fallback.steps");
        LocalizationDiagnostics.TRANSLATION_REQUESTS_INSTRUMENT_NAME.Should().Be("localization.translation.requests");
        LocalizationDiagnostics.TRANSLATION_CACHE_HITS_INSTRUMENT_NAME.Should().Be("localization.translation.cache.hits");
        LocalizationDiagnostics.TRANSLATION_CACHE_MISSES_INSTRUMENT_NAME.Should().Be("localization.translation.cache.misses");
        LocalizationDiagnostics.TRANSLATION_DURATION_INSTRUMENT_NAME.Should().Be("localization.translation.duration");
    }

    /// <summary>
    /// Verifies that localize async emits lookup activity with fallback step children.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task LocalizeAsync_EmitsLookupActivityWithFallbackStepChildrenAsync() {
        var activities = new List<Activity>();
        using var listener = ObservabilityTestHarness.CreateActivityListener(activities);
        var localizer = CreateLocalizer();

        var value = await localizer.LocalizeAsync("heading", _noParameters, CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        value.Should().Be(TEMPLATE_MARKER, "behavior is unchanged by the hooks");

        var lookup = activities.Single(static activity => activity.OperationName == LocalizationDiagnostics.LOOKUP_ACTIVITY_NAME);
        lookup.GetTagItem(LocalizationDiagnostics.KEY_TAG_NAME).Should().Be("heading");
        lookup.GetTagItem(LocalizationDiagnostics.CULTURE_TAG_NAME).Should().Be("fr-CA");
        lookup.GetTagItem(LocalizationDiagnostics.FOUND_TAG_NAME).Should().Be(true);

        var steps = activities.Where(static activity => activity.OperationName == LocalizationDiagnostics.FALLBACK_STEP_ACTIVITY_NAME).ToList();
        steps.Should().HaveCount(3, "the canonical chain queries fr-CA, its parent fr, then the default culture en");

        var stepCultures = steps.ConvertAll(static step => (string)step.GetTagItem(LocalizationDiagnostics.CULTURE_TAG_NAME)!);
        stepCultures.Should().Equal("fr-CA", "fr", "en");
        steps[0].GetTagItem(LocalizationDiagnostics.FOUND_TAG_NAME).Should().Be(false);
        steps[1].GetTagItem(LocalizationDiagnostics.FOUND_TAG_NAME).Should().Be(false);
        steps[2].GetTagItem(LocalizationDiagnostics.FOUND_TAG_NAME).Should().Be(true);

        foreach(var step in steps) {
            step.Parent!.SpanId.Should().Be(lookup.SpanId, "every fallback step is a child of the lookup activity");
        }
    }

    /// <summary>
    /// Verifies that localize async records hits misses latency and fallback steps.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task LocalizeAsync_RecordsHitsMissesLatencyAndFallbackStepsAsync() {
        var measurements = new List<ObservabilityTestHarness.InstrumentMeasurement>();
        using var listener = ObservabilityTestHarness.CreateMeterListener(measurements);
        var localizer = CreateLocalizer();

        await localizer.LocalizeAsync("heading", _noParameters, CultureInfo.GetCultureInfo("en"), CancellationToken.None);
        await localizer.LocalizeAsync("missing", _noParameters, CultureInfo.GetCultureInfo("en"), CancellationToken.None);

        Count(LocalizationDiagnostics.LOOKUP_HITS_INSTRUMENT_NAME).Should().Be(1);
        Count(LocalizationDiagnostics.LOOKUP_MISSES_INSTRUMENT_NAME).Should().Be(1);
        Count(LocalizationDiagnostics.FALLBACK_STEPS_INSTRUMENT_NAME).Should().Be(3,
            "the hit probes the exact culture once; the miss probes the exact culture and then the default-culture stage (canonical algorithm)");

        var durations = measurements.Where(m => m.InstrumentName == LocalizationDiagnostics.LOOKUP_DURATION_INSTRUMENT_NAME).ToList();
        durations.Should().HaveCount(2, "each completed lookup records one duration sample");
        durations.Should().OnlyContain(m => m.Value > 0);
        durations.Should().OnlyContain(m => m.HasTag(LocalizationDiagnostics.CULTURE_TAG_NAME, "en"));

        int Count(string instrumentName) => measurements.Count(m => m.InstrumentName == instrumentName);
    }

    /// <summary>
    /// Verifies that localize plural async emits lookup activity.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task LocalizePluralAsync_EmitsLookupActivityAsync() {
        var activities = new List<Activity>();
        using var listener = ObservabilityTestHarness.CreateActivityListener(activities);
        var localizer = CreateLocalizer();

        var value = await localizer.LocalizePluralAsync("one.msg", "many.msg", 1m, _noParameters, CultureInfo.GetCultureInfo("en"), CancellationToken.None);

        value.Should().Be("one");

        var lookup = activities.Single(static activity => activity.OperationName == LocalizationDiagnostics.LOOKUP_ACTIVITY_NAME);
        lookup.GetTagItem(LocalizationDiagnostics.KEY_TAG_NAME).Should().Be("one.msg", "the plural pair path anchors the activity on the singular key");
    }

    /// <summary>
    /// Verifies that diagnostics template text is never is tagged.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Diagnostics_TemplateTextIsNeverTaggedAsync() {
        var activities = new List<Activity>();
        using var activityListener = ObservabilityTestHarness.CreateActivityListener(activities);
        var measurements = new List<ObservabilityTestHarness.InstrumentMeasurement>();
        using var meterListener = ObservabilityTestHarness.CreateMeterListener(measurements);
        var localizer = CreateLocalizer();

        await localizer.LocalizeAsync("heading", _noParameters, CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        foreach(var activity in activities) {
            foreach(var tag in activity.TagObjects) {
                (tag.Value?.ToString() ?? string.Empty).Should().NotContain(TEMPLATE_MARKER,
                    "template text must never appear in trace tags");
            }
        }

        foreach(var measurement in measurements) {
            foreach(var tag in measurement.Tags) {
                (tag.Value?.ToString() ?? string.Empty).Should().NotContain(TEMPLATE_MARKER,
                    "template text must never appear in metric tags");
            }
        }
    }

    /// <summary>
    /// Verifies that sampled out activities are not created and lookups still is succeed.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task SampledOutActivities_AreNotCreated_AndLookupsStillSucceedAsync() {
        var activities = new List<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = static source => source.Name == LocalizationDiagnostics.SOURCE_NAME,
            Sample = static (ref _) => ActivitySamplingResult.None,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var value = await CreateLocalizer().LocalizeAsync("heading", _noParameters, CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        value.Should().Be(TEMPLATE_MARKER, "lookups are unaffected by sampling");
        activities.Should().BeEmpty("sampled-out activities are never created");
    }

    /// <summary>
    /// Verifies that without listeners lookups behave identically.
    /// </summary>
    [TestMethod]
    public async Task WithoutListeners_LookupsBehaveIdenticallyAsync() {
        // No listeners are registered by this test; behavior must be exactly the canonical contract.
        var localizer = CreateLocalizer();

        var hit = await localizer.LocalizeAsync("heading", _noParameters, CultureInfo.GetCultureInfo("en"), CancellationToken.None);
        var miss = await localizer.LocalizeAsync("missing", _noParameters, CultureInfo.GetCultureInfo("en"), CancellationToken.None);

        hit.Should().Be(TEMPLATE_MARKER);
        miss.Should().BeNull();
    }

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer() {
        var provider = new CultureSensitiveProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["heading"] = TEMPLATE_MARKER, ["one.msg"] = "one", ["many.msg"] = "many" }
        });

        return new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });
    }

    /// <summary>
    /// Provides culture sensitive.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class CultureSensitiveProvider(Dictionary<string, Dictionary<string, string>> templates) : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                templates.TryGetValue(culture.Name, out var byKey) && byKey.TryGetValue(key, out var template) ? template : null);
        }
    }
}
