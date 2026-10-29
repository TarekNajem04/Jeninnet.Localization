//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Po;

/// <summary>
/// PO (gettext text) interchange for message catalogs: export plus
/// validate-then-apply import for Poedit/msgcat-style workflows. Tooling only — files
/// are interchange, never a runtime source (the runtime reader stays
/// <c>Providers.Po</c>, untouched).
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Single-culture document: <c>msgid</c>
/// is the catalog key, <c>msgstr</c> the culture's template, the <c>Language</c>
/// header the culture; contexts split into <c>msgctxt</c> + <c>msgid</c> and join with
/// EOT on import; plural pair keys covered by a plural group are excluded
/// from the flat entries (tables win, no duplication). Deterministic ordinal ordering,
/// single-line strings with C-style escapes, UTF-8 (Unicode Transformation Format, 8-bit) without BOM (Byte Order Mark) on write, BOM
/// tolerated on read.
/// </item>
/// <item>Plural-Forms policy (recorded deviation):  <c>PluralRules</c> exposes
/// categorization only — no gettext expression table — so the export accepts an
/// optional caller-supplied header written verbatim (shape-validated); when absent,
/// no header is written and the provider's documented  pair fallback applies.
/// </item>
/// <item>Conflict policy (the shared <see cref="CatalogKeyStatus"/> vocabulary):
/// fuzzy entries and empty values <c>Skipped</c> (existing templates are never
/// cleared), keys unknown to the source catalog <c>UnknownKey</c>, contextual keys
/// against an XML target / plural form counts differing from the current table /
/// forms beyond the pair slots without tables <c>UnsupportedKey</c>, equal targets
/// <c>Unchanged</c>, otherwise <c>Updated</c>. <c>StaleSource</c> is not
/// representable: the PO file carries keys, not source templates (documented
/// boundary).
/// </item>
/// <item>Translator notes map to <c>#.</c> extracted comments: exported when
/// non-empty, parsed back, surfaced per key in the report, and never applied to
/// catalog files — the caller applies them through the governed
/// <c>IResourceAdminStore.SetNoteAsync</c> step.
/// </item>
/// <item>Apply side: the whole document validates before anything is computed; the
/// dictionary-level import returns the change-set (per-entry report + updated flat
/// map + updated plural tables, <see langword="null"/> dimensions when unchanged);
/// the file-level import rewrites the target catalog at most once (never on dry-run,
/// never when unchanged).
/// </item>
/// </list>
/// </remarks>
public static class PoCatalogInterchange {
    /// <summary>
    /// Exports one culture's catalog (plus optional plural tables) to standard PO text.
    /// </summary>
    /// <param name="catalog">The culture's flat templates (must be complete, non-null values; contextual keys use the EOT-encoded form).</param>
    /// <param name="culture">The file's culture (the <c>Language</c> header value).</param>
    /// <param name="pluralEntries">Optional plural tables; grouped pair keys export as plural entries only.</param>
    /// <param name="translatorNotes">
    /// Optional translator notes keyed by catalog key (plural groups look up the
    /// encoded singular key); null/empty values are skipped. Notes are written as
    /// <c>#.</c> extracted comments.
    /// </param>
    /// <param name="pluralForms">The verbatim <c>Plural-Forms</c> header value (for example <c>"nplurals=2; plural=(n != 1);"</c>), or <see langword="null"/> to omit the header.</param>
    /// <returns>The deterministic PO text (standard header, ordinal ordering, no BOM).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the culture is empty, a key is empty or has a null template, a plural group is invalid, or <paramref name="pluralForms"/> is malformed.</exception>
    public static string ExportCatalog(
        IReadOnlyDictionary<string, string?> catalog,
        string culture,
        IReadOnlyList<PoPluralEntry>? pluralEntries = null,
        IReadOnlyDictionary<string, string>? translatorNotes = null,
        string? pluralForms = null) {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrEmpty(culture);

        if(pluralForms is not null) {
            ValidatePluralFormsHeader(pluralForms);
        }

        var groupedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach(var group in pluralEntries ?? []) {
            ValidatePluralGroup(group);
            groupedKeys.Add(Encode(group.Context, group.SingularKey));
            groupedKeys.Add(Encode(group.Context, group.PluralKey));
        }

        var entries = new List<PoEntry>(catalog.Count);
        foreach(var key in catalog.Keys) {
            if(string.IsNullOrEmpty(key)) {
                throw new ArgumentException("Catalog keys must not be empty.", nameof(catalog));
            }

            if(catalog[key] is null) {
                throw new ArgumentException($"Source template for key '{key}' must not be null.", nameof(catalog));
            }

            if(groupedKeys.Contains(key)) {
                continue;
            }

            var (context, msgId) = SplitContext(key);
            entries.Add(new PoEntry {
                Key = msgId,
                Context = context,
                Value = catalog[key]!,
                TranslatorNote = NoteFor(translatorNotes, key),
                Fuzzy = false,
            });
        }

        foreach(var group in pluralEntries ?? []) {
            entries.Add(new PoEntry {
                Key = group.SingularKey,
                Context = group.Context,
                PluralKey = group.PluralKey,
                Forms = [.. group.Forms.Select(static form => form ?? string.Empty)],
                TranslatorNote = NoteFor(translatorNotes, Encode(group.Context, group.SingularKey)),
                Fuzzy = false,
            });
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(
            Encode(left.Context, left.Key),
            Encode(right.Context, right.Key)));

        return PoTextCodec.Write(entries, culture, pluralForms);
    }

