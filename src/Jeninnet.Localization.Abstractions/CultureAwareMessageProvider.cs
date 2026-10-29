//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Dispatches message-template lookups to per-culture inner providers.
/// </summary>
/// <remarks>
/// Performs no parent-culture fallback: for the requested culture it selects the provider
/// registered under the full culture name (e.g., <c>"fr-CA"</c>) and, if absent, the provider
/// registered under the two-letter ISO language name (e.g., <c>"fr"</c>). The original requested
/// <see cref="CultureInfo"/> is always passed through to the selected provider. Parent-culture
/// traversal and default-culture fallback are owned exclusively by <c>DefaultMessageLocalizer</c>.
/// </remarks>
/// <param name="providers">A dictionary mapping culture codes to message providers.</param>
public sealed class CultureAwareMessageProvider(
    IReadOnlyDictionary<string, IMessageProvider> providers
) : IInnerMessageProvider, IPluralFormMessageProvider {
    /// <inheritdoc />
    public async ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture == CultureInfo.InvariantCulture) {
            return null;
        }

        if(providers.TryGetValue(culture.Name, out var provider) ||
            providers.TryGetValue(culture.TwoLetterISOLanguageName, out provider)) {
            return await provider.GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetPluralTemplateAsync(string singularKey, string pluralKey, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture == CultureInfo.InvariantCulture) {
            return null;
        }

        if((providers.TryGetValue(culture.Name, out var provider) ||
            providers.TryGetValue(culture.TwoLetterISOLanguageName, out provider)) &&
            provider is IPluralFormMessageProvider pluralProvider) {
            return await pluralProvider.GetPluralTemplateAsync(singularKey, pluralKey, count, culture, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetPluralTemplateWithContextAsync(string singularKey, string pluralKey, string context, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(singularKey);
        ArgumentNullException.ThrowIfNull(pluralKey);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture == CultureInfo.InvariantCulture) {
            return null;
        }

        if((providers.TryGetValue(culture.Name, out var provider) ||
            providers.TryGetValue(culture.TwoLetterISOLanguageName, out provider)) &&
            provider is IPluralFormMessageProvider pluralProvider) {
            return await pluralProvider.GetPluralTemplateWithContextAsync(singularKey, pluralKey, context, count, culture, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
