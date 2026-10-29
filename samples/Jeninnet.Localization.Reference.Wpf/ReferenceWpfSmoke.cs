//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Wpf;

/// <summary>
/// Performs a headless, console-level verification of the WPF host composition so the canonical
/// pipeline can be exercised without a GUI session or UI automation.
/// </summary>
internal static class ReferenceWpfSmoke {
    /// <summary>
    /// Resolves the host composition, prints culturally switched reference content and order
    /// messages for every supported culture, and returns a process exit code.
    /// </summary>
    /// <returns>A process exit code; always zero when the smoke completes.</returns>
    public static async Task<int> RunAsync() {
        await using var provider = ReferenceWpfComposition.BuildServiceProvider();

        Console.WriteLine("Jeninnet.Localization.Reference.Wpf --smoke");
        Console.WriteLine("Provider: " + ResolveProviderName());
        Console.WriteLine();

        var viewModel = provider.GetRequiredService<BookStoreViewModel>();

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            viewModel.CurrentCulture = cultureCode;

            var expectedDirection = new CultureInfo(cultureCode).TextInfo.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
            if(viewModel.FlowDirection != expectedDirection) {
                await Console.Error.WriteLineAsync(
                    $"[{cultureCode}] unexpected flow direction: {viewModel.FlowDirection} (expected {expectedDirection})").ConfigureAwait(false);
                return 1;
            }

            var localizer = provider.GetRequiredService<IStringLocalizer<PageResources>>();
            var messageLocalizer = provider.GetRequiredService<IMessageLocalizer>();
            var orderMessage = await messageLocalizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                new Dictionary<string, object?> {
                    ["0"] = "ORD-0001",
                    ["1"] = 2,
                    ["2"] = 39.98m
                });

            Console.WriteLine($"[{cultureCode}] heading          : " + localizer[ReferenceKeys.CATALOG_HEADING]);
            Console.WriteLine($"[{cultureCode}] title            : " + localizer[ReferenceKeys.APPLICATION_TITLE]);
            Console.WriteLine($"[{cultureCode}] flowDirection    : " + viewModel.FlowDirection);
            Console.WriteLine($"[{cultureCode}] demoShowcase     : " + localizer[ReferenceKeys.DEMO_SHOWCASE]);
            Console.WriteLine($"[{cultureCode}] regionalNote     : " + localizer[ReferenceKeys.DEMO_REGIONAL_NOTE]);
            Console.WriteLine($"[{cultureCode}] missingEverywhere: " + localizer[ReferenceKeys.DEMO_MISSING_EVERYWHERE]);
            Console.WriteLine($"[{cultureCode}] orderMessage     : " + orderMessage);
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Resolves provider name.
    /// </summary>
    /// <returns>The provider name text.</returns>
    private static string ResolveProviderName() {
        var provider = Environment.GetEnvironmentVariable("ReferenceLocalization__Provider");
        return string.IsNullOrWhiteSpace(provider) ? ReferenceProviderNames.JSON : provider;
    }
}
