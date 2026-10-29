//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Extracts placeholder identifiers from one message template.
/// Text-level and ICU-aware (the tool stays standalone — no Core dependency):
/// balanced braces are scanned; an ICU (International Components for Unicode) construct (<c>{identifier, style, …}</c>)
/// contributes only its outer argument name and skips its body, so branch-internal
/// placeables (conditional at runtime) never count; any other balanced <c>{…}</c>
/// contributes its content verbatim — exactly the canonical
/// <c>DefaultMessageFormatter</c> parameter key. Unbalanced braces and <c>{}</c>
/// are literal text. Extraction is deterministic and culture-invariant.
/// </summary>
internal static class PlaceholderScanner {
    /// <summary>
    /// Extracts the placeholder identifiers of one template in ordinal order.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <returns>The identifier set (ordinal).</returns>
    public static SortedSet<string> ExtractIdentifiers(string template) {
        ArgumentNullException.ThrowIfNull(template);

        var identifiers = new SortedSet<string>(StringComparer.Ordinal);
        var index = 0;
        while(index < template.Length) {
            var open = template.IndexOf('{', index);
            if(open < 0) {
                break;
            }

            var closed = FindClosingBrace(template, open);
            if(closed < 0) {
                // Unbalanced: the remainder is literal text.
                break;
            }

            RecordIdentifier(identifiers, template[(open + 1)..closed]);
            index = closed + 1;
        }

        return identifiers;
    }

    /// <summary>
    /// Finds the brace matching the one at <paramref name="open"/>, honouring nesting.
    /// </summary>
    /// <param name="template">The template text.</param>
    /// <param name="open">The index of the opening brace.</param>
    /// <returns>The index of the matching closing brace, or <c>-1</c> when unbalanced.</returns>
    private static int FindClosingBrace(string template, int open) {
        var depth = 0;
        for(var position = open; position < template.Length; position++) {
            var current = template[position];
            if(current == '{') {
                depth++;
            } else if(current == '}' && --depth == 0) {
                return position;
            }
        }

        return -1;
    }

    /// <summary>
    /// Records the placeholder identifier of one balanced brace span.
    /// </summary>
    /// <param name="identifiers">The identifier set.</param>
    /// <param name="content">The text between the braces.</param>
    private static void RecordIdentifier(SortedSet<string> identifiers, string content) {
        var comma = content.IndexOf(',');
        if(comma > 0) {
            var candidate = content[..comma].Trim();
            if(IsIdentifier(candidate)) {
                // ICU construct: the outer argument name; branch internals are
                // conditional at runtime and are never identifiers of the template.
                identifiers.Add(candidate);
                return;
            }
        }

        if(content.Length > 0) {
            // The canonical DefaultMessageFormatter parameter key, verbatim.
            identifiers.Add(content);
        }
    }

    /// <summary>
    /// Determines whether the value is identifier.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsIdentifier(string text) =>
        text.Length > 0 &&
        (char.IsLetter(text[0]) || text[0] == '_') &&
        text.All(static current => char.IsLetterOrDigit(current) || current == '_');
}
