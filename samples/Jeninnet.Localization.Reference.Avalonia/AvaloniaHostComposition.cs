//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Avalonia;

/// <summary>
/// The composition entry point for the Avalonia host sample.
/// </summary>
public static class AvaloniaHostComposition {
    /// <summary>
    /// Builds the host service provider with the shared sample content and the Avalonia integration.
    /// </summary>
    /// <param name="configure">An optional Avalonia localization configuration.</param>
    /// <returns>The built service provider.</returns>
    public static ServiceProvider BuildServiceProvider(Action<AvaloniaLocalizationOptions>? configure = null) {
        var services = new ServiceCollection();
        services.AddHostSampleLocalization();
        services.AddAvaloniaLocalization(options => {
            options.DefaultCulture = HostSampleCultures.DEFAULT_CULTURE;
            foreach(var culture in HostSampleCultures.Supported) {
                options.SupportedCultures.Add(culture);
            }

            configure?.Invoke(options);
        });
        return services.BuildServiceProvider();
    }
}
