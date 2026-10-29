//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Csv;

/// <summary>
/// CSV interchange for message catalogs: export plus
/// validate-then-apply import for spreadsheet translation workflows. Tooling only —
/// files are interchange, never a runtime source.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Schema subset (RFC 4180): header row exactly <c>key,culture,value,note</c>;
/// one row per (key, culture); one source culture and one target culture per
/// document (API parameters — CSV has no document metadata); keys in ordinal order,
/// source row then target row per key, empty target cell when untranslated. UTF-8 (Unicode Transformation Format, 8-bit)
/// (files written with BOM (Byte Order Mark) for spreadsheets, BOM tolerated on read), CRLF (Carriage Return and Line Feed) records
/// on write (CRLF/LF/lone CR accepted on read), minimal RFC 4180 quoting on write,
/// strict parsing on read with file/line diagnostics. Plural tables are outside the
/// flat CSV subset (documented boundary); contextual keys travel as ordinary cell
/// text.
/// </item>
/// <item>Conflict policy: the shared <see cref="CatalogKeyStatus"/> vocabulary
///: empty value <c>Skipped</c>
/// (existing templates are never cleared), contextual key against an XML target
/// <c>UnsupportedKey</c>, key unknown to the source catalog <c>UnknownKey</c>,
/// missing or differing source row <c>StaleSource</c>, equal target
/// <c>Unchanged</c>, otherwise <c>Updated</c>. Rows for cultures other than the
/// declared source/target fail validation.
/// </item>
/// <item>Translator notes travel in the <c>note</c> column: exported on
/// every row of the key, validated to agree when non-empty, surfaced per key in
/// the report, and never applied to catalog files (JSON/XML catalogs have no note
/// storage) — the caller applies them through the governed
/// <c>IResourceAdminStore.SetNoteAsync</c> step.
/// </item>
/// <item>Apply side: the whole document validates before anything is computed; the
/// dictionary-level import returns the change-set (per-key report + updated target
/// map, <see langword="null"/> when unchanged); the file-level import rewrites the
/// target catalog at most once (never on dry-run, never when unchanged — the shared
/// codec preserves order/newline/BOM, so no-change round-trips are byte-identical).
/// </item>
/// </list>
/// </remarks>
public static class CsvCatalogInterchange {
    private const char CONTEXT_SEPARATOR = '\u0004';

    /// <summary>
    /// Exports a source/target catalog pair to CSV text.
    /// </summary>
    /// <param name="source">The source-language templates (must be complete, non-null values).</param>
    /// <param name="target">The target-language templates, or <see langword="null"/> for a source-only export (empty target cells).</param>
    /// <param name="srcLang">The source culture code.</param>
    /// <param name="trgLang">The target culture code.</param>
    /// <param name="translatorNotes">
    /// Optional translator notes keyed by catalog key; null/empty values are
    /// skipped. Notes are written on every row of the key.
    /// </param>
    /// <returns>The deterministic CSV text (header, ordinal key order, CRLF records, no BOM).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a culture is empty, the source and target cultures are equal, or a key is empty or has a null template.</exception>
    public static string ExportCatalog(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?>? target,
        string srcLang,
        string trgLang,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        ArgumentNullException.ThrowIfNull(source);
        ValidateLanguages(srcLang, trgLang);

        var rows = new List<CsvRow>(source.Count * 2);
        foreach(var key in source.Keys.Order(StringComparer.Ordinal)) {
            if(string.IsNullOrEmpty(key)) {
                throw new ArgumentException("Catalog keys must not be empty.", nameof(source));
            }

            if(source[key] is null) {
                throw new ArgumentException($"Source template for key '{key}' must not be null.", nameof(source));
            }

            var note = NoteFor(translatorNotes, key);
            string? targetTemplate = null;
            target?.TryGetValue(key, out targetTemplate);
            rows.Add(new CsvRow {
                Key = key,
                Culture = srcLang,
                Value = source[key]!,
                Note = note,
            });
            rows.Add(new CsvRow {
                Key = key,
                Culture = trgLang,
                Value = targetTemplate ?? string.Empty,
                Note = note,
            });
        }

        return CsvTextCodec.Write(rows);
    }

