//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Ini;

/// <summary>
/// Parses the flat localization subset of INI: blank lines and <c>;</c>/<c>#</c> full-line
/// comments are ignored, entries are <c>key = value</c> or <c>key : value</c>, and optional
/// <c>[Section]</c> headers qualify the keys that follow them as <c>Section.Key</c>.
/// Anything else — a missing separator, an empty key, an unclosed section header, or a
/// duplicate qualified key — is malformed and reported with a 1-based line number.
/// </summary>
internal static class IniParser {
    /// <summary>
    /// Parses INI text into key/value pairs in document order.
    /// </summary>
    /// <param name="text">The INI document text. A leading BOM is tolerated.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The entries in document order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text violates the subset, with the 1-based line number in the message.</exception>
    internal static List<KeyValuePair<string, string>> Parse(string text, string filePath) =>
        FlatCatalogParser.Parse(
            text,
            filePath,
            static line => line[0] == ';' || line[0] == '#',
            ParseSectionHeader,
            ParseEntry,
            "INI");

    /// <summary>
    /// Parses one entry line into its key and value.
    /// </summary>
    /// <param name="line">The trimmed source line.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The entry key and value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the line violates the subset.</exception>
    private static KeyValuePair<string, string> ParseEntry(string line, string filePath, int lineNumber) {
        var separator = line.IndexOfAny('=', ':');
        if(separator < 0) {
            throw new InvalidDataException($"Invalid INI entry in '{filePath}' at line {lineNumber}: expected 'key = value' or 'key : value'.");
        }

        var key = line[..separator].Trim();
        if(key.Length == 0) {
            throw new InvalidDataException($"Invalid INI entry in '{filePath}' at line {lineNumber}: the key is empty.");
        }

        return new KeyValuePair<string, string>(key, line[(separator + 1)..].Trim());
    }

    /// <summary>
    /// Parses and validates a section header line.
    /// </summary>
    /// <param name="line">The trimmed source line starting with <c>[</c>.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The section name.</returns>
    /// <exception cref="InvalidDataException">Thrown when the header is not closed on the same line or names an empty section.</exception>
    private static string ParseSectionHeader(string line, string filePath, int lineNumber) {
        if(line.Length < 3 || !line.EndsWith(']')) {
            throw new InvalidDataException($"Invalid INI section in '{filePath}' at line {lineNumber}: a section header must be '[Name]' on one line.");
        }

        var name = line[1..^1].Trim();
        if(name.Length == 0) {
            throw new InvalidDataException($"Invalid INI section in '{filePath}' at line {lineNumber}: the section name is empty.");
        }

        return name;
    }
}
