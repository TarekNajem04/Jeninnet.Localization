//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Avalonia;

/// <summary>
/// Registration helpers for the Avalonia localization integration.
/// </summary>
public static class AvaloniaLocalizationServiceCollectionExtensions {
    /// <summary>
    /// Registers the host culture service and a file-backed culture store for an Avalonia host.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional options delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddAvaloniaLocalization(
        this IServiceCollection services,
        Action<AvaloniaLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AvaloniaLocalizationOptions();
        configure?.Invoke(options);

        var storePath = string.IsNullOrWhiteSpace(options.CultureStoreFilePath)
            ? DefaultCultureStorePath
            : options.CultureStoreFilePath;

        services.TryAddSingleton(options);
        services.TryAddSingleton<HostCultureOptions>(options);
        services.TryAddSingleton<IHostCultureStore>(_ => new FileHostCultureStore(storePath));
        return services.AddHostCulture();
    }

    /// <summary>
    /// Gets the default culture-store path under the current user's application-data folder.
    /// </summary>
    public static string DefaultCultureStorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Jeninnet.Localization",
        "culture.txt");
}
