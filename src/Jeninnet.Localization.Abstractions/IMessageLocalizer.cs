//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Defines a localizer for messages.
/// </summary>
public interface IMessageLocalizer {
    /// <summary>
    /// Retrieves and formats a message by key.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior. Cancellation is observed after argument validation, propagates
    /// through every culture/default-culture fallback step, prevents further fallback work once
    /// requested, and is never converted into a missing-resource result.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for the key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/>, <paramref name="parameters"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and formats a message by key for the current UI culture.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior; the token is forwarded to the culture-aware overload.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for the key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and formats a plural message by key pair (gettext <c>ngettext</c> shape).
    /// </summary>
    /// <remarks>
    /// Architecture decision: the plural form is selected from the
    /// <see cref="PluralRules"/> cardinal category of <paramref name="count"/> in
    /// the requested <paramref name="culture"/> (<see cref="PluralCategory.One"/>
    /// selects <paramref name="singularKey"/>, every other category selects
    /// <paramref name="pluralKey"/>). Each key resolves through the same
    /// culture-fallback chain as <see cref="LocalizeAsync(string, IReadOnlyDictionary{string, object?}, CultureInfo, CancellationToken)"/>;
    /// providers stay exact-match and the category is always computed in the
    /// requested culture, even when the winning template comes from a parent or
    /// default culture. When only one of the two templates exists, the available
    /// form is returned (documented degraded fallback); when neither exists the
    /// result is <see langword="null"/> — existing miss semantics are unchanged,
    /// nothing is thrown and no form is fabricated. The count is not injected
    /// into <paramref name="parameters"/>; templates referencing it (for example
    /// <c>{count}</c>) require the caller to supply it. Languages with more than
    /// two rendered forms select through this same pair until the  PO (Portable Object)
    /// provider serves per-form entries (deferred; the pair plus count plus
    /// culture carries everything it needs).
    /// </remarks>
    /// <param name="singularKey">The message key for the singular (<c>one</c>) form.</param>
    /// <param name="pluralKey">The message key for every non-singular form.</param>
    /// <param name="count">The count selecting the form; the sign is ignored (magnitude only, per <see cref="PluralRules"/>).</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior. Cancellation is observed after argument validation, propagates
    /// through every culture/default-culture fallback step, prevents further fallback work once
    /// requested, and is never converted into a missing-resource result.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for either key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="singularKey"/>, <paramref name="pluralKey"/>, <paramref name="parameters"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and formats a plural message by key pair for the current UI culture.
    /// </summary>
    /// <param name="singularKey">The message key for the singular (<c>one</c>) form.</param>
    /// <param name="pluralKey">The message key for every non-singular form.</param>
    /// <param name="count">The count selecting the form; the sign is ignored (magnitude only, per <see cref="PluralRules"/>).</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior; the token is forwarded to the culture-aware overload.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for either key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="singularKey"/>, <paramref name="pluralKey"/>, or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and formats a message by key within a disambiguating context
    /// (gettext <c>pgettext</c> shape; compatible with PO <c>msgctxt</c> entries).
    /// </summary>
    /// <remarks>
    /// Architecture decision: the context is namespaced with the
    /// gettext-canonical separator so the  PO provider interoperates
    /// without a second lookup path — the provider key is
    /// <c>context + '\u0004' + key</c> (EOT (End of Transmission), as NGettext indexes
    /// <c>msgctxt</c> entries). The localizer owns this encoding; providers
    /// keep exact-match semantics on the opaque composite key, and the composite
    /// resolves through the same culture-fallback chain as
    /// <see cref="LocalizeAsync(string, IReadOnlyDictionary{string, object?}, CultureInfo, CancellationToken)"/>.
    /// Identical keys under different contexts (or no context) are fully
    /// independent entries. An empty <paramref name="context"/> behaves exactly
    /// like <see cref="LocalizeAsync(string, IReadOnlyDictionary{string, object?}, CultureInfo, CancellationToken)"/>.
    /// </remarks>
    /// <param name="key">The message key.</param>
    /// <param name="context">The disambiguating context (for example a UI location or domain).</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior. Cancellation is observed after argument validation, propagates
    /// through every culture/default-culture fallback step, prevents further fallback work once
    /// requested, and is never converted into a missing-resource result.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for the key in the given context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/>, <paramref name="context"/>, <paramref name="parameters"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and formats a message by key within a disambiguating context
    /// for the current UI culture.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="context">The disambiguating context (for example a UI location or domain).</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior; the token is forwarded to the culture-aware overload.
    /// </param>
    /// <returns>The localized and formatted message, or <see langword="null"/> when no template is found for the key in the given context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/>, <paramref name="context"/>, or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
}
