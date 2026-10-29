# Jeninnet.Localization.Reference.Maui

MAUI host sample for `Jeninnet.Localization.Integrations.Maui`. It registers the shared sample
content, switches between English and Arabic, persists the choice in MAUI preferences, and applies
the matching flow direction to the page.

## Run

```powershell
dotnet build samples/Jeninnet.Localization.Reference.Maui -f net10.0-windows10.0.19041.0
dotnet run --project samples/Jeninnet.Localization.Reference.Maui -f net10.0-windows10.0.19041.0
```

The sample targets Windows for local verification. The integration package itself targets
`net10.0`, so a MAUI app can add Android, iOS, or Mac Catalyst targets without changing its
registration.

## What it demonstrates

- `AddHostSampleLocalization` for the JSON message provider and default pipeline.
- `AddMauiLocalization` for host culture ownership and preferences persistence.
- `HostCultureService.InitializeAsync` and `SetCultureAsync` on the UI thread.
- `ToFlowDirection()` for right-to-left layouts.
- Runtime refresh of every visible label after a culture change.

## License

MIT — see [`LICENSE`](../../LICENSE).
