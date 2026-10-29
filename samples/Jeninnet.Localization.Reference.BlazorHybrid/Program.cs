//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.BlazorHybrid;

/// <summary>
/// The Blazor Hybrid reference host entry point.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the reference Blazor Hybrid host, or its smoke-based verification when <c>--smoke</c>
    /// is supplied.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    private static int Main(string[] args) {
        if(args.Contains("--smoke", StringComparer.Ordinal)) {
            return ReferenceBlazorHybridSmoke.RunAsync().GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();

        var defaultCulture = CultureInfo.GetCultureInfo(ReferenceCultures.DEFAULT_CULTURE);
        CultureInfo.CurrentCulture = defaultCulture;
        CultureInfo.CurrentUICulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

        using var services = ReferenceBlazorHybridComposition.BuildServiceProvider();

        var form = new MainForm(services);
        Application.Run(form);
        form.Dispose();

        return 0;
    }
}
