//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Diagnostics;

/// <summary>
/// Health check for the configured message provider: resolves an
/// explicit probe key for a culture through the canonical <see cref="IMessageProvider"/>.
/// </summary>
/// <remarks>
/// States: the probe key resolves → <see cref="HealthStatus.Healthy"/>; the provider is
/// reachable but does not serve the key → <see cref="HealthStatus.Degraded"/> (documented
/// reason); the provider throws → <see cref="HealthStatus.Unhealthy"/> (the exception type
/// is reported in the result data). Template text and exception messages never appear in
/// the result description/data (PII caution).
/// </remarks>
public sealed class LocalizationProviderHealthCheck : IHealthCheck {
    private readonly IMessageProvider _provider;
    private readonly LocalizationHealthChecksOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationProviderHealthCheck"/> class.
    /// </summary>
    /// <param name="provider">The canonical message provider to probe.</param>
    /// <param name="options">The health-check options (probe key and culture).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or <paramref name="options"/> is null.</exception>
    public LocalizationProviderHealthCheck(IMessageProvider provider, LocalizationHealthChecksOptions options) {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        _provider = provider;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var probeKey = _options.ProviderProbeKey;
        if(string.IsNullOrEmpty(probeKey)) {
            return HealthCheckResult.Degraded("No provider probe key is configured.");
        }

        var culture = string.IsNullOrEmpty(_options.ProviderProbeCulture)
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(_options.ProviderProbeCulture);

        try {
            var stopwatch = Stopwatch.StartNew();
            var template = await _provider.GetMessageTemplateAsync(probeKey, culture, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var data = new Dictionary<string, object> {
                ["probeKey"] = probeKey,
                ["culture"] = culture.Name,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
            };

            return template is null
                ? HealthCheckResult.Degraded(
                    $"The provider is reachable but does not serve probe key '{probeKey}' for culture '{culture.Name}'.",
                    data: data)
                : HealthCheckResult.Healthy(
                    $"The provider resolved probe key '{probeKey}' for culture '{culture.Name}'.",
                    data);
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            return HealthCheckResult.Unhealthy(
                $"The provider probe failed for key '{probeKey}' in culture '{culture.Name}'.",
                exception,
                new Dictionary<string, object> {
                    ["probeKey"] = probeKey,
                    ["culture"] = culture.Name,
                    ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name
                });
        }
    }
}
