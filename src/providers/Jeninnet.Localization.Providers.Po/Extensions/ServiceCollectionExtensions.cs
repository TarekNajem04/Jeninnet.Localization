//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po.Extensions;

/// <summary>
/// Provides extension methods for registering PO (Portable Object) localization providers.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds PO localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The options for PO localization.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/>.FilePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified PO file is not found.</exception>
    public static IServiceCollection AddPoLocalization(
        this IServiceCollection services,
        PoLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.FilePath);

        return ProviderRegistration.AddFileProvider(services, options.FilePath, "PO", () => new PoMessageProvider(options));
    }

    /// <summary>
    /// Adds PO localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="filePath">The path to the PO file containing validation messages.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified PO file is not found.</exception>
    public static IServiceCollection AddPoLocalization(
        this IServiceCollection services,
        string filePath
    ) => AddPoLocalization(
        services,
        new PoLocalizationOptions {
            FilePath = filePath
        }
    );

    /// <summary>
    /// Adds PO localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified PO file is not found.</exception>
    public static IServiceCollection AddPoLocalization(this IServiceCollection services, Action<PoLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new PoLocalizationOptions();
        configure(options);
        return services.AddPoLocalization(options);
    }

    /// <summary>
    /// Registers PO localization providers for multiple cultures using a
    /// convention of <c>{baseDirectory}/{subdirectory}/{culture}.po</c>.
    /// Each provider is wrapped in a <see cref="CultureAwareMessageProvider"/>,
    /// which dispatches by exact culture name and then by two-letter ISO language
    /// name (no provider-level fallback).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="cultures">The culture codes to register (e.g., ["en", "ar"]).</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/>, <paramref name="cultures"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty, or <paramref name="cultures"/> is empty or contains null/empty culture.</exception>
    public static IServiceCollection AddPoLocalizationProviders(
        this IServiceCollection services,
        string baseDirectory,
        string[] cultures,
        string subdirectory = "Localization"
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentNullException.ThrowIfNull(cultures);

        var locations = new DefaultPoFileLocationProvider(baseDirectory, subdirectory);
        return ProviderRegistration.AddCultureProviders(
            services,
            cultures,
            culture => locations.GetPoFilePath(new CultureInfo(culture)),
            static path => new PoMessageProvider(path));
    }

    /// <summary>
    /// Registers PO localization providers for multiple cultures using a custom
    /// <see cref="IPoFileLocationProvider"/> (OrchardCore location-provider SPI idea).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="locationProvider">The location provider resolving per-culture PO paths.</param>
    /// <param name="cultures">The culture codes to register.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/>, <paramref name="locationProvider"/>, or <paramref name="cultures"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultures"/> is empty or contains null/empty culture.</exception>
    public static IServiceCollection AddPoLocalizationProviders(
        this IServiceCollection services,
        IPoFileLocationProvider locationProvider,
        string[] cultures
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(locationProvider);
        ArgumentNullException.ThrowIfNull(cultures);

        return ProviderRegistration.AddCultureProviders(
            services,
            cultures,
            culture => locationProvider.GetPoFilePath(new CultureInfo(culture)),
            static path => new PoMessageProvider(path));
    }

    /// <summary>
    /// Registers PO localization providers for all cultures discovered by
    /// scanning <c>{baseDirectory}/{subdirectory}</c> for <c>*.po</c> files.
    /// File names (without extension) are used as culture codes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the localization directory does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no PO files are found.</exception>
    public static IServiceCollection AddPoLocalizationProviders(
        this IServiceCollection services,
        string baseDirectory,
        string subdirectory = "Localization"
    ) => ProviderRegistration.AddDiscoveredProviders(
            services,
            baseDirectory,
            subdirectory,
            ".po",
            "PO",
            static path => new PoMessageProvider(path));

    /// <summary>
    /// Registers PO localization providers using a builder for full control
    /// over per-culture file paths.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate that configures the builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddPoLocalizationProviders(
        this IServiceCollection services,
        Action<PoLocalizationProviderBuilder> configure
    ) => ProviderRegistration.AddBuilderProvider(services, configure, static builder => builder.Build());
}
