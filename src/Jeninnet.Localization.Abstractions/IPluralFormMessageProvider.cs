//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message provider that can resolve gettext-style plural forms for a culture in a
/// single count-aware lookup. Optional contract; implemented by the PO (Portable Object) provider.
/// </summary>
/// <remarks>
/// Architecture decision  (plural-form serving, recorded before the PO provider):
/// a PO plural entry (<c>msgid</c> / <c>msgid_plural</c> / <c>msgstr[n]</c>) is a single
/// logical entry whose rendered form is selected by the count. When the
/// catalog carries a <c>Plural-Forms</c> header, its <c>plural=</c> expression selects the
/// <c>msgstr</c> index; otherwise the <see cref="PluralRules"/> cardinal category of
/// the count selects through the  pair mapping
/// (<see cref="PluralCategory.One"/> selects the singular form, every other category
/// selects the plural side). The lookup itself stays exact-match (the cross-assembly contract ruleI):
/// no parent-culture walk happens inside the provider — culture fallback is owned
/// exclusively by <c>DefaultMessageLocalizer</c>, which calls this contract once per
/// culture in its fallback chain. An empty selected form (untranslated <c>msgstr</c>)
/// is reported as <see langword="null"/> so the localizer fallback chain applies;
/// a missing entry is likewise <see langword="null"/>. The sign of
/// the count is ignored (magnitude only, per <see cref="PluralRules"/>).
/// Contextual plurals (gettext <c>npgettext</c> shape) use
/// <see cref="GetPluralTemplateWithContextAsync"/> with the same EOT (End of Transmission) context-key
/// encoding as <c>LocalizeWithContextAsync</c>.
/// </remarks>
public interface IPluralFormMessageProvider : IMessageProvider {
    /// <summary>
    /// Resolves the plural template for the given singular/plural key pair and count.
    /// </summary>
    /// <param name="singularKey">The message key for the singular form (PO <c>msgid</c>).</param>
    /// <param name="pluralKey">The message key for the plural side (PO <c>msgid_plural</c>).</param>
    /// <param name="count">The count selecting the form; the sign is ignored (magnitude only).</param>
    /// <param name="culture">The culture to resolve for (exact-match; no provider-level fallback).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The selected template, or <see langword="null"/> when the entry or the selected form is missing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="singularKey"/>, <paramref name="pluralKey"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> GetPluralTemplateAsync(
        string singularKey,
        string pluralKey,
        decimal count,
        CultureInfo culture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the plural template for the given singular/plural key pair, count, and
    /// disambiguating context (gettext <c>npgettext</c> shape; PO <c>msgctxt</c> entries).
    /// </summary>
    /// <param name="singularKey">The message key for the singular form (PO <c>msgid</c>).</param>
    /// <param name="pluralKey">The message key for the plural side (PO <c>msgid_plural</c>).</param>
    /// <param name="context">The disambiguating context (PO <c>msgctxt</c>).</param>
    /// <param name="count">The count selecting the form; the sign is ignored (magnitude only).</param>
    /// <param name="culture">The culture to resolve for (exact-match; no provider-level fallback).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The selected template, or <see langword="null"/> when the entry or the selected form is missing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="singularKey"/>, <paramref name="pluralKey"/>, <paramref name="context"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> GetPluralTemplateWithContextAsync(
        string singularKey,
        string pluralKey,
        string context,
        decimal count,
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}
