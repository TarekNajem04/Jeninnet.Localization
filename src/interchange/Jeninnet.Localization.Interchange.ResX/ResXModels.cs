//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.ResX;

/// <summary>
/// One ResX data entry: the resource name, its string value (or
/// <see langword="null"/> for valueless entries), the optional comment (the
/// translator note carrier), and whether the entry is a plain string resource
/// (typed/<c>mimetype</c>/valueless entries are not message templates).
/// </summary>
public sealed class ResXEntry {
    /// <summary>
    /// Gets the resource name (the catalog key).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the string value, or <see langword="null"/> when the entry carries no
    /// <c>value</c> element.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the entry comment, or
    /// <see langword="null"/> when the entry carries none.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets whether the entry is a plain string resource. Entries with a <c>type</c> or
    /// <c>mimetype</c> attribute, or without a <c>value</c> element, are reported as
    /// <see cref="CatalogKeyStatus.UnsupportedKey"/> and never applied.
    /// </summary>
    public required bool IsStringResource { get; init; }
}

/// <summary>
/// A parsed ResX interchange document: the entries in file order plus the file path
/// used in diagnostics.
/// </summary>
public sealed class ResXDocument {
    /// <summary>
    /// Gets the file path used in diagnostics (set by
    /// <see cref="ResXCatalogInterchange.ParseDocument(string, string)"/>).
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the entries in file order.
    /// </summary>
    public required IReadOnlyList<ResXEntry> Entries { get; init; }
}

/// <summary>
/// Options for a ResX import.
/// </summary>
public sealed class ResXImportOptions {
    /// <summary>
    /// Gets whether the import only computes the report without changing anything.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// The per-key outcome of a ResX import.
/// </summary>
public sealed class ResXKeyReport {
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
    /// Gets the value seen in the document, or <see langword="null"/> when absent.
    /// </summary>
    public required string? Value { get; init; }

    /// <summary>
    /// Gets the comment seen for the key, or <see langword="null"/> when none.
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
/// The result of a ResX import: the per-key report plus the updated target catalog
/// (<see langword="null"/> means "no changes", so file importers skip the write).
/// </summary>
public sealed class ResXImportReport {
    /// <summary>
    /// Gets the per-key outcomes in document order.
    /// </summary>
    public required IReadOnlyList<ResXKeyReport> Keys { get; init; }

    /// <summary>
    /// Gets the number of keys with status <see cref="CatalogKeyStatus.Updated"/>.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the number of keys with a conflict status (<see cref="CatalogKeyStatus.UnknownKey"/>
    /// or <see cref="CatalogKeyStatus.UnsupportedKey"/>).
    /// </summary>
    public required int ConflictCount { get; init; }
}

/// <summary>
/// The result of a dictionary-level ResX import: the per-key report plus the updated
/// target catalog (<see langword="null"/> when unchanged).
/// </summary>
public sealed class ResXCatalogImportResult {
    /// <summary>
    /// Gets the per-key import report.
    /// </summary>
    public required ResXImportReport Report { get; init; }

    /// <summary>
    /// Gets the updated target templates, or <see langword="null"/> when unchanged.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? UpdatedTarget { get; init; }
}
