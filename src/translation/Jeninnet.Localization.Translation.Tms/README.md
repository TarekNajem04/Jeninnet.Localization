# Jeninnet.Localization.Translation.Tms

Vendor-neutral translation management system (TMS) connector contract for Jeninnet.Localization: pull translated snapshots, push translated catalogs with dry-run previews, and per-key conflict reporting reusing the interchange `CatalogKeyStatus` vocabulary. Two implementations share this surface so governed workflows never fork: Crowdin (SaaS) and Weblate (self-hosted).

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.Tms --version 1.0.0-preview.1
```

## Contract

`ITmsConnector` is the whole workflow:

```csharp
// Pull the translated snapshot for one culture.
TmsPullResult pulled = await connector.PullAsync(CultureInfo.GetCultureInfo("ar"));

// Push translated catalogs with a dry-run preview and conflict reporting.
TmsPushReport report = await connector.PushAsync(new TmsPushRequest {
    Culture = CultureInfo.GetCultureInfo("ar"),
    Source = sourceTemplates,
    Target = translatedTemplates,
    Format = TmsCatalogFormat.Po,
    DryRun = true,
});
```

Pull returns the served templates plus translator notes where the vendor carries them, both keyed by catalog key. Push compares the proposed target against the vendor snapshot (unknown keys, stale sources, unchanged and updated templates) and reports per key; a dry run uploads nothing, and a live push uploads the translated file only when something changed.

Applying an approved push to the governed store stays the caller's composition (through `IResourceAdminStore`); connectors never write catalog files or store entries themselves.

## Implementations

- [Jeninnet.Localization.Translation.Tms.Crowdin](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms.Crowdin/README.md) — Crowdin REST API: string/translation pull, PO/XLIFF file upload push.
- [Jeninnet.Localization.Translation.Tms.Weblate](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/translation/Jeninnet.Localization.Translation.Tms.Weblate/README.md) — Weblate REST API: translation-unit pull, single `translate` file upload push.

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [TMS connector round trip](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/17-tms-connector-round-trip.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
