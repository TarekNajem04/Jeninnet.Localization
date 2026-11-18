//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.DeepL;

/// <summary>
/// DeepL message Provider. Participates through the canonical provider contract
/// (<see cref="IInnerMessageProvider"/>/<see cref="IMessageProvider"/>) and implements the
/// Translation-02 capability model (<see cref="IProviderCapabilities"/>).
/// </summary>
/// <remarks>
/// The Provider's source template is the message key itself; the value is obtained by translating
/// that key into the requested target culture via the DeepL API v2 <c>translate</c> method.
/// All acquisition complexity — HTTP transport, batching, caching, single-flight coalescing, preload,
/// refresh, and acquisition events — is Provider-owned and does not leak into the canonical contract.
/// The shared <see cref="MachineTranslationEngine"/> owns every lookup path; this class owns only
/// the DeepL wire transport and the public surface.
/// Canonical single-key acquisitions follow the  Model C lifecycle: they are provider-owned and
/// detached from caller tokens, each caller awaits with its own token, and an in-flight entry lives
/// from installation until terminal completion, so one caller cancelling never cancels the shared work
/// nor affects other callers. Multi-key batch and preload acquisitions are instead scoped to the
/// invoking caller's token: their shared per-key entries observe that caller's cancellation and fault
/// when it cancels.
/// <para>
/// The resolution order for a cache-missed key is: translation memory (exact match) →
/// glossary-protected machine translation → TTL (Time To Live) cache write-back. A translation-memory hit
/// short-circuits the paid external call; a glossary hit replaces the matched source terms with
/// opaque placeholder tokens before the text is sent to the service and restores the forced targets
/// in the translated result, so a forced term survives a machine-translation paraphrase. Both layers
/// are configured through <see cref="Options.DeepLTranslationOptions"/> and snapshotted at construction
/// (the glossary terms) or captured at construction (the store reference). An acquisition served
/// fully by the translation memory raises no event, and the acquisition event payload covers only
/// externally acquired keys.
/// </para>
/// <para>
/// Every chunk HTTP exchange runs through the provider-owned resilience pipeline (hand-rolled,
/// BCL-only): <c>Timeout → Retry → Breaker → RateLimit → HTTP</c>. One total
/// per-chunk budget bounds every attempt, backoff, and rate-limiter wait and always surfaces as
/// cancellation; only idempotent-safe transient faults are retried; the breaker fails fast with
/// <see cref="BrokenCircuitException"/> while open; the token bucket paces each attempt.
/// Resilience state is per Provider instance and shared by every lookup path; rejections and
/// terminal failures raise the existing acquisition events, and a genuine caller cancellation
/// raises neither event.
/// </para>
/// </remarks>
public class DeepLTranslationProvider : IInnerMessageProvider,
    IProviderCapabilities,
    IMultiKeyMessageProvider,
    ISourceTemplateMessageProvider,
    IPreloadableMessageProvider,
    IRefreshableMessageProvider,
    IProviderEventSource,
    ITranslationTransport {
    /// <summary>
    /// Maximum keys per external translation request. DeepL accepts up to 50 texts per
    /// <c>translate</c> call; batch administration chunks to the same size.
    /// </summary>
    internal const int MAX_CHUNK_SIZE = 50;

    private readonly HttpClient _httpClient;
    private readonly string _fromLanguage;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly MachineTranslationEngine _engine;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepLTranslationProvider"/> class.
    /// </summary>
    /// <param name="options">The DeepL options.</param>
    /// <param name="httpClient">The HTTP client used to call the DeepL API.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="Options.DeepLTranslationOptions"/> are invalid.</exception>
    public DeepLTranslationProvider(IOptions<DeepLTranslationOptions> options, HttpClient httpClient)
        : this(options, httpClient, TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepLTranslationProvider"/> class with an
    /// explicit clock. Test-only: production construction always uses
    /// <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="options">The DeepL options.</param>
    /// <param name="httpClient">The HTTP client used to call the DeepL API.</param>
    /// <param name="timeProvider">The clock backing resilience timeouts, backoffs, breaker timing, and token refills.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>, <paramref name="httpClient"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="Options.DeepLTranslationOptions"/> are invalid.</exception>
    internal DeepLTranslationProvider(IOptions<DeepLTranslationOptions> options, HttpClient httpClient, TimeProvider timeProvider) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(timeProvider);
        _apiKey = value.ApiKey;
        _endpoint = value.Endpoint.TrimEnd('/');
        _fromLanguage = value.FromLanguage;
        _engine = new MachineTranslationEngine(
            value,
            this,
            timeProvider,
            acquired => TranslationAcquired?.Invoke(this, acquired),
            failed => AcquisitionFailed?.Invoke(this, failed));
    }

    /// <inheritdoc />
    public bool Supports(ProviderFeatures feature) => feature switch {
        ProviderFeatures.MultiKeys => true,
        ProviderFeatures.EventsListening => true,
        ProviderFeatures.Preload => _engine.CachingEnabled,
        ProviderFeatures.Refresh => true,
        ProviderFeatures.Caching => _engine.CachingEnabled,
        ProviderFeatures.SourceTemplates => true,
        _ => false,
    };

    /// <inheritdoc />
    public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => _engine.GetMessageTemplateAsync(key, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default)
        => _engine.GetMessageTemplatesAsync(keys, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string?> TranslateSourceAsync(string sourceTemplate, CultureInfo targetCulture, CancellationToken cancellationToken = default)
        => _engine.TranslateSourceAsync(sourceTemplate, targetCulture, cancellationToken);

    /// <inheritdoc />
    public ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default)
        => _engine.PreloadAsync(keys, culture, cancellationToken);

    /// <inheritdoc />
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        => _engine.RefreshAsync(cancellationToken);

    /// <inheritdoc />
    int ITranslationTransport.MaxChunkSize => MAX_CHUNK_SIZE;

    /// <inheritdoc />
    string ITranslationTransport.CircuitNoun => "DeepL translation";

    /// <inheritdoc />
    string ITranslationTransport.ServiceDisplayName => "DeepL Translate";

    /// <inheritdoc />
    async Task<HttpResponseMessage> ITranslationTransport.SendChunkAsync(IReadOnlyList<string> requestTexts, string targetCulture, CancellationToken cancellationToken) {
        var body = new DeepLTranslateRequest {
            Text = [.. requestTexts],
            TargetLang = ToDeepLLanguageCode(targetCulture),
            SourceLang = string.IsNullOrWhiteSpace(_fromLanguage) ? null : _fromLanguage,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTranslateUri());
        request.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + _apiKey);

        request.Content = JsonContent.Create(body, DeepLTranslationJsonContext.Default.DeepLTranslateRequest);

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    async Task<IReadOnlyList<string?>> ITranslationTransport.ParseChunkResponseAsync(
        HttpResponseMessage response,
        IReadOnlyList<string> requestTexts,
        string targetCulture,
        CancellationToken cancellationToken) {
        using(response) {
            if(!response.IsSuccessStatusCode) {
                throw new HttpRequestException(
                    $"DeepL Translate returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var payload = await response.Content.ReadFromJsonAsync(DeepLTranslationJsonContext.Default.DeepLTranslateResponse, cancellationToken);
            var translated = new string?[requestTexts.Count];
            if(payload is null) {
                return translated;
            }

            var aligned = Math.Min(requestTexts.Count, payload.Translations.Length);
            for(var index = 0; index < aligned; index++) {
                translated[index] = payload.Translations[index].Text;
            }

            return translated;
        }
    }

    /// <summary>
    /// Builds translate uri.
    /// </summary>
    /// <returns>The translate uri result.</returns>
    private Uri BuildTranslateUri() =>
        new(_endpoint + "/v2/translate", UriKind.Absolute);

    /// <summary>
    /// Maps a .NET culture name to the DeepL target language code: the upper-cased culture
    /// name (for example <c>fr</c> becomes <c>FR</c>). Cultures outside the DeepL language
    /// list fail with the service error; the Provider performs no silent codemapping.
    /// </summary>
    /// <param name="targetCulture">The target culture name.</param>
    /// <returns>The DeepL target language code.</returns>
    private static string ToDeepLLanguageCode(string targetCulture) =>
        targetCulture.ToUpperInvariant();
}
