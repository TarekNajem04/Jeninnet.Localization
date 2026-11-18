//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_FLATCATALOG_INI
namespace Jeninnet.Localization.Providers.Ini;
#elif JENINNET_FLATCATALOG_TOML
namespace Jeninnet.Localization.Providers.Toml;
#else
#error Compile FlatCatalog sources with JENINNET_FLATCATALOG_INI or JENINNET_FLATCATALOG_TOML defined.
#endif

/// <summary>
/// Shared driver for flat <c>key = value</c> catalog text with optional <c>[Section]</c>
/// qualifiers (<c>Section.Key</c>). Skips blank and comment lines, tracks the current section,
/// rejects duplicate qualified keys, and preserves document order. Entry and section grammars
/// (separators, value shapes, header rules) stay per-format through the callbacks.
/// </summary>
internal static class FlatCatalogParser {
    /// <summary>
    /// Parses flat catalog text into key/value pairs in document order.
    /// </summary>
    /// <param name="text">The document text. A leading BOM is tolerated.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="isSkippedLine">Returns true for blank and full-line comment lines (already trimmed).</param>
    /// <param name="parseSection">Parses a trimmed <c>[...]</c> header line into the section name.</param>
    /// <param name="parseEntry">Parses a trimmed entry line into its key and value.</param>
    /// <param name="formatLabel">The format name used in diagnostics (for example <c>INI</c>).</param>
    /// <returns>The entries in document order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text violates the subset, with the 1-based line number in the message.</exception>
    internal static List<KeyValuePair<string, string>> Parse(
        string text,
        string filePath,
        Func<string, bool> isSkippedLine,
        Func<string, string, int, string> parseSection,
        Func<string, string, int, KeyValuePair<string, string>> parseEntry,
        string formatLabel) {
        if(text.StartsWith('\uFEFF')) {
            text = text[1..];
        }

        var entries = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? section = null;
        var lines = text.Split('\n');

        for(var index = 0; index < lines.Length; index++) {
            var lineNumber = index + 1;
            var line = lines[index].Trim();
            if(line.Length == 0 || isSkippedLine(line)) {
                continue;
            }

            if(line[0] == '[') {
                section = parseSection(line, filePath, lineNumber);
                continue;
            }

            var entry = parseEntry(line, filePath, lineNumber);
            var qualified = section is null ? entry.Key : section + "." + entry.Key;
            if(!seen.Add(qualified)) {
                throw new InvalidDataException($"Duplicate {formatLabel} key '{qualified}' in '{filePath}' at line {lineNumber}.");
            }

            entries.Add(new KeyValuePair<string, string>(qualified, entry.Value));
        }

        return entries;
    }
}
