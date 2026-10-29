//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Maui;

/// <summary>
/// Registration helpers for the MAUI localization integration.
/// </summary>
public static class MauiLocalizationServiceCollectionExtensions {
    /// <summary>
    /// Registers the host culture service and the MAUI preferences-backed culture store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional options delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddMauiLocalization(
        this IServiceCollection services,
        Action<MauiLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MauiLocalizationOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<HostCultureOptions>(options);
        services.TryAddSingleton<IHostCultureStore>(_ =>
            new MauiPreferencesHostCultureStore(options.PreferencesKey));
        return services.AddHostCulture();
    }
}
