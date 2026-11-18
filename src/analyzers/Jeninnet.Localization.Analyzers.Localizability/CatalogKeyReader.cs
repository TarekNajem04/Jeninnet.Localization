//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.IO;
using System.Xml;

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// Tolerant catalog key reader for the catalog-backed rules (JLNLOC004–JLNLOC006):
/// extracts key sets, first templates, and first line numbers from the generator's
/// catalog inputs (<c>*.loc.json</c>, <c>*.resx</c>, <c>*.po</c>, <c>*.xliff</c>/
/// <c>*.xlf</c>). Report-only inputs: malformed files contribute no keys and never
/// throw — catalog diagnostics belong to the generator, never to this analyzer.
/// Reads once per compilation; callers cache the result.
/// </summary>
internal static class CatalogKeyReader {
    /// <summary>
    /// One catalog's key facts: the key set, the first template per key, and the
    /// first 1-based line per key.
    /// </summary>
    internal sealed class CatalogKeys {
        /// <summary>
        /// Gets the key set (ordinal, case-sensitive).
        /// </summary>
        internal HashSet<string> Keys { get; } = [with(StringComparer.Ordinal)];

        /// <summary>
        /// Gets the first template per key.
        /// </summary>
        internal Dictionary<string, string> Templates { get; } = [with(StringComparer.Ordinal)];

        /// <summary>
        /// Gets the first 1-based line per key.
        /// </summary>
        internal Dictionary<string, int> Lines { get; } = [with(StringComparer.Ordinal)];

        /// <summary>
        /// Adds a key occurrence, keeping the first template and line.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="template">The template.</param>
        /// <param name="line">The 1-based line.</param>
        internal void Add(string key, string template, int line) {
            Keys.Add(key);
            if(!Templates.ContainsKey(key)) {
                Templates[key] = template;
            }

            if(!Lines.ContainsKey(key)) {
                Lines[key] = line;
            }
        }
    }

