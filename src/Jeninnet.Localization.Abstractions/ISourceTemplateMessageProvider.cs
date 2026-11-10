//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message Provider that can translate an explicit source template instead of an
/// opaque key. Key-as-source stays the default path; this capability separates
/// "what to translate" from "how it is addressed" so two keys sharing one English
/// text translate (and cache) independently and a key rename retranslates.
/// </summary>
/// <remarks>
/// Source-template lookups keep their own cache identity
/// (<c>(sourceTemplate, culture)</c>); key lookups keep <c>(key, culture)</c>.
/// The translated result is placeholder-parity checked against the source template
/// (see <see cref="TranslationPlaceholderParity"/>) and a mismatch throws
/// <see cref="SourceTemplateParityException"/> so bad templates never silently serve.
/// </remarks>
public interface ISourceTemplateMessageProvider : IMessageProvider {
    /// <summary>
    /// Translates the given source template into the target culture.
    /// </summary>
    /// <param name="sourceTemplate">The source template to translate. Must not be null.</param>
    /// <param name="targetCulture">The culture to translate into.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The translated template, or null when the target culture is invariant or the provider has no translation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceTemplate"/> or <paramref name="targetCulture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="SourceTemplateParityException">Thrown when the translated template's placeholders do not match the source template's.</exception>
    ValueTask<string?> TranslateSourceAsync(
        string sourceTemplate,
        CultureInfo targetCulture,
        CancellationToken cancellationToken = default);
}
