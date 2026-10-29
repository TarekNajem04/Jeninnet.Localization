//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.WinForms;

/// <summary>
/// The WinForms reference host entry point.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the reference WinForms host, or its smoke-based verification when <c>--smoke</c> is supplied.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    private static int Main(string[] args) {
        if(args.Contains("--smoke", StringComparer.Ordinal)) {
            return ReferenceWinFormsSmoke.RunAsync().GetAwaiter().GetResult();
        }

        using var services = ReferenceWinFormsComposition.BuildServiceProvider();

        ApplicationConfiguration.Initialize();

        var form = new MainForm(services);
        Application.Run(form);
        form.Dispose();

        return 0;
    }
}
