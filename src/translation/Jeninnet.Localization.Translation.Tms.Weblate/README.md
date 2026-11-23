# Jeninnet.Localization.Translation.Tms.Weblate

Weblate TMS connector for Jeninnet.Localization: pulls translated snapshots through the Weblate REST API (translation units of one project component, keyed by unit context or source text) and pushes translated catalogs as files (single `translate` upload) with dry-run previews and `CatalogKeyStatus` conflict reporting.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.Tms.Weblate --version 1.0.0-preview.2
```

## Registration

```csharp
services.AddWeblateTmsConnector(options => {
    options.ApiToken = "<Weblate API token>";
    options.Endpoint = "https://hosted.weblate.org";
    options.Project = "my-project";
    options.Component = "messages";
});
```

Self-hosted servers point at their own origin instead:

```csharp
services.AddWeblateTmsConnector(options => {
    options.ApiToken = "<Weblate API token>";
    options.Endpoint = "https://weblate.example.com";
    options.Project = "my-project";
    options.Component = "messages";
});
```

## Configuration

`WeblateConnectorOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `ApiToken` | `string` | `""` | Required. Sent as the `Authorization: Token` header; never logged. Personal (`wlu_`) or project-scoped (`wlp_`) tokens both work. |
| `Endpoint` | `string` | `""` | Required. The Weblate server origin (hosted service or self-hosted). Must be an absolute http/https URI. |
| `Project` | `string` | `""` | Required. The Weblate project slug to pull from and push to. |
| `Component` | `string` | `""` | Required. The Weblate component slug to pull from and push to. |
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

- **Mapping.** The catalog key is the unit `context` when present, else the first source form. The template is the first target form (null when untranslated). The translator note is the unit `note`. Units with more than one source or target form (plurals) are outside the flat catalog subset and are skipped on pull.
- **Conflict policy.** The push comparison reuses the interchange vocabulary: `Skipped` (empty proposed value), `UnknownKey` (absent from the component), `StaleSource` (vendor source differs from the request source), `Unchanged`, `Updated`. XLIFF uploads additionally need the source template in the request and report `UnsupportedKey` without it.
- **Upload shape.** PO uploads carry notes as `#.` comments; XLIFF uploads carry them as translator notes. Uploads run with `method=translate` so only the compared keys change. A dry run and a no-change push upload nothing.
- **Calls.** One attempt per HTTP exchange with a per-operation `Timeout` budget (default 30 seconds); a breach surfaces as `OperationCanceledException`. Vendor failures surface as `TmsConnectorException` with the status code and a body snippet (never the token). Pagination follows the `next` page cursor (1000 rows per page).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. Request/response payloads serialize through a source-generated `JsonSerializerContext`. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Translation.Tms](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms/README.md) — the vendor-neutral connector contract
- [Jeninnet.Localization.Translation.Tms.Crowdin](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms.Crowdin/README.md) — the Crowdin connector over the same contract
- [Jeninnet.Localization.Translation.DeepL](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.DeepL/README.md) — runtime machine translation over DeepL API v2

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [TMS connector round trip](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/17-tms-connector-round-trip.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
