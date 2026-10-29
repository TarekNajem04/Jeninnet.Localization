//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Describes one glossary registration conflict: the rejected <see cref="GlossaryTerm"/> versus
/// the already registered term it conflicts with. Reported by
/// <see cref="MessageGlossary.Register(string, CultureInfo, string, bool, bool)"/>; a conflicting
/// term is never applied to the glossary.
/// </summary>
public sealed class GlossaryConflict {
    /// <summary>
    /// Initializes a new instance of the GlossaryConflict class.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="existing">The existing.</param>
    /// <param name="rejected">The rejected.</param>
    internal GlossaryConflict(GlossaryConflictKind kind, GlossaryTerm existing, GlossaryTerm rejected) {
        Kind = kind;
        Existing = existing;
        Rejected = rejected;
    }

    /// <summary>
    /// Gets the kind of conflict.
    /// </summary>
    public GlossaryConflictKind Kind { get; }

    /// <summary>
    /// Gets the already registered term involved in the conflict.
    /// </summary>
    public GlossaryTerm Existing { get; }

    /// <summary>
    /// Gets the rejected term that would have been registered.
    /// </summary>
    public GlossaryTerm Rejected { get; }
}
