//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Crowdin.Options;

/// <summary>
/// Configuration options for the Crowdin TMS connector: API token, endpoint,
/// project, upload file, source language, and per-operation timeout. The token
/// travels only in the <c>Authorization: Bearer</c> header and is never logged
/// or included in exception messages.
/// </summary>
public class CrowdinConnectorOptions {
    private const string DEFAULT_ENDPOINT = "https://api.crowdin.com";

    /// <summary>
    /// Gets or sets the Crowdin personal access token sent as the documented
    /// <c>Authorization: Bearer</c> header.
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
    /// Gets or sets the Crowdin API base endpoint. Defaults to the Crowdin cloud
    /// (<c>https://api.crowdin.com</c>); Enterprise organizations use their own
    /// <c>https://{organization}.crowdin.com</c> host instead.
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
    /// Gets or sets the Crowdin project id the connector pulls from and pushes to.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the Crowdin project file id the translated uploads attach to.
    /// Pull never needs it; push requires a positive value.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// Gets or sets the vendor source language (the Crowdin source language of the
    /// project, for example <c>en</c>). Reported on pull results and sent as the
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
            throw new InvalidOperationException("Crowdin ApiToken is not configured.");
        }

        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("Crowdin Endpoint is not configured or is not a valid absolute http/https URI.");
        }

        if(ProjectId <= 0) {
            throw new InvalidOperationException("Crowdin ProjectId must be a positive project id.");
        }

        if(string.IsNullOrWhiteSpace(SourceLanguage)) {
            throw new InvalidOperationException("Crowdin SourceLanguage must not be empty.");
        }

        if(Timeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("Crowdin Timeout must be positive.");
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
