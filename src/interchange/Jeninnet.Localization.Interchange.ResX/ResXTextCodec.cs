//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.ResX;

/// <summary>
/// The canonical ResX 2.0 writer/reader for the  subset (hand-rolled; the
/// classic BCL (Base Class Library) <c>ResXResourceWriter</c> is Windows-Desktop-only on net10.0). Writes
/// the VS-canonical header and <c>data</c> entries (strings + comments) with ordinal
/// key order, 2-space indent, and LF newlines; reads with BOM (Byte Order Mark) tolerance, standard
/// <c>assembly</c>/<c>metadata</c> root elements ignored, and file/line diagnostics
/// for every structural violation.
/// </summary>
internal static class ResXTextCodec {
    private const string RES_MIME_TYPE = "text/microsoft-resx";

    private const string RES_VERSION = "2.0";

    /// <summary>The canonical <c>value</c> element name.</summary>
    private const string VALUE_ELEMENT = "value";

    private const string READER_VALUE = "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

    private const string WRITER_VALUE = "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

    /// <summary>
    /// Writes entries as canonical ResX 2.0 text: the VS header, then one
    /// <c>data</c> entry per row in the given (ordinal) order, no trailing newline.
    /// </summary>
    /// <param name="entries">The entries in deterministic order.</param>
    /// <returns>The ResX document text (no BOM; the file writer adds it).</returns>
    /// <exception cref="ArgumentException">Thrown when a key, value, or comment contains characters XML 1.0 cannot represent.</exception>
    internal static string Write(IReadOnlyList<ResXEntry> entries) {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
            .Append("<root>\n");
        AppendHeader(builder, "resmimetype", RES_MIME_TYPE);
        AppendHeader(builder, "version", RES_VERSION);
        AppendHeader(builder, "reader", READER_VALUE);
        AppendHeader(builder, "writer", WRITER_VALUE);

        foreach(var entry in entries) {
            ValidateRepresentable(entry.Name, "key", entry.Name);
            if(entry.Value is not null) {
                ValidateRepresentable(entry.Value, VALUE_ELEMENT, entry.Name);
            }

            if(!string.IsNullOrEmpty(entry.Comment)) {
                ValidateRepresentable(entry.Comment, "comment", entry.Name);
            }

            builder.Append(CultureInfo.InvariantCulture, $"  <data name=\"{EscapeAttribute(entry.Name)}\" xml:space=\"preserve\">\n");
            if(entry.Value is not null) {
                builder.Append(CultureInfo.InvariantCulture, $"    <value>{EscapeText(entry.Value)}</value>\n");
            }

            if(!string.IsNullOrEmpty(entry.Comment)) {
                builder.Append(CultureInfo.InvariantCulture, $"    <comment>{EscapeText(entry.Comment)}</comment>\n");
            }

            builder.Append("  </data>\n");
        }

        builder.Append("</root>");
        return builder.ToString();
    }

    /// <summary>
    /// Parses ResX text against the  subset: optional BOM, a <c>root</c>
    /// element with valid <c>resmimetype</c>/<c>version</c> resheaders, <c>data</c>
    /// entries with unique non-empty names and at most one <c>value</c>/<c>comment</c>
    /// each; <c>assembly</c>/<c>metadata</c> root elements are ignored.
    /// </summary>
    /// <param name="text">The ResX document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed entries in file order.</returns>
    /// <exception cref="ResXInterchangeException">Thrown when the text violates the subset.</exception>
    internal static List<ResXEntry> Parse(string text, string filePath) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if(text.StartsWith('\uFEFF')) {
            text = text[1..];
        }

        XDocument document;
        try {
            document = XDocument.Parse(text, LoadOptions.SetLineInfo);
        }
        catch(XmlException ex) {
            throw new ResXInterchangeException(filePath, ex.LineNumber, $"Malformed ResX: {ex.Message}");
        }

        var root = document.Root;
        if(root is null || !string.Equals(root.Name.LocalName, "root", StringComparison.Ordinal)) {
            throw new ResXInterchangeException(filePath, LineOf(root), "A ResX document must carry a 'root' element.");
        }

        ValidateResHeaders(root, filePath);

