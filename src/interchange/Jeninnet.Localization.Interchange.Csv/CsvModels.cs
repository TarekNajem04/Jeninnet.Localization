//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Csv;

/// <summary>
/// One CSV interchange row: a catalog key, the culture the value belongs
/// to, the template value, and the key-level translator note (empty cells parse as
/// <see langword="null"/>).
/// </summary>
public sealed class CsvRow {
    /// <summary>
    /// Gets the catalog key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the culture code the value belongs to.
    /// </summary>
    public required string Culture { get; init; }

    /// <summary>
    /// Gets the template value (empty is a valid stored value).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the key-level translator note, or <see langword="null"/> when the
    /// row carries none.
    /// </summary>
    public string? Note { get; init; }
}

/// <summary>
/// A parsed CSV interchange document: the rows in file order plus the file path used
/// in diagnostics.
/// </summary>
public sealed class CsvDocument {
    /// <summary>
    /// Gets the file path used in diagnostics (set by
    /// <see cref="CsvCatalogInterchange.ParseDocument(string, string)"/>).
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the rows in file order.
    /// </summary>
    public required IReadOnlyList<CsvRow> Rows { get; init; }
}

/// <summary>
/// Options for a CSV import.
/// </summary>
public sealed class CsvImportOptions {
    /// <summary>
    /// Gets whether the import only computes the report without changing anything.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// The per-key outcome of a CSV import.
/// </summary>
public sealed class CsvKeyReport {
    /// <summary>
    /// Gets the catalog key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the outcome (the shared interchange conflict policy,
    /// <see cref="CatalogKeyStatus"/>).
    /// </summary>
    public required CatalogKeyStatus Status { get; init; }

    /// <summary>
    /// Gets the source-culture value seen in the document, or <see langword="null"/> when absent.
    /// </summary>
    public required string? Source { get; init; }

    /// <summary>
    /// Gets the target-culture value seen in the document, or <see langword="null"/> when absent.
    /// </summary>
    public required string? Target { get; init; }

    /// <summary>
    /// Gets the translator note seen for the key, or <see langword="null"/> when none.
    /// Notes are surfaced for the caller's governed application
    /// (<c>IResourceAdminStore.SetNoteAsync</c>); catalog files have no note storage, so a
    /// file import never applies them itself.
    /// </summary>
    public string? TranslatorNote { get; init; }

    /// <summary>
    /// Gets a human-readable detail (for example the reason a key was skipped or conflicted).
    /// </summary>
    public required string Detail { get; init; }
}

/// <summary>
/// The result of a CSV import: the per-key report plus the updated target catalog
/// (<see langword="null"/> means "no changes", so file importers skip the write).
/// </summary>
public sealed class CsvImportReport {
    /// <summary>
    /// Gets the per-key outcomes in document order.
    /// </summary>
    public required IReadOnlyList<CsvKeyReport> Keys { get; init; }

    /// <summary>
    /// Gets the number of keys with status <see cref="CatalogKeyStatus.Updated"/>.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the number of keys with a conflict status (<see cref="CatalogKeyStatus.UnknownKey"/>,
    /// <see cref="CatalogKeyStatus.StaleSource"/>, or <see cref="CatalogKeyStatus.UnsupportedKey"/>).
    /// </summary>
    public required int ConflictCount { get; init; }
}

/// <summary>
/// The result of a dictionary-level CSV import: the per-key report plus the updated
/// target catalog (<see langword="null"/> when unchanged).
/// </summary>
public sealed class CsvCatalogImportResult {
    /// <summary>
    /// Gets the per-key import report.
    /// </summary>
    public required CsvImportReport Report { get; init; }

    /// <summary>
    /// Gets the updated target templates, or <see langword="null"/> when unchanged.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? UpdatedTarget { get; init; }
}
