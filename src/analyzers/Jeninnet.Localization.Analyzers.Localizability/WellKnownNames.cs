//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// The curated shape tables behind JLNLOC001: UI base types and text
/// properties, message-taking APIs, and attribute arguments. Name-based by design
/// ( Roslyn symbols resolve framework and stub types identically), documented in
/// README as the true/false-positive boundary. Anything outside these shapes —
/// identifiers, logging calls, test asserts, local initializers — is quiet by
/// construction.
/// </summary>
internal static class WellKnownNames {
    /// <summary>
    /// UI base-type full names: assignments to <see cref="UiTextProperties"/> on
    /// receivers deriving from these are UI text.
    /// </summary>
    public static readonly string[] UiBaseTypes = [
        "System.Windows.Forms.Control",
        "System.Windows.Forms.ToolStripItem",
        "System.Windows.Forms.DataGridViewColumn",
        "System.Windows.Forms.ColumnHeader",
        "System.Windows.Forms.ListViewItem",
        "System.Windows.DependencyObject",
        "System.Windows.ContentElement",
        "System.Web.UI.Control",
        "Microsoft.AspNetCore.Components.ComponentBase",
    ];

    /// <summary>
    /// UI text property names assigned on UI receivers.
    /// </summary>
    public static readonly string[] UiTextProperties = [
        "Text",
        "Title",
        "Header",
        "Content",
        "Caption",
        "ToolTip",
        "PlaceholderText",
    ];

    /// <summary>
    /// Receiver names treated as view dictionaries: any string-literal value
    /// assigned through their single-string indexer is page text.
    /// </summary>
    public static readonly string[] ViewBagReceivers = [
        "ViewData",
        "ViewBag",
    ];

    /// <summary>
    /// Type names whose <c>Show</c> method takes a user-facing message first.
    /// </summary>
    public static readonly string[] MessageBoxTypes = [
        "MessageBox",
    ];

    /// <summary>
    /// Method names whose first string-literal argument is a user-facing message.
    /// </summary>
    public static readonly string[] MessageMethods = [
        "Problem",
        "AddModelError",
    ];

    /// <summary>
    /// Validation attribute names whose <c>ErrorMessage</c> argument is user-facing.
    /// Matching is by simple type name so qualified and stub declarations agree.
    /// </summary>
    public static readonly string[] ErrorMessageAttributes = [
        "RequiredAttribute",
        "StringLengthAttribute",
        "RangeAttribute",
        "RegularExpressionAttribute",
        "CompareAttribute",
        "MinLengthAttribute",
        "MaxLengthAttribute",
        "ValidationAttribute",
    ];

    /// <summary>
    /// Returns whether a type derives from a UI base type (ordinal full-name walk).
    /// </summary>
    /// <param name="type">The receiver type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for UI receivers.</returns>
    public static bool DerivesFromUiBase(ITypeSymbol? type) =>
        SelfAndBases(type).Any(symbol =>
            UiBaseTypes.Any(uiBase => DisplayName(symbol).Equals(uiBase, StringComparison.Ordinal)));

    /// <summary>
    /// Returns whether a type derives from <see cref="Exception"/> (ordinal walk).
    /// </summary>
    /// <param name="type">The constructed type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for exception types.</returns>
    public static bool DerivesFromException(ITypeSymbol? type) =>
        SelfAndBases(type).Any(symbol => DisplayName(symbol).Equals("System.Exception", StringComparison.Ordinal));

    /// <summary>
    /// Performs the self and bases operation.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The self and bases sequence.</returns>
    private static IEnumerable<ITypeSymbol> SelfAndBases(ITypeSymbol? type) {
        for(var current = type; current is not null; current = current.BaseType) {
            yield return current;
        }
    }

    /// <summary>
    /// Performs the display name operation.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The display name text.</returns>
    private static string DisplayName(ITypeSymbol symbol) =>
        symbol.OriginalDefinition?.ToDisplayString() ?? symbol.ToDisplayString();
}
