//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.HostSamples.Shared;

/// <summary>
/// Registers the shared localization content used by the host samples.
/// </summary>
public static class HostSampleLocalization {
    /// <summary>
    /// Adds the sample JSON provider, the default localization pipeline, and the sample text
    /// resolver.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddHostSampleLocalization(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddJsonLocalizationProviders(
            AppContext.BaseDirectory,
            [.. HostSampleCultures.Supported],
            "Localization");
        services.AddLocalization(options => options.DefaultCulture = HostSampleCultures.DEFAULT_CULTURE);
        services.AddSingleton<HostSampleText>();
        return services;
    }
}
