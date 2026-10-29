//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.WinUI;

/// <summary>
/// The composition entry point for the WinUI 3 host sample.
/// </summary>
public static class WinUiHostComposition {
    /// <summary>
    /// Builds the host service provider with the shared sample content and the WinUI integration.
    /// </summary>
    /// <returns>The built service provider.</returns>
    public static ServiceProvider BuildServiceProvider() {
        var services = new ServiceCollection();
        services.AddHostSampleLocalization();
        services.AddWinUiLocalization(options => {
            options.DefaultCulture = HostSampleCultures.DEFAULT_CULTURE;
            foreach(var culture in HostSampleCultures.Supported) {
                options.SupportedCultures.Add(culture);
            }
        });
        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider();
    }
}