    /// <summary>
    /// Determines whether the path is a generator catalog input.
    /// </summary>
    /// <param name="path">The AdditionalFile path.</param>
    /// <returns>true when the extension is a catalog input.</returns>
    internal static bool IsCatalogFile(string path) {
        if(string.IsNullOrEmpty(path)) {
            return false;
        }

        return path.EndsWith(".loc.json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".po", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".xliff", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".xlf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts top-level placeholder identifiers: groups are brace-balanced, and the
    /// identifier is the trimmed text before the first depth-zero comma.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The ordinal identifier set (possibly empty).</returns>
    internal static HashSet<string> PlaceholderIds(string template) =>
        PlaceholderScanner.ExtractIdentifiers(template);

    /// <summary>
    /// Reads one catalog file's key facts, tolerantly.
    /// </summary>
    /// <param name="path">The file path (diagnostics only).</param>
    /// <param name="text">The file text.</param>
    /// <returns>The key facts (possibly empty, never null).</returns>
    internal static CatalogKeys Read(string path, string text) {
        var result = new CatalogKeys();
        if(string.IsNullOrEmpty(text)) {
            return result;
        }

        try {
            if(path.EndsWith(".loc.json", StringComparison.OrdinalIgnoreCase)) {
                ReadLocJson(text, result);
            } else if(path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)) {
                ReadResX(text, result);
            } else if(path.EndsWith(".po", StringComparison.OrdinalIgnoreCase)) {
                ReadPo(text, result);
            } else {
                ReadXliffUnits(text, result);
            }
        }
        catch(Exception exception) when(exception is not (OutOfMemoryException or StackOverflowException)) {
            // Tolerant by design: a malformed catalog contributes nothing.
            return new CatalogKeys();
        }

        return result;
    }

    /// <summary>
    /// Reads flat JSON key/template pairs at depth one.
    /// </summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="result">The key facts sink.</param>
    private static void ReadLocJson(string text, CatalogKeys result) {
        var index = 0;
        var line = 1;
        var depth = 0;
        while(index < text.Length) {
            var current = text[index];
            if(current == '\n') {
                line++;
                index++;
                continue;
            }

            if(current is '{' or '[') {
                depth++;
                index++;
                continue;
            }

            if(current is '}' or ']') {
                depth--;
                index++;
                continue;
            }

            if(current != '"' || depth != 1) {
                index++;
                continue;
            }

            var keyLine = line;
            var key = ReadJsonString(text, ref index, ref line);
            if(key is null) {
                continue;
            }

            SkipJsonWhitespace(text, ref index, ref line);
            if(index >= text.Length || text[index] != ':') {
                continue;
            }

            index++;
            SkipJsonWhitespace(text, ref index, ref line);
            if(index < text.Length && text[index] == '"') {
                TryAddJsonValue(text, ref index, ref line, key, keyLine, result);
            }
        }
    }

    /// <summary>
    /// Reads one depth-one string value and adds it when well-formed.
    /// </summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="index">The index (advanced past the value).</param>
    /// <param name="line">The current line.</param>
    /// <param name="key">The key.</param>
    /// <param name="keyLine">The 1-based key line.</param>
    /// <param name="result">The key facts sink.</param>
    private static void TryAddJsonValue(string text, ref int index, ref int line, string key, int keyLine, CatalogKeys result) {
        var value = ReadJsonString(text, ref index, ref line);
        if(value is not null) {
            result.Add(key, value, keyLine);
        }
    }

    /// <summary>
    /// Reads one JSON string starting at the opening quote.
    /// </summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="index">The index (advanced past the closing quote).</param>
    /// <param name="line">The current line (advanced with newlines).</param>
    /// <returns>The unescaped value, or null when unterminated.</returns>
    private static string? ReadJsonString(string text, ref int index, ref int line) {
        var builder = new System.Text.StringBuilder();
        index++;
        while(index < text.Length) {
            var current = text[index];
            if(current == '"') {
                index++;
                return builder.ToString();
            }

            if(current == '\n') {
                line++;
            }

            if(current == '\\' && index + 1 < text.Length) {
                index++;
                AppendJsonEscape(builder, text[index]);
                index++;
                continue;
            }

            builder.Append(current);
            index++;
        }

        return null;
    }

    /// <summary>
    /// Appends one JSON escape sequence body.
    /// </summary>
    /// <param name="builder">The value sink.</param>
    /// <param name="escaped">The character after the backslash.</param>
    private static void AppendJsonEscape(System.Text.StringBuilder builder, char escaped) {
        builder.Append(escaped switch {
            '"' => "\"",
            '\\' => "\\",
            '/' => "/",
            'b' => "\b",
            'f' => "\f",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            _ => "\\" + escaped,
        });
    }

    /// <summary>
    /// Skips JSON whitespace and stray commas/colons between members.
    /// </summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="index">The index.</param>
    /// <param name="line">The current line.</param>
    private static void SkipJsonWhitespace(string text, ref int index, ref int line) {
        while(index < text.Length) {
            var current = text[index];
            if(current == '\n') {
                line++;
                index++;
            } else if(current is ' ' or '\t' or '\r') {
                index++;
            } else {
                return;
            }
        }
    }

    /// <summary>
    /// Reads ResX data names and values.
    /// </summary>
    /// <param name="text">The ResX text.</param>
    /// <param name="result">The key facts sink.</param>
    private static void ReadResX(string text, CatalogKeys result) {
        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreWhitespace = true,
        };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        var lineInfo = reader as IXmlLineInfo;
        var state = new ResXReadState(result);
        while(reader.Read()) {
            if(reader.NodeType == XmlNodeType.Element && reader.Name == "data") {
                state.BeginData(reader, lineInfo);
            } else if(reader.NodeType == XmlNodeType.Element && reader.Name == "value" && state.HasName) {
                state.BeginValue();
            } else if(reader.NodeType == XmlNodeType.Text && state.InValue) {
                state.AppendValueText(reader.Value);
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "value") {
                state.EndValue();
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "data") {
                state.CompleteData();
            }
        }
    }

    /// <summary>
    /// Tracks one in-progress ResX <c>&lt;data&gt;</c> element across reader callbacks.
    /// </summary>
    private sealed class ResXReadState {
        private readonly CatalogKeys _result;
        private string? _name;
        private string? _value;
        private int _nameLine;
        private bool _inValue;
        private readonly System.Text.StringBuilder _valueBuilder = new();

