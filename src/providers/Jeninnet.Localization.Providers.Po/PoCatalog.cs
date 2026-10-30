//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// The parsed PO (Portable Object) catalog: flattened exact-match templates plus full plural-form tables.
/// </summary>
/// <remarks>
/// Architecture decision: the context-key encoding reuses
/// (<c>context + '\u0004' + key</c>, EOT (End of Transmission), NGettext-compatible) so
/// <c>LocalizeWithContextAsync</c> interoperates without a second lookup path.
/// Plural entries additionally keep their full <c>msgstr[]</c> array for count-aware
/// selection through <see cref="IPluralFormMessageProvider"/>: the
/// <c>Plural-Forms</c> header formula selects the index where present, otherwise the
/// <see cref="PluralRules"/>  pair mapping applies
/// (<see cref="PluralCategory.One"/> selects form 0, every other category selects
/// form 1 when present). Empty (untranslated) selections report as missing so the
/// <c>DefaultMessageLocalizer</c> fallback chain applies. The same catalog is produced
/// from PO text (<see cref="PoParser"/>) and compiled MO (Machine Object) binaries (<see cref="MoParser"/>),
///.
/// </remarks>
internal sealed class PoCatalog {
    /// <summary>
    /// The gettext-canonical separator joining context and key.
    /// </summary>
    internal const char CONTEXT_SEPARATOR = '\u0004';

    /// <summary>
    /// Gets the parsed header fields (from the <c>msgid ""</c> entry), empty when absent.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Gets the parsed <c>Plural-Forms</c> header, or <see langword="null"/> when absent.
    /// </summary>
    internal PoPluralForms? PluralForms { get; }

    /// <summary>
    /// Gets the flattened exact-match templates (ordinal, case-sensitive).
    /// Empty (untranslated) selections are absent: they report as missing.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Messages { get; }

    private readonly Dictionary<string, string[]> _plurals;

    /// <summary>
    /// Initializes a new instance of the PoCatalog class.
    /// </summary>
    /// <param name="headers">The headers.</param>
    /// <param name="pluralForms">The plural forms.</param>
    /// <param name="messages">The messages.</param>
    /// <param name="plurals">The plurals.</param>
    internal PoCatalog(
        IReadOnlyDictionary<string, string> headers,
        PoPluralForms? pluralForms,
        IReadOnlyDictionary<string, string> messages,
        Dictionary<string, string[]> plurals) {
        Headers = headers;
        PluralForms = pluralForms;
        Messages = messages;
        _plurals = plurals;
    }

    /// <summary>
    /// Selects the plural template for the given key pair, context, and count.
    /// </summary>
    /// <param name="singularKey">The singular key (PO <c>msgid</c>).</param>
    /// <param name="pluralKey">The plural key (PO <c>msgid_plural</c>).</param>
    /// <param name="context">The context (PO <c>msgctxt</c>), or <see langword="null"/> for context-free lookups.</param>
    /// <param name="count">The count selecting the form.</param>
    /// <param name="culture">The culture whose  rules apply when no header is present.</param>
    /// <param name="template">The selected template when found and translated.</param>
    /// <returns><see langword="true"/> when a translated form was selected.</returns>
    internal bool TrySelectPluralForm(
        string singularKey,
        string pluralKey,
        string? context,
        decimal count,
        CultureInfo culture,
        out string? template) {
        template = null;

        var key = context is null
            ? singularKey + '\0' + pluralKey
            : context + '\0' + singularKey + '\0' + pluralKey;

        if(!_plurals.TryGetValue(key, out var forms) || forms.Length == 0) {
            return false;
        }

        int index;
        if(PluralForms is not null) {
            long n;
            try {
                n = (long)decimal.Truncate(decimal.Abs(count));
            }
            catch(OverflowException) {
                return false;
            }

            index = PluralForms.Evaluate(n);
            if(index < 0 || index >= forms.Length) {
                return false;
            }
        } else {
            var category = PluralRules.GetCardinalCategory(culture, count);
            index = category == PluralCategory.One || forms.Length <= 1 ? 0 : 1;
        }

        if(index >= forms.Length || forms[index].Length == 0) {
            return false;
        }

        template = forms[index];
        return true;
    }
}
