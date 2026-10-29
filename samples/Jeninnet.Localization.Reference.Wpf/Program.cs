//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Wpf;

/// <summary>
/// The WPF reference host entry point.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the reference WPF host, or its smoke-based verification when <c>--smoke</c> is supplied.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    private static int Main(string[] args) {
        if(args.Contains("--smoke", StringComparer.Ordinal)) {
            return ReferenceWpfSmoke.RunAsync().GetAwaiter().GetResult();
        }

        using var services = ReferenceWpfComposition.BuildServiceProvider();

        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new MainWindow(services.GetRequiredService<BookStoreViewModel>());

        application.Run(window);

        return 0;
    }
}