        internal ResXReadState(CatalogKeys result) => _result = result;

        internal bool HasName => _name is not null;

        internal bool InValue => _inValue;

        internal void BeginData(XmlReader reader, IXmlLineInfo? lineInfo) {
            _name = reader.GetAttribute("name");
            _nameLine = lineInfo?.LineNumber ?? 0;
            _value = null;
        }

        internal void BeginValue() {
            _inValue = true;
            _valueBuilder.Length = 0;
        }

        internal void AppendValueText(string text) {
            _valueBuilder.Append(text);
        }

        internal void EndValue() {
            _inValue = false;
            _value = _valueBuilder.ToString();
        }

        internal void CompleteData() {
            if(_name is { Length: > 0 }) {
                _result.Add(_name, _value ?? string.Empty, _nameLine <= 0 ? 1 : _nameLine);
            }

            _name = null;
            _value = null;
        }
    }

    /// <summary>
    /// Reads PO msgids and first msgstr forms. Multiline strings accumulate; only
    /// the singular (or <c>msgstr[0]</c>) form is kept as the template.
    /// </summary>
    /// <param name="text">The PO text.</param>
    /// <param name="result">The key facts sink.</param>
    private static void ReadPo(string text, CatalogKeys result) {
        var lines = text.Split('\n');
        var state = new PoReadState(result);
        for(var number = 0; number < lines.Length; number++) {
            state.ProcessLine(lines[number].Trim(), number);
        }

        state.Flush();
    }

    /// <summary>
    /// Tracks one in-progress PO entry across lines: the singular id, the first template,
    /// and the in-msgid/in-template continuation state.
    /// </summary>
    private sealed class PoReadState {
        private readonly CatalogKeys _result;
        private readonly System.Text.StringBuilder _msgid = new();
        private int _msgidLine;
        private bool _hasMsgid;
        private readonly System.Text.StringBuilder _template = new();
        private bool _hasTemplate;
        private bool _inMsgid;
        private bool _inTemplate;

        internal PoReadState(CatalogKeys result) => _result = result;

        internal void Flush() {
            if(_hasMsgid && _msgid.Length > 0) {
                _result.Add(_msgid.ToString(), _hasTemplate ? _template.ToString() : string.Empty, _msgidLine <= 0 ? 1 : _msgidLine);
            }

            _msgid.Length = 0;
            _msgidLine = 0;
            _hasMsgid = false;
            _template.Length = 0;
            _hasTemplate = false;
            _inMsgid = false;
            _inTemplate = false;
        }

        internal void ProcessLine(string trimmed, int number) {
            if(trimmed.Length == 0) {
                Flush();
                return;
            }

            if(trimmed[0] == '#') {
                return;
            }

            if(trimmed.StartsWith("msgctxt", StringComparison.Ordinal) ||
                trimmed.StartsWith("msgid_plural", StringComparison.Ordinal)) {
                return;
            }

            if(trimmed.StartsWith("msgstr[", StringComparison.Ordinal)) {
                ProcessPluralForm(trimmed);
                return;
            }

            if(trimmed.StartsWith("msgid", StringComparison.Ordinal)) {
                StartMsgid(trimmed, number);
                return;
            }

            if(trimmed.StartsWith("msgstr", StringComparison.Ordinal)) {
                StartTemplate(trimmed);
                return;
            }

            if(trimmed[0] == '"') {
                AppendContinuation(trimmed);
            }
        }

        private void ProcessPluralForm(string trimmed) {
            var bracket = trimmed.IndexOf(']');
            _inMsgid = false;
            _inTemplate = false;
            if(bracket > 7 && int.TryParse(trimmed.Substring(7, bracket - 7), out var slot) && slot == 0) {
                var quoted = ExtractQuoted(trimmed.Substring(bracket + 1).TrimStart());
                if(quoted is not null) {
                    _template.Length = 0;
                    _template.Append(quoted);
                    _hasTemplate = true;
                    _inTemplate = true;
                }
            }
        }

        private void StartMsgid(string trimmed, int number) {
            Flush();
            var quoted = ExtractQuoted(trimmed.Substring(5).TrimStart());
            if(quoted is not null) {
                _msgid.Append(quoted);
                _msgidLine = number + 1;
                _hasMsgid = true;
                _inMsgid = true;
            }
        }

