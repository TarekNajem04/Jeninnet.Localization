//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_TMS_CROWDIN
namespace Jeninnet.Localization.Translation.Tms.Crowdin;
#elif JENINNET_TMS_WEBLATE
namespace Jeninnet.Localization.Translation.Tms.Weblate;
#else
#error Compile TmsConnector sources with JENINNET_TMS_CROWDIN or JENINNET_TMS_WEBLATE defined.
#endif

/// <summary>
/// One normalized vendor row for push classification: the vendor's source and current
/// translation plus the flat-subset verdict. Each connector maps its own snapshot
/// DTOs into this shape; the classification flow below is vendor-neutral.
/// </summary>
/// <param name="Source">The vendor's source text, or <see langword="null"/> when unavailable.</param>
/// <param name="Current">The vendor's current translation, or <see langword="null"/> when untranslated.</param>
/// <param name="Unsupported">Whether the row is outside the flat catalog subset.</param>
/// <param name="UnsupportedDetail">Why the row is unsupported, or <see langword="null"/> when supported.</param>
/// <param name="Note">The translator note, or <see langword="null"/> when absent.</param>
internal readonly record struct TmsVendorRow(string? Source, string? Current, bool Unsupported, string? UnsupportedDetail, string? Note = null);

/// <summary>
/// Shared push classification and reporting for TMS connectors: per-key conflict vocabulary,
/// dry-run previews, and report assembly. Upload mechanics stay vendor-specific.
/// </summary>
internal static class TmsPushPlanner {
    /// <summary>
    /// Classifies every requested target key against the vendor snapshot and returns the
    /// per-key entries plus the keys selected for upload, in ordinal order.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="lookup">Resolves a catalog key to its normalized vendor row, or <see langword="null"/> when the vendor project has no such key.</param>
    /// <returns>The entries and the updated keys.</returns>
    internal static (List<TmsPushEntry> Entries, List<string> UpdatedKeys) Classify(TmsPushRequest request, Func<string, TmsVendorRow?> lookup) {
        var entries = new List<TmsPushEntry>(request.Target.Count);
        var updatedKeys = new List<string>();
        foreach(var key in request.Target.Keys.Order(StringComparer.Ordinal)) {
            var entry = ClassifySingleKey(request, key, lookup, out var updated);
            entries.Add(entry);
            if(updated) {
                updatedKeys.Add(key);
            }
        }

        return (entries, updatedKeys);
    }

