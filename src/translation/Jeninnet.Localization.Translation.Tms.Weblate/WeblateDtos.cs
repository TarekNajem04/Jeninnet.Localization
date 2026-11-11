//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Weblate;

/// <summary>
/// One Weblate translation-units list page: the unit rows plus the page cursor.
/// </summary>
internal sealed class WeblateUnitListResponse {
    /// <summary>
    /// Gets or sets the unit rows.
    /// </summary>
    [JsonPropertyName("results")]
    public List<WeblateUnit> Results { get; set; } = [];

    /// <summary>
    /// Gets or sets the next page URL, or null on the last page.
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

/// <summary>
/// One Weblate translation unit: the source forms, the target forms, the key
/// context, the translator note, and the unit state.
/// </summary>
internal sealed class WeblateUnit {
    /// <summary>
    /// Gets or sets the numeric unit id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the source forms (more than one for plural units).
    /// </summary>
    [JsonPropertyName("source")]
    public List<string> Source { get; set; } = [];

    /// <summary>
    /// Gets or sets the target forms (empty when the unit is untranslated).
    /// </summary>
    [JsonPropertyName("target")]
    public List<string> Target { get; set; } = [];

    /// <summary>
    /// Gets or sets the unit context (the catalog-key carrier), or empty when the
    /// component keys units by source text.
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }

    /// <summary>
    /// Gets or sets the translator note, or empty when the vendor carries none.
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the unit state (0 untranslated, 10 needs editing, 20
    /// translated, 30 approved, 100 read-only).
    /// </summary>
    [JsonPropertyName("state")]
    public int State { get; set; }
}

/// <summary>
/// A Weblate file-upload response: the accepted/skipped/not-found/total counters
/// the synchronous upload reports. Every counter is optional so older and newer
/// servers deserialize the same way.
/// </summary>
internal sealed class WeblateUploadResponse {
    /// <summary>
    /// Gets or sets the accepted units count.
    /// </summary>
    [JsonPropertyName("accepted")]
    public int Accepted { get; set; }

    /// <summary>
    /// Gets or sets the skipped units count.
    /// </summary>
    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets the not-found units count.
    /// </summary>
    [JsonPropertyName("not_found")]
    public int NotFound { get; set; }

    /// <summary>
    /// Gets or sets the total processed units count.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}
