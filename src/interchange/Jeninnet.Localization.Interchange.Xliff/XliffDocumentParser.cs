//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Xliff;

/// <summary>
/// Parses and validates XLIFF (XML Localization Interchange File Format) 2.0 text against the  schema subset.
/// Anything outside the subset fails fast with file/line diagnostics.
/// </summary>
internal static class XliffDocumentParser {
    /// <summary>
    /// Parses XLIFF text into a document, validating the whole subset first.
    /// </summary>
    /// <param name="xliffText">The XLIFF document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="XliffInterchangeException">Thrown when the text is malformed.</exception>
    internal static XliffDocument Parse(string xliffText, string filePath) {
        ArgumentNullException.ThrowIfNull(xliffText);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        XDocument doc;
        try {
            doc = XDocument.Parse(xliffText, LoadOptions.SetLineInfo);
        }
        catch(XmlException ex) {
            throw new XliffInterchangeException(filePath, ex.LineNumber, $"Malformed XML: {ex.Message}");
        }

        var root = doc.Root ?? throw new XliffInterchangeException(filePath, 0, "Missing XLIFF root element.");
        if(!string.Equals(root.Name.LocalName, "xliff", StringComparison.Ordinal)) {
            throw new XliffInterchangeException(filePath, LineOf(root), $"Expected the XLIFF root element 'xliff' but found '{root.Name.LocalName}'.");
        }

        RejectNamespacedChildren(root, filePath, "file");
        var version = Attribute(root, "version");
        if(!string.Equals(version, "2.0", StringComparison.Ordinal)) {
            throw new XliffInterchangeException(filePath, LineOf(root), $"Unsupported XLIFF version '{version ?? "<missing>"}': this interchange supports version 2.0.");
        }

        var srcLang = Attribute(root, "srcLang");
        if(string.IsNullOrEmpty(srcLang)) {
            throw new XliffInterchangeException(filePath, LineOf(root), "The XLIFF root element must carry a non-empty 'srcLang'.");
        }

        var trgLang = Attribute(root, "trgLang") ?? string.Empty;
        var files = root.Elements("file").ToArray();
        if(files.Length != 1) {
            throw new XliffInterchangeException(filePath, LineOf(root), $"This interchange supports exactly one 'file' element per document but found {files.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        var file = files[0];
        RejectNamespacedChildren(file, filePath, "unit");
        var fileId = Attribute(file, "id");
        if(string.IsNullOrEmpty(fileId)) {
            throw new XliffInterchangeException(filePath, LineOf(file), "The XLIFF 'file' element must carry a non-empty 'id'.");
        }

        var units = new List<XliffUnit>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach(var element in file.Elements()) {
            if(element.Name.Namespace != XNamespace.None) {
                continue;
            }

            if(!string.Equals(element.Name.LocalName, "unit", StringComparison.Ordinal)) {
                throw new XliffInterchangeException(filePath, LineOf(element), $"Unsupported XLIFF element '{element.Name.LocalName}': this interchange supports 'unit' elements only.");
            }

            units.Add(ParseUnit(element, filePath, seenIds));
        }

        return new XliffDocument {
            SourceLanguage = srcLang,
            TargetLanguage = trgLang,
            FileId = fileId,
            Units = units,
        };
    }

    /// <summary>
    /// Parses unit.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="seenIds">The seen ids.</param>
    /// <returns>The unit result.</returns>
    private static XliffUnit ParseUnit(XElement unit, string filePath, HashSet<string> seenIds) {
        RejectNamespacedChildren(unit, filePath, "notes", "segment");
        var id = Attribute(unit, "id");
        if(string.IsNullOrEmpty(id)) {
            throw new XliffInterchangeException(filePath, LineOf(unit), "Every XLIFF 'unit' must carry a non-empty 'id'.");
        }

        if(!seenIds.Add(id)) {
            throw new XliffInterchangeException(filePath, LineOf(unit), $"Duplicate XLIFF unit id '{id}'.");
        }

        var translate = Attribute(unit, "translate") switch {
            null or "yes" => true,
            "no" => false,
            var other => throw new XliffInterchangeException(filePath, LineOf(unit), $"Invalid translate value '{other}' on unit '{id}': expected 'yes' or 'no'."),
        };

        var notes = new UnitNoteReader(filePath, id).Read(unit);
        var plural = ResolvePlural(unit, filePath, id, notes);
        var segment = ReadSegment(unit, filePath, id);
        var effectiveTranslate = translate && !string.Equals(Attribute(segment.Element, "translate"), "no", StringComparison.Ordinal);

        return new XliffUnit {
            Id = id,
            Source = segment.Source,
            Target = segment.Target,
            State = segment.State,
            Translate = effectiveTranslate,
            Context = notes.Context,
            TranslatorNote = notes.TranslatorNote,
            Plural = plural,
        };
    }

    /// <summary>
    /// Resolves the plural info (validating its notes and encoded id) or, for a
    /// contextual non-plural unit, validates the contextual encoded id.
    /// </summary>
    /// <param name="unit">The <c>unit</c> element.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="id">The unit id.</param>
    /// <param name="notes">The notes parsed from the unit.</param>
    /// <returns>The plural info, or <see langword="null"/> for a non-plural unit.</returns>
    /// <exception cref="XliffInterchangeException">Thrown when the plural notes or encoded id are inconsistent.</exception>
    private static XliffPluralInfo? ResolvePlural(XElement unit, string filePath, string id, UnitNotes notes) {
        var pluralSingular = notes.PluralSingular;
        var pluralOther = notes.PluralOther;
        var pluralIndex = notes.PluralIndex;
        var pluralCount = notes.PluralCount;

        if(pluralSingular is not null || pluralOther is not null || pluralIndex.HasValue || pluralCount.HasValue) {
            if(string.IsNullOrEmpty(pluralSingular) || string.IsNullOrEmpty(pluralOther) || !pluralIndex.HasValue || !pluralCount.HasValue) {
                throw new XliffInterchangeException(filePath, LineOf(unit), $"Plural unit '{id}' must carry all plural notes (singular, other, index, count).");
            }

            if(pluralCount.Value < 1 || pluralIndex.Value < 0 || pluralIndex.Value >= pluralCount.Value) {
                throw new XliffInterchangeException(filePath, LineOf(unit), $"Plural unit '{id}' has index {pluralIndex.Value.ToString(CultureInfo.InvariantCulture)} out of {pluralCount.Value.ToString(CultureInfo.InvariantCulture)} forms.");
            }

            var expectedId = XliffConstants.EncodePluralId(PluralBaseId(notes.Context, pluralSingular), pluralIndex.Value);
            if(!string.Equals(id, expectedId, StringComparison.Ordinal)) {
                throw new XliffInterchangeException(filePath, LineOf(unit), $"Plural unit id '{id}' does not match its plural notes (expected '{expectedId}').");
            }

            return new XliffPluralInfo {
                SingularKey = pluralSingular,
                PluralKey = pluralOther,
                Index = pluralIndex.Value,
                Count = pluralCount.Value,
            };
        }

        var context = notes.Context;
        if(context is not null) {
            var expectedId = XliffConstants.EncodeId(context) + XliffConstants.ENCODED_CONTEXT_SEPARATOR + XliffConstants.EncodeId(Remainder(id, context, filePath, unit));
            if(!string.Equals(id, expectedId, StringComparison.Ordinal)) {
                throw new XliffInterchangeException(filePath, LineOf(unit), $"Contextual unit id '{id}' does not match its context note.");
            }
        }

        return null;
    }

    /// <summary>
    /// Reads and validates the unit's single <c>segment</c>: state, translate value,
    /// exactly one plain-text <c>source</c>, and at most one plain-text <c>target</c>.
    /// </summary>
    /// <param name="unit">The <c>unit</c> element.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="id">The unit id used in diagnostics.</param>
    /// <returns>The segment element and its source, target, and state values.</returns>
    /// <exception cref="XliffInterchangeException">Thrown when the segment violates the subset.</exception>
    private static SegmentContent ReadSegment(XElement unit, string filePath, string id) {
        var segments = unit.Elements("segment").ToArray();
        if(segments.Length != 1) {
            throw new XliffInterchangeException(filePath, LineOf(unit), $"Unit '{id}' must carry exactly one 'segment' but has {segments.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        var segment = segments[0];
        RejectNamespacedChildren(segment, filePath, "source", "target");
        var state = Attribute(segment, "state") ?? "initial";
        if(!XliffConstants.States.Contains(state, StringComparer.Ordinal)) {
            throw new XliffInterchangeException(filePath, LineOf(segment), $"Invalid segment state '{state}' on unit '{id}'.");
        }

        if(Attribute(segment, "translate") is string segmentTranslate && !string.Equals(segmentTranslate, "yes", StringComparison.Ordinal) && !string.Equals(segmentTranslate, "no", StringComparison.Ordinal)) {
            throw new XliffInterchangeException(filePath, LineOf(segment), $"Invalid translate value '{segmentTranslate}' on unit '{id}'.");
        }

        var sources = segment.Elements("source").ToArray();
        if(sources.Length != 1) {
            throw new XliffInterchangeException(filePath, LineOf(segment), $"Unit '{id}' must carry exactly one 'source'.");
        }

        if(sources[0].Elements().Any()) {
            throw new XliffInterchangeException(filePath, LineOf(sources[0]), $"Unit '{id}' source must be plain text: inline markup is outside the interchange subset.");
        }

        var targets = segment.Elements("target").ToArray();
        if(targets.Length > 1) {
            throw new XliffInterchangeException(filePath, LineOf(segment), $"Unit '{id}' must carry at most one 'target'.");
        }

        string? target = null;
        if(targets.Length == 1) {
            if(targets[0].Elements().Any()) {
                throw new XliffInterchangeException(filePath, LineOf(targets[0]), $"Unit '{id}' target must be plain text: inline markup is outside the interchange subset.");
            }

            target = targets[0].Value;
        }

        return new SegmentContent(segment, sources[0].Value, target, state);
    }

    /// <summary>
    /// The context, plural, and translator-note values read from a unit's notes.
    /// </summary>
    /// <param name="Context">The context note, or <see langword="null"/> when absent.</param>
    /// <param name="PluralSingular">The plural singular note, or <see langword="null"/> when absent.</param>
    /// <param name="PluralOther">The plural other note, or <see langword="null"/> when absent.</param>
    /// <param name="TranslatorNote">The translator note, or <see langword="null"/> when absent.</param>
    /// <param name="PluralIndex">The plural index note, or <see langword="null"/> when absent.</param>
    /// <param name="PluralCount">The plural count note, or <see langword="null"/> when absent.</param>
    private sealed record UnitNotes(string? Context, string? PluralSingular, string? PluralOther, string? TranslatorNote, int? PluralIndex, int? PluralCount);

    /// <summary>
    /// The validated segment content: the segment element (for the effective translate
    /// flag), its source, optional target, and state.
    /// </summary>
    /// <param name="Element">The validated <c>segment</c> element.</param>
    /// <param name="Source">The segment's source text.</param>
    /// <param name="Target">The segment's target text, or <see langword="null"/> when absent.</param>
    /// <param name="State">The segment state.</param>
    private sealed record SegmentContent(XElement Element, string Source, string? Target, string State);

    /// <summary>
    /// Reads a unit's <c>notes</c> element into the interchange's context, plural, and
    /// translator-note fields, rejecting conflicting duplicate notes and non-integer
    /// plural indexes.
    /// </summary>
    /// <param name="filePath">The file path for error reporting.</param>
    /// <param name="id">The unit ID.</param>
    private sealed class UnitNoteReader(string filePath, string id) {
        private readonly string _filePath = filePath;
        private readonly string _id = id;
        private string? _context;
        private string? _pluralSingular;
        private string? _pluralOther;
        private string? _translatorNote;
        private int? _pluralIndex;
        private int? _pluralCount;

        /// <summary>
        /// Reads the unit's notes, when present, and returns the accumulated values.
        /// </summary>
        /// <param name="unit">The <c>unit</c> element.</param>
        /// <returns>The unit's context, plural, and translator-note values.</returns>
        /// <exception cref="XliffInterchangeException">Thrown when a note violates the subset.</exception>
        internal UnitNotes Read(XElement unit) {
            var notes = unit.Element("notes");
            if(notes is not null) {
                RejectNamespacedChildren(notes, _filePath, "note");
                foreach(var note in notes.Elements("note")) {
                    ReadNote(note);
                }
            }

            return new UnitNotes(_context, _pluralSingular, _pluralOther, _translatorNote, _pluralIndex, _pluralCount);
        }

        /// <summary>
        /// Reads one note by its <c>category</c>, rejecting conflicting context or
        /// translator notes.
        /// </summary>
        /// <param name="note">The <c>note</c> element.</param>
        /// <exception cref="XliffInterchangeException">Thrown when the note violates the subset.</exception>
        private void ReadNote(XElement note) {
            var category = Attribute(note, "category");
            var value = note.Value;
            switch(category) {
                case XliffConstants.NOTE_CONTEXT:
                    _context ??= value;
                    if(!string.Equals(_context, value, StringComparison.Ordinal)) {
                        throw new XliffInterchangeException(_filePath, LineOf(note), $"Conflicting context notes on unit '{_id}'.");
                    }

                    break;
                case XliffConstants.NOTE_PLURAL_SINGULAR:
                    _pluralSingular = value;
                    break;
                case XliffConstants.NOTE_PLURAL_OTHER:
                    _pluralOther = value;
                    break;
                case XliffConstants.NOTE_PLURAL_INDEX:
                    _pluralIndex = ParseIndex(note, value);
                    break;
                case XliffConstants.NOTE_PLURAL_COUNT:
                    _pluralCount = ParseIndex(note, value);
                    break;
                case XliffConstants.NOTE_TRANSLATOR:
                    _translatorNote ??= value;
                    if(!string.Equals(_translatorNote, value, StringComparison.Ordinal)) {
                        throw new XliffInterchangeException(_filePath, LineOf(note), $"Conflicting translator notes on unit '{_id}'.");
                    }

                    break;
            }
        }

        /// <summary>
        /// Parses a plural index/count note as a non-negative invariant integer.
        /// </summary>
        /// <param name="note">The <c>note</c> element used for diagnostics.</param>
        /// <param name="value">The note's text.</param>
        /// <returns>The parsed integer.</returns>
        /// <exception cref="XliffInterchangeException">Thrown when the note is not an integer.</exception>
        private int ParseIndex(XElement note, string value) {
            if(!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) {
                throw new XliffInterchangeException(_filePath, LineOf(note), $"Plural unit '{_id}' carries a non-integer plural note '{value}'.");
            }

            return index;
        }
    }

    /// <summary>
    /// Performs the plural base id operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="singular">The singular.</param>
    /// <returns>The plural base id text.</returns>
    private static string PluralBaseId(string? context, string singular) =>
        context is null
            ? XliffConstants.EncodeId(singular)
            : XliffConstants.EncodeId(context) + XliffConstants.ENCODED_CONTEXT_SEPARATOR + XliffConstants.EncodeId(singular);

    /// <summary>
    /// Performs the remainder operation.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="context">The context.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>The remainder text.</returns>
    private static string Remainder(string id, string context, string filePath, XElement unit) {
        var prefix = XliffConstants.EncodeId(context) + XliffConstants.ENCODED_CONTEXT_SEPARATOR;
        if(!id.StartsWith(prefix, StringComparison.Ordinal) || id.Length == prefix.Length) {
            throw new XliffInterchangeException(filePath, LineOf(unit), $"Contextual unit id '{id}' does not match its context note.");
        }

        return id[prefix.Length..];
    }

    /// <summary>
    /// Rejects namespaced children.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="allowed">The allowed.</param>
    private static void RejectNamespacedChildren(XElement parent, string filePath, params string[] allowed) {
        foreach(var child in parent.Elements()) {
            if(child.Name.Namespace != XNamespace.None) {
                continue;
            }

            if(!allowed.Contains(child.Name.LocalName, StringComparer.Ordinal)) {
                throw new XliffInterchangeException(filePath, LineOf(child), $"Unsupported XLIFF element '{child.Name.LocalName}': outside the interchange subset.");
            }
        }
    }

    /// <summary>
    /// Performs the attribute operation.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The name.</param>
    /// <returns>The attribute result.</returns>
    private static string? Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value;

    /// <summary>
    /// Performs the line of operation.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The line of value.</returns>
    private static int LineOf(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