    /// <summary>
    /// Classifies one requested target key against the vendor snapshot.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="lookup">Resolves a catalog key to its normalized vendor row.</param>
    /// <param name="updated">Whether the key was selected for upload.</param>
    /// <returns>The per-key entry.</returns>
    private static TmsPushEntry ClassifySingleKey(TmsPushRequest request, string key, Func<string, TmsVendorRow?> lookup, out bool updated) {
        updated = false;
        var proposed = request.Target[key];
        if(string.IsNullOrEmpty(proposed)) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.Skipped,
                Detail = "empty target value; existing vendor translations are never cleared.",
            };
        }

        var vendor = lookup(key);
        if(vendor is null) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.UnknownKey,
                Detail = "key is unknown to the vendor project.",
            };
        }

        if(IsStaleSource(request, key, vendor.Value)) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.StaleSource,
                Detail = "vendor source differs from the request source.",
            };
        }

        if(vendor.Value.Unsupported) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.UnsupportedKey,
                Detail = vendor.Value.UnsupportedDetail ?? "key is outside the flat catalog subset.",
            };
        }

        if(string.Equals(vendor.Value.Current, proposed, StringComparison.Ordinal)) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.Unchanged,
                Detail = "vendor translation already matches.",
            };
        }

        if(IsMissingXliffSource(request, key)) {
            return new TmsPushEntry {
                Key = key,
                Status = CatalogKeyStatus.UnsupportedKey,
                Detail = "XLIFF upload needs the source template; supply it in request.Source.",
            };
        }

        updated = true;
        return new TmsPushEntry {
            Key = key,
            Status = CatalogKeyStatus.Updated,
            Detail = request.DryRun ? "translation would be uploaded." : "translation uploaded.",
        };
    }

    /// <summary>
    /// Determines whether the vendor source differs from the governed request source.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="vendor">The normalized vendor row.</param>
    /// <returns>True when stale.</returns>
    private static bool IsStaleSource(TmsPushRequest request, string key, TmsVendorRow vendor)
        => request.Source.TryGetValue(key, out var governed) &&
            governed is not null &&
            !string.Equals(vendor.Source, governed, StringComparison.Ordinal);

    /// <summary>
    /// Determines whether an XLIFF upload lacks its required source template.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="key">The catalog key.</param>
    /// <returns>True when missing.</returns>
    private static bool IsMissingXliffSource(TmsPushRequest request, string key)
        => request.Format == TmsCatalogFormat.Xliff &&
            (!request.Source.TryGetValue(key, out var sourceTemplate) || sourceTemplate is null);

    /// <summary>
    /// Assembles the push report.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="entries">The classified entries.</param>
    /// <param name="updatedKeys">The updated keys.</param>
    /// <param name="uploadedFileName">The uploaded file name, or <see langword="null"/> on dry runs and empty updates.</param>
    /// <returns>The push report.</returns>
    internal static TmsPushReport Report(TmsPushRequest request, List<TmsPushEntry> entries, List<string> updatedKeys, string? uploadedFileName)
        => new() {
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
/// Shared pull mapping for TMS connectors: skips rows outside the flat catalog subset,
/// serves untranslated rows as missing templates, and carries non-empty translator notes.
/// </summary>
internal static class TmsPullMapper {
    /// <summary>
    /// Pulls one translated snapshot through the vendor snapshot reader.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <param name="sourceLanguage">The vendor source language carried by the result.</param>
    /// <param name="timeout">The per-operation budget.</param>
    /// <param name="getSnapshot">Reads the vendor snapshot keyed by catalog key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pull result.</returns>
    internal static async Task<TmsPullResult> PullAsync(
        CultureInfo culture,
        string sourceLanguage,
        TimeSpan timeout,
        Func<string, CancellationToken, Task<IReadOnlyDictionary<string, TmsVendorRow>>> getSnapshot,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(culture);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        var rows = await getSnapshot(culture.Name, budget.Token).ConfigureAwait(false);

        var templates = new Dictionary<string, string?>(StringComparer.Ordinal);
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var pair in rows) {
            if(pair.Value.Unsupported) {
                continue;
            }

            templates[pair.Key] = pair.Value.Current;
            if(!string.IsNullOrEmpty(pair.Value.Note)) {
                notes[pair.Key] = pair.Value.Note;
            }
        }

        return new TmsPullResult {
            Culture = culture,
            SourceLanguage = sourceLanguage,
            Templates = templates,
            Notes = notes,
        };
    }
}

/// <summary>
/// Shared push orchestration for TMS connectors: per-operation budget, snapshot lookup,
/// classification, conditional upload, and reporting. Validation and vendor mapping stay
/// in the connector; everything else is vendor-neutral.
/// </summary>
internal static class TmsPushFlow {
    /// <summary>
    /// Executes one push request through classification and conditional upload.
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="timeout">The per-operation budget.</param>
    /// <param name="getLookup">Resolves the vendor snapshot to a key lookup.</param>
    /// <param name="uploadAsync">Uploads the updated keys and returns the file name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The push report.</returns>
    internal static async Task<TmsPushReport> ExecuteAsync(
        TmsPushRequest request,
        TimeSpan timeout,
        Func<string, CancellationToken, Task<Func<string, TmsVendorRow?>>> getLookup,
        Func<TmsPushRequest, List<string>, CancellationToken, Task<string>> uploadAsync,
        CancellationToken cancellationToken) {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        var token = budget.Token;

        var lookup = await getLookup(request.Culture.Name, token).ConfigureAwait(false);
        var (entries, updatedKeys) = TmsPushPlanner.Classify(request, lookup);

        string? uploadedFileName = null;
        if(!request.DryRun && updatedKeys.Count > 0) {
            uploadedFileName = await uploadAsync(request, updatedKeys, token).ConfigureAwait(false);
        }

        return TmsPushPlanner.Report(request, entries, updatedKeys, uploadedFileName);
    }
}