        private void StartTemplate(string trimmed) {
            _inMsgid = false;
            _inTemplate = false;
            var quoted = ExtractQuoted(trimmed.Substring(6).TrimStart());
            if(quoted is not null) {
                _template.Length = 0;
                _template.Append(quoted);
                _hasTemplate = true;
                _inTemplate = true;
            }
        }

        private void AppendContinuation(string trimmed) {
            var quoted = ExtractQuoted(trimmed);
            if(quoted is null) {
                return;
            }

            if(_inMsgid) {
                _msgid.Append(quoted);
            } else if(_inTemplate) {
                _template.Append(quoted);
            }
        }

        /// <summary>
        /// Extracts one quoted PO body (unescaping the common escapes).
        /// </summary>
        /// <param name="text">The text starting at the opening quote.</param>
        /// <returns>The unescaped body, or null when not quoted.</returns>
        private static string? ExtractQuoted(string text) {
            var trimmed = text.Trim();
            if(trimmed.Length < 2 || trimmed[0] != '"' || trimmed[trimmed.Length - 1] != '"') {
                return null;
            }

            var body = trimmed.Substring(1, trimmed.Length - 2);
            return body.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
        }
    }

    /// <summary>
    /// Reads XLIFF 2.0 unit ids with target (or source) templates.
    /// </summary>
    /// <param name="text">The XLIFF text.</param>
    /// <param name="result">The key facts sink.</param>
    private static void ReadXliffUnits(string text, CatalogKeys result) {
        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreWhitespace = true,
        };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        var state = new XliffReadState(result, reader as IXmlLineInfo);
        while(reader.Read()) {
            if(reader.NodeType == XmlNodeType.Element && reader.Name == "unit") {
                state.BeginUnit(reader);
            } else if(reader.NodeType == XmlNodeType.Element && (reader.Name == XliffReadState.SourceElement || reader.Name == XliffReadState.TargetElement) && state.HasId) {
                state.BeginText(reader);
            } else if(reader.NodeType == XmlNodeType.Text && state.InText) {
                state.AppendText(reader.Value);
            } else if(reader.NodeType == XmlNodeType.EndElement && (reader.Name == XliffReadState.SourceElement || reader.Name == XliffReadState.TargetElement)) {
                state.EndText(reader);
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "unit") {
                state.CompleteUnit();
            }
        }
    }

    /// <summary>
    /// Tracks one in-progress XLIFF <c>unit</c> across reader callbacks.
    /// </summary>
    private sealed class XliffReadState {
        internal const string SourceElement = "source";
        internal const string TargetElement = "target";

        private readonly CatalogKeys _result;
        private readonly IXmlLineInfo? _lineInfo;
        private string? _id;
        private int _idLine;
        private string? _source;
        private string? _target;
        private string _element = string.Empty;
        private readonly System.Text.StringBuilder _builder = new();

        internal XliffReadState(CatalogKeys result, IXmlLineInfo? lineInfo) {
            _result = result;
            _lineInfo = lineInfo;
        }

        internal bool HasId => _id is not null;

        internal bool InText => _element.Length > 0;

        internal void BeginUnit(XmlReader reader) {
            _id = reader.GetAttribute("id");
            _idLine = _lineInfo?.LineNumber ?? 0;
            _source = null;
            _target = null;
        }

        internal void BeginText(XmlReader reader) {
            _element = reader.Name;
            _builder.Length = 0;
            if(reader.IsEmptyElement) {
                if(_element == SourceElement) {
                    _source = string.Empty;
                } else {
                    _target = string.Empty;
                }

                _element = string.Empty;
            }
        }

        internal void AppendText(string text) {
            _builder.Append(text);
        }

        internal void EndText(XmlReader reader) {
            if(reader.Name == SourceElement) {
                _source = _builder.ToString();
            } else {
                _target = _builder.ToString();
            }

            _element = string.Empty;
        }

        internal void CompleteUnit() {
            if(_id is { Length: > 0 }) {
                _result.Add(_id, _target ?? _source ?? string.Empty, _idLine <= 0 ? 1 : _idLine);
            }

            _id = null;
        }
    }
}
