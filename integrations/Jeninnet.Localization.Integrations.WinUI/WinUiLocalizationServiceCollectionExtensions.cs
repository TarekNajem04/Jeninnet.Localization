//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.WinUI;

/// <summary>
/// Registration helpers for the WinUI 3 localization integration.
/// </summary>
public static class WinUiLocalizationServiceCollectionExtensions {
    /// <summary>
    /// Registers the host culture service and the application-data-backed culture store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional options delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWinUiLocalization(
        this IServiceCollection services,
        Action<WinUiLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new WinUiLocalizationOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<HostCultureOptions>(options);
        services.TryAddSingleton<IHostCultureStore>(_ =>
            new WinUiApplicationDataHostCultureStore(options.SettingsKey));
        return services.AddHostCulture();
    }
}
