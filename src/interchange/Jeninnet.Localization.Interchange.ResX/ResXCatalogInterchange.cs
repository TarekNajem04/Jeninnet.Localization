//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.ResX;

/// <summary>
/// ResX interchange for message catalogs: export plus
/// validate-then-apply import for the ResX editor and translator ecosystem. Tooling
/// only — files are interchange, never a runtime source.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Writer: the classic BCL (Base Class Library) <c>ResXResourceWriter</c> is Windows-Desktop-only on
/// net10.0, so the interchange ships a hand-rolled canonical ResX 2.0 writer/reader
/// (strings + comments) emitting the VS-canonical header, ordinal key order, 2-space
/// indent, LF newlines, and UTF-8-with-BOM files; correctness is pinned against the
/// available BCL reader (<c>System.Resources.Extensions.DeserializingResourceReader</c>).
/// </item>
/// <item>Document model: one culture per document (ResX carries no culture metadata);
/// the API is culture-blind — the export/import pair is source catalog ↔ target-culture
/// resx, and pair-file (neutral + satellite) workflows are the caller's composition.
/// </item>
/// <item>Conflict policy (the shared <see cref="CatalogKeyStatus"/> vocabulary):
/// non-string entries (typed/<c>mimetype</c>/valueless) <c>UnsupportedKey</c>, empty
/// value <c>Skipped</c> (existing templates are never cleared), key unknown to the
/// source catalog <c>UnknownKey</c>, equal target <c>Unchanged</c>, otherwise
/// <c>Updated</c>. <c>StaleSource</c> is not representable: ResX carries no
/// source-template snapshot (documented boundary). Keys/values/comments must be
/// XML 1.0-representable; illegal characters are rejected at export.
/// </item>
/// <item>Translator notes map to <c>comment</c>: exported when non-empty,
/// parsed back, surfaced per key in the report, and never applied to catalog files —
/// the caller applies them through the governed <c>IResourceAdminStore.SetNoteAsync</c>
/// step.
/// </item>
/// <item>Apply side: the whole document validates before anything is computed; the
/// dictionary-level import returns the change-set (per-key report + updated target
/// map, <see langword="null"/> when unchanged); the file-level import rewrites the
/// target catalog at most once (never on dry-run, never when unchanged — the shared
/// codec preserves order/newline/BOM (Byte Order Mark), so no-change round-trips are byte-identical).
/// </item>
/// </list>
/// </remarks>
public static class ResXCatalogInterchange {
    /// <summary>
    /// Exports one culture's catalog to canonical ResX 2.0 text.
    /// </summary>
    /// <param name="catalog">The culture's templates (must be complete, non-null values).</param>
    /// <param name="translatorNotes">
    /// Optional translator notes keyed by catalog key; null/empty values are
    /// skipped. Notes are written as entry comments.
    /// </param>
    /// <returns>The deterministic ResX text (canonical header, ordinal key order, no BOM).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a key is empty, a template is null, or a key/value/comment contains characters XML 1.0 cannot represent.</exception>
    public static string ExportCatalog(
        IReadOnlyDictionary<string, string?> catalog,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        ArgumentNullException.ThrowIfNull(catalog);

        var entries = new List<ResXEntry>(catalog.Count);
        foreach(var key in catalog.Keys.Order(StringComparer.Ordinal)) {
            if(string.IsNullOrEmpty(key)) {
                throw new ArgumentException("Catalog keys must not be empty.", nameof(catalog));
            }

            if(catalog[key] is null) {
                throw new ArgumentException($"Source template for key '{key}' must not be null.", nameof(catalog));
            }

            entries.Add(new ResXEntry {
                Name = key,
                Value = catalog[key]!,
                Comment = NoteFor(translatorNotes, key),
                IsStringResource = true,
            });
        }

        return ResXTextCodec.Write(entries);
    }

