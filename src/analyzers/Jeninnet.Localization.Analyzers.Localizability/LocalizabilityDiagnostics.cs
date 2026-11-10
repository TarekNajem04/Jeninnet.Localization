//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// The localizability diagnostics: one suggestion identifier for every hardcoded
/// user-facing string shape, plus warnings for composite shapes (concatenated
/// fragments, uninterpolated holes, placeholder mismatches) and catalog-backed
/// findings (referenced-but-missing and catalogued-but-unreferenced keys).
/// Suggestion severity by default for JLNLOC001; warnings by default for the
/// composite and catalog rules — repositories opt out through the
/// <c>LocalizabilitySeverity</c> MSBuild property or standard
/// <c>dotnet_diagnostic</c> configuration — so the analyzer guides authors without
/// ever blocking builds uninvited.
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

    /// <summary>
    /// Concatenated user-facing fragments.
    /// </summary>
    public static readonly DiagnosticDescriptor ConcatenatedFragments = new(
        id: "JLNLOC002",
        title: "Concatenated fragments should be one localizable template",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "User-facing text is built with '+' from fragments in a localizable sink. Build one template instead (interpolation keeps the holes), then move it to a catalog; the code fix performs the mechanical rewrite.");

    /// <summary>
    /// Non-localized interpolated string.
    /// </summary>
    public static readonly DiagnosticDescriptor NonLocalizedInterpolation = new(
        id: "JLNLOC003",
        title: "Interpolated user-facing string sits outside the localization pipeline",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An interpolated string flows directly into a user-facing sink instead of through the message pipeline. Move the template to a catalog and resolve it through IMessageLocalizer.");

    /// <summary>
    /// Placeholder mismatch against the catalog template.
    /// </summary>
    public static readonly DiagnosticDescriptor PlaceholderMismatch = new(
        id: "JLNLOC004",
        title: "Format arguments do not match the catalog template's placeholders",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A string.Format call passes arguments that cannot fill the referenced catalog key's template (a positional hole beyond the argument list, or a named hole string.Format cannot fill). Fix the call or the catalog; either side may be stale.");

    /// <summary>
    /// Referenced key missing from every visible catalog.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingCatalogKey = new(
        id: "JLNLOC005",
        title: "Referenced key is missing from every visible catalog",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A generated key constant or key-position literal names a key that no visible catalog file (*.loc.json, *.resx, *.po, *.xliff/.xlf) serves. Add the key to a catalog or fix the reference; the rule is silent when no catalog is visible to the compilation.");

    /// <summary>
    /// Unused catalog key.
    /// </summary>
    public static readonly DiagnosticDescriptor UnusedCatalogKey = new(
        id: "JLNLOC006",
        title: "Catalog key is never referenced in source",
        messageFormat: "{0}",
        category: "Localizability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A catalog key is served but never referenced by a key constant or a key-position literal in this compilation. Remove it or reference it; reported on the key's catalog line.",
        customTags: "CompilationEnd");
}
