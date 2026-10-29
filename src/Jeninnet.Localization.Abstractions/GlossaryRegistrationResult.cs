//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// The outcome of a <see cref="MessageGlossary.Register(string, CultureInfo, string, bool, bool)"/>
/// call: whether the term is now part of the glossary, and the conflicts that prevented it when it
/// is not. Conflicts are always reported, never applied silently.
/// </summary>
public sealed class GlossaryRegistrationResult {
    /// <summary>
    /// Initializes a new instance of the GlossaryRegistrationResult class.
    /// </summary>
    /// <param name="registered">The registered.</param>
    /// <param name="conflicts">The conflicts.</param>
    internal GlossaryRegistrationResult(bool registered, IReadOnlyList<GlossaryConflict> conflicts) {
        Registered = registered;
        Conflicts = conflicts;
    }

    /// <summary>
    /// Gets a value indicating whether the glossary now contains the registered term. This is also
    /// <see langword="true"/> for an idempotent re-registration of an identical term.
    /// </summary>
    public bool Registered { get; }

    /// <summary>
    /// Gets the conflicts that prevented registration, ordered by the existing terms they were
    /// detected against. Empty when <see cref="Registered"/> is <see langword="true"/>.
    /// </summary>
    public IReadOnlyList<GlossaryConflict> Conflicts { get; }
}
