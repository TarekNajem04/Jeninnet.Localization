//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Avalonia;

/// <summary>
/// Headless verification for the Avalonia host composition.
/// </summary>
internal static class AvaloniaHostSmoke {
    /// <summary>
    /// Initializes the host culture, switches to Arabic, and verifies the localized text and RTL
    /// direction.
    /// </summary>
    /// <returns>Zero when the smoke checks pass; otherwise, one.</returns>
    public static async Task<int> RunAsync() {
        var directory = Path.Combine(Path.GetTempPath(), "jeninnet-avalonia-sample-" + Guid.NewGuid().ToString("N"));
        try {
            await using var services = AvaloniaHostComposition.BuildServiceProvider(options =>
                options.CultureStoreFilePath = Path.Combine(directory, "culture.txt"));

            var culture = services.GetRequiredService<HostCultureService>();
            var text = services.GetRequiredService<HostSampleText>();

            await culture.InitializeAsync();
            await culture.SetCultureAsync("ar");
            CultureInfo.CurrentCulture = culture.CultureInfo;
            CultureInfo.CurrentUICulture = culture.CultureInfo;

            var title = await text.GetAsync(HostSampleKeys.APP_TITLE);
            var direction = culture.ToFlowDirection();
            Console.WriteLine($"{title} | {culture.Culture} | {direction}");

            return culture.Culture == "ar"
                && culture.IsRightToLeft
                && direction == FlowDirection.RightToLeft
                && title == "مضيف تعريب Jeninnet"
                ? 0
                : 1;
        }
        finally {
            if(Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
