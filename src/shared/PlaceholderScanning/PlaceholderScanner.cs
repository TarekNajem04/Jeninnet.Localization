//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_PLACEHOLDERS_ABSTRACTIONS
namespace Jeninnet.Localization.Abstractions;
#elif JENINNET_PLACEHOLDERS_ANALYZERS
namespace Jeninnet.Localization.Analyzers.Localizability;
#else
#error Compile PlaceholderScanning sources with JENINNET_PLACEHOLDERS_ABSTRACTIONS or JENINNET_PLACEHOLDERS_ANALYZERS defined.
#endif

/// <summary>
/// Brace-group placeholder extraction shared by parity validation and catalog analysis:
/// top-level <c>{...}</c> groups are collected with nesting awareness; the identifier of a
/// group is the trimmed text before the first depth-zero comma. Unbalanced braces, lone
/// closing braces, and empty identifiers are treated as literal text.
/// </summary>
internal static class PlaceholderScanner {
    /// <summary>
    /// Extracts the placeholder identifier set of a template.
    /// </summary>
    /// <param name="template">The template. Must not be null; callers validate.</param>
    /// <returns>The ordinal identifier set (possibly empty).</returns>
    internal static HashSet<string> ExtractIdentifiers(string template) {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        while(index < template.Length) {
            if(template[index] != '{') {
                index++;
                continue;
            }

            var end = FindGroupEnd(template, index);
            if(end < 0) {
                index++;
                continue;
            }

            var body = template.Substring(index + 1, end - index - 1);
            var identifier = TopLevelIdentifier(body);
            if(identifier.Length > 0) {
                identifiers.Add(identifier);
            }

            index = end + 1;
        }

        return identifiers;
    }

    /// <summary>
    /// Finds the closing brace matching the group opening at <paramref name="start"/>.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="start">The opening brace offset.</param>
    /// <returns>The closing brace offset, or -1 when unbalanced.</returns>
    private static int FindGroupEnd(string template, int start) {
        var depth = 0;
        for(var scan = start; scan < template.Length; scan++) {
            if(template[scan] == '{') {
                depth++;
            } else if(template[scan] == '}') {
                depth--;
                if(depth == 0) {
                    return scan;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Takes the top-level identifier of a group body: the trimmed text before the
    /// first depth-zero comma (commas inside nested groups do not terminate it).
    /// </summary>
    /// <param name="body">The group body (between the outer braces).</param>
    /// <returns>The trimmed identifier, possibly empty.</returns>
    internal static string TopLevelIdentifier(string body) {
        var depth = 0;
        for(var index = 0; index < body.Length; index++) {
            if(body[index] == '{') {
                depth++;
            } else if(body[index] == '}') {
                depth--;
            } else if(body[index] == ',' && depth == 0) {
                #pragma warning disable IDE0057 // System.Range is unavailable on netstandard2.0, where this shared source also compiles.
                return body.Substring(0, index).Trim();
#pragma warning restore IDE0057
            }
        }

        return body.Trim();
    }
}
