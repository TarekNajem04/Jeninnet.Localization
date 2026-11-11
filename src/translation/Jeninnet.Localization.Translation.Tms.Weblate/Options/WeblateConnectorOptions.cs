//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Weblate.Options;

/// <summary>
/// Configuration options for the Weblate TMS connector: API token, server endpoint,
/// project, component, source language, and per-operation timeout. The token
/// travels only in the <c>Authorization: Token</c> header and is never logged
/// or included in exception messages.
/// </summary>
public class WeblateConnectorOptions {
    /// <summary>
    /// Gets or sets the Weblate API token (personal <c>wlu_</c> or project-scoped
    /// <c>wlp_</c> token) sent as the documented <c>Authorization: Token</c> header.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public string ApiToken {
        get;
        set {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the Weblate server base endpoint (for example
    /// <c>https://hosted.weblate.org</c> for the hosted service, or the
    /// self-hosted instance origin). Must be an absolute http/https URI; the
    /// connector appends the <c>/api/...</c> paths. There is no fixed cloud host:
    /// every customer points at their own server.
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
    } = string.Empty;

    /// <summary>
    /// Gets or sets the Weblate project slug the connector pulls from and pushes to.
    /// </summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Weblate component slug the connector pulls from and pushes to.
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the vendor source language (the Weblate source language of the
    /// component, for example <c>en</c>). Reported on pull results and sent as the
    /// XLIFF source language on XLIFF uploads.
    /// </summary>
    public string SourceLanguage { get; set; } = "en";

    /// <summary>
    /// Gets or sets the total budget for one connector operation (a pull snapshot
    /// or a push including the upload), covering every HTTP exchange. Expiry
    /// surfaces as <see cref="OperationCanceledException"/>. Defaults to 30 seconds.
    /// Single attempt per exchange: the connector performs no retries.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(string.IsNullOrWhiteSpace(ApiToken)) {
            throw new InvalidOperationException("Weblate ApiToken is not configured.");
        }

        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("Weblate Endpoint is not configured or is not a valid absolute http/https URI.");
        }

        if(string.IsNullOrWhiteSpace(Project)) {
            throw new InvalidOperationException("Weblate Project must be a non-empty project slug.");
        }

        if(string.IsNullOrWhiteSpace(Component)) {
            throw new InvalidOperationException("Weblate Component must be a non-empty component slug.");
        }

        if(string.IsNullOrWhiteSpace(SourceLanguage)) {
            throw new InvalidOperationException("Weblate SourceLanguage must not be empty.");
        }

        if(Timeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("Weblate Timeout must be positive.");
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
