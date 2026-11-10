//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Placeholder-parity validation shared by source-template translation lookups:
/// the translated template must carry exactly the source template's placeholder
/// identifiers, so a dropped or invented placeholder fails loudly instead of
/// serving a broken template.
/// </summary>
/// <remarks>
/// <para>
/// Extraction rule (documented heuristic, deliberately small and dependency-free):
/// top-level <c>{...}</c> groups are collected with nesting awareness; the
/// identifier of a group is the trimmed text before the first <c>,</c> or the
/// closing brace at depth zero. Both <c>{name}</c> substitutions and ICU argument
/// names (<c>{count, plural, ...}</c> yields <c>count</c>) map to identifiers this
/// way. Unbalanced braces, lone closing braces, and empty identifiers are treated
/// as literal text, mirroring the lenient default formatter. Apostrophe quoting is
/// not special: braces inside quotes still count, so both sides must quote alike.
/// Comparison is an ordinal identifier-set comparison: order may differ (named
/// reordering across languages is legitimate), but missing or extra identifiers fail.
/// </para>
/// </remarks>
public static class TranslationPlaceholderParity {
    /// <summary>
    /// Validates the translated template against the source template.
    /// </summary>
    /// <param name="sourceTemplate">The source template. Must not be null.</param>
    /// <param name="translatedTemplate">The translated template, or null when there is nothing to check.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceTemplate"/> is null.</exception>
    /// <exception cref="SourceTemplateParityException">Thrown when the translated template drops or invents placeholders.</exception>
    public static void Validate(string sourceTemplate, string? translatedTemplate) {
        ArgumentNullException.ThrowIfNull(sourceTemplate);

        if(translatedTemplate is null) {
            return;
        }

        var missing = new HashSet<string>(ExtractPlaceholders(sourceTemplate), StringComparer.Ordinal);
        missing.ExceptWith(ExtractPlaceholders(translatedTemplate));
        var extra = new HashSet<string>(ExtractPlaceholders(translatedTemplate), StringComparer.Ordinal);
        extra.ExceptWith(ExtractPlaceholders(sourceTemplate));

        if(missing.Count == 0 && extra.Count == 0) {
            return;
        }

        throw new SourceTemplateParityException(
            $"Translated template placeholders do not match the source template's." +
            (missing.Count > 0 ? $" Missing: {string.Join(", ", missing.Order(StringComparer.Ordinal))}." : string.Empty) +
            (extra.Count > 0 ? $" Extra: {string.Join(", ", extra.Order(StringComparer.Ordinal))}." : string.Empty));
    }

    /// <summary>
    /// Extracts the placeholder identifier set of a template.
    /// </summary>
    /// <param name="template">The template. Must not be null.</param>
    /// <returns>The ordinal identifier set (possibly empty).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> is null.</exception>
    public static IReadOnlySet<string> ExtractPlaceholders(string template) {
        ArgumentNullException.ThrowIfNull(template);

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        while(index < template.Length) {
            if(template[index] != '{') {
                index++;
                continue;
            }

            var depth = 0;
            var start = index;
            var end = -1;
            for(var scan = index; scan < template.Length; scan++) {
                if(template[scan] == '{') {
                    depth++;
                } else if(template[scan] == '}') {
                    depth--;
                    if(depth == 0) {
                        end = scan;
                        break;
                    }
                }
            }

            if(end < 0) {
                index++;
                continue;
            }

            var body = template.Substring(start + 1, end - start - 1);
            var identifier = TopLevelIdentifier(body);
            if(identifier.Length > 0) {
                identifiers.Add(identifier);
            }

            index = end + 1;
        }

        return identifiers;
    }

    /// <summary>
    /// Takes the top-level identifier of a group body: the trimmed text before the
    /// first depth-zero comma (commas inside nested groups do not terminate it).
    /// </summary>
    /// <param name="body">The group body (between the outer braces).</param>
    /// <returns>The trimmed identifier, possibly empty.</returns>
    private static string TopLevelIdentifier(string body) {
        var depth = 0;
        for(var index = 0; index < body.Length; index++) {
            if(body[index] == '{') {
                depth++;
            } else if(body[index] == '}') {
                depth--;
            } else if(body[index] == ',' && depth == 0) {
                return body[..index].Trim();
            }
        }

        return body.Trim();
    }
}
