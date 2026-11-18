//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Shared XML catalog reading surface for the generator's catalog parsers: hardened reader
/// construction and well-formed XML failure mapping with file/line diagnostics.
/// </summary>
internal static class XmlCatalogReader {
    /// <summary>
    /// Creates the hardened catalog reader: the DTD is accepted but external entities are never
    /// resolved, and comments and processing instructions are ignored.
    /// </summary>
    /// <param name="text">The catalog text.</param>
    /// <returns>The reader. Cast to <see cref="IXmlLineInfo"/> for line numbers.</returns>
    internal static XmlReader CreateReader(string text) {
        var settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
        };

        return XmlReader.Create(new StringReader(text), settings);
    }

    /// <summary>
    /// Maps a malformed-XML failure to the catalog diagnostic tuple.
    /// </summary>
    /// <param name="formatLabel">The format name used in diagnostics (for example <c>ResX</c>).</param>
    /// <param name="text">The catalog text.</param>
    /// <param name="starts">The line starts.</param>
    /// <param name="entries">The entries parsed before the failure.</param>
    /// <param name="ex">The XML failure.</param>
    /// <returns>The entries, the diagnostic, and the failure offset.</returns>
    internal static (List<ParsedCatalogEntry> Entries, string? Error, int Offset) MalformedXml(
        string formatLabel,
        string text,
        int[] starts,
        List<ParsedCatalogEntry> entries,
        XmlException ex)
        => (entries, $"{formatLabel} catalog is not well-formed XML: " + ex.Message, CatalogLineMap.OffsetOf(starts, text.Length, ex.LineNumber, ex.LinePosition));
}
