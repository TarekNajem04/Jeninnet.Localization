//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Json.Options;

/// <summary>
/// Options for JSON localization provider.
/// </summary>
public class JsonLocalizationOptions {
    /// <summary>
    /// Gets or sets the path to the JSON file containing validation messages.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the provider watches the file for on-disk changes and
    /// reloads them without restarts. Opt-in only; the default
    /// <see langword="false"/> preserves the static snapshot behavior.
    /// </summary>
    public bool WatchForChanges { get; set; }

    /// <summary>
    /// Gets or sets the quiet window that must pass before a watched change triggers a
    /// reload. Defaults to 500 milliseconds. Must be positive when
    /// <see cref="WatchForChanges"/> is enabled.
    /// </summary>
    public TimeSpan ReloadDebounce { get; set; } = TimeSpan.FromMilliseconds(500);
}
