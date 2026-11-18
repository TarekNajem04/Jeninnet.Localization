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

        var missing = ExceptIn(sourceTemplate, translatedTemplate);
        var extra = ExceptIn(translatedTemplate, sourceTemplate);
        if(missing.Count == 0 && extra.Count == 0) {
            return;
        }

        throw new SourceTemplateParityException(
            "Translated template placeholders do not match the source template's." +
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
        return PlaceholderScanner.ExtractIdentifiers(template);
    }

    /// <summary>
    /// Computes the placeholders present in <paramref name="first"/> but absent from <paramref name="second"/>.
    /// </summary>
    /// <param name="first">The template to take placeholders from.</param>
    /// <param name="second">The template to subtract.</param>
    /// <returns>The difference set, possibly empty.</returns>
    private static HashSet<string> ExceptIn(string first, string second) {
        var missing = new HashSet<string>(PlaceholderScanner.ExtractIdentifiers(first), StringComparer.Ordinal);
        missing.ExceptWith(PlaceholderScanner.ExtractIdentifiers(second));
        return missing;
    }

}
