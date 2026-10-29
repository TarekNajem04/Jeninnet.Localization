//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// One catalog member in file order with raw offsets for diagnostics.
/// </summary>
/// <param name="key">The unescaped key.</param>
/// <param name="keyOffset">The raw offset of the opening quote of the key.</param>
/// <param name="keyLength">The raw length of the quoted key, quotes included.</param>
/// <param name="value">The unescaped template, or <see langword="null"/> for non-string values.</param>
/// <param name="isStringValue">Whether the value is a string template.</param>
/// <param name="valueOffset">The raw offset of the start of the value.</param>
internal sealed class LocJsonEntry(string key, int keyOffset, int keyLength, string? value, bool isStringValue, int valueOffset) {

    /// <summary>
    /// Gets the unescaped key.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the raw offset of the opening quote of the key.
    /// </summary>
    public int KeyOffset { get; } = keyOffset;

    /// <summary>
    /// Gets the raw length of the quoted key, quotes included.
    /// </summary>
    public int KeyLength { get; } = keyLength;

    /// <summary>
    /// Gets the unescaped template, or <see langword="null"/> for non-string values.
    /// </summary>
    public string? Value { get; } = value;

    /// <summary>
    /// Gets whether the value is a string template.
    /// </summary>
    public bool IsStringValue { get; } = isStringValue;

    /// <summary>
    /// Gets the raw offset of the start of the value.
    /// </summary>
    public int ValueOffset { get; } = valueOffset;
}

/// <summary>
/// A structural catalog failure: the file is not a well-formed flat string map.
/// </summary>
/// <param name="message">The failure message.</param>
/// <param name="offset">The raw offset the failure points at.</param>
internal sealed class LocJsonSyntaxError(string message, int offset) {

    /// <summary>
    /// Gets the failure message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Gets the raw offset the failure points at.
    /// </summary>
    public int Offset { get; } = offset;
}

/// <summary>
/// The outcome of parsing one catalog file: members in file order plus, when the
/// file is structurally broken, a single syntax error. Semantic problems
/// (non-string values) surface as entries with <see cref="LocJsonEntry.IsStringValue"/>
/// false so the generator can locate every one of them.
/// </summary>
/// <param name="entries">The members in file order.</param>
/// <param name="syntaxError">The single structural failure, or <see langword="null"/> when the file parsed.</param>
internal sealed class LocJsonParseResult(List<LocJsonEntry> entries, LocJsonSyntaxError? syntaxError) {

    /// <summary>
    /// Gets the members in file order.
    /// </summary>
    public List<LocJsonEntry> Entries { get; } = entries;

    /// <summary>
    /// Gets the structural failure, or <see langword="null"/> when well-formed.
    /// </summary>
    public LocJsonSyntaxError? SyntaxError { get; } = syntaxError;
}

