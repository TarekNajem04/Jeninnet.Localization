# Jeninnet.Localization.Reference.WinUI

WinUI 3 host sample for `Jeninnet.Localization.Integrations.WinUI`. It registers the shared sample
content, switches between English and Arabic, persists the choice in Windows application data, and
applies the matching flow direction to the root layout.

## Run

```powershell
dotnet run --project samples/Jeninnet.Localization.Reference.WinUI
```

The sample is unpackaged and targets `net10.0-windows10.0.19041.0`. It requires the Windows App SDK
runtime or a self-contained deployment.

## What it demonstrates

- `AddHostSampleLocalization` for the JSON message provider and default pipeline.
- `AddWinUiLocalization` for host culture ownership and application-data persistence.
- `HostCultureService.InitializeAsync` and `SetCultureAsync` on the UI thread.
- `ToFlowDirection()` for right-to-left XAML layouts.
- Runtime refresh of every visible label after a culture change.

## License

MIT — see [`LICENSE`](../../LICENSE).
