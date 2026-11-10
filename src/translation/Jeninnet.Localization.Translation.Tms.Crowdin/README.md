# Jeninnet.Localization.Translation.Tms.Crowdin

Crowdin TMS connector for Jeninnet.Localization: pulls translated snapshots through the Crowdin REST API (project strings plus language translations joined on the string id) and pushes translated catalogs as files (storage upload plus translations upload) with dry-run previews and `CatalogKeyStatus` conflict reporting.

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.Tms.Crowdin --version 1.0.0-preview.1
```

## Registration

```csharp
services.AddCrowdinTmsConnector(options => {
    options.ApiToken = "<Crowdin personal access token>";
    options.ProjectId = 123456;
    options.FileId = 789;
});
```

Enterprise organizations set their own host:

```csharp
services.AddCrowdinTmsConnector(options => {
    options.ApiToken = "<Crowdin personal access token>";
    options.Endpoint = "https://my-org.crowdin.com";
    options.ProjectId = 123456;
    options.FileId = 789;
});
```

## Configuration

`CrowdinConnectorOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `ApiToken` | `string` | `""` | Required. Sent as the `Authorization: Bearer` header; never logged. |
| `Endpoint` | `string` | `https://api.crowdin.com` | Must be an absolute http/https URI. |
| `ProjectId` | `long` | `0` | Required. The Crowdin project id to pull from and push to. |
| `FileId` | `long` | `0` | The project file id translated uploads attach to. Pull never needs it; live push requires it. |
| `SourceLanguage` | `string` | `"en"` | The vendor source language; reported on pull results and sent as the XLIFF source language. |
| `Timeout` | `TimeSpan` | 30 seconds | Total budget per pull or push (including the upload). One attempt per HTTP exchange. |

Mutating the options object after registration does not affect the connector.

## Usage

```csharp
// Pull Arabic: templates plus translator notes keyed by catalog key.
TmsPullResult pulled = await connector.PullAsync(CultureInfo.GetCultureInfo("ar"));

// Preview a push without uploading anything.
TmsPushReport preview = await connector.PushAsync(new TmsPushRequest {
    Culture = CultureInfo.GetCultureInfo("ar"),
    Source = sourceTemplates,
    Target = translatedTemplates,
    Notes = translatorNotes,
    Format = TmsCatalogFormat.Po,
    DryRun = true,
});

// Live push: uploads messages.ar.po only for changed keys.
TmsPushReport report = await connector.PushAsync(new TmsPushRequest {
    Culture = CultureInfo.GetCultureInfo("ar"),
    Source = sourceTemplates,
    Target = translatedTemplates,
    Notes = translatorNotes,
    Format = TmsCatalogFormat.Po,
});
```

## Behavior and limits

- **Mapping.** The catalog key is the Crowdin string `identifier` when present, else the source `text`. The template is the translation `content` (null when untranslated). The translator note is the string `context`. Plural-form translation rows are outside the flat catalog subset and are skipped on pull.
- **Conflict policy.** The push comparison reuses the interchange vocabulary: `Skipped` (empty proposed value), `UnknownKey` (absent from the project), `StaleSource` (vendor source differs from the request source), `Unchanged`, `Updated`. XLIFF uploads additionally need the source template in the request and report `UnsupportedKey` without it.
- **Upload shape.** PO uploads carry notes as `#.` comments; XLIFF uploads carry them as translator notes. A dry run and a no-change push upload nothing.
- **Calls.** One attempt per HTTP exchange with a per-operation timeout; vendor failures surface as `TmsConnectorException` with the status code and a body snippet (never the token). Pagination follows the vendor cursor (500 rows per page).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. Request/response payloads serialize through a source-generated `JsonSerializerContext`. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Translation.Tms](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms/README.md) — the vendor-neutral connector contract
- [Jeninnet.Localization.Translation.DeepL](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.DeepL/README.md) — runtime machine translation over DeepL API v2

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [TMS connector round trip](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/17-tms-connector-round-trip.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
