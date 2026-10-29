//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Maui;

/// <summary>
/// Builds the MAUI host application.
/// </summary>
public static class MauiProgram {
    /// <summary>
    /// Creates the MAUI application and registers the sample localization composition.
    /// </summary>
    /// <returns>The configured MAUI application.</returns>
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddHostSampleLocalization();
        builder.Services.AddMauiLocalization(options => {
            options.DefaultCulture = HostSampleCultures.DEFAULT_CULTURE;
            foreach(var culture in HostSampleCultures.Supported) {
                options.SupportedCultures.Add(culture);
            }
        });
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
