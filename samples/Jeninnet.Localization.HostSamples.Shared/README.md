# Jeninnet.Localization.HostSamples.Shared

Shared content and composition for the Avalonia, MAUI, and WinUI host samples. It uses the JSON
provider, the default pipeline, and the same English/Arabic messages in every host so the only
difference is the platform integration.

## Layout

- `HostSampleKeys` — stable localization keys.
- `HostSampleCultures` — `en` (default) and `ar`.
- `HostSampleText` — resolves a key for the current UI culture.
- `HostSampleLocalization` — registers the JSON provider, localization pipeline, and text resolver.
- `Localization/en.json`, `Localization/ar.json` — flat message tables copied to the host output.

## Usage

```csharp
services.AddHostSampleLocalization();
services.AddAvaloniaLocalization(); // or AddMauiLocalization / AddWinUiLocalization
```

This project is sample-only and is not packaged.

## License

MIT — see [`LICENSE`](../../LICENSE).
