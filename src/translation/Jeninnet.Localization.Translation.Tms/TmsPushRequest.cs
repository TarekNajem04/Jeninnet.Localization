//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// One push of translated catalogs: the target culture, the source templates used
/// for governance (unknown-key and stale-source detection), the proposed translated
/// templates, the upload file shape, and whether anything may be uploaded.
/// </summary>
public sealed class TmsPushRequest {
    /// <summary>
    /// Gets the target culture the translations belong to.
    /// </summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets the current source-language templates (key governance: keys absent here
    /// that the vendor does not know report <see cref="CatalogKeyStatus.UnknownKey"/>,
    /// and vendor source text differing from these reports
    /// <see cref="CatalogKeyStatus.StaleSource"/>).
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Source { get; init; }

    /// <summary>
    /// Gets the proposed translated templates. Empty values are skipped (existing
    /// vendor translations are never cleared) and report
    /// <see cref="CatalogKeyStatus.Skipped"/>.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Target { get; init; }

    /// <summary>
    /// Gets the translator notes to carry in the uploaded file (PO <c>#.</c> comments,
    /// XLIFF translator notes). Null or empty values are skipped.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Notes { get; init; }

    /// <summary>
    /// Gets the upload file shape. Defaults to <see cref="TmsCatalogFormat.Po"/>.
    /// </summary>
    public TmsCatalogFormat Format { get; init; } = TmsCatalogFormat.Po;

    /// <summary>
    /// Gets whether the connector compares and reports without uploading anything.
    /// Defaults to <see langword="false"/> (live push).
    /// </summary>
    public bool DryRun { get; init; }
}
