//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// One parsed catalog member: value equality so the incremental pipeline can tell
/// an unchanged parse result from a changed one.
/// </summary>
/// <param name="key">The unescaped key.</param>
/// <param name="keyOffset">The raw offset of the opening quote of the key.</param>
/// <param name="keyLength">The raw length of the quoted key, quotes included.</param>
/// <param name="value">The unescaped template, or <see langword="null"/> for non-string values.</param>
/// <param name="isStringValue">Whether the value is a string template.</param>
/// <param name="valueOffset">The raw offset of the start of the value.</param>
internal sealed class ParsedCatalogEntry(string key, int keyOffset, int keyLength, string? value, bool isStringValue, int valueOffset) : IEquatable<ParsedCatalogEntry> {

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

    /// <inheritdoc/>
    public bool Equals(ParsedCatalogEntry? other) =>
        other is not null &&
        string.Equals(Key, other.Key, StringComparison.Ordinal) &&
        KeyOffset == other.KeyOffset &&
        KeyLength == other.KeyLength &&
        string.Equals(Value, other.Value, StringComparison.Ordinal) &&
        IsStringValue == other.IsStringValue &&
        ValueOffset == other.ValueOffset;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ParsedCatalogEntry);

    /// <inheritdoc/>
    public override int GetHashCode() {
        unchecked {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Key);
            hash = (hash * 31) + KeyOffset;
            hash = (hash * 31) + KeyLength;
            hash = (hash * 31) + (Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value));
            hash = (hash * 31) + (IsStringValue ? 1 : 0);
            hash = (hash * 31) + ValueOffset;
            return hash;
        }
    }
}

/// <summary>
/// One parsed catalog file: value equality so the incremental pipeline reuses cached
/// parse results for unchanged files.
/// </summary>
/// <param name="path">The additional-file path.</param>
/// <param name="text">The file content.</param>
/// <param name="entries">The parsed members in file order.</param>
/// <param name="syntaxError">The structural failure message, or <see langword="null"/> when the file parsed.</param>
/// <param name="syntaxOffset">The raw offset the structural failure points at.</param>
internal sealed class ParsedCatalogFile(string path, string text, ImmutableArray<ParsedCatalogEntry> entries, string? syntaxError, int syntaxOffset) : IEquatable<ParsedCatalogFile> {

    /// <summary>
    /// Gets the additional-file path.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// Gets the members in file order.
    /// </summary>
    public ImmutableArray<ParsedCatalogEntry> Entries { get; } = entries;

    /// <summary>
    /// Gets the structural failure message, or <see langword="null"/> when well-formed.
    /// </summary>
    public string? SyntaxError { get; } = syntaxError;

    /// <summary>
    /// Gets the raw offset of the structural failure.
    /// </summary>
    public int SyntaxOffset { get; } = syntaxOffset;

    /// <inheritdoc/>
    public bool Equals(ParsedCatalogFile? other) =>
        other is not null &&
        string.Equals(Path, other.Path, StringComparison.Ordinal) &&
        string.Equals(Text, other.Text, StringComparison.Ordinal) &&
        Entries.SequenceEqual(other.Entries) &&
        string.Equals(SyntaxError, other.SyntaxError, StringComparison.Ordinal) &&
        SyntaxOffset == other.SyntaxOffset;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ParsedCatalogFile);

    /// <inheritdoc/>
    public override int GetHashCode() {
        unchecked {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Path);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Text);
            for(var index = 0; index < Entries.Length; index++) {
                hash = (hash * 31) + Entries[index].GetHashCode();
            }

            hash = (hash * 31) + (SyntaxError is null ? 0 : StringComparer.Ordinal.GetHashCode(SyntaxError));
            hash = (hash * 31) + SyntaxOffset;
            return hash;
        }
    }
}
