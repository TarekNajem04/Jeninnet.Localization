//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Docs;

/// <summary>
/// Runs the documentation quick start against the real packages and reports a process exit code.
/// Compile-only snippets live in <see cref="CompileOnlySnippets"/> and are never executed.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the application entry point.
    /// </summary>
    /// <returns>A task whose result is main.</returns>
    private static async Task<int> Main() {
        try {
            await RunQuickStartAsync();
            await Console.Out.WriteLineAsync("DOCS SNIPPETS OK");
            return 0;
        }
        catch(Exception exception) {
            await Console.Error.WriteLineAsync("DOCS SNIPPETS FAILED: " + exception);
            return 1;
        }
    }

    /// <summary>
    /// Mirrors docs/en/01-getting-started/03-quick-start.md: the canonical pipeline over per-culture
    /// JSON, exact culture, fallback, missing key, and plural selection.
    /// </summary>
    private static async Task RunQuickStartAsync() {
        var services = new ServiceCollection();

        services.AddLocalization(options => options.DefaultCulture = "en");
        services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);

        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        var parameters = new Dictionary<string, object?> { ["name"] = "Ada" };

        var english = await localizer.LocalizeAsync("Greeting", parameters, new CultureInfo("en"));
        Require(english == "Hello, Ada!", "English greeting", english);

        var arabic = await localizer.LocalizeAsync("Greeting", parameters, new CultureInfo("ar"));
        Require(arabic == "مرحباً، Ada!", "Arabic greeting", arabic);

        var fallback = await localizer.LocalizeAsync("Greeting", parameters, new CultureInfo("fr"));
        Require(fallback == "Hello, Ada!", "default-culture fallback", fallback);

        var missing = await localizer.LocalizeAsync(
            "Missing.Key",
            new Dictionary<string, object?>(),
            new CultureInfo("en"));
        Require(missing is null, "missing key returns null", missing);

        var plural = await localizer.LocalizePluralAsync(
            "Cart.One",
            "Cart.Other",
            2,
            new Dictionary<string, object?> { ["count"] = 2 },
            new CultureInfo("en"));
        Require(plural == "You have 2 items.", "plural selection", plural);
    }

    /// <summary>
    /// Performs the require operation.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="actual">The actual value.</param>
    private static void Require(bool condition, string scenario, string? actual) {
        if(!condition) {
            throw new InvalidOperationException(
                $"Quick-start verification failed for '{scenario}'. Actual: '{actual ?? "(null)"}'.");
        }
    }
}
