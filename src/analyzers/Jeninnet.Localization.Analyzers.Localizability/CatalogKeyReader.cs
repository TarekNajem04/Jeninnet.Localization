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
    internal static HashSet<string> PlaceholderIds(string template) {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        while(index < template.Length) {
            if(template[index] != '{') {
                index++;
                continue;
            }

            var depth = 0;
            var end = -1;
            for(var scan = index; scan < template.Length; scan++) {
                if(template[scan] == '{') {
                    depth++;
                } else if(template[scan] == '}') {
                    depth--;
                    if(depth == 0) {
                        end = scan;
                        break;
                    }
                }
            }

            if(end < 0) {
                index++;
                continue;
            }

            var body = template.Substring(index + 1, end - index - 1);
            var identifier = TopLevelIdentifier(body);
            if(identifier.Length > 0) {
                identifiers.Add(identifier);
            }

            index = end + 1;
        }

        return identifiers;
    }

    /// <summary>
    /// Takes the top-level identifier of a group body.
    /// </summary>
    /// <param name="body">The group body.</param>
    /// <returns>The trimmed identifier, possibly empty.</returns>
    private static string TopLevelIdentifier(string body) {
        var depth = 0;
        for(var index = 0; index < body.Length; index++) {
            if(body[index] == '{') {
                depth++;
            } else if(body[index] == '}') {
                depth--;
            } else if(body[index] == ',' && depth == 0) {
                return body.Substring(0, index).Trim();
            }
        }

        return body.Trim();
    }

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
                var value = ReadJsonString(text, ref index, ref line);
                if(value is not null) {
                    result.Add(key, value, keyLine);
                }
            }
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
                var escaped = text[index];
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
                index++;
                continue;
            }

            builder.Append(current);
            index++;
        }

        return null;
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
        string? name = null;
        string? value = null;
        var nameLine = 0;
        var inValue = false;
        var valueBuilder = new System.Text.StringBuilder();
        while(reader.Read()) {
            if(reader.NodeType == XmlNodeType.Element && reader.Name == "data") {
                name = reader.GetAttribute("name");
                nameLine = lineInfo?.LineNumber ?? 0;
                value = null;
            } else if(reader.NodeType == XmlNodeType.Element && reader.Name == "value" && name is not null) {
                inValue = true;
                valueBuilder.Length = 0;
            } else if(reader.NodeType == XmlNodeType.Text && inValue) {
                valueBuilder.Append(reader.Value);
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "value") {
                inValue = false;
                value = valueBuilder.ToString();
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "data") {
                if(name is { Length: > 0 }) {
                    result.Add(name, value ?? string.Empty, nameLine <= 0 ? 1 : nameLine);
                }

                name = null;
                value = null;
            }
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
        var msgid = new System.Text.StringBuilder();
        var msgidLine = 0;
        var hasMsgid = false;
        var template = new System.Text.StringBuilder();
        var hasTemplate = false;
        var inMsgid = false;
        var inTemplate = false;

        void Flush() {
            if(hasMsgid && msgid.Length > 0) {
                result.Add(msgid.ToString(), hasTemplate ? template.ToString() : string.Empty, msgidLine <= 0 ? 1 : msgidLine);
            }

            msgid.Length = 0;
            msgidLine = 0;
            hasMsgid = false;
            template.Length = 0;
            hasTemplate = false;
            inMsgid = false;
            inTemplate = false;
        }

        for(var number = 0; number < lines.Length; number++) {
            var trimmed = lines[number].Trim();
            if(trimmed.Length == 0) {
                Flush();
                continue;
            }

            if(trimmed[0] == '#') {
                continue;
            }

            if(trimmed.StartsWith("msgctxt", StringComparison.Ordinal) ||
                trimmed.StartsWith("msgid_plural", StringComparison.Ordinal)) {
                continue;
            }

            if(trimmed.StartsWith("msgstr[", StringComparison.Ordinal)) {
                var bracket = trimmed.IndexOf(']');
                inMsgid = false;
                inTemplate = false;
                if(bracket > 7 && int.TryParse(trimmed.Substring(7, bracket - 7), out var slot) && slot == 0) {
                    var quoted = ExtractQuoted(trimmed.Substring(bracket + 1).TrimStart());
                    if(quoted is not null) {
                        template.Length = 0;
                        template.Append(quoted);
                        hasTemplate = true;
                        inTemplate = true;
                    }
                }

                continue;
            }

            if(trimmed.StartsWith("msgid", StringComparison.Ordinal)) {
                Flush();
                var quoted = ExtractQuoted(trimmed.Substring(5).TrimStart());
                if(quoted is not null) {
                    msgid.Append(quoted);
                    msgidLine = number + 1;
                    hasMsgid = true;
                    inMsgid = true;
                }

                continue;
            }

            if(trimmed.StartsWith("msgstr", StringComparison.Ordinal)) {
                inMsgid = false;
                inTemplate = false;
                var quoted = ExtractQuoted(trimmed.Substring(6).TrimStart());
                if(quoted is not null) {
                    template.Length = 0;
                    template.Append(quoted);
                    hasTemplate = true;
                    inTemplate = true;
                }

                continue;
            }

            if(trimmed[0] == '"') {
                var quoted = ExtractQuoted(trimmed);
                if(quoted is null) {
                    continue;
                }

                if(inMsgid) {
                    msgid.Append(quoted);
                } else if(inTemplate) {
                    template.Append(quoted);
                }
            }
        }

        Flush();
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
        var lineInfo = reader as IXmlLineInfo;
        string? id = null;
        var idLine = 0;
        string? source = null;
        string? target = null;
        var element = string.Empty;
        var builder = new System.Text.StringBuilder();
        while(reader.Read()) {
            if(reader.NodeType == XmlNodeType.Element && reader.Name == "unit") {
                id = reader.GetAttribute("id");
                idLine = lineInfo?.LineNumber ?? 0;
                source = null;
                target = null;
            } else if(reader.NodeType == XmlNodeType.Element && (reader.Name == "source" || reader.Name == "target") && id is not null) {
                element = reader.Name;
                builder.Length = 0;
                if(reader.IsEmptyElement) {
                    if(element == "source") {
                        source = string.Empty;
                    } else {
                        target = string.Empty;
                    }

                    element = string.Empty;
                }
            } else if(reader.NodeType == XmlNodeType.Text && element.Length > 0) {
                builder.Append(reader.Value);
            } else if(reader.NodeType == XmlNodeType.EndElement && (reader.Name == "source" || reader.Name == "target")) {
                if(reader.Name == "source") {
                    source = builder.ToString();
                } else {
                    target = builder.ToString();
                }

                element = string.Empty;
            } else if(reader.NodeType == XmlNodeType.EndElement && reader.Name == "unit") {
                if(id is { Length: > 0 }) {
                    result.Add(id, target ?? source ?? string.Empty, idLine <= 0 ? 1 : idLine);
                }

                id = null;
            }
        }
    }
}
