//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Consumer;

/// <summary>
/// The consumer's end-to-end scenario, driven only by the packed packages: the canonical
/// pipeline over the Memory provider keyed by the generator-emitted constants, the same
/// pipeline over per-culture JSON catalogs, and a CSV translator hand-off. Every step
/// would fail if a package payload, dependency, or analyzer/generator asset were missing.
/// </summary>
public static class CheckoutScenario {
    /// <summary>
    /// Runs the scenario.
    /// </summary>
    /// <param name="baseDirectory">The directory whose <c>Localization</c> subdirectory holds the JSON catalogs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The localized strings and the translator CSV.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseDirectory"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a catalog does not resolve the scenario keys.</exception>
    public static async Task<CheckoutResult> RunAsync(string baseDirectory, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal) { ["customer"] = "Lina" };
        var english = CultureInfo.GetCultureInfo("en");
        var arabic = CultureInfo.GetCultureInfo("ar");

        // 1) Canonical pipeline over the Memory provider, keyed by the generated constants.
        //    The Core registration is called explicitly: the framework ships an unrelated
        //    AddLocalization for the ASP.NET Core localization services in the same namespace.
        var memoryServices = new ServiceCollection();
        JeninnetLocalizationExtensions.AddLocalization(memoryServices);
        memoryServices.AddMemoryLocalization(options => options.Messages = new Dictionary<string, string>(StringComparer.Ordinal) {
            [LocalizationKeys.CHECKOUT_THANK_YOU] = "Thank you, {customer}!",
            [LocalizationKeys.CHECKOUT_TOTAL] = "Total: {amount}",
        });

        await using var memoryProvider = memoryServices.BuildServiceProvider();
        var memoryLocalizer = memoryProvider.GetRequiredService<IMessageLocalizer>();

        var thanks = await memoryLocalizer.LocalizeAsync(LocalizationKeys.CHECKOUT_THANK_YOU, parameters, english, cancellationToken)
            ?? throw new InvalidOperationException($"The memory catalog does not serve '{LocalizationKeys.CHECKOUT_THANK_YOU}'.");
        var total = await memoryLocalizer.LocalizeAsync(
                LocalizationKeys.CHECKOUT_TOTAL,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["amount"] = "133.38" },
                english,
                cancellationToken)
            ?? throw new InvalidOperationException($"The memory catalog does not serve '{LocalizationKeys.CHECKOUT_TOTAL}'.");

        // 2) The same canonical pipeline over per-culture JSON catalogs (a separate container:
        //    exactly one provider is registered per pipeline).
        var fileServices = new ServiceCollection();
        JeninnetLocalizationExtensions.AddLocalization(fileServices);
        fileServices.AddJsonLocalizationProviders(baseDirectory, ["en", "ar"]);

        await using var fileProvider = fileServices.BuildServiceProvider();
        var fileLocalizer = fileProvider.GetRequiredService<IMessageLocalizer>();

        var arabicThanks = await fileLocalizer.LocalizeAsync(LocalizationKeys.CHECKOUT_THANK_YOU, parameters, arabic, cancellationToken)
            ?? throw new InvalidOperationException($"The 'ar' JSON catalog does not serve '{LocalizationKeys.CHECKOUT_THANK_YOU}'.");

        // 3) Translator hand-off: CSV export of the resolved source/target pair.
        var source = new Dictionary<string, string?>(StringComparer.Ordinal) {
            [LocalizationKeys.CHECKOUT_THANK_YOU] = "Thank you, {customer}!",
        };
        var target = new Dictionary<string, string?>(StringComparer.Ordinal) {
            [LocalizationKeys.CHECKOUT_THANK_YOU] = arabicThanks,
        };
        var csv = CsvCatalogInterchange.ExportCatalog(source, target, "en", "ar");

        return new CheckoutResult(thanks, total, arabicThanks, csv);
    }
}
