//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po.Options;

/// <summary>
/// Options for the PO (Portable Object) localization provider.
/// </summary>
public class PoLocalizationOptions {
    /// <summary>
    /// Gets or sets the path to the catalog file containing localized messages
    /// (PO text or GNU MO (Machine Object) binary,).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the catalog file is read:
    /// <see cref="PoCatalogFormat.Auto"/> (default) selects by file extension
    /// (<c>.mo</c> → GNU MO binary reader, otherwise the PO text reader), while
    /// <see cref="PoCatalogFormat.Po"/> / <see cref="PoCatalogFormat.Mo"/> force a reader.
    /// The default location provider (<c>{culture}.po</c>) never probes for <c>.mo</c> files;
    /// compiled catalogs are configured through explicit paths or a custom location provider.
    /// </summary>
    public PoCatalogFormat Format { get; set; } = PoCatalogFormat.Auto;

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
