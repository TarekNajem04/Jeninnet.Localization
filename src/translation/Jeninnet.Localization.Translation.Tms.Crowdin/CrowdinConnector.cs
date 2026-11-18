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
    public Task<TmsPullResult> PullAsync(CultureInfo culture, CancellationToken cancellationToken = default)
        => TmsPullMapper.PullAsync(culture, _sourceLanguage, _timeout, async (language, token) => {
            var snapshot = await GetSnapshotAsync(language, token).ConfigureAwait(false);
            return snapshot.Strings.ToDictionary(
                static pair => pair.Key,
                pair => new TmsVendorRow(
                    pair.Value.SourceText,
                    snapshot.Translations.TryGetValue(pair.Value.StringId, out var content) ? content : null,
                    snapshot.PluralStringIds.Contains(pair.Value.StringId),
                    null,
                    string.IsNullOrEmpty(pair.Value.Context) ? null : pair.Value.Context),
                StringComparer.Ordinal);
        }, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Crowdin push: file bytes go to storage, then the translations upload against the
    /// configured project file. The shared <see cref="TmsPushFlow"/> owns budget, classification,
    /// and reporting; this method owns validation, snapshot mapping, and the two-step upload.
    /// </remarks>
    public Task<TmsPushReport> PushAsync(TmsPushRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Culture);

        if(!request.DryRun && _fileId <= 0) {
            throw new InvalidOperationException("Crowdin FileId must be a positive project file id for a live push.");
        }

        if(request.Format is not (TmsCatalogFormat.Po or TmsCatalogFormat.Xliff)) {
            throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported catalog format '{request.Format}'.");
        }

        return TmsPushFlow.ExecuteAsync(request, _timeout, MapSnapshotAsync, UploadAsync, cancellationToken);
    }

    /// <summary>
    /// Maps one Crowdin snapshot to the shared vendor-row lookup.
    /// </summary>
    /// <param name="language">The Crowdin language id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key lookup.</returns>
    private async Task<Func<string, TmsVendorRow?>> MapSnapshotAsync(string language, CancellationToken cancellationToken) {
        var snapshot = await GetSnapshotAsync(language, cancellationToken).ConfigureAwait(false);
        return key => {
            if(!snapshot.Strings.TryGetValue(key, out var vendor)) {
                return null;
            }

            return new TmsVendorRow(
                vendor.SourceText,
                snapshot.Translations.TryGetValue(vendor.StringId, out var current) ? current : null,
                snapshot.PluralStringIds.Contains(vendor.StringId),
                "plural-form strings are outside the flat catalog subset.",
                null);
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
            ? TmsCatalogFiles.BuildPoFile(request, updatedKeys)
            : TmsCatalogFiles.BuildXliffFile(request, updatedKeys, _sourceLanguage, _fileId.ToString(CultureInfo.InvariantCulture));
        var bytes = Encoding.UTF8.GetBytes(content);

        long storageId;
        using(var form = new MultipartFormDataContent()) {
            form.Add(new ByteArrayContent(bytes), "file", fileName);
            using var storageRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/api/v2/storages");
            storageRequest.Content = form;
            storageRequest.Headers.TryAddWithoutValidation("Crowdin-API-FileName", fileName);
            using var storageResponse = await TmsHttp.SendAsync(_httpClient, storageRequest, "Bearer", _apiToken, "Crowdin", MAX_BODY_SNIPPET, cancellationToken);
            var storage = await TmsHttp.ReadJsonAsync(storageResponse, CrowdinJsonContext.Default.CrowdinStorageResponse, "Crowdin", cancellationToken);
            storageId = storage.Data.Id;
        }

        var body = new CrowdinUploadTranslationsRequest { StorageId = storageId, FileId = _fileId };
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/translations/{request.Culture.Name}") {
            Content = JsonContent.Create(body, CrowdinJsonContext.Default.CrowdinUploadTranslationsRequest),
        };
        using var uploadResponse = await TmsHttp.SendAsync(_httpClient, uploadRequest, "Bearer", _apiToken, "Crowdin", MAX_BODY_SNIPPET, cancellationToken);
        await TmsHttp.ReadJsonAsync(uploadResponse, CrowdinJsonContext.Default.CrowdinUploadTranslationsResponse, "Crowdin", cancellationToken);

        return fileName;
    }

    /// <summary>
    /// Reads one vendor snapshot: all strings plus all translations for the language.
    /// </summary>
    /// <param name="languageId">The Crowdin language id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot.</returns>
    private async Task<VendorSnapshot> GetSnapshotAsync(string languageId, CancellationToken cancellationToken) {
        var strings = await ReadAllStringsAsync(cancellationToken);
        var (translations, pluralStringIds) = await ReadAllTranslationsAsync(languageId, cancellationToken);
        return new VendorSnapshot(strings, translations, pluralStringIds);
    }

    /// <summary>
    /// Reads all project strings, following the offset pages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The strings keyed by catalog key.</returns>
    private async Task<Dictionary<string, VendorString>> ReadAllStringsAsync(CancellationToken cancellationToken) {
        var strings = new Dictionary<string, VendorString>(StringComparer.Ordinal);
        var offset = 0;
        while(true) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/strings?limit={PAGE_LIMIT.ToString(CultureInfo.InvariantCulture)}&offset={offset.ToString(CultureInfo.InvariantCulture)}");
            using var response = await TmsHttp.SendAsync(_httpClient, request, "Bearer", _apiToken, "Crowdin", MAX_BODY_SNIPPET, cancellationToken);
            var page = await TmsHttp.ReadJsonAsync(response, CrowdinJsonContext.Default.CrowdinStringListResponse, "Crowdin", cancellationToken);
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

        return strings;
    }

    /// <summary>
    /// Reads all translations for the language, following the offset pages.
    /// </summary>
    /// <param name="languageId">The Crowdin language id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The translations by string id and the plural-form string ids.</returns>
    private async Task<(Dictionary<long, string?> Translations, HashSet<long> PluralStringIds)> ReadAllTranslationsAsync(string languageId, CancellationToken cancellationToken) {
        var translations = new Dictionary<long, string?>();
        var pluralStringIds = new HashSet<long>();
        var offset = 0;
        while(true) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + $"/api/v2/projects/{_projectId.ToString(CultureInfo.InvariantCulture)}/languages/{languageId}/translations?limit={PAGE_LIMIT.ToString(CultureInfo.InvariantCulture)}&offset={offset.ToString(CultureInfo.InvariantCulture)}");
            using var response = await TmsHttp.SendAsync(_httpClient, request, "Bearer", _apiToken, "Crowdin", MAX_BODY_SNIPPET, cancellationToken);
            var page = await TmsHttp.ReadJsonAsync(response, CrowdinJsonContext.Default.CrowdinTranslationListResponse, "Crowdin", cancellationToken);
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

        return (translations, pluralStringIds);
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
