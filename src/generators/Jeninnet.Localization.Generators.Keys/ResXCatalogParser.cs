//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Parses the flat key subset of <c>.resx</c> catalogs for key generation: every
/// <c>&lt;data name="Key"&gt;</c> element contributes its <c>name</c>, independent of
/// culture. <c>&lt;resheader&gt;</c>, <c>&lt;assembly&gt;</c>, and <c>&lt;metadata&gt;</c>
/// elements are authoring metadata, never keys. Entries carrying a <c>type</c> or
/// <c>mimetype</c> attribute are non-string values and surface as entries with
/// <see cref="ParsedCatalogEntry.IsStringValue"/> false so the generator reports them;
/// a missing <c>&lt;value&gt;</c> resolves to the empty template. The reader prohibits
/// DTDs and never resolves external entities.
/// </summary>
internal static class ResXCatalogParser {
    /// <summary>
    /// Parses ResX text into catalog entries in document order.
    /// </summary>
    /// <param name="text">The ResX document text.</param>
    /// <returns>The entries plus, when the document is not well-formed, a single syntax error.</returns>
    internal static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset) Parse(string text) {
        var entries = new List<ParsedCatalogEntry>();
        var starts = CatalogLineMap.LineStarts(text);

        try {
            using var reader = XmlCatalogReader.CreateReader(text);
            var lineInfo = (IXmlLineInfo)reader;
            while(reader.Read()) {
                if(reader.NodeType != XmlNodeType.Element || reader.Name != "data") {
                    continue;
                }

                var (entry, error, offset) = ReadDataElement(reader, lineInfo, text, starts);
                if(error is not null) {
                    return (entries, error, offset);
                }

                if(entry is not null) {
                    entries.Add(entry);
                }
            }
        }
        catch(XmlException ex) {
            return XmlCatalogReader.MalformedXml("ResX", text, starts, entries, ex);
        }

        return (entries, null, 0);
    }

    /// <summary>
    /// Reads one <c>&lt;data&gt;</c> element into a catalog entry.
    /// </summary>
    /// <param name="reader">The reader positioned on the <c>&lt;data&gt;</c> start element.</param>
    /// <param name="lineInfo">The line info.</param>
    /// <param name="text">The ResX document text.</param>
    /// <param name="starts">The line starts.</param>
    /// <returns>The entry, or the diagnostic when the element has no name.</returns>
    private static (ParsedCatalogEntry? Entry, string? Error, int Offset) ReadDataElement(XmlReader reader, IXmlLineInfo lineInfo, string text, int[] starts) {
        var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
        var name = reader.GetAttribute("name");
        if(string.IsNullOrEmpty(name)) {
            return (null, "ResX <data> element requires a name.", CatalogLineMap.OffsetOf(starts, text.Length, line, 1));
        }

        var isString = reader.GetAttribute("type") is null && reader.GetAttribute("mimetype") is null;
        var value = isString ? ReadValueText(reader) : null;
        var rawLine = CatalogLineMap.LineText(text, starts, line);
        CatalogLineMap.QuotedKeyPosition(rawLine, CatalogLineMap.OffsetOf(starts, text.Length, line, 1), name, out var keyOffset, out var keyLength);
        return (new ParsedCatalogEntry(name, keyOffset, keyLength, value, isString, keyOffset), null, 0);
    }

    /// <summary>
    /// Reads the text content of the current <c>&lt;data&gt;</c> element's
    /// <c>&lt;value&gt;</c> child, preserving whitespace exactly. A missing
    /// <c>&lt;value&gt;</c> resolves to the empty template.
    /// </summary>
    /// <param name="reader">The reader positioned on the <c>&lt;data&gt;</c> start element.</param>
    /// <returns>The value text.</returns>
    private static string ReadValueText(XmlReader reader) {
        if(reader.IsEmptyElement) {
            return string.Empty;
        }

        var value = new StringBuilder();
        var found = false;
        var depth = reader.Depth;
        while(reader.Read() && reader.Depth > depth) {
            if(reader.NodeType == XmlNodeType.Element && reader.Name == "value" && !found) {
                found = true;
                if(reader.IsEmptyElement) {
                    continue;
                }

                var valueDepth = reader.Depth;
                while(reader.Read() && reader.Depth > valueDepth) {
                    if(reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) {
                        value.Append(reader.Value);
                    }
                }
            }
        }

        return value.ToString();
    }
}
