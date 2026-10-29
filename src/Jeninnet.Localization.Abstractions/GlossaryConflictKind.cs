//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Classifies a glossary registration conflict reported by
/// <see cref="MessageGlossary.Register(string, CultureInfo, string, bool, bool)"/>.
/// </summary>
public enum GlossaryConflictKind {
    /// <summary>
    /// The same source term (under the effective case rule) is already registered for the same
    /// target culture with a different target or different matching rules.
    /// </summary>
    DuplicateTerm = 0,

    /// <summary>
    /// The source term overlaps an already registered term for the same target culture: one term
    /// can match inside a text occurrence of the other, so the forced translation would be
    /// ambiguous.
    /// </summary>
    OverlappingTerms = 1
}
