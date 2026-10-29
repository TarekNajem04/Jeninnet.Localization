//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Avalonia;

/// <summary>
/// The Avalonia reference host entry point.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the Avalonia host, or its headless smoke verification when <c>--smoke</c> is supplied.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    private static int Main(string[] args) {
        if(args.Contains("--smoke", StringComparer.Ordinal)) {
            return AvaloniaHostSmoke.RunAsync().GetAwaiter().GetResult();
        }

        using var services = AvaloniaHostComposition.BuildServiceProvider();
        App.Services = services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Builds the Avalonia application.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
