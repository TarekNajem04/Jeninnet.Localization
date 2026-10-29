//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A translation-memory store: exact-match reuse of previously approved translations, keyed by the
/// source text (hashed internally) and the target culture. Fuzzy matching is explicitly out of the
/// contract — only a stored entry whose source text equals the requested source text exactly, for
/// the same culture, yields a target.
/// </summary>
/// <remarks>
///  translation-memory contract. Translation Providers consult the store before machine
/// translation; a hit short-circuits the paid translation call. Implementations must return
/// <see langword="false"/> for a miss rather than throwing; the Provider never writes machine
/// translations back into the store (the store holds approved translations only).
/// </remarks>
public interface ITranslationMemory {
    /// <summary>
    /// Attempts to find the stored translation of the exact <paramref name="source"/> text for the
    /// requested <paramref name="targetCulture"/>.
    /// </summary>
    /// <param name="source">The source text to look up. Must not be null, empty, or whitespace.</param>
    /// <param name="targetCulture">The target culture to look up.</param>
    /// <param name="target">The stored translation when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an exact-match entry exists; otherwise <see langword="false"/>.</returns>
    bool TryLookup(string source, CultureInfo targetCulture, [NotNullWhen(true)] out string? target);
}