    /// <summary>
    /// Exports a catalog file (JSON or XML by extension) to a PO file (UTF-8 without BOM).
    /// </summary>
    /// <param name="catalogPath">The culture's catalog file.</param>
    /// <param name="poPath">The PO file to write.</param>
    /// <param name="culture">The file's culture (the <c>Language</c> header value).</param>
    /// <param name="pluralEntries">Optional plural tables; grouped pair keys export as plural entries only.</param>
    /// <param name="translatorNotes">Optional translator notes keyed by catalog key; null/empty values are skipped.</param>
    /// <param name="pluralForms">The verbatim <c>Plural-Forms</c> header value, or <see langword="null"/> to omit the header.</param>
    /// <exception cref="ArgumentException">Thrown when a path or culture is null or empty, or the header is malformed.</exception>
    /// <exception cref="PoInterchangeException">Thrown when the catalog file is missing or malformed.</exception>
    public static void ExportCatalogFiles(
        string catalogPath,
        string poPath,
        string culture,
        IReadOnlyList<PoPluralEntry>? pluralEntries = null,
        IReadOnlyDictionary<string, string>? translatorNotes = null,
        string? pluralForms = null) {
        ArgumentException.ThrowIfNullOrEmpty(catalogPath);
        ArgumentException.ThrowIfNullOrEmpty(poPath);

        var catalog = ReadCatalog(catalogPath);
        var text = ExportCatalog(catalog.AsLookup(), culture, pluralEntries, translatorNotes, pluralForms);
        File.WriteAllText(poPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Parses and validates PO text against the interchange subset.
    /// </summary>
    /// <param name="poText">The PO document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="poText"/> is null.</exception>
    /// <exception cref="PoInterchangeException">Thrown when the text violates the subset.</exception>
    public static PoDocument ParseDocument(string poText, string filePath) {
        var (headers, entries) = PoTextCodec.Parse(poText, filePath);
        return new PoDocument {
            FilePath = filePath,
            Headers = headers,
            Entries = entries,
        };
    }

    /// <summary>
    /// Imports a PO document into catalogs without touching files.
    /// </summary>
    /// <param name="document">The parsed PO document (one culture).</param>
    /// <param name="source">The current source-language templates (key governance; contextual keys use the EOT-encoded form).</param>
    /// <param name="currentTarget">The current target-language templates.</param>
    /// <param name="currentPlurals">The current target plural tables, or <see langword="null"/> for flat-only targets (plural pair slots).</param>
    /// <param name="xmlTarget">Whether the eventual target file is XML (contextual keys unsupported).</param>
    /// <returns>The report plus the updated catalogs (<see langword="null"/> dimensions when unchanged).</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public static PoCatalogImportResult ImportDocument(
        PoDocument document,
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        IReadOnlyList<PoPluralEntry>? currentPlurals = null,
        bool xmlTarget = false) {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(currentTarget);

        var plan = new ImportPlan(source, currentTarget, currentPlurals, xmlTarget);
        foreach(var entry in document.Entries) {
            plan.ApplyEntry(entry);
        }

        return plan.Finish();
    }

    /// <summary>
    /// Imports a PO file into a target catalog file (validate-then-apply).
    /// Only changed targets are written; dry-run mode changes nothing. Translator notes
    /// are surfaced in the returned report and never applied to the file
    /// (catalog files have no note storage).
    /// </summary>
    /// <param name="poPath">The PO file to import.</param>
    /// <param name="sourcePath">The source-language catalog file (key governance).</param>
    /// <param name="targetPath">The target-language catalog file to update.</param>
    /// <param name="currentPlurals">The current target plural tables, or <see langword="null"/> for flat-only targets.</param>
    /// <param name="options">The import options (dry-run).</param>
    /// <returns>The per-entry import report (including any translator notes).</returns>
    /// <exception cref="ArgumentException">Thrown when a path is null or empty.</exception>
    /// <exception cref="PoInterchangeException">Thrown when any input is missing or malformed. Nothing is applied then.</exception>
    public static PoImportReport ImportCatalogFiles(
        string poPath,
        string sourcePath,
        string targetPath,
        IReadOnlyList<PoPluralEntry>? currentPlurals = null,
        PoImportOptions? options = null) {
        ArgumentException.ThrowIfNullOrEmpty(poPath);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        if(!File.Exists(poPath)) {
            throw new PoInterchangeException(poPath, "PO file not found.");
        }

        string poText;
        try {
            poText = File.ReadAllText(poPath, Encoding.UTF8);
        }
        catch(IOException ex) {
            throw new PoInterchangeException(poPath, $"Cannot read PO file: {ex.Message}");
        }

        var document = ParseDocument(poText, poPath);
        var source = ReadCatalog(sourcePath);
        var target = ReadCatalog(targetPath);
        var result = ImportDocument(
            document,
            source.AsLookup(),
            target.AsLookup(),
            currentPlurals,
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
    /// Performs the encode operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="key">The message key.</param>
    /// <returns>The encode text.</returns>
    private static string Encode(string? context, string key) =>
        context is null ? key : context + PoTextCodec.CONTEXT_SEPARATOR + key;

    /// <summary>
    /// Splits context.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <returns>The split context result.</returns>
    private static (string? Context, string Key) SplitContext(string key) {
        var separator = key.IndexOf(PoTextCodec.CONTEXT_SEPARATOR);
        return separator < 0 ? (null, key) : (key[..separator], key[(separator + 1)..]);
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
    /// Validates plural group.
    /// </summary>
    /// <param name="group">The group.</param>
    private static void ValidatePluralGroup(PoPluralEntry group) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrEmpty(group.SingularKey);
        ArgumentException.ThrowIfNullOrEmpty(group.PluralKey);
        ArgumentNullException.ThrowIfNull(group.Forms);

        if(group.Forms.Count == 0) {
            throw new ArgumentException($"Plural group '{group.SingularKey}' must carry at least one form.");
        }
    }

    /// <summary>
    /// Validates plural forms header.
    /// </summary>
    /// <param name="pluralForms">The plural forms.</param>
    private static void ValidatePluralFormsHeader(string pluralForms) {
        const string npluralsSearch = "nplurals=";
        var start = pluralForms.IndexOf(npluralsSearch, StringComparison.Ordinal);
        if(start < 0) {
            throw new ArgumentException($"Invalid Plural-Forms header '{pluralForms}': missing 'nplurals='.", nameof(pluralForms));
        }

        start += npluralsSearch.Length;
        var end = pluralForms.IndexOf(';', start);
        var raw = (end < 0 ? pluralForms[start..] : pluralForms[start..end]).Trim();
        if(!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var nplurals) || nplurals < 1 || nplurals > 6) {
            throw new ArgumentException($"Invalid Plural-Forms nplurals '{raw}': expected an integer 1-6.", nameof(pluralForms));
        }

        var pluralStart = pluralForms.IndexOf("plural=", StringComparison.Ordinal);
        if(pluralStart < 0 || pluralForms[(pluralStart + "plural=".Length)..].Trim().Length == 0) {
            throw new ArgumentException($"Invalid Plural-Forms header '{pluralForms}': missing plural expression.", nameof(pluralForms));
        }
    }

    /// <summary>
    /// Reads a catalog file through the shared codec, wrapping the internal
    /// failure into the public PO exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <returns>The parsed catalog file.</returns>
    /// <exception cref="PoInterchangeException">Thrown when the file cannot be read or parsed.</exception>
    private static CatalogFileCodec.CatalogFile ReadCatalog(string path) {
        try {
            return CatalogFileCodec.Read(path);
        }
        catch(CatalogInterchangeException ex) {
            throw new PoInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Writes a catalog file through the shared codec, wrapping the internal
    /// failure into the public PO exception with message/file/line preserved.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <param name="catalog">The parsed catalog file providing the formatting facts.</param>
    /// <param name="entries">The entries to write.</param>
    /// <exception cref="PoInterchangeException">Thrown when the file cannot be written.</exception>
    private static void WriteCatalog(string path, CatalogFileCodec.CatalogFile catalog, IReadOnlyList<KeyValuePair<string, string>> entries) {
        try {
            CatalogFileCodec.Write(path, catalog, entries);
        }
        catch(CatalogInterchangeException ex) {
            throw new PoInterchangeException(ex.FilePath, ex.LineNumber, ex.Message);
        }
    }

    /// <summary>
    /// Represents an import plan.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="currentTarget">The current target.</param>
    /// <param name="currentPlurals">The current plurals.</param>
    /// <param name="xmlTarget">The xml target.</param>
    private sealed class ImportPlan(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?> currentTarget,
        IReadOnlyList<PoPluralEntry>? currentPlurals,
        bool xmlTarget) {
        private readonly Dictionary<string, string?> _updated = new(currentTarget, StringComparer.Ordinal);
        private readonly Dictionary<PoPluralGroupKey, List<string?>> _tables = [];
        private readonly List<PoKeyReport> _keys = [];
        private bool _flatChanged;
        private bool _tablesChanged;

        /// <summary>
        /// Applies entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        internal void ApplyEntry(PoEntry entry) {
            var encoded = Encode(entry.Context, entry.Key);

            if(entry.Fuzzy) {
                Report(entry, encoded, CatalogKeyStatus.Skipped, "fuzzy entry.");
                return;
            }

            if(xmlTarget && encoded.Contains(PoTextCodec.CONTEXT_SEPARATOR)) {
                Report(entry, encoded, CatalogKeyStatus.UnsupportedKey, "contextual keys cannot be XML element names.");
                return;
            }

            if(entry.PluralKey is null) {
                ApplyFlat(entry, encoded);
            } else {
                ApplyPlural(entry, encoded);
            }
        }

        /// <summary>
        /// Performs the finish operation.
        /// </summary>
        /// <returns>The finish result.</returns>
        internal PoCatalogImportResult Finish() {
            IReadOnlyDictionary<string, string?>? updatedTarget = null;
            if(_flatChanged) {
                updatedTarget = new Dictionary<string, string?>(_updated, StringComparer.Ordinal);
            }

            IReadOnlyList<PoPluralEntry>? updatedPlurals = null;
            if(_tablesChanged && currentPlurals is not null) {
                updatedPlurals = [.. currentPlurals
                    .Select(entry => {
                        var key = new PoPluralGroupKey(entry.SingularKey, entry.PluralKey, entry.Context);
                        return _tables.TryGetValue(key, out var forms)
                            ? new PoPluralEntry {
                                SingularKey = entry.SingularKey,
                                PluralKey = entry.PluralKey,
                                Context = entry.Context,
                                Forms = [.. forms],
                            }
                            : entry;
                    })];
            }

            var updated = _keys.Count(static k => k.Status == CatalogKeyStatus.Updated);
            var conflicts = _keys.Count(static k =>
                k.Status is CatalogKeyStatus.UnknownKey or CatalogKeyStatus.UnsupportedKey);

            return new PoCatalogImportResult {
                Report = new PoImportReport {
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
        /// <param name="entry">The entry.</param>
        /// <param name="encoded">The encoded.</param>
        private void ApplyFlat(PoEntry entry, string encoded) {
            if(string.IsNullOrEmpty(entry.Value)) {
                Report(entry, encoded, CatalogKeyStatus.Skipped, "empty msgstr.");
                return;
            }

            if(!source.ContainsKey(encoded)) {
                Report(entry, encoded, CatalogKeyStatus.UnknownKey, "key is unknown to the source catalog.");
                return;
            }

            currentTarget.TryGetValue(encoded, out var current);
            if(string.Equals(entry.Value, current, StringComparison.Ordinal)) {
                Report(entry, encoded, CatalogKeyStatus.Unchanged, "target already matches.");
                return;
            }

            _updated[encoded] = entry.Value;
            _flatChanged = true;
            Report(entry, encoded, CatalogKeyStatus.Updated, "target applied.");
        }

        /// <summary>
        /// Applies plural.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="encodedSingular">The encoded singular.</param>
        private void ApplyPlural(PoEntry entry, string encodedSingular) {
            var pluralKey = entry.PluralKey!;
            var encodedPlural = Encode(entry.Context, pluralKey);
            var forms = entry.Forms!;

            if(!source.ContainsKey(encodedSingular) || !source.ContainsKey(encodedPlural)) {
                Report(entry, encodedSingular, CatalogKeyStatus.UnknownKey, "plural pair is unknown to the source catalog.");
                return;
            }

            if(forms.All(static form => form.Length == 0)) {
                Report(entry, encodedSingular, CatalogKeyStatus.Skipped, "all plural forms are empty.");
                return;
            }

            var group = new PoPluralGroupKey(entry.Key, pluralKey, entry.Context);
            if(currentPlurals is not null && FindTable(group) is List<string?> table) {
                ApplyPluralTable(entry, encodedSingular, forms, table);
                return;
            }

            ApplyPluralPair(entry, encodedSingular, encodedPlural, forms);
        }

        /// <summary>
        /// Merges an incoming plural entry into an existing plural table, slot by slot.
        /// </summary>
        /// <param name="entry">The incoming plural entry.</param>
        /// <param name="encodedSingular">The context-encoded singular key used for reporting.</param>
        /// <param name="forms">The incoming plural forms.</param>
        /// <param name="table">The current plural table, updated in place.</param>
        private void ApplyPluralTable(
            PoEntry entry,
            string encodedSingular,
            IReadOnlyList<string> forms,
            List<string?> table) {
            if(forms.Count != table.Count) {
                Report(entry, encodedSingular, CatalogKeyStatus.UnsupportedKey, "form count differs from the current plural table.");
                return;
            }

            var changed = false;
            for(var index = 0; index < forms.Count; index++) {
                if(forms[index].Length == 0) {
                    continue;
                }

                if(!string.Equals(forms[index], table[index], StringComparison.Ordinal)) {
                    table[index] = forms[index];
                    changed = true;
                }
            }

            if(changed) {
                _tablesChanged = true;
                Report(entry, encodedSingular, CatalogKeyStatus.Updated, "plural forms applied.");
            } else {
                Report(entry, encodedSingular, CatalogKeyStatus.Unchanged, "plural forms already match.");
            }
        }

        /// <summary>
        /// Applies an incoming plural entry to the flat singular/plural pair slots.
        /// </summary>
        /// <param name="entry">The incoming plural entry.</param>
        /// <param name="encodedSingular">The context-encoded singular key.</param>
        /// <param name="encodedPlural">The context-encoded plural key.</param>
        /// <param name="forms">The incoming plural forms.</param>
        private void ApplyPluralPair(
            PoEntry entry,
            string encodedSingular,
            string encodedPlural,
            IReadOnlyList<string> forms) {
            if(forms.Count > 2) {
                Report(entry, encodedSingular, CatalogKeyStatus.UnsupportedKey, "flat catalogs only carry the singular/plural pair slots; supply the current plural tables.");
                return;
            }

            var changedSlot = false;
            changedSlot |= ApplyPairSlot(encodedSingular, forms[0]);
            if(forms.Count == 2) {
                changedSlot |= ApplyPairSlot(encodedPlural, forms[1]);
            }

            if(changedSlot) {
                _flatChanged = true;
                Report(entry, encodedSingular, CatalogKeyStatus.Updated, "plural pair slots applied.");
            } else {
                Report(entry, encodedSingular, CatalogKeyStatus.Unchanged, "plural pair slots already match.");
            }
        }

        /// <summary>
        /// Applies pair slot.
        /// </summary>
        /// <param name="encoded">The encoded.</param>
        /// <param name="form">The form.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        private bool ApplyPairSlot(string encoded, string form) {
            if(form.Length == 0) {
                return false;
            }

            currentTarget.TryGetValue(encoded, out var current);
            if(string.Equals(form, current, StringComparison.Ordinal)) {
                return false;
            }

            _updated[encoded] = form;
            return true;
        }

        /// <summary>
        /// Finds table.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <returns>The table sequence.</returns>
        private List<string?>? FindTable(PoPluralGroupKey group) {
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

            cached = [.. match.Forms];
            _tables[group] = cached;
            return cached;
        }

        /// <summary>
        /// Performs the report operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="key">The message key.</param>
        /// <param name="status">The status.</param>
        /// <param name="detail">The detail.</param>
        private void Report(PoEntry entry, string key, CatalogKeyStatus status, string detail) =>
            _keys.Add(new PoKeyReport {
                Key = key,
                Status = status,
                Value = entry.PluralKey is null ? entry.Value : null,
                TranslatorNote = entry.TranslatorNote,
                Detail = detail,
            });
    }

    /// <summary>
    /// Represents a po plural group key.
    /// </summary>
    /// <param name="SingularKey">The singular key.</param>
    /// <param name="PluralKey">The plural key.</param>
    /// <param name="Context">The context.</param>
    private sealed record PoPluralGroupKey(string SingularKey, string PluralKey, string? Context) {
        /// <summary>
        /// Determines whether the value matches.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        internal bool Matches(PoPluralEntry entry) =>
            string.Equals(SingularKey, entry.SingularKey, StringComparison.Ordinal) &&
            string.Equals(PluralKey, entry.PluralKey, StringComparison.Ordinal) &&
            string.Equals(Context, entry.Context, StringComparison.Ordinal);
    }
}
