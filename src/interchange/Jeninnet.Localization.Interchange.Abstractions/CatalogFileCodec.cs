//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Abstractions;

/// <summary>
/// Reads and writes flat catalog files (JSON objects and XML message documents),
/// preserving key order, newline style, and BOM (Byte Order Mark) so untouched files stay byte-identical
///. Failures carry file/line facts
/// through the internal <see cref="CatalogInterchangeException"/>; format packages
/// wrap them into their own public interchange exception.
/// </summary>
internal static class CatalogFileCodec {
    /// <summary>
    /// The supported catalog file kinds.
    /// </summary>
    internal enum CatalogKind {
        /// <summary>A flat JSON object catalog.</summary>
        Json = 0,
        /// <summary>A flat XML message catalog.</summary>
        Xml = 1,
    }

    /// <summary>
    /// An ordered flat catalog plus the formatting facts needed to rewrite it faithfully.
    /// </summary>
    internal sealed class CatalogFile {
        /// <summary>
        /// Gets the detected file kind.
        /// </summary>
        internal required CatalogKind Kind { get; init; }

        /// <summary>
        /// Gets the entries in document order.
        /// </summary>
        internal required List<KeyValuePair<string, string>> Entries { get; init; }

        /// <summary>
        /// Gets the newline sequence used by the file.
        /// </summary>
        internal required string NewLine { get; init; }

        /// <summary>
        /// Gets whether the file starts with a UTF-8 (Unicode Transformation Format, 8-bit) BOM.
        /// </summary>
        internal required bool HasBom { get; init; }

        /// <summary>
        /// Gets the entries as an ordinal lookup.
        /// </summary>
        /// <returns>The as lookup result.</returns>
        internal IReadOnlyDictionary<string, string?> AsLookup() =>
            Entries.ToDictionary(static e => e.Key, static e => (string?)e.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads a catalog file, detecting JSON vs XML by extension.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <returns>The ordered catalog.</returns>
    /// <exception cref="CatalogInterchangeException">Thrown when the file is missing or malformed.</exception>
    internal static CatalogFile Read(string path) {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if(!File.Exists(path)) {
            throw new CatalogInterchangeException(path, "Catalog file not found.");
        }

        var extension = Path.GetExtension(path);
        if(string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)) {
            return ReadXml(path);
        }

        if(string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)) {
            return ReadJson(path);
        }

        throw new CatalogInterchangeException(path, $"Unsupported catalog format '{extension}': interchange v1 reads JSON and XML catalogs (PO file adapters are deferred).");
    }

    /// <summary>
    /// Writes entries back to a catalog file, preserving order, newline style, and BOM.
    /// </summary>
    /// <param name="path">The catalog file path.</param>
    /// <param name="catalog">The previously read catalog (carries the formatting facts).</param>
    /// <param name="entries">The updated entries in document order.</param>
    /// <exception cref="CatalogInterchangeException">Thrown when entries cannot be represented.</exception>
    internal static void Write(string path, CatalogFile catalog, IReadOnlyList<KeyValuePair<string, string>> entries) {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entries);

        var text = catalog.Kind == CatalogKind.Xml
            ? WriteXml(entries, catalog.NewLine)
            : WriteJson(entries, catalog.NewLine);

