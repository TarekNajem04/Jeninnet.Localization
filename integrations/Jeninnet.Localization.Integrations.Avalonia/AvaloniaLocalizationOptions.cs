//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Avalonia;

/// <summary>
/// Configuration for the Avalonia localization integration.
/// </summary>
public sealed class AvaloniaLocalizationOptions : HostCultureOptions {
    /// <summary>
    /// Gets or sets the file used to persist the selected culture. When unset,
    /// <see cref="AvaloniaLocalizationServiceCollectionExtensions.DefaultCultureStorePath"/> is used.
    /// </summary>
    public string? CultureStoreFilePath { get; set; }
}