    /// <summary>
    /// Exports catalog files (JSON or XML by extension) to a CSV file (UTF-8 with BOM).
    /// </summary>
    /// <param name="sourcePath">The source-language catalog file.</param>
    /// <param name="targetPath">The target-language catalog file.</param>
    /// <param name="csvPath">The CSV file to write.</param>
    /// <param name="srcLang">The source culture code.</param>
    /// <param name="trgLang">The target culture code.</param>
    /// <param name="translatorNotes">Optional translator notes keyed by catalog key; null/empty values are skipped.</param>
    /// <exception cref="ArgumentException">Thrown when a path or culture is null or empty.</exception>
    /// <exception cref="CsvInterchangeException">Thrown when a catalog file is missing or malformed.</exception>
    public static void ExportCatalogFiles(
        string sourcePath,
        string targetPath,
        string csvPath,
        string srcLang,
        string trgLang,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentException.ThrowIfNullOrEmpty(csvPath);

        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var text = ExportCatalog(source.AsLookup(), target.AsLookup(), srcLang, trgLang, translatorNotes);
        File.WriteAllText(csvPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Parses and validates CSV text against the interchange subset.
    /// </summary>
    /// <param name="csvText">The CSV document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="csvText"/> is null.</exception>
    /// <exception cref="CsvInterchangeException">Thrown when the text violates the subset.</exception>
    public static CsvDocument ParseDocument(string csvText, string filePath) =>
        new() {
            FilePath = filePath,
            Rows = CsvTextCodec.Parse(csvText, filePath),
        };

    /// <summary>
    /// Imports a CSV document into catalogs without touching files.
    /// </summary>
    /// <param name="document">The parsed CSV document.</param>
    /// <param name="source">The current source-language templates (stale-source detection).</param>
    /// <param name="currentTarget">The current target-language templates.</param>
    /// <param name="srcLang">The source culture code.</param>
    /// <param name="trgLang">The target culture code.</param>
    /// <param name="xmlTarget">Whether the eventual target file is XML (contextual keys unsupported).</param>
    /// <returns>The report plus the updated target map (<see langword="null"/> when unchanged).</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a culture is empty or the source and target cultures are equal.</exception>
    /// <exception cref="CsvInterchangeException">Thrown when the document carries rows for other cultures; nothing is computed then.</exception>
    public static CsvCatalogImportResult ImportDocument(
        CsvDocument document,
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        string srcLang,
        string trgLang,
        bool xmlTarget = false) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(currentTarget);
        ValidateLanguages(srcLang, trgLang);

        var sourceRows = new Dictionary<string, string>(StringComparer.Ordinal);
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        var targetRows = new List<CsvRow>();
        foreach(var row in document.Rows) {
            if(string.Equals(row.Culture, srcLang, StringComparison.Ordinal)) {
                sourceRows[row.Key] = row.Value;
            } else if(string.Equals(row.Culture, trgLang, StringComparison.Ordinal)) {
                targetRows.Add(row);
            } else {
                throw new CsvInterchangeException(
                    document.FilePath,
                    $"Row culture '{row.Culture}' is neither the source culture '{srcLang}' nor the target culture '{trgLang}'.");
            }

            if(row.Note is not null) {
                notes[row.Key] = row.Note;
            }
        }

        var plan = new ImportPlan(source, currentTarget, xmlTarget);
        foreach(var row in targetRows) {
            plan.ApplyRow(row, sourceRows, notes);
        }

        return plan.Finish();
    }

    /// <summary>
    /// Imports a CSV file into a target catalog file (validate-then-apply).
    /// Only changed targets are written; dry-run mode changes nothing. Translator notes
    /// are surfaced in the returned report and never applied to the file
    /// (catalog files have no note storage).
    /// </summary>
    /// <param name="csvPath">The CSV file to import.</param>
    /// <param name="sourcePath">The source-language catalog file (stale-source detection).</param>
    /// <param name="targetPath">The target-language catalog file to update.</param>
    /// <param name="srcLang">The source culture code.</param>
    /// <param name="trgLang">The target culture code.</param>
    /// <param name="options">The import options (dry-run).</param>
    /// <returns>The per-key import report (including any translator notes).</returns>
    /// <exception cref="ArgumentException">Thrown when a path or culture is null or empty.</exception>
    /// <exception cref="CsvInterchangeException">Thrown when any input is missing or malformed. Nothing is applied then.</exception>
    public static CsvImportReport ImportCatalogFiles(
        string csvPath,
        string sourcePath,
        string targetPath,
        string srcLang,
        string trgLang,
        CsvImportOptions? options = null) {
        ArgumentException.ThrowIfNullOrEmpty(csvPath);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        if(!File.Exists(csvPath)) {
            throw new CsvInterchangeException(csvPath, "CSV file not found.");
        }

        string csvText;
        try {
            csvText = File.ReadAllText(csvPath, Encoding.UTF8);
        }
        catch(IOException ex) {
            throw new CsvInterchangeException(csvPath, $"Cannot read CSV file: {ex.Message}");
        }

        var document = ParseDocument(csvText, csvPath);
        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var result = ImportDocument(
            document,
            source.AsLookup(),
            target.AsLookup(),
            srcLang,
            trgLang,
            xmlTarget: target.Kind == CatalogFileCodec.CatalogKind.Xml);

        if(result.UpdatedTarget is not null && options?.DryRun != true) {
            var ordered = target.Entries
                .Select(static e => e.Key)
                .Select(key => new KeyValuePair<string, string>(key, result.UpdatedTarget[key]!))
                .ToArray();
            WriteCatalog(targetPath, target, ordered);
        }

        return result.Report;
    }

    /// <summary>
    /// Validates languages.
    /// </summary>
    /// <param name="srcLang">The src lang.</param>
    /// <param name="trgLang">The trg lang.</param>
    private static void ValidateLanguages(string srcLang, string trgLang) {
        ArgumentException.ThrowIfNullOrEmpty(srcLang);
        ArgumentException.ThrowIfNullOrEmpty(trgLang);

        if(string.Equals(srcLang, trgLang, StringComparison.Ordinal)) {
            throw new ArgumentException($"The source and target cultures must differ (both are '{srcLang}').", nameof(trgLang));
        }
    }

    /// <summary>
    /// Performs the note for operation.
    /// </summary>
    /// <param name="translatorNotes">The translator notes.</param>
    /// <param name="key">The message key.</param>
    /// <returns>The note for result.</returns>
    private static string? NoteFor(IReadOnlyDictionary<string, string>? translatorNotes, string key) =>
        translatorNotes is not null && translatorNotes.TryGetValue(key, out var note) && !string.IsNullOrEmpty(note)
            ? note
            : null;

    /// <summary>
    /// Reads a catalog file through the shared codec, wrapping the internal
    /// failure into the public CSV exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <returns>The parsed catalog file.</returns>
    /// <exception cref="CsvInterchangeException">Thrown when the file cannot be read or parsed.</exception>
    private static CatalogFileCodec.CatalogFile ReadCatalog(string path) {
        try {
            return CatalogFileCodec.Read(path);
        }
        catch(CatalogInterchangeException ex) {
            throw new CsvInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Writes a catalog file through the shared codec, wrapping the internal
    /// failure into the public CSV exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <param name="catalog">The parsed catalog file providing the formatting facts.</param>
    /// <param name="entries">The entries to write.</param>
    /// <exception cref="CsvInterchangeException">Thrown when the file cannot be written.</exception>
    private static void WriteCatalog(string path, CatalogFileCodec.CatalogFile catalog, IReadOnlyList<KeyValuePair<string, string>> entries) {
        try {
            CatalogFileCodec.Write(path, catalog, entries);
        }
        catch(CatalogInterchangeException ex) {
            throw new CsvInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Represents an import plan.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="currentTarget">The current target.</param>
    /// <param name="xmlTarget">The xml target.</param>
    private sealed class ImportPlan(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        bool xmlTarget) {
        private readonly Dictionary<string, string?> _updated = new(currentTarget, StringComparer.Ordinal);
        private readonly List<CsvKeyReport> _keys = [];
        private bool _changed;

        /// <summary>
        /// Performs the finish operation.
        /// </summary>
        /// <returns>The finish result.</returns>
        internal CsvCatalogImportResult Finish() {
            var updated = _keys.Count(static k => k.Status == CatalogKeyStatus.Updated);
            var conflicts = _keys.Count(static k =>
                k.Status is CatalogKeyStatus.UnknownKey or CatalogKeyStatus.StaleSource or CatalogKeyStatus.UnsupportedKey);

            return new CsvCatalogImportResult {
                Report = new CsvImportReport {
                    Keys = [.. _keys],
                    UpdatedCount = updated,
                    ConflictCount = conflicts,
                },
                UpdatedTarget = _changed ? new Dictionary<string, string?>(_updated, StringComparer.Ordinal) : null,
            };
        }

        /// <summary>
        /// Applies row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="sourceRows">The source rows.</param>
        /// <param name="notes">The notes.</param>
        internal void ApplyRow(CsvRow row, Dictionary<string, string> sourceRows, Dictionary<string, string> notes) {
            var key = row.Key;

            if(string.IsNullOrEmpty(row.Value)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.Skipped, "empty target value.");
                return;
            }

            if(xmlTarget && key.Contains(CONTEXT_SEPARATOR)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.UnsupportedKey, "contextual keys cannot be XML element names.");
                return;
            }

            if(!source.TryGetValue(key, out var sourceTemplate)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.UnknownKey, "key is unknown to the source catalog.");
                return;
            }

            if(!sourceRows.TryGetValue(key, out var csvSource)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.StaleSource, "the document carries no source row for this key.");
                return;
            }

            if(!string.Equals(csvSource, sourceTemplate, StringComparison.Ordinal)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.StaleSource, "the source row differs from the source catalog.");
                return;
            }

            currentTarget.TryGetValue(key, out var current);
            if(string.Equals(row.Value, current, StringComparison.Ordinal)) {
                Report(row, sourceRows, notes, CatalogKeyStatus.Unchanged, "target already matches.");
                return;
            }

            _updated[key] = row.Value;
            _changed = true;
            Report(row, sourceRows, notes, CatalogKeyStatus.Updated, "target applied.");
        }

        /// <summary>
        /// Performs the report operation.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="sourceRows">The source rows.</param>
        /// <param name="notes">The notes.</param>
        /// <param name="status">The status.</param>
        /// <param name="detail">The detail.</param>
        private void Report(CsvRow row, Dictionary<string, string> sourceRows, Dictionary<string, string> notes, CatalogKeyStatus status, string detail) =>
            _keys.Add(new CsvKeyReport {
                Key = row.Key,
                Status = status,
                Source = sourceRows.TryGetValue(row.Key, out var csvSource) ? csvSource : null,
                Target = row.Value,
                TranslatorNote = notes.TryGetValue(row.Key, out var note) ? note : null,
                Detail = detail,
            });
    }
}
