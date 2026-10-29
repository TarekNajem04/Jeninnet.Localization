//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Diagnostic descriptors for the localization-keys generator.
/// Every failure of the generator is a build <em>error</em> with a file/line location:
/// malformed or off-contract content never produces a silent skip or a partial class.
/// </summary>
internal static class GeneratorDiagnostics {
    private const string CATEGORY = "LocalizationKeys";

    /// <summary>
    /// The catalog is not well-formed JSON (or not a JSON object at the root).
    /// </summary>
    public static readonly DiagnosticDescriptor MalformedContent = new(
        id: "JLNKEY001",
        title: "Localization catalog is not well-formed",
        messageFormat: "Localization catalog is not valid JSON: {0}",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A catalog entry does not map a string key to a string template.
    /// </summary>
    public static readonly DiagnosticDescriptor NonStringEntry = new(
        id: "JLNKEY002",
        title: "Localization catalog entry must map a string key to a string template",
        messageFormat: "Localization catalog entry '{0}' must map to a string template",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The same key occurs more than once in a single catalog file.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateKey = new(
        id: "JLNKEY003",
        title: "Localization catalog contains a duplicate key",
        messageFormat: "Duplicate localization key '{0}' (first seen at line {1})",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Two distinct keys sanitize to the same C# identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor IdentifierCollision = new(
        id: "JLNKEY004",
        title: "Localization keys collide on one generated identifier",
        messageFormat: "Localization keys '{0}' and '{1}' both generate identifier '{2}'",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A key carries no identifier characters at all.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyIdentifier = new(
        id: "JLNKEY005",
        title: "Localization key generates no identifier",
        messageFormat: "Localization key '{0}' contains no identifier characters",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The requested generated namespace is not a legal dotted identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidNamespace = new(
        id: "JLNKEY006",
        title: "Generated namespace is not a legal identifier",
        messageFormat: "LocalizationKeysNamespace '{0}' is not a legal dotted C# identifier",
        category: CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
