//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Line/offset arithmetic shared by the line-oriented catalog parsers (ResX, PO, XLIFF).
/// Diagnostics carry raw offsets; this map converts between 1-based line/column positions
/// and offsets without depending on a text-model package (netstandard2.0, BCL-only).
/// </summary>
internal static class CatalogLineMap {
    /// <summary>
    /// Computes the raw offset where each 1-based line starts.
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <returns>The line-start offsets; entry 0 is always 0.</returns>
    internal static int[] LineStarts(string text) {
        var starts = new List<int> { 0 };
        for(var index = 0; index < text.Length; index++) {
            if(text[index] == '\n') {
                starts.Add(index + 1);
            }
        }

        return [.. starts];
    }

    /// <summary>
    /// Converts a 1-based line/column position to a raw offset, clamped into the text.
    /// </summary>
    /// <param name="starts">The line-start offsets from <see cref="LineStarts"/>.</param>
    /// <param name="textLength">The file content length.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <returns>The clamped raw offset.</returns>
    internal static int OffsetOf(int[] starts, int textLength, int line, int column) {
        if(starts.Length == 0) {
            return 0;
        }

        var lineIndex = line - 1;
        if(lineIndex < 0) {
            lineIndex = 0;
        }

        if(lineIndex >= starts.Length) {
            lineIndex = starts.Length - 1;
        }

        var offset = starts[lineIndex] + column - 1;
        if(offset < 0) {
            return 0;
        }

        return offset > textLength ? textLength : offset;
    }

    /// <summary>
    /// Returns the raw text of one 1-based line without its terminator.
    /// </summary>
    /// <param name="text">The file content.</param>
    /// <param name="starts">The line-start offsets from <see cref="LineStarts"/>.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <returns>The line text, or <see langword="string.Empty"/> when out of range.</returns>
    internal static string LineText(string text, int[] starts, int line) {
        var lineIndex = line - 1;
        if(lineIndex < 0 || lineIndex >= starts.Length) {
            return string.Empty;
        }

        var start = starts[lineIndex];
        var end = lineIndex + 1 < starts.Length ? starts[lineIndex + 1] : text.Length;
        while(end > start && (text[end - 1] == '\n' || text[end - 1] == '\r')) {
            end--;
        }

        return text.Substring(start, end - start);
    }

    /// <summary>
    /// Locates the quoted occurrence of a key within its raw source line for diagnostics.
    /// Falls back to the line start when the quoted form is absent.
    /// </summary>
    /// <param name="lineText">The raw source line.</param>
    /// <param name="lineStart">The raw offset where the line starts.</param>
    /// <param name="key">The key to locate.</param>
    /// <param name="offset">The raw offset of the quoted key, or the line start.</param>
    /// <param name="length">The quoted length, or the key length on fallback.</param>
    internal static void QuotedKeyPosition(string lineText, int lineStart, string key, out int offset, out int length) {
        var quoted = "\"" + key + "\"";
        var index = lineText.IndexOf(quoted, StringComparison.Ordinal);
        if(index >= 0) {
            offset = lineStart + index;
            length = quoted.Length;
            return;
        }

        offset = lineStart;
        length = key.Length;
    }
}
