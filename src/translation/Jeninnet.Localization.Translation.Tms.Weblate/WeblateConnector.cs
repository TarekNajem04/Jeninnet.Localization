//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Weblate;

/// <summary>
/// The Weblate <see cref="ITmsConnector"/>: pulls translated snapshots through the
/// Weblate REST API (translation units of one project component, keyed by unit
/// context or source text) and pushes translated catalogs as files (single
/// <c>translate</c> upload) with dry-run previews and <see cref="CatalogKeyStatus"/>
/// conflict reporting.
/// </summary>
/// <remarks>
/// <para>
/// Mapping (one Weblate project component): the catalog key is the unit
/// <c>context</c> when present, else the first source form; the template is the
/// first target form (null when the vendor has no translation yet); the
/// translator note is the unit <c>note</c>. Plural units (more than one source
/// or target form) are outside the flat catalog subset and are skipped on pull
/// (documented boundary).
/// </para>
/// <para>
/// Auth is a personal (<c>wlu_</c>) or project-scoped (<c>wlp_</c>) token in the
/// <c>Authorization: Token</c> header. The token is never logged and never
/// appears in exception messages. Push uploads the translated file (PO
/// <c>#.</c> comments or XLIFF translator notes carry
/// <see cref="TmsPushRequest.Notes"/>) in one vendor call against the configured
/// project component and language. Uploads run with <c>method=translate</c> so
/// they add translations without clearing the vendor state the comparison did
/// not select.
/// </para>
/// <para>
/// Lifecycle: the options values are captured as an immutable construction snapshot
/// and the connector never re-reads the options object. One attempt per HTTP
/// exchange (no retries); a <see cref="WeblateConnectorOptions.Timeout"/> breach
/// surfaces as <see cref="OperationCanceledException"/>. The connector never
/// writes catalog files or store entries; applying an approved push stays the
/// caller's governed composition. There is no fixed cloud host: the endpoint is
/// the customer's own Weblate server (self-hosted) or the hosted service.
/// </para>
/// </remarks>
public sealed class WeblateConnector : ITmsConnector {
    private const int PAGE_SIZE = 1000;
    private const int MAX_BODY_SNIPPET = 500;

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiToken;
    private readonly string _project;
    private readonly string _component;
    private readonly string _sourceLanguage;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeblateConnector"/> class.
    /// </summary>
    /// <param name="options">The Weblate connector options.</param>
    /// <param name="httpClient">The HTTP client (from <c>IHttpClientFactory</c> in production, a fake handler in tests).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the options are incomplete.</exception>
    public WeblateConnector(IOptions<WeblateConnectorOptions> options, HttpClient httpClient) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _baseUrl = value.Endpoint.TrimEnd('/');
        _apiToken = value.ApiToken;
        _project = value.Project;
        _component = value.Component;
        _sourceLanguage = value.SourceLanguage;
        _timeout = value.Timeout;
    }

    /// <inheritdoc />
    public async Task<TmsPullResult> PullAsync(CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(culture);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_timeout);
        var token = budget.Token;

        var snapshot = await GetSnapshotAsync(culture.Name, token);
        var templates = new Dictionary<string, string?>(StringComparer.Ordinal);
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var pair in snapshot.Units) {
            if(pair.Value.IsPlural) {
                continue;
            }

            templates[pair.Key] = pair.Value.Target;
            if(!string.IsNullOrEmpty(pair.Value.Note)) {
                notes[pair.Key] = pair.Value.Note;
            }
        }

        return new TmsPullResult {
            Culture = culture,
            SourceLanguage = _sourceLanguage,
            Templates = templates,
            Notes = notes,
        };
    }

    /// <inheritdoc />
    public async Task<TmsPushReport> PushAsync(TmsPushRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Culture);

        if(request.Format is not (TmsCatalogFormat.Po or TmsCatalogFormat.Xliff)) {
            throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported catalog format '{request.Format}'.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_timeout);
        var token = budget.Token;

        var snapshot = await GetSnapshotAsync(request.Culture.Name, token);
        var entries = new List<TmsPushEntry>(request.Target.Count);
        var updatedKeys = new List<string>();
        foreach(var key in request.Target.Keys.Order(StringComparer.Ordinal)) {
            var proposed = request.Target[key];
            if(string.IsNullOrEmpty(proposed)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.Skipped,
                    Detail = "empty target value; existing vendor translations are never cleared.",
                });
                continue;
            }

            if(!snapshot.Units.TryGetValue(key, out var vendor)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.UnknownKey,
                    Detail = "key is unknown to the vendor project.",
                });
                continue;
            }

            if(request.Source.TryGetValue(key, out var governed) &&
                governed is not null &&
                !string.Equals(vendor.Source, governed, StringComparison.Ordinal)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.StaleSource,
                    Detail = "vendor source differs from the request source.",
                });
                continue;
            }

            if(vendor.IsPlural) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.UnsupportedKey,
                    Detail = "plural-form units are outside the flat catalog subset.",
                });
                continue;
            }

            if(string.Equals(vendor.Target, proposed, StringComparison.Ordinal)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.Unchanged,
                    Detail = "vendor translation already matches.",
                });
                continue;
            }

            if(request.Format == TmsCatalogFormat.Xliff &&
                (!request.Source.TryGetValue(key, out var sourceTemplate) || sourceTemplate is null)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.UnsupportedKey,
                    Detail = "XLIFF upload needs the source template; supply it in request.Source.",
                });
                continue;
            }

            entries.Add(new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.Updated,
                Detail = request.DryRun ? "translation would be uploaded." : "translation uploaded.",
            });
            updatedKeys.Add(key);
        }

        string? uploadedFileName = null;
        if(!request.DryRun && updatedKeys.Count > 0) {
            uploadedFileName = await UploadAsync(request, updatedKeys, token);
        }

        return new TmsPushReport {
            Culture = request.Culture,
            DryRun = request.DryRun,
            Entries = entries,
            UpdatedCount = updatedKeys.Count,
            ConflictCount = entries.Count(static entry =>
                entry.Status is CatalogKeyStatus.UnknownKey or CatalogKeyStatus.StaleSource),
            UploadedFileName = uploadedFileName,
        };
    }

    /// <summary>
    /// Uploads the translated file for the updated keys in one vendor call with
    /// <c>method=translate</c>.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="updatedKeys">The updated keys in ordinal order.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The uploaded file name.</returns>
    private async Task<string> UploadAsync(TmsPushRequest request, List<string> updatedKeys, CancellationToken cancellationToken) {
        var fileName = request.Format == TmsCatalogFormat.Po
            ? $"messages.{request.Culture.Name}.po"
            : $"messages.{request.Culture.Name}.xliff";
        var content = request.Format == TmsCatalogFormat.Po
            ? BuildPoFile(request, updatedKeys)
            : BuildXliffFile(request, updatedKeys);
        var bytes = Encoding.UTF8.GetBytes(content);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        form.Add(new StringContent("translate"), "method");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, TranslationFileUrl(request.Culture.Name)) {
            Content = form,
        };
        using var uploadResponse = await SendAsync(uploadRequest, cancellationToken);
        await ReadJsonAsync(uploadResponse, WeblateJsonContext.Default.WeblateUploadResponse, cancellationToken);

        return fileName;
    }

    /// <summary>
    /// Builds the PO upload file for the updated keys (notes travel as <c>#.</c> comments).
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="updatedKeys">The updated keys in ordinal order.</param>
    /// <returns>The PO text.</returns>
    private static string BuildPoFile(TmsPushRequest request, List<string> updatedKeys) {
        var catalog = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach(var key in updatedKeys) {
            catalog[key] = request.Target[key];
        }

        return PoCatalogInterchange.ExportCatalog(catalog, request.Culture.Name, translatorNotes: request.Notes);
    }

    /// <summary>
    /// Builds the XLIFF upload file for the updated keys (notes travel as translator notes).
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="updatedKeys">The updated keys in ordinal order.</param>
    /// <returns>The XLIFF text.</returns>
    private string BuildXliffFile(TmsPushRequest request, List<string> updatedKeys) {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal);
        var target = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach(var key in updatedKeys) {
            source[key] = request.Source[key];
            target[key] = request.Target[key];
        }

        return XliffCatalogInterchange.ExportCatalog(source, target, _sourceLanguage, request.Culture.Name, $"messages.{request.Culture.Name}", translatorNotes: request.Notes);
    }

    /// <summary>
    /// Reads one vendor snapshot: every unit of the configured project component
    /// for the language, following the <c>next</c> page cursor.
    /// </summary>
    /// <param name="languageCode">The Weblate language code.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot.</returns>
    private async Task<VendorSnapshot> GetSnapshotAsync(string languageCode, CancellationToken cancellationToken) {
        var units = new Dictionary<string, VendorUnit>(StringComparer.Ordinal);
        var page = 1;
        while(true) {
            using var request = new HttpRequestMessage(HttpMethod.Get, TranslationUnitsUrl(languageCode, page));
            using var response = await SendAsync(request, cancellationToken);
            var body = await ReadJsonAsync(response, WeblateJsonContext.Default.WeblateUnitListResponse, cancellationToken);
            foreach(var unit in body.Results) {
                var source = unit.Source.Count > 0 ? unit.Source[0] : null;
                var key = !string.IsNullOrEmpty(unit.Context) ? unit.Context : source;
                if(string.IsNullOrEmpty(key) || units.ContainsKey(key)) {
                    continue;
                }

                units[key] = new VendorUnit(
                    source,
                    unit.Target.Count > 0 && !string.IsNullOrEmpty(unit.Target[0]) ? unit.Target[0] : null,
                    unit.Source.Count > 1 || unit.Target.Count > 1,
                    !string.IsNullOrEmpty(unit.Note) ? unit.Note : null);
            }

            if(string.IsNullOrEmpty(body.Next) || body.Results.Count == 0) {
                break;
            }

            page++;
        }

        return new VendorSnapshot(units);
    }

    /// <summary>
    /// Builds the units list URL for one page.
    /// </summary>
    /// <param name="languageCode">The Weblate language code.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <returns>The URL.</returns>
    private string TranslationUnitsUrl(string languageCode, int page) =>
        $"{_baseUrl}/api/translations/{_project}/{_component}/{languageCode}/units/?page={page.ToString(CultureInfo.InvariantCulture)}&page_size={PAGE_SIZE.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Builds the translation file URL.
    /// </summary>
    /// <param name="languageCode">The Weblate language code.</param>
    /// <returns>The URL.</returns>
    private string TranslationFileUrl(string languageCode) =>
        $"{_baseUrl}/api/translations/{_project}/{_component}/{languageCode}/file/";

    /// <summary>
    /// Sends one vendor exchange with the token and maps failures.
    /// </summary>
    /// <param name="request">The request (the connector owns disposal through the caller).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response (the caller owns disposal).</returns>
    /// <exception cref="TmsConnectorException">Thrown when the exchange fails.</exception>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiToken);

        HttpResponseMessage response;
        try {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            throw new TmsConnectorException($"Weblate {request.Method} {request.RequestUri} failed to send: {exception.Message}", exception);
        }

        if(response.IsSuccessStatusCode) {
            return response;
        }

        string snippet;
        try {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            snippet = body.Length <= MAX_BODY_SNIPPET ? body : body[..MAX_BODY_SNIPPET];
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) {
            snippet = $"(unreadable body: {exception.Message})";
        }

        response.Dispose();
        throw new TmsConnectorException($"Weblate {request.Method} {request.RequestUri} failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {snippet}");
    }

    /// <summary>
    /// Reads one JSON response through the source-generated contract.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="response">The response.</param>
    /// <param name="typeInfo">The source-generated type info.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response value.</returns>
    /// <exception cref="TmsConnectorException">Thrown when the body is not the documented shape.</exception>
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken) {
        try {
            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
                ?? throw new TmsConnectorException("Weblate returned an empty JSON body.");
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) when(exception is not TmsConnectorException) {
            throw new TmsConnectorException($"Weblate returned an unreadable JSON body: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// One vendor unit keyed by catalog key.
    /// </summary>
    /// <param name="Source">The source text.</param>
    /// <param name="Target">The translated template, or null when untranslated.</param>
    /// <param name="IsPlural">Whether the unit carries plural forms.</param>
    /// <param name="Note">The translator note, or null when the vendor carries none.</param>
    private sealed record VendorUnit(string? Source, string? Target, bool IsPlural, string? Note);

    /// <summary>
    /// One vendor snapshot: units by catalog key.
    /// </summary>
    /// <param name="Units">The units by catalog key.</param>
    private sealed record VendorSnapshot(Dictionary<string, VendorUnit> Units);
}
