//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A per-culture glossary of forced term translations used by translation Providers before machine
/// translation. Terms are registered per target culture; translation Providers snapshot the
/// registered terms when they are constructed, so later registrations do not change an existing
/// Provider's behavior.
/// </summary>
/// <remarks>
/// Registration reports conflicts instead of applying them silently: a term that duplicates an
/// already registered term for the same culture (under the effective case rule, with a different
/// target or different matching rules) or that overlaps an already registered term is rejected and
/// reported through <see cref="GlossaryRegistrationResult.Conflicts"/>. Two terms overlap when one
/// source term matches inside a text occurrence of the other under the contained term's whole-word
/// rule; partially overlapping terms with no whole-word containment (for example <c>abcd</c> and
/// <c>cdef</c>) are accepted and resolved deterministically by the consuming Provider. A
/// re-registration that is identical (case comparison per the effective case rule, equal target and
/// matching rules) is idempotent and does not add a second entry.
/// </remarks>
public sealed class MessageGlossary {
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private readonly Dictionary<string, List<GlossaryTerm>> _termsByCulture = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a glossary term for one target culture.
    /// </summary>
    /// <param name="sourceTerm">The source term to match. Must not be null, empty, or whitespace.</param>
    /// <param name="targetCulture">The target culture the forced <paramref name="target"/> applies to.</param>
    /// <param name="target">The forced translation for the target culture. Must not be null.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive. Defaults to <see langword="false"/>.</param>
    /// <param name="wholeWord">Whether matches must be whole words (letters/digits boundaries). Defaults to <see langword="true"/>.</param>
    /// <returns>
    /// The registration result: <see cref="GlossaryRegistrationResult.Registered"/> is
    /// <see langword="true"/> when the term is now part of the glossary (including an idempotent
    /// re-registration); otherwise the conflicting terms are reported and none of them is applied.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sourceTerm"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetCulture"/> or <paramref name="target"/> is null.</exception>
    public GlossaryRegistrationResult Register(
        string sourceTerm,
        CultureInfo targetCulture,
        string target,
        bool caseSensitive = false,
        bool wholeWord = true) {
        var term = new GlossaryTerm(sourceTerm, targetCulture, target, caseSensitive, wholeWord);

        lock(_sync) {
            if(!_termsByCulture.TryGetValue(targetCulture.Name, out var terms)) {
                terms = [];
                _termsByCulture[targetCulture.Name] = terms;
            }

            var conflicts = new List<GlossaryConflict>();
            foreach(var existing in terms) {
                var conflict = DetectConflict(existing, term);
                if(conflict is not null) {
                    conflicts.Add(conflict);
                }
            }

            if(conflicts.Count > 0) {
                return new GlossaryRegistrationResult(registered: false, conflicts);
            }

            if(!terms.Any(existing => IsIdentical(existing, term))) {
                terms.Add(term);
            }

            return new GlossaryRegistrationResult(registered: true, []);
        }
    }

    /// <summary>
    /// Gets a snapshot of all registered terms across every target culture.
    /// </summary>
    public IReadOnlyList<GlossaryTerm> Terms {
        get {
            lock(_sync) {
                var terms = new List<GlossaryTerm>();
                foreach(var cultureTerms in _termsByCulture.Values) {
                    terms.AddRange(cultureTerms);
                }

                return terms;
            }
        }
    }

    /// <summary>
    /// Performs the detect conflict operation.
    /// </summary>
    /// <param name="existing">The existing.</param>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The detect conflict result.</returns>
    private static GlossaryConflict? DetectConflict(GlossaryTerm existing, GlossaryTerm candidate) {
        var comparison = EffectiveComparison(existing, candidate);
        var overlaps = ContainsMatch(candidate.SourceTerm, existing, comparison)
            || ContainsMatch(existing.SourceTerm, candidate, comparison);

        if(!overlaps || IsIdentical(existing, candidate)) {
            return null;
        }

        var duplicate = string.Equals(existing.SourceTerm, candidate.SourceTerm, comparison);
        return new GlossaryConflict(
            duplicate ? GlossaryConflictKind.DuplicateTerm : GlossaryConflictKind.OverlappingTerms,
            existing,
            candidate);
    }

    /// <summary>
    /// Determines whether the value is identical.
    /// </summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsIdentical(GlossaryTerm first, GlossaryTerm second) =>
        string.Equals(first.SourceTerm, second.SourceTerm, EffectiveComparison(first, second))
        && string.Equals(first.Target, second.Target, StringComparison.Ordinal)
        && first.CaseSensitive == second.CaseSensitive
        && first.WholeWord == second.WholeWord;

    /// <summary>
    /// Performs the effective comparison operation.
    /// </summary>
    /// <param name="first">The first.</param>
    /// <param name="second">The second.</param>
    /// <returns>The effective comparison result.</returns>
    private static StringComparison EffectiveComparison(GlossaryTerm first, GlossaryTerm second) =>
        first.CaseSensitive && second.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Determines whether the value contains match.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <param name="contained">The contained.</param>
    /// <param name="comparison">The comparison.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool ContainsMatch(string container, GlossaryTerm contained, StringComparison comparison) {
        var index = container.IndexOf(contained.SourceTerm, comparison);
        while(index >= 0) {
            if(!contained.WholeWord || IsWholeWord(container, index, contained.SourceTerm.Length)) {
                return true;
            }

            index = container.IndexOf(contained.SourceTerm, index + 1, comparison);
        }

        return false;
    }

    /// <summary>
    /// Determines whether the value is whole word.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <param name="length">The length.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWholeWord(string text, int index, int length) =>
        (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
        && (index + length == text.Length || !char.IsLetterOrDigit(text[index + length]));
}