        var entries = new List<ResXEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var element in root.Elements()) {
            switch(element.Name.LocalName) {
                case "resheader":
                case "assembly":
                case "metadata":
                    continue;
                case "data":
                    break;
                default:
                    throw new ResXInterchangeException(filePath, LineOf(element), $"Unexpected element '{element.Name.LocalName}': the interchange subset allows resheader, data, assembly, and metadata.");
            }

            entries.Add(ParseDataEntry(element, filePath, seen));
        }

        return entries;
    }

    /// <summary>
    /// Validates the <c>resmimetype</c>/<c>version</c> resheaders that identify a ResX 2.0
    /// document, ignoring any other root child.
    /// </summary>
    /// <param name="root">The document's <c>root</c> element.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <exception cref="ResXInterchangeException">Thrown when the required resheaders are missing or foreign.</exception>
    private static void ValidateResHeaders(XElement root, string filePath) {
        string? mimeType = null;
        string? version = null;
        foreach(var element in root.Elements()) {
            if(!string.Equals(element.Name.LocalName, "resheader", StringComparison.Ordinal)) {
                continue;
            }

            var headerName = (string?)element.Attribute("name");
            var value = element.Element(VALUE_ELEMENT)?.Value;
            if(string.Equals(headerName, "resmimetype", StringComparison.Ordinal)) {
                mimeType = value;
            } else if(string.Equals(headerName, "version", StringComparison.Ordinal)) {
                version = value;
            }
        }

        if(!string.Equals(mimeType, RES_MIME_TYPE, StringComparison.Ordinal) || !string.Equals(version, RES_VERSION, StringComparison.Ordinal)) {
            throw new ResXInterchangeException(filePath, "Foreign or malformed ResX: the 'resmimetype'/'version' resheaders must declare 'text/microsoft-resx' 2.0.");
        }
    }

    /// <summary>
    /// Validates one <c>data</c> element (unique non-empty name, at most one
    /// <c>value</c>/<c>comment</c>, no other children) and converts it into an entry.
    /// </summary>
    /// <param name="element">The <c>data</c> element.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="seen">The data entry names seen so far.</param>
    /// <returns>The parsed entry.</returns>
    /// <exception cref="ResXInterchangeException">Thrown when the element violates the subset.</exception>
    private static ResXEntry ParseDataEntry(XElement element, string filePath, HashSet<string> seen) {
        var name = (string?)element.Attribute("name");
        if(string.IsNullOrEmpty(name)) {
            throw new ResXInterchangeException(filePath, LineOf(element), "Every 'data' entry must carry a non-empty 'name'.");
        }

        if(!seen.Add(name)) {
            throw new ResXInterchangeException(filePath, LineOf(element), $"Duplicate data entry name '{name}'.");
        }

        var values = element.Elements(VALUE_ELEMENT).ToArray();
        var comments = element.Elements("comment").ToArray();
        var unknownChildren = element.Elements()
            .Where(static child => child.Name.LocalName is not (VALUE_ELEMENT or "comment"))
            .ToArray();

        if(values.Length > 1) {
            throw new ResXInterchangeException(filePath, LineOf(element), $"Data entry '{name}' must carry at most one 'value'.");
        }

        if(comments.Length > 1) {
            throw new ResXInterchangeException(filePath, LineOf(element), $"Data entry '{name}' must carry at most one 'comment'.");
        }

        if(unknownChildren.Length > 0) {
            throw new ResXInterchangeException(filePath, LineOf(unknownChildren[0]), $"Data entry '{name}' carries unexpected element '{unknownChildren[0].Name.LocalName}'.");
        }

        var value = values.Length == 1 ? values[0].Value : null;
        var comment = comments.Length == 1 ? comments[0].Value : null;
        var typed = element.Attribute("type") is not null || element.Attribute("mimetype") is not null;

        return new ResXEntry {
            Name = name,
            Value = value,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            IsStringResource = !typed && value is not null,
        };
    }

    /// <summary>
    /// Appends header.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    private static void AppendHeader(StringBuilder builder, string name, string value) => builder.Append(CultureInfo.InvariantCulture, $"  <resheader name=\"{name}\">\n")
            .Append(CultureInfo.InvariantCulture, $"    <value>{value}</value>\n")
            .Append("  </resheader>\n");

    /// <summary>
    /// Validates representable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="what">The what.</param>
    /// <param name="key">The message key.</param>
    private static void ValidateRepresentable(string value, string what, string key) {
        try {
            _ = XmlConvert.VerifyXmlChars(value);
        }
        catch(XmlException ex) {
            throw new ArgumentException($"The {what} of key '{key}' cannot be represented in ResX (XML 1.0): {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape text.</returns>
    private static string EscapeText(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\r", "&#13;", StringComparison.Ordinal);

    /// <summary>
    /// Escapes a value for use inside a double-quoted XML attribute, preserving tabs, line feeds, and carriage returns as character references.
    /// </summary>
    /// <param name="value">The raw attribute value.</param>
    /// <returns>The XML-safe attribute value.</returns>
    private static string EscapeAttribute(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("\t", "&#9;", StringComparison.Ordinal)
            .Replace("\n", "&#10;", StringComparison.Ordinal)
            .Replace("\r", "&#13;", StringComparison.Ordinal);

    /// <summary>
    /// Performs the line of operation.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The line of value.</returns>
    private static int LineOf(XObject? node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
