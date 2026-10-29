//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Diagnostics.Extensions;

/// <summary>
/// Registers the localization health checks.
/// </summary>
public static class LocalizationHealthChecksExtensions {
    /// <summary>
    /// Adds the provider and store health checks to the health-check pipeline.
    /// </summary>
    /// <remarks>
    /// The provider check registers only when <see cref="LocalizationHealthChecksOptions.ProviderProbeKey"/>
    /// is configured (the probe never dumps undefined keys); it probes the canonical
    /// <see cref="IMessageProvider"/> exact-match contract. The store check registers only when an
    /// <see cref="ILocalizationStore"/> is already registered in the service collection, so call this
    /// method after the host's store registration when the store should be probed.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured options are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the composition is registered twice.</exception>
    public static IServiceCollection AddLocalizationHealthChecks(this IServiceCollection services, Action<LocalizationHealthChecksOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(LocalizationHealthChecksOptions))) {
            throw new InvalidOperationException(
                $"Localization health checks are already registered in the service collection. Configure them through a single {nameof(AddLocalizationHealthChecks)} call.");
        }

        var options = new LocalizationHealthChecksOptions();
        configure?.Invoke(options);

        ValidateOptions(options);

        services.AddSingleton(options);

        var builder = services.AddHealthChecks();
        if(!string.IsNullOrEmpty(options.ProviderProbeKey)) {
            _ = builder.AddCheck<LocalizationProviderHealthCheck>(options.ProviderCheckName);
        }

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(ILocalizationStore))) {
            _ = builder.AddCheck<LocalizationStoreHealthCheck>(options.StoreCheckName);
        }

        return services;
    }

    /// <summary>
    /// Validates options.
    /// </summary>
    /// <param name="options">The options instance.</param>
    private static void ValidateOptions(LocalizationHealthChecksOptions options) {
        if(options.ProviderProbeKey is not null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.ProviderProbeKey);
        }

        if(!string.IsNullOrEmpty(options.ProviderProbeCulture)) {
            _ = CultureInfo.GetCultureInfo(options.ProviderProbeCulture);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProviderCheckName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StoreCheckName);
    }
}
