//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// In-memory listener harness for the  observability tests: activity and meter
/// listeners sample the stable <c>Jeninnet.Localization</c> source/meter only.
/// Tests using these listeners must opt out of parallel execution (listeners are
/// process-global).
/// </summary>
internal static class ObservabilityTestHarness {
    /// <summary>
    /// Represents an instrument measurement.
    /// </summary>
    /// <param name="InstrumentName">The instrument name.</param>
    /// <param name="Value">The value.</param>
    /// <param name="Tags">The tags.</param>
    internal sealed record InstrumentMeasurement(string InstrumentName, double Value, KeyValuePair<string, object?>[] Tags) {
        /// <summary>
        /// Determines whether the value has tag.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        public bool HasTag(string name, string value) =>
            Tags.Any(tag => tag.Key == name && string.Equals(tag.Value?.ToString(), value, StringComparison.Ordinal));
    }

    /// <summary>Creates and registers an activity listener sampling the localization source.</summary>
    /// <param name="activities">The collection stopped activities are added to.</param>
    /// <returns>The listener to dispose at test end.</returns>
    public static ActivityListener CreateActivityListener(List<Activity> activities) {
        var listener = new ActivityListener {
            ShouldListenTo = static source => source.Name == LocalizationDiagnostics.SOURCE_NAME,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>Creates and starts a meter listener recording the localization meter's instruments.</summary>
    /// <param name="measurements">The collection measurements are added to.</param>
    /// <returns>The listener to dispose at test end.</returns>
    public static MeterListener CreateMeterListener(List<InstrumentMeasurement> measurements) {
        var listener = new MeterListener {
            InstrumentPublished = (instrument, activeListener) => {
                if(instrument.Meter.Name == LocalizationDiagnostics.SOURCE_NAME) {
                    activeListener.EnableMeasurementEvents(instrument, measurements);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>(
            static (instrument, value, tags, state) => ((List<InstrumentMeasurement>)state!).Add(new InstrumentMeasurement(instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>(
            static (instrument, value, tags, state) => ((List<InstrumentMeasurement>)state!).Add(new InstrumentMeasurement(instrument.Name, value, tags.ToArray())));
        listener.Start();
        return listener;
    }
}
