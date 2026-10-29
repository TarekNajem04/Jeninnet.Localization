//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Avalonia;

/// <summary>
/// The Avalonia host application.
/// </summary>
public class App : Application {
    /// <summary>
    /// Gets or sets the host service provider.
    /// </summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted() {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow(
                Services.GetRequiredService<HostCultureService>(),
                Services.GetRequiredService<HostSampleText>());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
