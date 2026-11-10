//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// The translated snapshot pulled for one culture: the served templates plus the
/// translator notes the vendor carries, both keyed by catalog key.
/// </summary>
public sealed class TmsPullResult {
    /// <summary>
    /// Gets the culture the snapshot was pulled for.
    /// </summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets the vendor's source language for this snapshot (for example <c>en</c>).
    /// </summary>
    public required string SourceLanguage { get; init; }

    /// <summary>
    /// Gets the translated templates keyed by catalog key. A null template means the
    /// vendor carries the key but has no translation yet (an untranslated unit).
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Templates { get; init; }

    /// <summary>
    /// Gets the translator notes keyed by catalog key. Vendors without note storage
    /// report an empty map; notes never gate application.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Notes { get; init; }
}
