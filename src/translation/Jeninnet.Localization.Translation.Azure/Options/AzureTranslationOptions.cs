//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Abstractions;

namespace Jeninnet.Localization.Translation.Azure.Options;

/// <summary>
/// Configuration options for the Azure Translator API.
/// </summary>
public class AzureTranslationOptions : TranslationProviderOptions {
    private const string DEFAULT_ENDPOINT = "https://api.cognitive.microsofttranslator.com";

    /// <summary>
    /// Gets or sets the region associated with the API key. Optional; sent as the
    /// <c>Ocp-Apim-Subscription-Region</c> header only when non-blank.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base API endpoint for Azure Translator. Defaults to the global endpoint.
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
    /// Gets or sets the source language used to disambiguate the translation. Optional; when blank,
    /// the Azure Translator service auto-detects the source language.
    /// </summary>
    public string FromLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("Azure Translator Endpoint is not configured or is not a valid absolute http/https URI.");
        }

        ValidateCore("Azure Translator");
    }
}
