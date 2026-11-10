//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Crowdin;

/// <summary>
/// The Crowdin <see cref="ITmsConnector"/>: pulls translated snapshots through the
/// Crowdin REST API (strings plus language translations joined on the string id)
/// and pushes translated catalogs as files (storage upload plus translations
/// upload) with dry-run previews and <see cref="CatalogKeyStatus"/> conflict
/// reporting.
/// </summary>
/// <remarks>
/// <para>
/// Mapping (file-based Crowdin projects): the catalog key is the Crowdin string
/// <c>identifier</c> when present, else the source <c>text</c>; the template is the
/// translation <c>content</c> (null when the vendor has no translation yet); the
/// translator note is the string <c>context</c>. Plural-form translation rows are
/// outside the flat catalog subset and are skipped on pull (documented boundary).
/// </para>
/// <para>
/// Auth is a personal access token in the <c>Authorization: Bearer</c> header. The
/// token is never logged and never appears in exception messages. Push uploads the
/// translated file (PO <c>#.</c> comments or XLIFF translator notes carry
/// <see cref="TmsPushRequest.Notes"/>) in two vendor calls: file bytes to storage,
/// then the translations upload against the configured project file.
/// </para>
/// <para>
/// Lifecycle: the options values are captured as an immutable construction snapshot
/// and the connector never re-reads the options object. One attempt per HTTP
/// exchange (no retries); a <see cref="CrowdinConnectorOptions.Timeout"/> breach
/// surfaces as <see cref="OperationCanceledException"/>. The connector never
/// writes catalog files or store entries; applying an approved push stays the
/// caller's governed composition.
/// </para>
/// </remarks>
public sealed class CrowdinConnector : ITmsConnector {
    private const int PAGE_LIMIT = 500;
    private const int MAX_BODY_SNIPPET = 500;

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiToken;
    private readonly long _projectId;
    private readonly long _fileId;
    private readonly string _sourceLanguage;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrowdinConnector"/> class.
    /// </summary>
    /// <param name="options">The Crowdin connector options.</param>
    /// <param name="httpClient">The HTTP client (from <c>IHttpClientFactory</c> in production, a fake handler in tests).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the options are incomplete.</exception>
    public CrowdinConnector(IOptions<CrowdinConnectorOptions> options, HttpClient httpClient) {
        var value = options?.Value ?? throw new ArgumentNullException(nameof(options));
        value.Validate();
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _baseUrl = value.Endpoint.TrimEnd('/');
        _apiToken = value.ApiToken;
        _projectId = value.ProjectId;
        _fileId = value.FileId;
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
        foreach(var pair in snapshot.Strings) {
            if(snapshot.PluralStringIds.Contains(pair.Value.StringId)) {
                continue;
            }

            templates[pair.Key] = snapshot.Translations.TryGetValue(pair.Value.StringId, out var content) ? content : null;
            if(!string.IsNullOrEmpty(pair.Value.Context)) {
                notes[pair.Key] = pair.Value.Context;
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

        if(!request.DryRun && _fileId <= 0) {
            throw new InvalidOperationException("Crowdin FileId must be a positive project file id for a live push.");
        }

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

            if(!snapshot.Strings.TryGetValue(key, out var vendor)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.UnknownKey,
                    Detail = "key is unknown to the vendor project.",
                });
                continue;
            }

            if(request.Source.TryGetValue(key, out var governed) &&
                governed is not null &&
                !string.Equals(vendor.SourceText, governed, StringComparison.Ordinal)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.StaleSource,
                    Detail = "vendor source differs from the request source.",
                });
                continue;
            }

            if(snapshot.PluralStringIds.Contains(vendor.StringId)) {
                entries.Add(new TmsPushEntry {
                    Key = key,
                    Status = CatalogKeyStatus.UnsupportedKey,
                    Detail = "plural-form strings are outside the flat catalog subset.",
                });
                continue;
            }

            snapshot.Translations.TryGetValue(vendor.StringId, out var current);
            if(string.Equals(current, proposed, StringComparison.Ordinal)) {
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
    /// Uploads the translated file for the updated keys: file bytes to storage,
    /// then the translations upload against the configured project file.
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

        long storageId;
        using(var form = new MultipartFormDataContent()) {
            form.Add(new ByteArrayContent(bytes), "file", fileName);
            using var storageRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/api/v2/storages");
            storageRequest.Content = form;
            storageRequest.Headers.TryAddWithoutValidation("Crowdin-API-FileName", fileName);
            using var storageResponse = await SendAsync(storageRequest, cancellationToken);
            var storage = await ReadJsonAsync(storageResponse, CrowdinJsonContext.Default.CrowdinStorageResponse, cancellationToken);
            storageId = storage.Data.Id;
        }

        var body = new CrowdinUploadTranslationsRequest { StorageId = storageId, FileId = _fileId };
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/translations/{request.Culture.Name}") {
            Content = JsonContent.Create(body, CrowdinJsonContext.Default.CrowdinUploadTranslationsRequest),
        };
        using var uploadResponse = await SendAsync(uploadRequest, cancellationToken);
        await ReadJsonAsync(uploadResponse, CrowdinJsonContext.Default.CrowdinUploadTranslationsResponse, cancellationToken);

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

        return XliffCatalogInterchange.ExportCatalog(source, target, _sourceLanguage, request.Culture.Name, _fileId.ToString(CultureInfo.InvariantCulture), translatorNotes: request.Notes);
    }

    /// <summary>
    /// Reads one vendor snapshot: all strings plus all translations for the language.
    /// </summary>
    /// <param name="languageId">The Crowdin language id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot.</returns>
    private async Task<VendorSnapshot> GetSnapshotAsync(string languageId, CancellationToken cancellationToken) {
        var strings = new Dictionary<string, VendorString>(StringComparer.Ordinal);
        var offset = 0;
        while(true) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/strings?limit={PAGE_LIMIT.ToString(CultureInfo.InvariantCulture)}&offset={offset.ToString(CultureInfo.InvariantCulture)}");
            using var response = await SendAsync(request, cancellationToken);
            var page = await ReadJsonAsync(response, CrowdinJsonContext.Default.CrowdinStringListResponse, cancellationToken);
            foreach(var item in page.Data.Select(static entry => entry.Data)) {
                var key = !string.IsNullOrEmpty(item.Identifier) ? item.Identifier : item.Text;
                if(!strings.ContainsKey(key)) {
                    strings[key] = new VendorString(item.Id, item.Text, item.Context);
                }
            }

            if(page.Data.Count < PAGE_LIMIT) {
                break;
            }

            offset += PAGE_LIMIT;
        }

        var translations = new Dictionary<long, string?>();
        var pluralStringIds = new HashSet<long>();
        offset = 0;
        while(true) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/languages/{languageId}/translations?limit={PAGE_LIMIT.ToString(CultureInfo.InvariantCulture)}&offset={offset.ToString(CultureInfo.InvariantCulture)}");
            using var response = await SendAsync(request, cancellationToken);
            var page = await ReadJsonAsync(response, CrowdinJsonContext.Default.CrowdinTranslationListResponse, cancellationToken);
            foreach(var item in page.Data.Select(static entry => entry.Data)) {
                if(!string.IsNullOrEmpty(item.PluralFormName)) {
                    pluralStringIds.Add(item.StringId);
                    continue;
                }

                translations.TryAdd(item.StringId, item.Content);
            }

            if(page.Data.Count < PAGE_LIMIT) {
                break;
            }

            offset += PAGE_LIMIT;
        }

        return new VendorSnapshot(strings, translations, pluralStringIds);
    }

    /// <summary>
    /// Sends one vendor exchange with the bearer token and maps failures.
    /// </summary>
    /// <param name="request">The request (the connector owns disposal through the caller).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response (the caller owns disposal).</returns>
    /// <exception cref="TmsConnectorException">Thrown when the exchange fails.</exception>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

        HttpResponseMessage response;
        try {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            throw new TmsConnectorException($"Crowdin {request.Method} {request.RequestUri} failed to send: {exception.Message}", exception);
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
        throw new TmsConnectorException($"Crowdin {request.Method} {request.RequestUri} failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {snippet}");
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
                ?? throw new TmsConnectorException("Crowdin returned an empty JSON body.");
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) when(exception is not TmsConnectorException) {
            throw new TmsConnectorException($"Crowdin returned an unreadable JSON body: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// One vendor string keyed by catalog key.
    /// </summary>
    /// <param name="StringId">The numeric string id.</param>
    /// <param name="SourceText">The source text.</param>
    /// <param name="Context">The string context.</param>
    private sealed record VendorString(long StringId, string SourceText, string? Context);

    /// <summary>
    /// One vendor snapshot: strings by catalog key, translations by string id, and
    /// the plural-form string ids outside the flat subset.
    /// </summary>
    /// <param name="Strings">The strings by catalog key.</param>
    /// <param name="Translations">The translations by string id.</param>
    /// <param name="PluralStringIds">The plural-form string ids.</param>
    private sealed record VendorSnapshot(
        Dictionary<string, VendorString> Strings,
        Dictionary<long, string?> Translations,
        HashSet<long> PluralStringIds);
}
