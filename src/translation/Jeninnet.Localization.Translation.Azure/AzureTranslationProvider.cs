//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Azure Translator message Provider. Participates through the canonical provider contract
/// (<see cref="IInnerMessageProvider"/>/<see cref="IMessageProvider"/>) and implements the
/// Translation-02 capability model (<see cref="IProviderCapabilities"/>).
/// </summary>
/// <remarks>
/// The Provider's source template is the message key itself; the value is obtained by translating
/// that key into the requested target culture via the Azure Translator v3 REST API (Translation-02).
/// All acquisition complexity — HTTP transport, batching, caching, single-flight coalescing, preload,
/// refresh, and acquisition events — is Provider-owned and does not leak into the canonical contract.
/// The shared <see cref="MachineTranslationEngine"/> owns every lookup path; this class owns only
/// the Azure wire transport and the public surface.
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
/// are configured through <see cref="Options.AzureTranslationOptions"/> and snapshotted at construction
/// (the glossary terms) or captured at construction (the store reference). An acquisition served
/// fully by the translation memory raises no event, and the acquisition event payload covers only
/// externally acquired keys.
/// </para>
/// <para>
/// Every chunk HTTP exchange runs through the provider-owned resilience pipeline (,
/// hand-rolled BCL-only): <c>Timeout → Retry → Breaker → RateLimit → HTTP</c>. One total
/// per-chunk budget bounds every attempt, backoff, and rate-limiter wait and always surfaces as
/// cancellation; only idempotent-safe transient faults are retried; the breaker fails fast with
/// <see cref="BrokenCircuitException"/> while open; the token bucket paces each attempt.
/// Resilience state is per Provider instance and shared by every lookup path; rejections and
/// terminal failures raise the existing acquisition events, and a genuine caller cancellation
/// raises neither event.
/// </para>
/// </remarks>
public class AzureTranslationProvider : IInnerMessageProvider,
    IProviderCapabilities,
    IMultiKeyMessageProvider,
    ISourceTemplateMessageProvider,
    IPreloadableMessageProvider,
    IRefreshableMessageProvider,
    IProviderEventSource,
    ITranslationTransport {
    /// <summary>
    /// Maximum keys per external translation request. Batch administration chunks to the same
    /// size so dry-run estimates and real runs group keys identically.
    /// </summary>
    internal const int MAX_CHUNK_SIZE = 100;

    private readonly HttpClient _httpClient;
    private readonly string _fromLanguage;
    private readonly string _apiKey;
    private readonly string _region;
    private readonly string _endpoint;
    private readonly MachineTranslationEngine _engine;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionEventArgs>? TranslationAcquired;

    /// <inheritdoc />
    public event EventHandler<ProviderAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTranslationProvider"/> class.
    /// </summary>
    /// <param name="options">The Azure Translator options.</param>
    /// <param name="httpClient">The HTTP client used to call the Azure Translator API.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="Options.AzureTranslationOptions"/> are invalid.</exception>
    public AzureTranslationProvider(IOptions<AzureTranslationOptions> options, HttpClient httpClient)
        : this(options, httpClient, TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTranslationProvider"/> class with an
    /// explicit clock. Test-only: production construction always uses
    /// <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="options">The Azure Translator options.</param>
    /// <param name="httpClient">The HTTP client used to call the Azure Translator API.</param>
    /// <param name="timeProvider">The clock backing resilience timeouts, backoffs, breaker timing, and token refills.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>, <paramref name="httpClient"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="Options.AzureTranslationOptions"/> are invalid.</exception>
    internal AzureTranslationProvider(IOptions<AzureTranslationOptions> options, HttpClient httpClient, TimeProvider timeProvider) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(timeProvider);
        _apiKey = value.ApiKey;
        _region = value.Region;
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

    internal bool TryPeekLocalTemplate(string key, string cultureName, out string? template)
        => _engine.TryPeekLocalTemplate(key, cultureName, out template);

    /// <inheritdoc />
    int ITranslationTransport.MaxChunkSize => MAX_CHUNK_SIZE;

    /// <inheritdoc />
    string ITranslationTransport.CircuitNoun => "Azure translation";

    /// <inheritdoc />
    string ITranslationTransport.ServiceDisplayName => "Azure Translator";

    /// <inheritdoc />
    async Task<HttpResponseMessage> ITranslationTransport.SendChunkAsync(IReadOnlyList<string> requestTexts, string targetCulture, CancellationToken cancellationToken) {
        var body = new TranslationRequestBody[requestTexts.Count];
        for(var index = 0; index < requestTexts.Count; index++) {
            body[index] = new TranslationRequestBody { Text = requestTexts[index] };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTranslateUri(targetCulture));
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _apiKey);
        if(!string.IsNullOrWhiteSpace(_region)) {
            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", _region);
        }

        request.Content = JsonContent.Create(body, AzureTranslationJsonContext.Default.TranslationRequestBodyArray);

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
                    $"Azure Translator returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var items = await response.Content.ReadFromJsonAsync(AzureTranslationJsonContext.Default.TranslationItemArray, cancellationToken);
            var translated = new string?[requestTexts.Count];
            if(items is null) {
                return translated;
            }

            var aligned = Math.Min(requestTexts.Count, items.Length);
            for(var index = 0; index < aligned; index++) {
                translated[index] = items[index].Translations.FirstOrDefault(static t => t.To is not null)?.Text;
            }

            return translated;
        }
    }

    /// <summary>
    /// Builds translate uri.
    /// </summary>
    /// <param name="targetCulture">The target culture.</param>
    /// <returns>The translate uri result.</returns>
    private Uri BuildTranslateUri(string targetCulture) {
        var query = $"api-version=3.0&to={Uri.EscapeDataString(targetCulture)}";
        if(!string.IsNullOrWhiteSpace(_fromLanguage)) {
            query += $"&from={Uri.EscapeDataString(_fromLanguage)}";
        }

        return new UriBuilder(new Uri(_endpoint + "/translate", UriKind.Absolute)) { Query = query }.Uri;
    }
}
