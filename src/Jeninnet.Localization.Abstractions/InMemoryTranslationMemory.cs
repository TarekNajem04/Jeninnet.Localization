//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// An in-process <see cref="ITranslationMemory"/> implementation. Entries are keyed by the SHA-256 (Secure Hash Algorithm, 256-bit)
/// hash of the exact source text (UTF-8 (Unicode Transformation Format, 8-bit)) plus the target culture name, so a lookup matches only the
/// exact source text — no fuzzy or case-insensitive matching. All members are thread-safe.
/// </summary>
/// <remarks>
/// The cryptographic hash keeps entry keys bounded regardless of source-text length; a hash
/// collision would require two distinct UTF-8 source texts with equal SHA-256 digests, which is
/// computationally infeasible, so hash equality is treated as exact match.
/// </remarks>
public sealed class InMemoryTranslationMemory : ITranslationMemory {
    private readonly ConcurrentDictionary<(string Culture, string SourceHash), string> _entries = new();

    /// <summary>
    /// Adds or replaces the approved translation of <paramref name="source"/> for
    /// <paramref name="targetCulture"/>.
    /// </summary>
    /// <param name="source">The source text. Must not be null, empty, or whitespace.</param>
    /// <param name="targetCulture">The target culture the translation applies to.</param>
    /// <param name="target">The approved translation. Must not be null.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetCulture"/> or <paramref name="target"/> is null.</exception>
    public void Add(string source, CultureInfo targetCulture, string target) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(targetCulture);
        ArgumentNullException.ThrowIfNull(target);

        _entries[(targetCulture.Name, Hash(source))] = target;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetCulture"/> is null.</exception>
    public bool TryLookup(string source, CultureInfo targetCulture, [NotNullWhen(true)] out string? target) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(targetCulture);

        return _entries.TryGetValue((targetCulture.Name, Hash(source)), out target);
    }

    /// <summary>
    /// Determines whether the value hash.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The hash text.</returns>
    private static string Hash(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
