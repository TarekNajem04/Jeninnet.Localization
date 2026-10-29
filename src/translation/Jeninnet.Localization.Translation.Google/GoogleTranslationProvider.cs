//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Google;

/// <summary>
/// Google Cloud Translation message Provider. Participates through the canonical provider
/// contract (<see cref="IInnerMessageProvider"/>/<see cref="IMessageProvider"/>).
/// </summary>
/// <remarks>
/// The Provider's source template is the message key itself; the value is obtained by translating
/// that key into the requested target culture through the Google Cloud Translation v2 (Basic) REST
/// API at the configured <see cref="GoogleTranslationOptions.Endpoint"/> (the canonical
/// <c>translate</c> method, authenticated with the API key). The API key is sent as the documented
/// <c>key</c> query parameter; the source language is not sent, so the service auto-detects it
/// (documented service behavior). Invariant-culture lookups resolve to <see langword="null"/>
/// immediately without an HTTP request. The Provider implements only the mandatory canonical
/// single-key lookup: it performs no batching, no caching, no preload, no refresh, and emits no
/// acquisition events, and therefore honestly reports <see langword="false"/> for every
/// <see cref="ProviderFeature"/> (the capability model).
/// </remarks>
public class GoogleTranslationProvider : IInnerMessageProvider, IProviderCapabilities {
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleTranslationProvider"/> class.
    /// </summary>
    /// <param name="options">The Google Translation options.</param>
    /// <param name="httpClient">The HTTP client used to call the Google Translation API.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="GoogleTranslationOptions"/> are invalid.</exception>
    public GoogleTranslationProvider(IOptions<GoogleTranslationOptions> options, HttpClient httpClient) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = value.ApiKey;
        _endpoint = value.Endpoint.TrimEnd('/');
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeature feature) => false;

    /// <inheritdoc />
    public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(culture.Equals(CultureInfo.InvariantCulture)) {
            return ValueTask.FromResult<string?>(null);
        }

        return new ValueTask<string?>(TranslateSingleAsync(key, culture.Name, cancellationToken));
    }

    /// <summary>
    /// Performs the translate single operation.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is translate single.</returns>
    private async Task<string?> TranslateSingleAsync(string key, string targetLanguage, CancellationToken cancellationToken) {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}?key={Uri.EscapeDataString(_apiKey)}");
        request.Content = JsonContent.Create(
            new TranslateRequest {
                Q = key,
                Target = targetLanguage,
                Format = "text",
            },
            GoogleTranslationJsonContext.Default.TranslateRequest);

        // The caller's cancellation token is passed to and honored by the HTTP operation: a
        // cancellation arriving mid-operation propagates through SendAsync and surfaces as
        // OperationCanceledException, never as a missing-resource result.
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if(!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"Google Translation returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var payload = await response.Content.ReadFromJsonAsync(GoogleTranslationJsonContext.Default.TranslateResponse, cancellationToken)
            .ConfigureAwait(false);
        return payload?.Data?.Translations.FirstOrDefault()?.TranslatedText;
    }

    /// <summary>
    /// Represents a translate request.
    /// </summary>
    internal sealed class TranslateRequest {
        /// <summary>
        /// Gets or sets the q.
        /// </summary>
        [JsonPropertyName("q")]
        public string Q { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the target.
        /// </summary>
        [JsonPropertyName("target")]
        public string Target { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents a translate response.
    /// </summary>
    internal sealed class TranslateResponse {
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        [JsonPropertyName("data")]
        public TranslateData? Data { get; init; }
    }

    /// <summary>
    /// Represents a translate data.
    /// </summary>
    internal sealed class TranslateData {
        /// <summary>
        /// Gets or sets the translations.
        /// </summary>
        [JsonPropertyName("translations")]
        public Translation[] Translations { get; init; } = [];
    }

    /// <summary>
    /// Represents a translation.
    /// </summary>
    internal sealed class Translation {
        /// <summary>
        /// Gets or sets the translated text.
        /// </summary>
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; init; }
    }
}
