//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Po;

/// <summary>
/// One PO (Portable Object) entry: a flat/contextual entry (<see cref="Value"/> set) or a
/// plural entry (<see cref="PluralKey"/>/<see cref="Forms"/> set), plus the optional
/// translator note (<c>#.</c> extracted comments) and the fuzzy flag.
/// </summary>
public sealed class PoEntry {
    /// <summary>
    /// Gets the message id (the catalog key; contexts are carried separately).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the message context (<c>msgctxt</c>), or <see langword="null"/> for context-free entries.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the flat/contextual value (<c>msgstr</c>; empty is a valid untranslated
    /// value), or <see langword="null"/> for plural entries.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the plural message id (<c>msgid_plural</c>), or <see langword="null"/> for flat/contextual entries.
    /// </summary>
    public string? PluralKey { get; init; }

    /// <summary>
    /// Gets the ordered plural forms (<c>msgstr[0..n-1]</c>; empty strings mark
    /// untranslated forms), or <see langword="null"/> for flat/contextual entries.
    /// </summary>
    public IReadOnlyList<string>? Forms { get; init; }

    /// <summary>
    /// Gets the translator note (, <c>#.</c> extracted comments joined with
    /// newlines), or <see langword="null"/> when the entry carries none.
    /// </summary>
    public string? TranslatorNote { get; init; }

    /// <summary>
    /// Gets whether the entry is flagged fuzzy (<c>#, fuzzy</c>): fuzzy entries are
    /// reported as <see cref="CatalogKeyStatus.Skipped"/> and never applied.
    /// </summary>
    public required bool Fuzzy { get; init; }
}

/// <summary>
/// A parsed PO interchange document: the header fields, the entries in file order, and
/// the file path used in diagnostics.
/// </summary>
public sealed class PoDocument {
    /// <summary>
    /// Gets the file path used in diagnostics (set by
    /// <see cref="PoCatalogInterchange.ParseDocument(string, string)"/>).
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the parsed header fields (from the <c>msgid ""</c> entry; empty when absent).
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Gets the entries in file order.
    /// </summary>
    public required IReadOnlyList<PoEntry> Entries { get; init; }
}

/// <summary>
/// A plural group supplied to export (or returned from import): the full ordered form
/// table for one singular/plural key pair plus optional context.
/// </summary>
public sealed class PoPluralEntry {
    /// <summary>
    /// Gets the singular key (PO <c>msgid</c>).
    /// </summary>
    public required string SingularKey { get; init; }

    /// <summary>
    /// Gets the plural key (PO <c>msgid_plural</c>).
    /// </summary>
    public required string PluralKey { get; init; }

    /// <summary>
    /// Gets the context (PO <c>msgctxt</c>), or <see langword="null"/>.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the ordered forms (<c>msgstr[0..n-1]</c>); <see langword="null"/> entries
    /// mark untranslated forms.
    /// </summary>
    public required IReadOnlyList<string?> Forms { get; init; }
}

/// <summary>
/// Options for a PO import.
/// </summary>
public sealed class PoImportOptions {
    /// <summary>
    /// Gets whether the import only computes the report without changing anything.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// The per-entry outcome of a PO import (plural groups report once).
/// </summary>
public sealed class PoKeyReport {
    /// <summary>
    /// Gets the catalog key (the context-encoded key for contextual entries; the
    /// singular key for plural groups).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the outcome (the shared interchange conflict policy,
    /// <see cref="CatalogKeyStatus"/>).
    /// </summary>
    public required CatalogKeyStatus Status { get; init; }

    /// <summary>
    /// Gets the flat/contextual value seen in the document, or <see langword="null"/>
    /// for plural groups.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the translator note seen for the entry, or <see langword="null"/>.
    /// Notes are surfaced for the caller's governed application
    /// (<c>IResourceAdminStore.SetNoteAsync</c>); catalog files have no note storage, so a
    /// file import never applies them itself.
    /// </summary>
    public string? TranslatorNote { get; init; }

    /// <summary>
    /// Gets a human-readable detail (for example the reason an entry was skipped or conflicted).
    /// </summary>
    public required string Detail { get; init; }
}

/// <summary>
/// The result of a PO import: the per-entry report plus the updated dimensions
/// (<see langword="null"/> members mean "no changes in that dimension").
/// </summary>
public sealed class PoImportReport {
    /// <summary>
    /// Gets the per-entry outcomes in document order.
    /// </summary>
    public required IReadOnlyList<PoKeyReport> Keys { get; init; }

    /// <summary>
    /// Gets the number of entries with status <see cref="CatalogKeyStatus.Updated"/>.
    /// </summary>
    public required int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the number of entries with a conflict status (<see cref="CatalogKeyStatus.UnknownKey"/>
    /// or <see cref="CatalogKeyStatus.UnsupportedKey"/>).
    /// </summary>
    public required int ConflictCount { get; init; }
}

/// <summary>
/// The result of a dictionary-level PO import: the per-entry report plus the updated
/// catalogs (<see langword="null"/> members mean "no changes in that dimension").
/// </summary>
public sealed class PoCatalogImportResult {
    /// <summary>
    /// Gets the per-entry import report.
    /// </summary>
    public required PoImportReport Report { get; init; }

    /// <summary>
    /// Gets the updated flat target templates, or <see langword="null"/> when unchanged.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? UpdatedTarget { get; init; }

    /// <summary>
    /// Gets the updated plural tables, or <see langword="null"/> when unchanged
    /// (always <see langword="null"/> when no current tables were supplied).
    /// </summary>
    public IReadOnlyList<PoPluralEntry>? UpdatedPlurals { get; init; }
}
