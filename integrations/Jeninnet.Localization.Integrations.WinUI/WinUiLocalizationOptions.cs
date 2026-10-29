//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.WinUI;

/// <summary>
/// Configuration for the WinUI 3 localization integration.
/// </summary>
public sealed class WinUiLocalizationOptions : HostCultureOptions {
    /// <summary>
    /// The default application-data settings key used to persist the selected culture.
    /// </summary>
    public const string DEFAULT_SETTINGS_KEY = "Jeninnet.Localization.HostCulture";

    /// <summary>
    /// Gets or sets the application-data settings key used to persist the selected culture.
    /// </summary>
    public string SettingsKey { get; set; } = DEFAULT_SETTINGS_KEY;
}
