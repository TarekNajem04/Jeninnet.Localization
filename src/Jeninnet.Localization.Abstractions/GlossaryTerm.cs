//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A single glossary term: a source term forced to a target translation for one target culture,
/// with the matching rules that decide when the term applies.
/// </summary>
/// <remarks>
///  glossary model. A term matches an occurrence of <see cref="SourceTerm"/> in a source
/// text compared ordinally (case-sensitive) or ordinally case-insensitively (default); when
/// <see cref="WholeWord"/> is set (default), the characters directly before and after the
/// occurrence must not be letters or digits. Terms are registered through
/// <see cref="MessageGlossary.Register(string, CultureInfo, string, bool, bool)"/>, which reports
/// conflicting terms instead of applying them silently.
/// </remarks>
public sealed class GlossaryTerm {
    /// <summary>
    /// Initializes a new instance of the <see cref="GlossaryTerm"/> class.
    /// </summary>
    /// <param name="sourceTerm">The source term to match. Must not be null, empty, or whitespace.</param>
    /// <param name="targetCulture">The target culture the forced <paramref name="target"/> applies to.</param>
    /// <param name="target">The forced translation for the target culture. Must not be null.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive. Defaults to <see langword="false"/>.</param>
    /// <param name="wholeWord">Whether matches must be whole words (letters/digits boundaries). Defaults to <see langword="true"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sourceTerm"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetCulture"/> or <paramref name="target"/> is null.</exception>
    public GlossaryTerm(
        string sourceTerm,
        CultureInfo targetCulture,
        string target,
        bool caseSensitive = false,
        bool wholeWord = true) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTerm);
        ArgumentNullException.ThrowIfNull(targetCulture);
        ArgumentNullException.ThrowIfNull(target);

        SourceTerm = sourceTerm;
        TargetCulture = targetCulture;
        Target = target;
        CaseSensitive = caseSensitive;
        WholeWord = wholeWord;
    }

    /// <summary>
    /// Gets the source term to match.
    /// </summary>
    public string SourceTerm { get; }

    /// <summary>
    /// Gets the target culture the forced translation applies to.
    /// </summary>
    public CultureInfo TargetCulture { get; }

    /// <summary>
    /// Gets the forced translation applied when the term matches.
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// Gets a value indicating whether matching is case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; }

    /// <summary>
    /// Gets a value indicating whether matches must be whole words.
    /// </summary>
    public bool WholeWord { get; }
}
