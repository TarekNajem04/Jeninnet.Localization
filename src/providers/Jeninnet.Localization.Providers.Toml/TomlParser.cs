//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Toml;

/// <summary>
/// Parses the flat localization subset of TOML: blank lines and full-line <c>#</c> comments
/// are ignored, entries are <c>key = "value"</c> or <c>key = 'value'</c> with an optional
/// trailing <c>#</c> comment, and optional <c>[Section]</c> headers qualify the keys that
/// follow them as <c>Section.Key</c>. Only string values are supported: basic strings honor
/// the common escapes (<c>\\</c>, <c>\"</c>, <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c>,
/// <c>\t</c>, <c>\uXXXX</c>, <c>\UXXXXXXXX</c>), literal strings are verbatim. Anything
/// else — a missing separator, an empty key, a dotted key, an unclosed or empty section
/// header, a non-string value, a duplicate qualified key, or trailing content that is
/// neither whitespace nor a comment — is malformed and reported with a 1-based line number.
/// </summary>
internal static class TomlParser {
    /// <summary>
    /// Parses TOML text into key/value pairs in document order.
    /// </summary>
    /// <param name="text">The TOML document text. A leading BOM is tolerated.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The entries in document order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text violates the subset, with the 1-based line number in the message.</exception>

    internal static List<KeyValuePair<string, string>> Parse(string text, string filePath) =>
        FlatCatalogParser.Parse(
            text,
            filePath,
            static line => line[0] == '#',
            ParseSectionHeader,
            ParseEntry,
            "TOML");


    /// <summary>
    /// Parses and validates a section header line.
    /// </summary>
    /// <param name="line">The trimmed source line starting with <c>[</c>.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The section name.</returns>
    /// <exception cref="InvalidDataException">Thrown when the header is not a bare <c>[Name]</c> on one line.</exception>
    private static string ParseSectionHeader(string line, string filePath, int lineNumber) {
        if(line.Length < 3 || !line.EndsWith(']')) {
            throw new InvalidDataException($"Invalid TOML section in '{filePath}' at line {lineNumber}: a section header must be '[Name]' on one line.");
        }

        var name = line[1..^1].Trim();
        if(!IsBareWord(name)) {
            throw new InvalidDataException($"Invalid TOML section in '{filePath}' at line {lineNumber}: only bare '[Name]' sections are supported (no dotted or quoted sections).");
        }

        return name;
    }

    /// <summary>
    /// Parses one entry line into its key and value.
    /// </summary>
    /// <param name="line">The trimmed source line.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The entry key and value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the line violates the subset.</exception>
    private static KeyValuePair<string, string> ParseEntry(string line, string filePath, int lineNumber) {
        var separator = line.IndexOf('=');
        if(separator < 0) {
            throw new InvalidDataException($"Invalid TOML entry in '{filePath}' at line {lineNumber}: expected 'key = \"value\"'.");
        }

        var keyPart = line[..separator].Trim();
        var key = ParseKey(keyPart, filePath, lineNumber);

        var rest = line[(separator + 1)..].TrimStart();
        if(rest.Length == 0 || (rest[0] != '"' && rest[0] != '\'')) {
            throw new InvalidDataException($"Invalid TOML value in '{filePath}' at line {lineNumber}: only string values are supported.");
        }

        var (value, consumed) = rest[0] == '"' ? ParseBasicString(rest, filePath, lineNumber) : ParseLiteralString(rest, filePath, lineNumber);
        var tail = rest[consumed..].TrimStart();
        if(tail.Length != 0 && tail[0] != '#') {
            throw new InvalidDataException($"Invalid TOML entry in '{filePath}' at line {lineNumber}: only whitespace or a '#' comment may follow the value.");
        }

        return new KeyValuePair<string, string>(key, value);
    }

    /// <summary>
    /// Parses and validates an entry key.
    /// </summary>
    /// <param name="keyPart">The trimmed text before <c>=</c>.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The key.</returns>
    /// <exception cref="InvalidDataException">Thrown for an empty, dotted, or otherwise unsupported key.</exception>
    private static string ParseKey(string keyPart, string filePath, int lineNumber) {
        if(keyPart.Length == 0) {
            throw new InvalidDataException($"Invalid TOML entry in '{filePath}' at line {lineNumber}: the key is empty.");
        }

        if(keyPart[0] is '"' or '\'') {
            var (value, consumed) = keyPart[0] == '"' ? ParseBasicString(keyPart, filePath, lineNumber) : ParseLiteralString(keyPart, filePath, lineNumber);
            if(keyPart[consumed..].Trim().Length != 0) {
                throw new InvalidDataException($"Invalid TOML key in '{filePath}' at line {lineNumber}: dotted keys are not supported.");
            }

            return value;
        }

        if(!IsBareWord(keyPart)) {
            throw new InvalidDataException($"Invalid TOML key in '{filePath}' at line {lineNumber}: dotted keys are not supported.");
        }

        return keyPart;
    }