/// <summary>
/// Shared catalog file builders for TMS uploads: PO <c>#.</c> comments and XLIFF translator
/// notes carry the request notes.
/// </summary>
internal static class TmsCatalogFiles {
    /// <summary>
    /// Builds the PO upload file for the updated keys (notes travel as <c>#.</c> comments).
    /// </summary>
    /// <param name="request">The push request.</param>
    /// <param name="updatedKeys">The updated keys in ordinal order.</param>
    /// <returns>The PO text.</returns>
    internal static string BuildPoFile(TmsPushRequest request, List<string> updatedKeys) {
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
    /// <param name="sourceLanguage">The vendor source language.</param>
    /// <param name="unitId">The vendor unit id carried by the XLIFF file.</param>
    /// <returns>The XLIFF text.</returns>
    internal static string BuildXliffFile(TmsPushRequest request, List<string> updatedKeys, string sourceLanguage, string unitId) {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal);
        var target = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach(var key in updatedKeys) {
            source[key] = request.Source[key];
            target[key] = request.Target[key];
        }

        return XliffCatalogInterchange.ExportCatalog(source, target, sourceLanguage, request.Culture.Name, unitId, translatorNotes: request.Notes);
    }
}

/// <summary>
/// Shared HTTP plumbing for TMS connectors: authenticated send with truncated failure bodies
/// and source-generated JSON parsing. The token never appears in exception messages.
/// </summary>
internal static class TmsHttp {
    /// <summary>
    /// Sends one vendor HTTP exchange.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="request">The request message.</param>
    /// <param name="authScheme">The authorization scheme (<c>Bearer</c> or <c>Token</c>).</param>
    /// <param name="apiToken">The API token, never logged.</param>
    /// <param name="vendorDisplayName">The vendor display name used in failure messages.</param>
    /// <param name="maxBodySnippet">The maximum failure-body snippet length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The received response.</returns>
    /// <exception cref="TmsConnectorException">Thrown when the exchange fails to send or the vendor rejects it.</exception>
    internal static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        string authScheme,
        string apiToken,
        string vendorDisplayName,
        int maxBodySnippet,
        CancellationToken cancellationToken) {
        request.Headers.Authorization = new AuthenticationHeaderValue(authScheme, apiToken);

        HttpResponseMessage response;
        try {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            throw new TmsConnectorException($"{vendorDisplayName} {request.Method} {request.RequestUri} failed to send: {exception.Message}", exception);
        }

        if(response.IsSuccessStatusCode) {
            return response;
        }

        string snippet;
        try {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            snippet = body.Length <= maxBodySnippet ? body : body[..maxBodySnippet];
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) {
            snippet = $"(unreadable body: {exception.Message})";
        }

        response.Dispose();
        throw new TmsConnectorException($"{vendorDisplayName} {request.Method} {request.RequestUri} failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {snippet}");
    }

    /// <summary>
    /// Reads one JSON response through the source-generated contract.
    /// </summary>
    /// <typeparam name="T">The response type.</typeparam>
    /// <param name="response">The response.</param>
    /// <param name="typeInfo">The source-generated type info.</param>
    /// <param name="vendorDisplayName">The vendor display name used in failure messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response value.</returns>
    /// <exception cref="TmsConnectorException">Thrown when the body is not the documented shape.</exception>
    internal static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, string vendorDisplayName, CancellationToken cancellationToken) {
        try {
            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false)
                ?? throw new TmsConnectorException($"{vendorDisplayName} returned an empty JSON body.");
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) when(exception is not TmsConnectorException) {
            throw new TmsConnectorException($"{vendorDisplayName} returned an unreadable JSON body: {exception.Message}", exception);
        }
    }
}
