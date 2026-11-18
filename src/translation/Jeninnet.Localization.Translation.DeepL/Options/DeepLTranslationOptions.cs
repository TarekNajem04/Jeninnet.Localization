//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Abstractions;

namespace Jeninnet.Localization.Translation.DeepL.Options;

/// <summary>
/// Configuration options for the DeepL API.
/// </summary>
public class DeepLTranslationOptions : TranslationProviderOptions {
    private const string DEFAULT_ENDPOINT = "https://api.deepl.com";

    /// <summary>
    /// Gets or sets the base API endpoint for DeepL. Defaults to the paid endpoint
    /// (<c>https://api.deepl.com</c>); free-tier authentication keys must use
    /// <c>https://api-free.deepl.com</c> instead.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or not an absolute http/https URI.</exception>
    public string Endpoint {
        get;
        set {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if(!IsValidAbsoluteHttpUri(value)) {
                throw new ArgumentException(
                    "Endpoint must be a valid absolute URI using the http or https scheme.",
                    nameof(value));
            }

            field = value;
        }
    } = DEFAULT_ENDPOINT;

    /// <summary>
    /// Gets or sets the source language used to disambiguate the translation, as a DeepL
    /// language code (for example <c>EN</c>). Optional; when blank, the DeepL service
    /// auto-detects the source language.
    /// </summary>
    public string FromLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("DeepL Endpoint is not configured or is not a valid absolute http/https URI.");
        }

        ValidateCore("DeepL");
    }
}
