//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Xml.Extensions;

/// <summary>
/// Provides extension methods for registering XML localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds XML localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The XML localization options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/>.FilePath is null or empty.</exception>
    public static IServiceCollection AddXmlLocalization(
        this IServiceCollection services,
        XmlLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.FilePath);

        return ProviderRegistration.AddFileProvider(services, options.FilePath, "XML", () => new XmlMessageProvider(options));
    }

    /// <summary>
    /// Adds XML localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    public static IServiceCollection AddXmlLocalization(
        this IServiceCollection services,
        string filePath
        ) => AddXmlLocalization(
            services,
            new XmlLocalizationOptions {
                FilePath = filePath
            }
        );

    /// <summary>
    /// Adds XML localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddXmlLocalization(this IServiceCollection services, Action<XmlLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new XmlLocalizationOptions();
        configure(options);
        return services.AddXmlLocalization(options);
    }

    // ─── Multi-culture registration ─────────────────────────────────────

    /// <summary>
    /// Registers XML localization providers for multiple cultures using a
    /// convention of <c>{baseDirectory}/{subdirectory}/{culture}.xml</c>.
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
    public static IServiceCollection AddXmlLocalizationProviders(
        this IServiceCollection services,
        string baseDirectory,
        string[] cultures,
        string subdirectory = "Localization"
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentNullException.ThrowIfNull(cultures);

        return ProviderRegistration.AddCultureProviders(
            services,
            cultures,
            culture => Path.Combine(baseDirectory, subdirectory, $"{culture}.xml"),
            static path => new XmlMessageProvider(path));
    }

    /// <summary>
    /// Registers XML localization providers for all cultures discovered by
    /// scanning <c>{baseDirectory}/{subdirectory}</c> for <c>*.xml</c> files.
    /// File names (without extension) are used as culture codes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseDirectory">The base directory containing localization folders.</param>
    /// <param name="subdirectory">The subdirectory under baseDirectory (default "Localization").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the localization directory does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no XML files are found.</exception>
    public static IServiceCollection AddXmlLocalizationProviders(
        this IServiceCollection services,
        string baseDirectory,
        string subdirectory = "Localization"
    ) => ProviderRegistration.AddDiscoveredProviders(
            services,
            baseDirectory,
            subdirectory,
            ".xml",
            "XML",
            static path => new XmlMessageProvider(path));

    /// <summary>
    /// Registers XML localization providers using a builder for full control
    /// over per-culture file paths.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate that configures the builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddXmlLocalizationProviders(
        this IServiceCollection services,
        Action<XmlLocalizationProviderBuilder> configure
    ) => ProviderRegistration.AddBuilderProvider(services, configure, static builder => builder.Build());
}
