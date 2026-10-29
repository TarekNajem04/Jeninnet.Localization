//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Xliff;

/// <summary>
/// XLIFF (XML Localization Interchange File Format) 2.0 interchange for message catalogs: export plus validate-then-apply import.
/// Tooling only — files are interchange, never a runtime source (no provider here).
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Schema subset: XLIFF 2.0 (<c>version="2.0"</c>, <c>srcLang</c> required,
/// <c>trgLang</c> accepted when present), exactly one <c>file</c>, unique <c>unit</c>
/// ids each with exactly one <c>segment</c> (required plain-text <c>source</c>,
/// optional plain-text <c>target</c>), segment states <c>initial</c>/<c>translated</c>/
/// <c>reviewed</c>/<c>final</c>, <c>translate="yes"|"no"</c>. Context travels in a
/// <c>note category="context"</c>; each plural form travels as its own
/// unit with <c>plural-singular</c>/<c>plural-other</c>/<c>plural-index</c>/
/// <c>plural-count</c> notes; the key-level translator note travels in a
/// <c>note category="translator"</c> and is surfaced in the
/// import report — catalog files have no note storage, so notes are never applied to
/// files. The EOT (End of Transmission) context separator (\u0004, illegal
/// in XML 1.0) encodes as ␄ (U+2404) in unit ids. Namespaced (vendor) elements are
/// ignored with their subtrees; anything else outside the subset is rejected.
/// No full-XSD validation: code-based subset validation is the conformance check.
/// </item>
/// <item>Conflict policy per key: <c>Unchanged</c> (target matches, nothing applied),
/// <c>Updated</c> (changed target applied), <c>Skipped</c> (empty/missing target or
/// <c>translate="no"</c> — existing templates are never cleared),
/// <c>UnknownKey</c> (keys are governed; interchange never invents keys),
/// <c>StaleSource</c> (unit source differs from the current source catalog),
/// <c>UnsupportedKey</c> (plural forms beyond the pair slots, or contextual keys,
/// against flat XML catalogs). State is reported per key, never gates application.
/// </item>
/// <item>Apply side:  is not PASS (no admin write API exists), so import takes the
/// narrow reviewed path — it validates the whole XLIFF first, computes the full
/// per-key plan, then rewrites the target catalog file at most once. Dry-run mode
/// changes nothing.  may later consume the dictionary-level change-set.
/// </item>
/// </list>
/// </remarks>
public static class XliffCatalogInterchange {
    /// <summary>
    /// Exports flat catalogs (plus optional plural tables) to XLIFF 2.0 text.
    /// </summary>
    /// <param name="source">The source-language templates (must be complete, non-null values).</param>
    /// <param name="target">The target-language templates, or <see langword="null"/> for a source-only export. Null or empty values export as untranslated.</param>
    /// <param name="srcLang">The source language (BCP-47).</param>
    /// <param name="trgLang">The target language (BCP-47).</param>
    /// <param name="fileId">The XLIFF file id (typically the catalog name).</param>
    /// <param name="pluralEntries">Optional plural tables; grouped pair keys export as form units only.</param>
    /// <param name="translatorNotes">
    /// Optional translator notes keyed by flat catalog key; null/empty values are
    /// skipped. Notes attach to flat units only — plural-form units never carry translator
    /// notes in this subset.
    /// </param>
    /// <returns>The deterministic XLIFF 2.0 document text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a language, the file id, a key, or a plural group is invalid.</exception>
    public static string ExportCatalog(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?>? target,
        string srcLang,
        string trgLang,
        string fileId,
        IReadOnlyList<XliffPluralEntry>? pluralEntries = null,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        var document = XliffDocumentSerializer.BuildDocument(source, target, srcLang, trgLang, fileId, pluralEntries, translatorNotes);
        return XliffDocumentSerializer.Serialize(document);
    }

    /// <summary>
    /// Exports catalog files (JSON or XML by extension) to an XLIFF 2.0 file.
    /// </summary>
    /// <param name="sourcePath">The source-language catalog file.</param>
    /// <param name="targetPath">The target-language catalog file.</param>
    /// <param name="xliffPath">The XLIFF file to write.</param>
    /// <param name="srcLang">The source language (BCP-47).</param>
    /// <param name="trgLang">The target language (BCP-47).</param>
    /// <param name="fileId">The XLIFF file id, or <see langword="null"/> to derive it from the target file name.</param>
    /// <param name="translatorNotes">Optional translator notes keyed by flat catalog key; null/empty values are skipped.</param>
    /// <exception cref="ArgumentException">Thrown when a path or language is null or empty.</exception>
    /// <exception cref="XliffInterchangeException">Thrown when a catalog file is missing or malformed.</exception>
    public static void ExportCatalogFiles(
        string sourcePath,
        string targetPath,
        string xliffPath,
        string srcLang,
        string trgLang,
        string? fileId = null,
        IReadOnlyDictionary<string, string>? translatorNotes = null) {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentException.ThrowIfNullOrEmpty(xliffPath);

        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var text = ExportCatalog(
            source.AsLookup(),
            target.AsLookup(),
            srcLang,
            trgLang,
            fileId ?? Path.GetFileNameWithoutExtension(targetPath),
            translatorNotes: translatorNotes);
        File.WriteAllText(xliffPath, text, new UTF8Encoding(false));
    }

    /// <summary>
    /// Parses and validates XLIFF 2.0 text against the interchange subset.
    /// </summary>
    /// <param name="xliffText">The XLIFF document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="xliffText"/> is null.</exception>
    /// <exception cref="XliffInterchangeException">Thrown when the text violates the subset.</exception>
    public static XliffDocument ParseDocument(string xliffText, string filePath) =>
        XliffDocumentParser.Parse(xliffText, filePath);

    /// <summary>
    /// Imports an XLIFF document into catalogs without touching files.
    /// </summary>
    /// <param name="document">The parsed XLIFF document.</param>
    /// <param name="source">The current source-language templates (stale-source detection).</param>
    /// <param name="currentTarget">The current target-language templates.</param>
    /// <param name="sourcePlurals">The current source plural tables, or <see langword="null"/> to derive source forms from the flat pair.</param>
    /// <param name="currentPlurals">The current target plural tables, or <see langword="null"/> for flat-only targets.</param>
    /// <param name="xmlTarget">Whether the eventual target file is XML (contextual keys unsupported).</param>
    /// <returns>The report plus updated maps (<see langword="null"/> when that dimension is unchanged).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/>, <paramref name="source"/>, or <paramref name="currentTarget"/> is null.</exception>
    public static XliffCatalogImportResult ImportDocument(
        XliffDocument document,
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        IReadOnlyList<XliffPluralEntry>? sourcePlurals = null,
        IReadOnlyList<XliffPluralEntry>? currentPlurals = null,
        bool xmlTarget = false) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(currentTarget);

        var plan = new ImportPlan(source, currentTarget, sourcePlurals, currentPlurals, xmlTarget);
        foreach(var unit in document.Units) {
            plan.ApplyUnit(unit);
        }

        return plan.Finish();
    }

    /// <summary>
    /// Imports an XLIFF file into a target catalog file (validate-then-apply).
    /// Only changed targets are written; dry-run mode changes nothing. Translator notes
    /// are surfaced in the returned report and never applied to the file
    /// (catalog files have no note storage).
    /// </summary>
    /// <param name="xliffPath">The XLIFF file to import.</param>
    /// <param name="sourcePath">The source-language catalog file (stale-source detection).</param>
    /// <param name="targetPath">The target-language catalog file to update.</param>
    /// <param name="options">The import options (dry-run).</param>
    /// <returns>The per-key import report (including any translator notes).</returns>
    /// <exception cref="ArgumentException">Thrown when a path is null or empty.</exception>
    /// <exception cref="XliffInterchangeException">Thrown when any input is missing or malformed. Nothing is applied then.</exception>
    public static XliffImportReport ImportCatalogFiles(
        string xliffPath,
        string sourcePath,
        string targetPath,
        XliffImportOptions? options = null) {
        ArgumentException.ThrowIfNullOrEmpty(xliffPath);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        if(!File.Exists(xliffPath)) {
            throw new XliffInterchangeException(xliffPath, "XLIFF file not found.");
        }

        string xliffText;
        try {
            xliffText = File.ReadAllText(xliffPath, Encoding.UTF8);
        }
        catch(IOException ex) {
            throw new XliffInterchangeException(xliffPath, $"Cannot read XLIFF file: {ex.Message}");
        }

        var document = XliffDocumentParser.Parse(xliffText, xliffPath);
        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var result = ImportDocument(
            document,
            source.AsLookup(),
            target.AsLookup(),
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
    /// Reads a catalog file through the shared codec, wrapping the internal
    /// failure into the public XLIFF exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <returns>The parsed catalog file.</returns>
    /// <exception cref="XliffInterchangeException">Thrown when the file cannot be read or parsed.</exception>
    private static CatalogFileCodec.CatalogFile ReadCatalog(string path) {
        try {
            return CatalogFileCodec.Read(path);
        }
        catch(CatalogInterchangeException ex) {
            throw new XliffInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Writes a catalog file through the shared codec, wrapping the internal
    /// failure into the public XLIFF exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <param name="catalog">The parsed catalog file providing the formatting facts.</param>
    /// <param name="entries">The entries to write.</param>
    /// <exception cref="XliffInterchangeException">Thrown when the file cannot be written.</exception>
    private static void WriteCatalog(string path, CatalogFileCodec.CatalogFile catalog, IReadOnlyList<KeyValuePair<string, string>> entries) {
        try {
            CatalogFileCodec.Write(path, catalog, entries);
        }
        catch(CatalogInterchangeException ex) {
            throw new XliffInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Represents an import plan.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="currentTarget">The current target.</param>
    /// <param name="sourcePlurals">The source plurals.</param>
    /// <param name="currentPlurals">The current plurals.</param>
    /// <param name="xmlTarget">The xml target.</param>
    private sealed class ImportPlan(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        IReadOnlyList<XliffPluralEntry>? sourcePlurals,
        IReadOnlyList<XliffPluralEntry>? currentPlurals,
        bool xmlTarget) {
        private readonly Dictionary<string, string?> _updated = new(currentTarget, StringComparer.Ordinal);
        private readonly Dictionary<XliffPluralGroupKey, List<string?>> _tables = [];
        private readonly List<XliffKeyReport> _keys = [];
        private bool _flatChanged;
        private bool _tablesChanged;

        /// <summary>
        /// Applies unit.
        /// </summary>
        /// <param name="unit">The unit.</param>
        internal void ApplyUnit(XliffUnit unit) {
            if(!unit.Translate) {
                Report(unit, FlatKey(unit), CatalogKeyStatus.Skipped, "translate=\"no\".");
                return;
            }

            if(string.IsNullOrEmpty(unit.Target)) {
                Report(unit, FlatKey(unit), CatalogKeyStatus.Skipped, "empty or missing target.");
                return;
            }

            if(unit.Plural is null) {
                ApplyFlat(unit, DecodeKey(unit));
            } else {
                ApplyPluralForm(unit);
            }
        }

        /// <summary>
        /// Performs the finish operation.
        /// </summary>
        /// <returns>The finish result.</returns>
        internal XliffCatalogImportResult Finish() {
            IReadOnlyDictionary<string, string?>? updatedTarget = null;
            if(_flatChanged) {
                updatedTarget = new Dictionary<string, string?>(_updated, StringComparer.Ordinal);
            }

            IReadOnlyList<XliffPluralEntry>? updatedPlurals = null;
            if(_tablesChanged && currentPlurals is not null) {
                updatedPlurals = [.. currentPlurals
                    .Select(entry => {
                        var key = new XliffPluralGroupKey(entry.SingularKey, entry.PluralKey, entry.Context);
                        return _tables.TryGetValue(key, out var forms)
                            ? new XliffPluralEntry {
                                SingularKey = entry.SingularKey,
                                PluralKey = entry.PluralKey,
                                Context = entry.Context,
                                TargetForms = [.. forms],
                                SourceForms = entry.SourceForms,
                            }
                            : entry;
                    })];
            }

            var updated = _keys.Count(static k => k.Status == CatalogKeyStatus.Updated);
            var conflicts = _keys.Count(static k =>
                k.Status is CatalogKeyStatus.UnknownKey or CatalogKeyStatus.StaleSource or CatalogKeyStatus.UnsupportedKey);
            return new XliffCatalogImportResult {
                Report = new XliffImportReport {
                    Keys = [.. _keys],
                    UpdatedCount = updated,
                    ConflictCount = conflicts,
                },
                UpdatedTarget = updatedTarget,
                UpdatedPlurals = updatedPlurals,
            };
        }

        /// <summary>
        /// Applies flat.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="key">The message key.</param>
        private void ApplyFlat(XliffUnit unit, string key) {
            if(xmlTarget && key.Contains(XliffConstants.CONTEXT_SEPARATOR)) {
                Report(unit, key, CatalogKeyStatus.UnsupportedKey, "contextual keys cannot be XML element names.");
                return;
            }

            if(!source.TryGetValue(key, out var sourceTemplate)) {
                Report(unit, key, CatalogKeyStatus.UnknownKey, "key is unknown to the source catalog.");
                return;
            }

            if(!string.Equals(unit.Source, sourceTemplate, StringComparison.Ordinal)) {
                Report(unit, key, CatalogKeyStatus.StaleSource, "unit source differs from the source catalog.");
                return;
            }

            currentTarget.TryGetValue(key, out var current);
            if(string.Equals(unit.Target, current, StringComparison.Ordinal)) {
                Report(unit, key, CatalogKeyStatus.Unchanged, "target already matches.");
                return;
            }

            _updated[key] = unit.Target;
            _flatChanged = true;
            Report(unit, key, CatalogKeyStatus.Updated, "target applied.");
        }

        /// <summary>
        /// Applies plural form.
        /// </summary>
        /// <param name="unit">The unit.</param>
        private void ApplyPluralForm(XliffUnit unit) {
            var plural = unit.Plural!;
            var group = new XliffPluralGroupKey(plural.SingularKey, plural.PluralKey, unit.Context);
            var reportKey = ReportPluralKey(unit, plural);
            var expectedSource = ExpectedSourceForm(group, plural.Index);

            if(expectedSource is null) {
                Report(unit, reportKey, CatalogKeyStatus.UnknownKey, "plural group is unknown to the source catalog.");
                return;
            }

            if(!string.Equals(unit.Source, expectedSource, StringComparison.Ordinal)) {
                Report(unit, reportKey, CatalogKeyStatus.StaleSource, "unit source differs from the source catalog.");
                return;
            }

            if(currentPlurals is not null && FindTable(group) is List<string?> forms) {
                if(plural.Index >= forms.Count) {
                    Report(unit, reportKey, CatalogKeyStatus.UnsupportedKey, "form index exceeds the current table.");
                    return;
                }

                if(string.Equals(unit.Target, forms[plural.Index], StringComparison.Ordinal)) {
                    Report(unit, reportKey, CatalogKeyStatus.Unchanged, "target already matches.");
                    return;
                }

                forms[plural.Index] = unit.Target;
                _tablesChanged = true;
                Report(unit, reportKey, CatalogKeyStatus.Updated, "plural form applied.");
                return;
            }

            if(plural.Index > 1) {
                Report(unit, reportKey, CatalogKeyStatus.UnsupportedKey, "flat catalogs only carry the singular/plural pair slots.");
                return;
            }

            ApplyFlat(unit, PairSlotKey(group, plural.Index));
        }

        /// <summary>
        /// Performs the expected source form operation.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="index">The index.</param>
        /// <returns>The expected source form result.</returns>
        private string? ExpectedSourceForm(XliffPluralGroupKey group, int index) {
            if(sourcePlurals is not null) {
                foreach(var entry in sourcePlurals) {
                    if(!group.Matches(entry)) {
                        continue;
                    }

                    var forms = entry.SourceForms ?? entry.TargetForms;
                    return index < forms.Count ? forms[index] : null;
                }

                return null;
            }

            var singularFlat = FlatKey(group.Context, group.SingularKey);
            var pluralFlat = FlatKey(group.Context, group.PluralKey);
            if(!source.TryGetValue(singularFlat, out var singular) || singular is null) {
                return null;
            }

            if(!source.TryGetValue(pluralFlat, out var plural) || string.IsNullOrEmpty(plural)) {
                plural = singular;
            }

            return index == 0 ? singular : plural;
        }

        /// <summary>
        /// Finds table.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>The table sequence.</returns>
        private List<string?>? FindTable(XliffPluralGroupKey group) {
            if(_tables.TryGetValue(group, out var cached)) {
                return cached;
            }

            if(currentPlurals is null) {
                return null;
            }

            var match = currentPlurals.FirstOrDefault(group.Matches);
            if(match is null) {
                return null;
            }

            cached = [.. match.TargetForms];
            _tables[group] = cached;
            return cached;
        }

        /// <summary>
        /// Performs the report operation.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="key">The message key.</param>
        /// <param name="status">The status.</param>
        /// <param name="detail">The detail.</param>
        private void Report(XliffUnit unit, string key, CatalogKeyStatus status, string detail) =>
            _keys.Add(new XliffKeyReport {
                Key = key,
                Status = status,
                Source = unit.Source,
                Target = unit.Target,
                State = unit.State,
                TranslatorNote = unit.TranslatorNote,
                Detail = detail,
            });

        /// <summary>
        /// Decodes key.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <returns>The decode key text.</returns>
        private static string DecodeKey(XliffUnit unit) {
            if(unit.Context is null) {
                return unit.Id.Replace(
                    XliffConstants.ENCODED_CONTEXT_SEPARATOR.ToString(),
                    XliffConstants.CONTEXT_SEPARATOR.ToString(),
                    StringComparison.Ordinal);
            }

            var prefix = XliffConstants.EncodeId(unit.Context) + XliffConstants.ENCODED_CONTEXT_SEPARATOR;
            var suffix = unit.Id.StartsWith(prefix, StringComparison.Ordinal) ? unit.Id[prefix.Length..] : unit.Id;
            return unit.Context + XliffConstants.CONTEXT_SEPARATOR + suffix.Replace(
                XliffConstants.ENCODED_CONTEXT_SEPARATOR.ToString(),
                XliffConstants.CONTEXT_SEPARATOR.ToString(),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Reports plural key.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="plural">The plural.</param>
        /// <returns>The report plural key text.</returns>
        private static string ReportPluralKey(XliffUnit unit, XliffPluralInfo plural) {
            var baseKey = unit.Context is null
                ? plural.SingularKey
                : unit.Context + XliffConstants.CONTEXT_SEPARATOR + plural.SingularKey;
            return $"{baseKey}[{plural.Index.ToString(CultureInfo.InvariantCulture)}]";
        }

        /// <summary>
        /// Performs the pair slot key operation.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="index">The index.</param>
        /// <returns>The pair slot key text.</returns>
        private static string PairSlotKey(XliffPluralGroupKey group, int index) {
            var key = index == 0 ? group.SingularKey : group.PluralKey;
            return group.Context is null ? key : group.Context + XliffConstants.CONTEXT_SEPARATOR + key;
        }

        /// <summary>
        /// Performs the flat key operation.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="key">The message key.</param>
        /// <returns>The flat key text.</returns>
        private static string FlatKey(string? context, string key) =>
            context is null ? key : context + XliffConstants.CONTEXT_SEPARATOR + key;

        /// <summary>
        /// Performs the flat key operation.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <returns>The flat key text.</returns>
        private static string FlatKey(XliffUnit unit) =>
            unit.Plural is null ? DecodeKey(unit) : ReportPluralKey(unit, unit.Plural);
    }

    /// <summary>
    /// Represents a xliff plural group key.
    /// </summary>
    /// <param name="SingularKey">The singular key.</param>
    /// <param name="PluralKey">The plural key.</param>
    /// <param name="Context">The context.</param>
    private sealed record XliffPluralGroupKey(string SingularKey, string PluralKey, string? Context) {
        /// <summary>
        /// Determines whether the value matches.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        internal bool Matches(XliffPluralEntry entry) =>
            string.Equals(SingularKey, entry.SingularKey, StringComparison.Ordinal) &&
            string.Equals(PluralKey, entry.PluralKey, StringComparison.Ordinal) &&
            string.Equals(Context, entry.Context, StringComparison.Ordinal);
    }
}
