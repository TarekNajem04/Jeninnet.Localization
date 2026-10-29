//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.ResX.Extensions;

/// <summary>
/// Provides extension methods for registering RESX localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds RESX localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The RESX localization options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddResXLocalization(
        this IServiceCollection services,
        ResXLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.BaseName);
        ArgumentNullException.ThrowIfNull(options.ResourceAssembly);

        services.AddSingleton<IInnerMessageProvider>(new ResXMessageProvider(options));

        return services;
    }

    /// <summary>
    /// Adds RESX localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resourceBaseName">The base name of the resource file.</param>
    /// <param name="resourceAssembly">The assembly containing the resource file.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddResXLocalization(
        this IServiceCollection services,
        string resourceBaseName,
        Assembly resourceAssembly
        ) => AddResXLocalization(
            services,
            new ResXLocalizationOptions {
                BaseName = resourceBaseName,
                ResourceAssembly = resourceAssembly
            }
        );

    /// <summary>
    /// Adds RESX localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddResXLocalization(this IServiceCollection services, Action<ResXLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ResXLocalizationOptions();
        configure(options);
        return services.AddResXLocalization(options);
    }
}
