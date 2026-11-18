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
    public Task<TmsPullResult> PullAsync(CultureInfo culture, CancellationToken cancellationToken = default)
        => TmsPullMapper.PullAsync(culture, _sourceLanguage, _timeout, async (language, token) => {
            var snapshot = await GetSnapshotAsync(language, token).ConfigureAwait(false);
            return snapshot.Units.ToDictionary(
                static pair => pair.Key,
                pair => new TmsVendorRow(pair.Value.Source, pair.Value.Target, pair.Value.IsPlural, null, pair.Value.Note),
                StringComparer.Ordinal);
        }, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Weblate push: one <c>translate</c> upload against the configured project component.
    /// The shared <see cref="TmsPushFlow"/> owns budget, classification, and reporting; this
    /// method owns validation, snapshot mapping, and the single-call upload.
    /// </remarks>
    public Task<TmsPushReport> PushAsync(TmsPushRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Culture);

        if(request.Format is not (TmsCatalogFormat.Po or TmsCatalogFormat.Xliff)) {
            throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported catalog format '{request.Format}'.");
        }

        return TmsPushFlow.ExecuteAsync(request, _timeout, MapSnapshotAsync, UploadAsync, cancellationToken);
    }

    /// <summary>
    /// Maps one Weblate snapshot to the shared vendor-row lookup.
    /// </summary>
    /// <param name="language">The Weblate language code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key lookup.</returns>
    private async Task<Func<string, TmsVendorRow?>> MapSnapshotAsync(string language, CancellationToken cancellationToken) {
        var snapshot = await GetSnapshotAsync(language, cancellationToken).ConfigureAwait(false);
        return key => {
            if(!snapshot.Units.TryGetValue(key, out var vendor)) {
                return null;
            }

            return new TmsVendorRow(vendor.Source, vendor.Target, vendor.IsPlural, "plural-form units are outside the flat catalog subset.", vendor.Note);
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
            ? TmsCatalogFiles.BuildPoFile(request, updatedKeys)
            : TmsCatalogFiles.BuildXliffFile(request, updatedKeys, _sourceLanguage, $"messages.{request.Culture.Name}");
        var bytes = Encoding.UTF8.GetBytes(content);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        form.Add(new StringContent("translate"), "method");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, TranslationFileUrl(request.Culture.Name)) {
            Content = form,
        };
        using var uploadResponse = await TmsHttp.SendAsync(_httpClient, uploadRequest, "Token", _apiToken, "Weblate", MAX_BODY_SNIPPET, cancellationToken);
        await TmsHttp.ReadJsonAsync(uploadResponse, WeblateJsonContext.Default.WeblateUploadResponse, "Weblate", cancellationToken);

        return fileName;
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
            using var response = await TmsHttp.SendAsync(_httpClient, request, "Token", _apiToken, "Weblate", MAX_BODY_SNIPPET, cancellationToken);
            var body = await TmsHttp.ReadJsonAsync(response, WeblateJsonContext.Default.WeblateUnitListResponse, "Weblate", cancellationToken);
            AddPageUnits(body, units);

            if(string.IsNullOrEmpty(body.Next) || body.Results.Count == 0) {
                break;
            }

            page++;
        }

        return new VendorSnapshot(units);
    }

    /// <summary>
    /// Adds one page of translation units keyed by catalog key, keeping the first
    /// occurrence of each key.
    /// </summary>
    /// <param name="body">The unit list page.</param>
    /// <param name="units">The units by catalog key.</param>
    private static void AddPageUnits(WeblateUnitListResponse body, Dictionary<string, VendorUnit> units) {
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