        var encoding = new UTF8Encoding(catalog.HasBom);
        File.WriteAllText(path, text, encoding);
    }

    /// <summary>
    /// Reads json.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The json result.</returns>
    private static CatalogFile ReadJson(string path) {
        string text;
        bool hasBom;
        try {
            var bytes = File.ReadAllBytes(path);
            hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            text = Encoding.UTF8.GetString(bytes);
        }
        catch(IOException ex) {
            throw new CatalogInterchangeException(path, $"Cannot read catalog file: {ex.Message}");
        }

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(text);
        }
        catch(JsonException ex) {
            throw new CatalogInterchangeException(path, (int)(ex.LineNumber ?? 0), $"Malformed JSON catalog: {ex.Message}");
        }

        using(doc) {
            if(doc.RootElement.ValueKind != JsonValueKind.Object) {
                throw new CatalogInterchangeException(path, "Malformed JSON catalog: the root must be an object.");
            }

            var entries = new List<KeyValuePair<string, string>>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach(var property in doc.RootElement.EnumerateObject()) {
                if(property.Value.ValueKind == JsonValueKind.Null) {
                    continue;
                }

                if(property.Value.ValueKind != JsonValueKind.String) {
                    throw new CatalogInterchangeException(path, $"Malformed JSON catalog: value for key '{property.Name}' must be a string.");
                }

                if(positions.TryGetValue(property.Name, out var position)) {
                    entries[position] = new KeyValuePair<string, string>(property.Name, property.Value.GetString()!);
                } else {
                    positions[property.Name] = entries.Count;
                    entries.Add(new KeyValuePair<string, string>(property.Name, property.Value.GetString()!));
                }
            }

            return new CatalogFile {
                Kind = CatalogKind.Json,
                Entries = entries,
                NewLine = DetectNewLine(text),
                HasBom = hasBom,
            };
        }
    }

    /// <summary>
    /// Reads xml.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The xml result.</returns>
    private static CatalogFile ReadXml(string path) {
        byte[] bytes;
        try {
            bytes = File.ReadAllBytes(path);
        }
        catch(IOException ex) {
            throw new CatalogInterchangeException(path, $"Cannot read catalog file: {ex.Message}");
        }

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var raw = Encoding.UTF8.GetString(bytes);

        XDocument doc;
        try {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch(XmlException ex) {
            throw new CatalogInterchangeException(path, ex.LineNumber, $"Malformed XML catalog: {ex.Message}");
        }

        var root = doc.Root ?? throw new CatalogInterchangeException(path, "Malformed XML catalog: missing root element.");
        var entries = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var element in root.Elements()) {
            var key = element.Name.LocalName;
            if(!seen.Add(key)) {
                throw new CatalogInterchangeException(path, LineOf(element), $"Malformed XML catalog: duplicate key '{key}'.");
            }

            entries.Add(new KeyValuePair<string, string>(key, element.Value));
        }

        return new CatalogFile {
            Kind = CatalogKind.Xml,
            Entries = entries,
            NewLine = DetectNewLine(raw),
            HasBom = hasBom,
        };
    }

    /// <summary>
    /// Writes json.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="newLine">The new line.</param>
    /// <returns>The json text.</returns>
    private static string WriteJson(IReadOnlyList<KeyValuePair<string, string>> entries, string newLine) {
        var builder = new StringBuilder();
        builder.Append('{').Append(newLine);
        for(var i = 0; i < entries.Count; i++) {
            builder.Append("  \"")
                .Append(EscapeJson(entries[i].Key))
                .Append("\": \"")
                .Append(EscapeJson(entries[i].Value))
                .Append('\"');
            if(i < entries.Count - 1) {
                builder.Append(',');
            }

            builder.Append(newLine);
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Writes xml.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="newLine">The new line.</param>
    /// <returns>The xml text.</returns>
    private static string WriteXml(IReadOnlyList<KeyValuePair<string, string>> entries, string newLine) {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>")
            .Append(newLine)
            .Append("<messages>")
            .Append(newLine);
        foreach(var entry in entries) {
            string name;
            try {
                name = XmlConvert.VerifyName(entry.Key);
            }
            catch(XmlException ex) {
                throw new CatalogInterchangeException("<catalog>", $"Key '{entry.Key}' cannot be an XML element name: {ex.Message}");
            }

            builder.Append("  <")
                .Append(name)
                .Append('>')
                .Append(EscapeXml(entry.Value))
                .Append("</")
                .Append(name)
                .Append('>')
                .Append(newLine);
        }

        builder.Append("</messages>");
        return builder.ToString();
    }

    /// <summary>
    /// Performs the detect new line operation.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The detect new line text.</returns>
    private static string DetectNewLine(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>
    /// Escapes json.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape json text.</returns>
    private static string EscapeJson(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(c switch {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                _ when c < 0x20 => $"\\u{(int)c:X4}",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Escapes xml.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape xml text.</returns>
    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// Performs the line of operation.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The line of value.</returns>
    private static int LineOf(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
