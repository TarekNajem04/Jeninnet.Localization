//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Maui;

/// <summary>
/// Configuration for the MAUI localization integration.
/// </summary>
public sealed class MauiLocalizationOptions : HostCultureOptions {
    /// <summary>
    /// The default preferences key used to persist the selected culture.
    /// </summary>
    public const string DEFAULT_PREFERENCES_KEY = "Jeninnet.Localization.HostCulture";

    /// <summary>
    /// Gets or sets the MAUI preferences key used to persist the selected culture.
    /// </summary>
    public string PreferencesKey { get; set; } = DEFAULT_PREFERENCES_KEY;
}
