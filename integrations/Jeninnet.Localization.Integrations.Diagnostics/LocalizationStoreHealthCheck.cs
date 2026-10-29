//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Diagnostics;

/// <summary>
/// Health check for the configured localization store: probes
/// reachability through <see cref="ILocalizationStore.GetSupportedCulturesAsync"/>.
/// </summary>
/// <remarks>
/// States: the store answers with cultures → <see cref="HealthStatus.Healthy"/>; the store
/// is reachable but reports no supported cultures → <see cref="HealthStatus.Degraded"/>
/// (documented reason); the store throws → <see cref="HealthStatus.Unhealthy"/> (the
/// exception type is reported in the result data). Only counts — never store content —
/// appear in the result data.
/// </remarks>
public sealed class LocalizationStoreHealthCheck : IHealthCheck {
    private readonly ILocalizationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationStoreHealthCheck"/> class.
    /// </summary>
    /// <param name="store">The localization store to probe.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
    public LocalizationStoreHealthCheck(ILocalizationStore store) {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            var stopwatch = Stopwatch.StartNew();
            var cultures = await _store.GetSupportedCulturesAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var data = new Dictionary<string, object> {
                ["supportedCultureCount"] = cultures.Count,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
            };

            return cultures.Count == 0
                ? HealthCheckResult.Degraded("The store is reachable but reports no supported cultures.", data: data)
                : HealthCheckResult.Healthy($"The store is reachable ({cultures.Count} supported cultures).", data);
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            return HealthCheckResult.Unhealthy(
                "The store reachability probe failed.",
                exception,
                new Dictionary<string, object> {
                    ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name
                });
        }
    }
}
