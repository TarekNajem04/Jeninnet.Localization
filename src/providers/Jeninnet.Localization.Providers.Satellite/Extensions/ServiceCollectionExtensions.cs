//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Satellite.Extensions;

/// <summary>
/// Provides extension methods for registering the satellite-assembly localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the satellite-assembly localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The satellite localization options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/>.BaseName is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>.ResourceAssembly is null.</exception>
    public static IServiceCollection AddSatelliteLocalization(
        this IServiceCollection services,
        SatelliteLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.BaseName);
        ArgumentNullException.ThrowIfNull(options.ResourceAssembly);

        services.AddSingleton<IInnerMessageProvider>(new SatelliteMessageProvider(options));

        return services;
    }

    /// <summary>
    /// Adds the satellite-assembly localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resourceBaseName">The base name of the compiled resources.</param>
    /// <param name="resourceAssembly">The assembly that owns the resources and next to which satellite packs are probed.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceBaseName"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceAssembly"/> is null.</exception>
    public static IServiceCollection AddSatelliteLocalization(
        this IServiceCollection services,
        string resourceBaseName,
        Assembly resourceAssembly
        ) => AddSatelliteLocalization(
            services,
            new SatelliteLocalizationOptions {
                BaseName = resourceBaseName,
                ResourceAssembly = resourceAssembly
            }
        );

    /// <summary>
    /// Adds the satellite-assembly localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddSatelliteLocalization(this IServiceCollection services, Action<SatelliteLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SatelliteLocalizationOptions();
        configure(options);
        return services.AddSatelliteLocalization(options);
    }
}
