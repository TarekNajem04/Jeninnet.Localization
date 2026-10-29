//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Xliff;

/// <summary>
/// A single XLIFF (XML Localization Interchange File Format) 2.0 translation unit: one segment with a
/// required source and an optional target, plus optional / metadata notes.
/// </summary>
public sealed class XliffUnit {
    /// <summary>
    /// Gets the unique unit id within the document. Contextual keys encode the EOT (End of Transmission)
    /// separator as ␄ (U+2404); plural-form units use <c>{key}[{index}]</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the source-language template.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the target-language template, or <see langword="null"/> when untranslated.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets the segment state (<c>initial</c>, <c>translated</c>, <c>reviewed</c>, <c>final</c>).
    /// </summary>
    public string State { get; init; } = "initial";

    /// <summary>
    /// Gets whether the unit is translatable (<c>translate="no"</c> skips it on import).
    /// </summary>
    public bool Translate { get; init; } = true;

    /// <summary>
    /// Gets the disambiguating context (PO (Portable Object) <c>msgctxt</c>), or <see langword="null"/> for plain units.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the translator note, or
    /// <see langword="null"/> when the unit carries none. Key-level translator context
    /// travels beside the structural metadata notes.
    /// </summary>
    public string? TranslatorNote { get; init; }

    /// <summary>
    /// Gets the plural-form metadata, or <see langword="null"/> for non-plural units.
    /// </summary>
    public XliffPluralInfo? Plural { get; init; }
}

/// <summary>
/// Plural-form metadata for an <see cref="XliffUnit"/>.
/// </summary>
public sealed class XliffPluralInfo {
    /// <summary>
    /// Gets the singular key (PO <c>msgid</c>).
    /// </summary>
    public required string SingularKey { get; init; }

    /// <summary>
    /// Gets the plural key (PO <c>msgid_plural</c>).
    /// </summary>
    public required string PluralKey { get; init; }

    /// <summary>
    /// Gets the zero-based form index (<c>msgstr[index]</c>).
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the total number of forms in the group.
    /// </summary>
    public required int Count { get; init; }
}

/// <summary>
/// An in-memory XLIFF 2.0 document: one file, one source language, one target language.
/// </summary>
public sealed class XliffDocument {
    /// <summary>
    /// Gets the source language (BCP-47, XLIFF <c>srcLang</c>).
    /// </summary>
    public required string SourceLanguage { get; init; }

    /// <summary>
    /// Gets the target language (BCP-47, XLIFF <c>trgLang</c>).
    /// </summary>
    public required string TargetLanguage { get; init; }

    /// <summary>
    /// Gets the file id (XLIFF <c>file id</c>, typically the catalog name).
    /// </summary>
    public required string FileId { get; init; }

    /// <summary>
    /// Gets the translation units in document order.
    /// </summary>
    public required IReadOnlyList<XliffUnit> Units { get; init; }
}

/// <summary>
/// A plural group supplied to export (or returned from import): the full ordered
/// form table for one singular/plural key pair plus optional context.
/// </summary>
public sealed class XliffPluralEntry {
    /// <summary>
    /// Gets the singular key (PO <c>msgid</c>).
    /// </summary>
    public required string SingularKey { get; init; }

    /// <summary>
    /// Gets the plural key (PO <c>msgid_plural</c>).
    /// </summary>
    public required string PluralKey { get; init; }

    /// <summary>
    /// Gets the disambiguating context (PO <c>msgctxt</c>), or <see langword="null"/>.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the ordered target form templates (<c>msgstr[0..n-1]</c>); <see langword="null"/>
    /// entries mark untranslated forms.
    /// </summary>
    public required IReadOnlyList<string?> TargetForms { get; init; }

    /// <summary>
    /// Gets the ordered source form templates, or <see langword="null"/> to derive them
    /// from the flat source pair (<c>singularKey</c>/<c>pluralKey</c>, clamped for higher
    /// indices). Provided forms take precedence when the source catalog is itself plural.
    /// </summary>
    public IReadOnlyList<string?>? SourceForms { get; init; }
}

/// <summary>
/// Options for an XLIFF import.
/// </summary>
public sealed class XliffImportOptions {
    /// <summary>
    /// Gets whether the import only computes the report without changing anything.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// The per-key outcome of an XLIFF import.
/// </summary>
public sealed class XliffKeyReport {
    /// <summary>
    /// Gets the catalog key. Plural forms report <c>{singularKey}[{index}]</c> when
    /// plural tables are managed, otherwise the pair-slot key they update.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public required CatalogKeyStatus Status { get; init; }

    /// <summary>
    /// Gets the source template seen in the XLIFF unit, or <see langword="null"/> when absent.
    /// </summary>
    public required string? Source { get; init; }

    /// <summary>
    /// Gets the target template seen in the XLIFF unit, or <see langword="null"/> when absent.
    /// </summary>
    public required string? Target { get; init; }

    /// <summary>
    /// Gets the segment state seen in the XLIFF unit.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the translator note seen in the XLIFF unit, or <see langword="null"/>
    /// when the unit carries none. Notes are surfaced for the caller's governed
    /// application (<c>IResourceAdminStore.SetNoteAsync</c>); catalog files have no note
    /// storage, so a file import never applies them itself.
    /// </summary>
    public string? TranslatorNote { get; init; }

    /// <summary>
    /// Gets a human-readable detail (for example the reason a key was skipped or conflicted).
    /// </summary>
    public required string Detail { get; init; }
}

/// <summary>
/// The result of a dictionary-level XLIFF import: the per-key report plus the updated
/// catalogs (<see langword="null"/> members mean "no changes in that dimension").
/// </summary>
public sealed class XliffCatalogImportResult {
    /// <summary>
    /// Gets the per-key import report.
    /// </summary>
    public required XliffImportReport Report { get; init; }

    /// <summary>
    /// Gets the updated flat target templates, or <see langword="null"/> when unchanged.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? UpdatedTarget { get; init; }

    /// <summary>
    /// Gets the updated plural tables, or <see langword="null"/> when unchanged
    /// (always <see langword="null"/> when no current tables were supplied).
    /// </summary>
    public IReadOnlyList<XliffPluralEntry>? UpdatedPlurals { get; init; }
}

/// <summary>
/// The result of an XLIFF import: the per-key report plus the updated catalogs
/// (<see langword="null"/> maps mean "no changes", so file importers skip the write).
/// </summary>
public sealed class XliffImportReport {
    /// <summary>
    /// Gets the per-key outcomes in XLIFF document order.
    /// </summary>
    public required IReadOnlyList<XliffKeyReport> Keys { get; init; }

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
