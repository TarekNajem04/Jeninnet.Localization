//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Translation.Google.Options;

/// <summary>
/// Configuration options for the Google Cloud Translation API.
/// </summary>
public class GoogleTranslationOptions {
    private const string DEFAULT_ENDPOINT = "https://translation.googleapis.com/language/translate/v2";

    /// <summary>
    /// Gets or sets the API key for Google Cloud Translation.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public string ApiKey {
        get;
        set {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the base API endpoint for Google Cloud Translation. Defaults to the standard v2 endpoint.
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
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(string.IsNullOrWhiteSpace(ApiKey)) {
            throw new InvalidOperationException("Google Translation ApiKey is not configured.");
        }

        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("Google Translation Endpoint is not configured or is not a valid absolute http/https URI.");
        }
    }

    /// <summary>
    /// Determines whether the value is valid absolute http uri.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsValidAbsoluteHttpUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";
}