    /// <summary>
    /// Determines whether the text is a bare word (letters, digits, underscore, hyphen).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsBareWord(string text) {
        if(text.Length == 0) {
            return false;
        }

        return text.All(static character => char.IsLetterOrDigit(character) || character is '_' or '-');
    }

    /// <summary>
    /// Parses a basic string starting at the opening quote.
    /// </summary>
    /// <param name="text">The text starting with <c>"</c>.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The value and the count of consumed characters including both quotes.</returns>
    /// <exception cref="InvalidDataException">Thrown for an unterminated string or an unsupported escape.</exception>
    private static (string Value, int Consumed) ParseBasicString(string text, string filePath, int lineNumber) {
        var value = new StringBuilder(text.Length);
        var index = 1;
        while(index < text.Length && text[index] != '"') {
            if(text[index] == '\n') {
                throw new InvalidDataException($"Invalid TOML string in '{filePath}' at line {lineNumber}: multi-line strings are not supported.");
            }

            if(text[index] != '\\') {
                value.Append(text[index]);
                index++;
                continue;
            }

            index++;
            if(index >= text.Length) {
                break;
            }

            value.Append(text[index] switch {
                '\\' => "\\",
                '"' => "\"",
                'b' => "\b",
                'f' => "\f",
                'n' => "\n",
                'r' => "\r",
                't' => "\t",
                'u' => ParseUnicodeEscape(text, ref index, 4, filePath, lineNumber),
                'U' => ParseUnicodeEscape(text, ref index, 8, filePath, lineNumber),
                _ => throw new InvalidDataException($"Invalid TOML escape in '{filePath}' at line {lineNumber}: unsupported escape '\\{text[index]}'."),
            });
            index++;
        }

        if(index >= text.Length || text[index] != '"') {
            throw new InvalidDataException($"Invalid TOML string in '{filePath}' at line {lineNumber}: the string is not closed on the same line.");
        }

        return (value.ToString(), index + 1);
    }

    /// <summary>
    /// Parses a <c>\uXXXX</c> or <c>\UXXXXXXXX</c> escape starting after the marker letter.
    /// </summary>
    /// <param name="text">The full text.</param>
    /// <param name="index">The index of the marker letter; updated past the last hex digit.</param>
    /// <param name="digits">The required hex digit count.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The decoded character(s).</returns>
    /// <exception cref="InvalidDataException">Thrown for a malformed or out-of-range escape.</exception>
    private static string ParseUnicodeEscape(string text, ref int index, int digits, string filePath, int lineNumber) {
        if(index + digits >= text.Length) {
            throw new InvalidDataException($"Invalid TOML escape in '{filePath}' at line {lineNumber}: truncated '\\u' escape.");
        }

        var code = 0;
        for(var offset = 1; offset <= digits; offset++) {
            var digit = HexValue(text[index + offset]);
            if(digit < 0) {
                throw new InvalidDataException($"Invalid TOML escape in '{filePath}' at line {lineNumber}: '\\u' escapes require hexadecimal digits.");
            }

            code = (code << 4) | digit;
        }

        index += digits;
        try {
            return char.ConvertFromUtf32(code);
        }
        catch(ArgumentOutOfRangeException) {
            throw new InvalidDataException($"Invalid TOML escape in '{filePath}' at line {lineNumber}: the code point is out of range.");
        }
    }

    /// <summary>
    /// Converts a hexadecimal digit to its value.
    /// </summary>
    /// <param name="character">The digit character.</param>
    /// <returns>The value, or -1 when the character is not a hexadecimal digit.</returns>
    private static int HexValue(char character) {
        if(character is >= '0' and <= '9') {
            return character - '0';
        }

        if(character is >= 'a' and <= 'f') {
            return character - 'a' + 10;
        }

        if(character is >= 'A' and <= 'F') {
            return character - 'A' + 10;
        }

        return -1;
    }

    /// <summary>
    /// Parses a literal string starting at the opening quote.
    /// </summary>
    /// <param name="text">The text starting with <c>'</c>.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <returns>The verbatim value and the count of consumed characters including both quotes.</returns>
    /// <exception cref="InvalidDataException">Thrown for an unterminated string.</exception>
    private static (string Value, int Consumed) ParseLiteralString(string text, string filePath, int lineNumber) {
        var closing = text.IndexOf('\'', 1);
        if(closing < 0) {
            throw new InvalidDataException($"Invalid TOML string in '{filePath}' at line {lineNumber}: the string is not closed on the same line.");
        }

        return (text[1..closing], closing + 1);
    }
}
