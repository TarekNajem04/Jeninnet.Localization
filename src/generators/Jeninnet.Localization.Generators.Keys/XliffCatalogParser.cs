//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Parses the flat key subset of <c>.xliff</c>/<c>.xlf</c> catalogs for key generation:
/// every XLIFF 2.0 <c>&lt;unit id="Key"&gt;</c> element contributes its <c>id</c>,
/// independent of culture. Only XLIFF 2.0 is supported; a 1.x document is reported as
/// malformed rather than silently yielding an empty class. The reader prohibits DTDs
/// and never resolves external entities.
/// </summary>
internal static class XliffCatalogParser {
    /// <summary>
    /// Parses XLIFF text into catalog entries in document order.
    /// </summary>
    /// <param name="text">The XLIFF document text.</param>
    /// <returns>The entries plus, when the document is not supported, a single syntax error.</returns>
    internal static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset) Parse(string text) {
        var entries = new List<ParsedCatalogEntry>();
        var starts = CatalogLineMap.LineStarts(text);
        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
        };

        try {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            var lineInfo = (IXmlLineInfo)reader;
            var version = string.Empty;
            while(reader.Read()) {
                if(reader.NodeType != XmlNodeType.Element) {
                    continue;
                }

                if(reader.Name == "xliff") {
                    version = reader.GetAttribute("version") ?? string.Empty;
                    if(version.StartsWith("1.", StringComparison.Ordinal)) {
                        return (entries, "Only XLIFF 2.0 <unit> catalogs are supported for key generation.", CatalogLineMap.OffsetOf(starts, text.Length, 1, 1));
                    }

                    continue;
                }

                if(reader.Name != "unit") {
                    continue;
                }

                var line = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
                var id = reader.GetAttribute("id");
                if(string.IsNullOrEmpty(id)) {
                    return (entries, "XLIFF <unit> element requires an id.", CatalogLineMap.OffsetOf(starts, text.Length, line, 1));
                }

                var rawLine = CatalogLineMap.LineText(text, starts, line);
                CatalogLineMap.QuotedKeyPosition(rawLine, CatalogLineMap.OffsetOf(starts, text.Length, line, 1), id, out var keyOffset, out var keyLength);
                entries.Add(new ParsedCatalogEntry(id, keyOffset, keyLength, string.Empty, true, keyOffset));
            }
        }
        catch(XmlException ex) {
            return (entries, "XLIFF catalog is not well-formed XML: " + ex.Message, CatalogLineMap.OffsetOf(starts, text.Length, ex.LineNumber, ex.LinePosition));
        }

        return (entries, null, 0);
    }
}