/// <summary>
/// Strict parser for the generator input contract: a JSON object mapping
/// string keys to string templates. Anything outside that subset — nested values,
/// numbers, comments, trailing commas, trailing content — is rejected so the
/// generator can fail the build instead of guessing. Pure function of the text:
/// fully deterministic, no I/O, no timestamps.
/// </summary>
internal static class LocJsonParser {
    /// <summary>
    /// Parses one catalog file.
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <returns>Members in file order plus any structural failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static LocJsonParseResult Parse(string text) {
        if(text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        var reader = new CatalogReader(text);
        return reader.ReadCatalog();
    }

    /// <summary>
    /// Computes the 1-based line and column for a raw offset (clamped to the text).
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <param name="offset">The raw offset.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static void GetLineAndColumn(string text, int offset, out int line, out int column) {
        if(text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        if(offset < 0) {
            offset = 0;
        }

        if(offset > text.Length) {
            offset = text.Length;
        }

        line = 1;
        var lineStart = 0;
        for(var index = 0; index < offset; index++) {
            if(text[index] == '\n') {
                line++;
                lineStart = index + 1;
            }
        }

        column = offset - lineStart + 1;
    }

    /// <summary>
    /// Represents a catalog reader.
    /// </summary>
    private sealed class CatalogReader {
        private readonly string _text;
        private int _position;

        /// <summary>
        /// Initializes a new instance of the CatalogReader class.
        /// </summary>
        /// <param name="text">The text.</param>
        public CatalogReader(string text) {
            _text = text;
            _position = 0;
            if(_text.Length > 0 && _text[0] == '\uFEFF') {
                _position = 1;
            }
        }

        /// <summary>
        /// Reads catalog.
        /// </summary>
        /// <returns>The catalog result.</returns>
        public LocJsonParseResult ReadCatalog() {
            var entries = new List<LocJsonEntry>();
            SkipWhitespace();
            if(!TryConsume('{')) {
                return new LocJsonParseResult(entries, Error("Catalog must be a JSON object mapping string keys to string templates."));
            }

            SkipWhitespace();
            if(TryConsume('}')) {
                return Trailing(entries);
            }

            while(true) {
                SkipWhitespace();
                if(!TryParseString(out var key, out var keyOffset, out var keyLength, out var keyError)) {
                    return new LocJsonParseResult(entries, keyError ?? Error("Expected a string key."));
                }

                SkipWhitespace();
                if(!TryConsume(':')) {
                    return new LocJsonParseResult(entries, Error("Expected ':' after catalog key."));
                }

                SkipWhitespace();
                if(TryParseString(out var value, out var valueOffset, out _, out var valueError)) {
                    entries.Add(new LocJsonEntry(key, keyOffset, keyLength, value, isStringValue: true, valueOffset: valueOffset));
                    valueError = null;
                } else if(valueError is not null) {
                    return new LocJsonParseResult(entries, valueError);
                } else {
                    var nonStringOffset = _position;
                    SkipNonStringValue();
                    entries.Add(new LocJsonEntry(key, keyOffset, keyLength, value: null, isStringValue: false, valueOffset: nonStringOffset));
                }

                SkipWhitespace();
                if(TryConsume(',')) {
                    continue;
                }

                if(TryConsume('}')) {
                    return Trailing(entries);
                }

                return new LocJsonParseResult(entries, Error("Expected ',' or '}' after catalog entry."));
            }
        }

        /// <summary>
        /// Performs the trailing operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <returns>The trailing result.</returns>
        private LocJsonParseResult Trailing(List<LocJsonEntry> entries) {
            SkipWhitespace();
            if(_position < _text.Length) {
                return new LocJsonParseResult(entries, Error("Unexpected content after catalog."));
            }

            return new LocJsonParseResult(entries, syntaxError: null);
        }

        /// <summary>
        /// Performs the error operation.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The error result.</returns>
        private LocJsonSyntaxError Error(string message) => new(message, _position);

        /// <summary>
        /// Performs the skip whitespace operation.
        /// </summary>
        private void SkipWhitespace() {
            while(_position < _text.Length) {
                var current = _text[_position];
                if(current is not ' ' and not '\t' and not '\n' and not '\r') {
                    return;
                }

                _position++;
            }
        }

        /// <summary>
        /// Attempts to consume.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryConsume(char expected) {
            if(_position < _text.Length && _text[_position] == expected) {
                _position++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="start">The start.</param>
        /// <param name="rawLength">The raw length.</param>
        /// <param name="error">The error.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryParseString(out string value, out int start, out int rawLength, out LocJsonSyntaxError? error) {
            value = string.Empty;
            start = _position;
            rawLength = 0;
            error = null;

            if(_position >= _text.Length || _text[_position] != '"') {
                return false;
            }

            _position++;
            var builder = new StringBuilder();
            while(true) {
                if(_position >= _text.Length) {
                    error = new LocJsonSyntaxError("Unterminated string.", start);
                    return false;
                }

                var current = _text[_position];
                if(current == '"') {
                    _position++;
                    value = builder.ToString();
                    rawLength = _position - start;
                    return true;
                }

                if(current != '\\') {
                    builder.Append(current);
                    _position++;
                    continue;
                }

                _position++;
                if(_position >= _text.Length) {
                    error = new LocJsonSyntaxError("Unterminated escape sequence.", start);
                    return false;
                }

                if(!TryAppendEscape(builder, start, out var escapeError)) {
                    error = escapeError;
                    return false;
                }
            }
        }

        /// <summary>
        /// Appends the character of one escape sequence (the backslash is already consumed).
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="stringStart">The index of the string's opening quote (diagnostics).</param>
        /// <param name="error">The syntax error on failure.</param>
        /// <returns><see langword="true"/> when the escape was consumed.</returns>
        private bool TryAppendEscape(StringBuilder builder, int stringStart, out LocJsonSyntaxError? error) {
            error = null;
            switch(_text[_position]) {
                case '"': builder.Append('"'); _position++; return true;
                case '\\': builder.Append('\\'); _position++; return true;
                case '/': builder.Append('/'); _position++; return true;
                case 'b': builder.Append('\b'); _position++; return true;
                case 'f': builder.Append('\f'); _position++; return true;
                case 'n': builder.Append('\n'); _position++; return true;
                case 'r': builder.Append('\r'); _position++; return true;
                case 't': builder.Append('\t'); _position++; return true;
                case 'u':
                    return TryParseUnicodeEscape(builder, stringStart, out error);
                default:
                    error = new LocJsonSyntaxError("Invalid escape sequence.", _position - 1);
                    return false;
            }
        }

        /// <summary>
        /// Attempts to parse unicode escape.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="stringStart">The string start.</param>
        /// <param name="error">The error.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryParseUnicodeEscape(StringBuilder builder, int stringStart, out LocJsonSyntaxError? error) {
            error = null;
            if(_position + 5 > _text.Length) {
                error = new LocJsonSyntaxError("Unterminated unicode escape sequence.", stringStart);
                return false;
            }

            if(!TryReadHexUnit(1, out var unit)) {
                error = new LocJsonSyntaxError("Invalid unicode escape sequence.", _position);
                return false;
            }

            _position += 5;

            if(unit is >= 0xD800 and <= 0xDBFF) {
                if(TryAppendSurrogatePair(builder, unit)) {
                    return true;
                }

                error = new LocJsonSyntaxError("Unpaired Unicode surrogate escape.", _position);
                return false;
            }

            if(unit is >= 0xDC00 and <= 0xDFFF) {
                error = new LocJsonSyntaxError("Unpaired Unicode surrogate escape.", _position);
                return false;
            }

            builder.Append((char)unit);
            return true;
        }

        /// <summary>
        /// Appends the character of a high-surrogate escape together with its low-surrogate
        /// pair when the following escape completes it.
        /// </summary>
        /// <param name="builder">The string builder.</param>
        /// <param name="unit">The high-surrogate code unit.</param>
        /// <returns><see langword="true"/> when the pair was appended.</returns>
        private bool TryAppendSurrogatePair(StringBuilder builder, int unit) {
            if(_position + 6 > _text.Length ||
               _text[_position] != '\\' ||
               _text[_position + 1] != 'u' ||
               !TryReadHexUnit(2, out var low) ||
               low is < 0xDC00 or > 0xDFFF) {
                return false;
            }

            builder.Append(char.ConvertFromUtf32(0x10000 + ((unit - 0xD800) * 0x400) + (low - 0xDC00)));
            _position += 6;
            return true;
        }

        /// <summary>
        /// Reads four hexadecimal digits into one code unit.
        /// </summary>
        /// <param name="offset">The offset from the current position to the first digit.</param>
        /// <param name="unit">The parsed code unit.</param>
        /// <returns><see langword="true"/> when four valid digits were read.</returns>
        private bool TryReadHexUnit(int offset, out int unit) {
            unit = 0;
            var units = new char[4];
            for(var index = 0; index < 4; index++) {
                var digit = _text[_position + offset + index];
                if(!IsHexDigit(digit)) {
                    return false;
                }

                units[index] = digit;
            }

            unit = Convert.ToInt32(new string(units), 16);
            return true;
        }

        /// <summary>
        /// Determines whether the value is hex digit.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        private static bool IsHexDigit(char value) =>
value is (>= '0' and <= '9') or
            (>= 'a' and <= 'f') or
            (>= 'A' and <= 'F');

        /// <summary>
        /// Performs the skip non string value operation.
        /// </summary>
        private void SkipNonStringValue() {
            var depth = 0;
            while(_position < _text.Length) {
                var current = _text[_position];
                if(current == '"') {
                    SkipStringBody();
                    continue;
                }

                if(current is '{' or '[') {
                    depth++;
                    _position++;
                    continue;
                }

                if(current is '}' or ']') {
                    if(depth == 0) {
                        return;
                    }

                    depth--;
                    _position++;
                    continue;
                }

                if(current == ',' && depth == 0) {
                    return;
                }

                _position++;
            }
        }

        /// <summary>
        /// Skips one JSON string body starting at its opening quote (escape-aware).
        /// </summary>
        private void SkipStringBody() {
            _position++;
            while(_position < _text.Length) {
                var current = _text[_position];
                if(current == '\\') {
                    _position += 2;
                    continue;
                }

                if(current == '"') {
                    _position++;
                    return;
                }

                _position++;
            }
        }
    }
}
