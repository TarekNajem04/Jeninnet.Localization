//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Pseudo.Extensions;

/// <summary>
/// Provides extension methods for registering the pseudo-localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the pseudo-localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pseudo-localization options.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
    public static IServiceCollection AddPseudoLocalization(
        this IServiceCollection services,
        PseudoLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton<IInnerMessageProvider>(new PseudoMessageProvider(options));

        return services;
    }

    /// <summary>
    /// Adds the pseudo-localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="sourceTemplates">The source templates transformed for the pseudo culture.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="sourceTemplates"/> is null.</exception>
    public static IServiceCollection AddPseudoLocalization(
        this IServiceCollection services,
        IReadOnlyDictionary<string, string> sourceTemplates
        ) {
        ArgumentNullException.ThrowIfNull(sourceTemplates);

        return services.AddPseudoLocalization(
            new PseudoLocalizationOptions {
                SourceTemplates = sourceTemplates
            }
        );
    }

    /// <summary>
    /// Adds the pseudo-localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The updated service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddPseudoLocalization(this IServiceCollection services, Action<PseudoLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new PseudoLocalizationOptions();
        configure(options);
        return services.AddPseudoLocalization(options);
    }
}
