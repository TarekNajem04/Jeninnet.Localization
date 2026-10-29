//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// The localizability diagnostic: one suggestion identifier for
/// every hardcoded user-facing string shape. Suggestion severity by default —
/// repositories opt into warnings through the <c>LocalizabilitySeverity</c>
/// MSBuild property or standard <c>dotnet_diagnostic</c> configuration — so the
/// analyzer guides authors without ever blocking builds uninvited.
/// </summary>
internal static class LocalizabilityDiagnostics {
    /// <summary>
    /// Hardcoded user-facing string.
    /// </summary>
    public static readonly DiagnosticDescriptor HardcodedString = new(
        id: "JLNLOC001",
        title: "Hardcoded user-facing string should go through the localization pipeline",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: true,
        description: "A user-facing string literal sits outside the message pipeline (UI text, exception message, or validation message). Move it to a catalog and resolve it through IMessageLocalizer, or suppress with justification when the literal is intentional.");
}
