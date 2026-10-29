//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Deterministic mechanical mapping from catalog keys to C# identifiers:
/// ordinal, invariant-culture, stable across machines and rebuilds. Keys split into
/// words on separators and on camel/acronym boundaries (<c>BookId</c> → <c>BOOK_ID</c>,
/// <c>OrderID</c> → <c>ORDER_ID</c>), join as upper snake case, and gain a leading
/// underscore when they would start with a digit. Two distinct keys mapping to one
/// identifier is a build error (<c>JLNKEY004</c>), never a silent rename.
/// </summary>
internal static class LocalizationKeyNaming {
    /// <summary>
    /// Sanitizes a catalog key to a C# identifier, or empty when the key carries no
    /// identifier characters.
    /// </summary>
    /// <param name="key">The catalog key.</param>
    /// <returns>The upper-snake identifier, possibly empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    public static string ToIdentifier(string key) {
        if(key is null) {
            throw new ArgumentNullException(nameof(key));
        }

        var words = SplitWords(key);
        if(words.Count == 0) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for(var index = 0; index < words.Count; index++) {
            if(index > 0) {
                builder.Append('_');
            }

            builder.Append(words[index]);
        }

        var identifier = builder.ToString();
        if(identifier.Length > 0 && identifier[0] >= '0' && identifier[0] <= '9') {
            return "_" + identifier;
        }

        return identifier;
    }

    /// <summary>
    /// Escapes a catalog key as a C# double-quoted string literal body.
    /// </summary>
    /// <param name="value">The raw key.</param>
    /// <returns>The escaped literal body (without surrounding quotes).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string EscapeLiteral(string value) {
        if(value is null) {
            throw new ArgumentNullException(nameof(value));
        }

        var builder = new StringBuilder(value.Length);
        for(var index = 0; index < value.Length; index++) {
            var current = value[index];
            switch(current) {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\0': builder.Append("\\0"); break;
                case '\a': builder.Append("\\a"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\v': builder.Append("\\v"); break;
                default:
                    if(current is < (char)0x20 or (char)0x7F) {
                        builder.Append("\\u")
                            .Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                    } else {
                        builder.Append(current);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns whether a dotted name is a legal C# namespace.
    /// </summary>
    /// <param name="candidate">The candidate namespace.</param>
    /// <returns><see langword="true"/> for a non-empty dot-joined identifier chain.</returns>
    public static bool IsValidNamespace(string? candidate) {
        if(string.IsNullOrEmpty(candidate)) {
            return false;
        }

        var parts = candidate!.Split('.');
        for(var index = 0; index < parts.Length; index++) {
            if(!IsIdentifier(parts[index])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Splits words.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <returns>The split words sequence.</returns>
    private static List<string> SplitWords(string key) {
        var words = new List<string>();
        var current = new StringBuilder();
        for(var index = 0; index < key.Length; index++) {
            var currentChar = key[index];
            if(!IsWordChar(currentChar)) {
                FlushWord(words, current);
                continue;
            }

            if(current.Length > 0 && StartsNewWord(key, index)) {
                FlushWord(words, current);
            }

            current.Append(ToUpperInvariant(currentChar));
        }

        FlushWord(words, current);
        return words;
    }

    /// <summary>
    /// Performs the starts new word operation.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="index">The index.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool StartsNewWord(string key, int index) {
        var current = key[index];
        var previous = key[index - 1];
        if(!IsAsciiLetter(current) || !IsAsciiLetter(previous)) {
            return false;
        }

        if(IsLower(previous) && IsUpper(current)) {
            return true;
        }

        if(IsUpper(previous) && IsUpper(current) && index + 1 < key.Length) {
            var next = key[index + 1];
            return IsAsciiLetter(next) && IsLower(next);
        }

        return false;
    }

    /// <summary>
    /// Performs the flush word operation.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <param name="current">The current.</param>
    private static void FlushWord(List<string> words, StringBuilder current) {
        if(current.Length > 0) {
            words.Add(current.ToString());
            current.Clear();
        }
    }

    /// <summary>
    /// Determines whether the value is word char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWordChar(char value) =>
        (value >= 'a' && value <= 'z') ||
        (value >= 'A' && value <= 'Z') ||
        (value >= '0' && value <= '9') ||
        value == '_' ||
        (value > 0x7F && (char.IsLetterOrDigit(value) || value == '_'));

    /// <summary>
    /// Determines whether the value is ascii letter.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsAsciiLetter(char value) =>
value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>
    /// Determines whether the value is lower.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsLower(char value) => value is >= 'a' and <= 'z';

    /// <summary>
    /// Determines whether the value is upper.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsUpper(char value) => value is >= 'A' and <= 'Z';

    /// <summary>
    /// Converts to upper invariant.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The to upper invariant result.</returns>
    private static char ToUpperInvariant(char value) {
        if(value is >= 'a' and <= 'z') {
            return (char)(value - 32);
        }

        return char.ToUpperInvariant(value);
    }

    /// <summary>
    /// Determines whether the value is identifier.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsIdentifier(string candidate) {
        if(candidate.Length == 0) {
            return false;
        }

        var first = candidate[0];
        if(!(IsAsciiLetter(first) || first == '_' || (first > 0x7F && char.IsLetter(first)))) {
            return false;
        }

        for(var index = 1; index < candidate.Length; index++) {
            var current = candidate[index];
            if(!(IsWordChar(current) || (current > 0x7F && char.IsLetterOrDigit(current)))) {
                return false;
            }
        }

        return true;
    }
}
