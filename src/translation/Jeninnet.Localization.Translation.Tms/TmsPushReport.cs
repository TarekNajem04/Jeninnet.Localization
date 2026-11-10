//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// One pushed key: its catalog key, the interchange <see cref="CatalogKeyStatus"/>
/// outcome, and a human-readable detail.
/// </summary>
public sealed class TmsPushEntry {
    /// <summary>
    /// Gets the catalog key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the push outcome reusing the interchange conflict vocabulary.
    /// </summary>
    public required CatalogKeyStatus Status { get; init; }

    /// <summary>
    /// Gets the human-readable detail.
    /// </summary>
    public required string Detail { get; init; }
}

/// <summary>
/// The per-key outcome of a push: updated keys, conflicts, and whether anything
/// was uploaded (never on dry-run, never when nothing changed).
/// </summary>
public sealed class TmsPushReport {
    /// <summary>
    /// Gets the target culture the push addressed.
    /// </summary>
    public required CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets whether this report came from a dry-run preview (nothing uploaded).
    /// </summary>
    public required bool DryRun { get; init; }

    /// <summary>
    /// Gets the per-key outcomes in ordinal key order.
    /// </summary>
    public required IReadOnlyList<TmsPushEntry> Entries { get; init; }

    /// <summary>
    /// Gets the count of entries with status <see cref="CatalogKeyStatus.Updated"/>.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the count of entries with status <see cref="CatalogKeyStatus.UnknownKey"/>
    /// or <see cref="CatalogKeyStatus.StaleSource"/>.
    /// </summary>
    public required int ConflictCount { get; init; }

    /// <summary>
    /// Gets the uploaded file name when a live push uploaded a file, or
    /// <see langword="null"/> on dry-run and no-change pushes.
    /// </summary>
    public string? UploadedFileName { get; init; }
}