    /// <summary>
    /// Exports a catalog file (JSON or XML by extension) to a ResX file (UTF-8 (Unicode Transformation Format, 8-bit) with BOM).
    /// </summary>
    /// <param name="catalogPath">The culture's catalog file.</param>
    /// <param name="resxPath">The ResX file to write.</param>
    /// <param name="translatorNotes">Optional translator notes keyed by catalog key; null/empty values are skipped.</param>
    /// <exception cref="ArgumentException">Thrown when a path is null or empty.</exception>
    /// <exception cref="ResXInterchangeException">Thrown when the catalog file is missing or malformed.</exception>
    public static void ExportCatalogFiles(
        string catalogPath,
        string resxPath,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        ArgumentException.ThrowIfNullOrEmpty(catalogPath);
        ArgumentException.ThrowIfNullOrEmpty(resxPath);

        var catalog = ReadCatalog(catalogPath);
        var text = ExportCatalog(catalog.AsLookup(), translatorNotes);
        File.WriteAllText(resxPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Parses and validates ResX text against the interchange subset.
    /// </summary>
    /// <param name="resxText">The ResX document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resxText"/> is null.</exception>
    /// <exception cref="ResXInterchangeException">Thrown when the text violates the subset.</exception>
    public static ResXDocument ParseDocument(string resxText, string filePath) =>
        new() {
            FilePath = filePath,
            Entries = ResXTextCodec.Parse(resxText, filePath),
        };

    /// <summary>
    /// Imports a ResX document into catalogs without touching files.
    /// </summary>
    /// <param name="document">The parsed ResX document (one culture).</param>
    /// <param name="source">The current source-language templates (key governance).</param>
    /// <param name="currentTarget">The current target-language templates.</param>
    /// <returns>The report plus the updated target map (<see langword="null"/> when unchanged).</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public static ResXCatalogImportResult ImportDocument(
        ResXDocument document,
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(currentTarget);

        var plan = new ImportPlan(source, currentTarget);
        foreach(var entry in document.Entries) {
            plan.ApplyEntry(entry);
        }

        return plan.Finish();
    }

    /// <summary>
    /// Imports a ResX file into a target catalog file (validate-then-apply).
    /// Only changed targets are written; dry-run mode changes nothing. Translator notes
    /// are surfaced in the returned report and never applied to the file
    /// (catalog files have no note storage).
    /// </summary>
    /// <param name="resxPath">The ResX file to import.</param>
    /// <param name="sourcePath">The source-language catalog file (key governance).</param>
    /// <param name="targetPath">The target-language catalog file to update.</param>
    /// <param name="options">The import options (dry-run).</param>
    /// <returns>The per-key import report (including any translator notes).</returns>
    /// <exception cref="ArgumentException">Thrown when a path is null or empty.</exception>
    /// <exception cref="ResXInterchangeException">Thrown when any input is missing or malformed. Nothing is applied then.</exception>
    public static ResXImportReport ImportCatalogFiles(
        string resxPath,
        string sourcePath,
        string targetPath,
        ResXImportOptions? options = null) {
        ArgumentException.ThrowIfNullOrEmpty(resxPath);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        if(!File.Exists(resxPath)) {
            throw new ResXInterchangeException(resxPath, "ResX file not found.");
        }

        string resxText;
        try {
            resxText = File.ReadAllText(resxPath, Encoding.UTF8);
        }
        catch(IOException ex) {
            throw new ResXInterchangeException(resxPath, $"Cannot read ResX file: {ex.Message}");
        }

        var document = ParseDocument(resxText, resxPath);
        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var result = ImportDocument(document, source.AsLookup(), target.AsLookup());

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
    /// failure into the public ResX exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <returns>The parsed catalog file.</returns>
    /// <exception cref="ResXInterchangeException">Thrown when the file cannot be read or parsed.</exception>
    private static CatalogFileCodec.CatalogFile ReadCatalog(string path) {
        try {
            return CatalogFileCodec.Read(path);
        }
        catch(CatalogInterchangeException ex) {
            throw new ResXInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Writes a catalog file through the shared codec, wrapping the internal
    /// failure into the public ResX exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <param name="catalog">The parsed catalog file providing the formatting facts.</param>
    /// <param name="entries">The entries to write.</param>
    /// <exception cref="ResXInterchangeException">Thrown when the file cannot be written.</exception>
    private static void WriteCatalog(string path, CatalogFileCodec.CatalogFile catalog, IReadOnlyList<KeyValuePair<string, string>> entries) {
        try {
            CatalogFileCodec.Write(path, catalog, entries);
        }
        catch(CatalogInterchangeException ex) {
            throw new ResXInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Represents an import plan.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="currentTarget">The current target.</param>
    private sealed class ImportPlan(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget) {
        private readonly Dictionary<string, string?> _updated = new(currentTarget, StringComparer.Ordinal);
        private readonly List<ResXKeyReport> _keys = [];
        private bool _changed;

        /// <summary>
        /// Applies entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        internal void ApplyEntry(ResXEntry entry) {
            if(!entry.IsStringResource) {
                Report(entry, CatalogKeyStatus.UnsupportedKey, "non-string resource (typed, mimetype, or valueless entry).");
                return;
            }

            if(string.IsNullOrEmpty(entry.Value)) {
                Report(entry, CatalogKeyStatus.Skipped, "empty value.");
                return;
            }

            if(!source.ContainsKey(entry.Name)) {
                Report(entry, CatalogKeyStatus.UnknownKey, "key is unknown to the source catalog.");
                return;
            }

            currentTarget.TryGetValue(entry.Name, out var current);
            if(string.Equals(entry.Value, current, StringComparison.Ordinal)) {
                Report(entry, CatalogKeyStatus.Unchanged, "target already matches.");
                return;
            }

            _updated[entry.Name] = entry.Value;
            _changed = true;
            Report(entry, CatalogKeyStatus.Updated, "target applied.");
        }

        /// <summary>
        /// Performs the finish operation.
        /// </summary>
        /// <returns>The finish result.</returns>
        internal ResXCatalogImportResult Finish() {
            var updated = _keys.Count(static k => k.Status == CatalogKeyStatus.Updated);
            var conflicts = _keys.Count(static k =>
                k.Status is CatalogKeyStatus.UnknownKey or CatalogKeyStatus.UnsupportedKey);

            return new ResXCatalogImportResult {
                Report = new ResXImportReport {
                    Keys = [.. _keys],
                    UpdatedCount = updated,
                    ConflictCount = conflicts,
                },
                UpdatedTarget = _changed ? new Dictionary<string, string?>(_updated, StringComparer.Ordinal) : null,
            };
        }

        /// <summary>
        /// Performs the report operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="status">The status.</param>
        /// <param name="detail">The detail.</param>
        private void Report(ResXEntry entry, CatalogKeyStatus status, string detail) =>
            _keys.Add(new ResXKeyReport {
                Key = entry.Name,
                Status = status,
                Value = entry.Value,
                TranslatorNote = entry.Comment,
                Detail = detail,
            });
    }
}
