# Jeninnet.Localization.Integrations.Maui

MAUI host integration for `Jeninnet.Localization`. The package owns the active culture for a MAUI
application, persists the user's choice in MAUI preferences, and exposes the direction needed for
right-to-left layouts.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.Maui
```

The package targets `net10.0`, the runtime target shared by MAUI 10 applications. It depends on
`Jeninnet.Localization.Abstractions`, `Microsoft.Maui.Core`, and `Microsoft.Maui.Essentials`; it
does not depend on `Core`, so the host chooses its message provider and store separately.

## Registration

```csharp
using Jeninnet.Localization.Integrations.Maui;

builder.Services.AddMauiLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.PreferencesKey = "MyApp.Culture";
});
```

`AddMauiLocalization` registers:

- `MauiLocalizationOptions` and its `HostCultureOptions` base;
- `IHostCultureStore`, backed by `MauiPreferencesHostCultureStore` unless the host registers one first;
- `HostCultureService`.

## Usage

Call `InitializeAsync` once during startup, then apply the selected culture on the UI thread:

```csharp
var culture = services.GetRequiredService<HostCultureService>();
await culture.InitializeAsync();

culture.CultureChanged += (_, change) => {
    // Re-render the page; change.NewCulture is the applied culture.
    MainThread.BeginInvokeOnMainThread(() => ApplyDirection());
};

await culture.SetCultureAsync("ar");
```

For a MAUI page or layout:

```csharp
FlowDirection direction = culture.ToFlowDirection();
```

The host remains responsible for applying `FlowDirection` to each visual element or window and for
any layout-specific RTL rules.

## Store

`MauiPreferencesHostCultureStore` stores one culture name under a configurable key through
`Preferences.Default`. A host that already uses platform preferences can register its own
`IHostCultureStore` before calling `AddMauiLocalization`; the registration uses `TryAdd`, so the
host store wins.

## Behavior

- A persisted choice wins over the configured default culture.
- `InitializeAsync` ignores an unknown or unsupported persisted culture and falls back to the
  default culture.
- `SetCultureAsync` validates the culture name and the supported-culture list before writing.
- The service applies `CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, and
  `DefaultThreadCurrentUICulture`.
- Call the service on the UI thread; the package does not marshal to a dispatcher.

## AOT and trimming

The integration uses no reflection and no dynamic code. Preference keys and culture names are
ordinary strings, so the package is safe under trimming and Native AOT. MAUI and the target
platform determine whether a trimmed or AOT-published application is viable; the host must keep
the controls, handlers, and resources it uses.

## Related packages

- [`Jeninnet.Localization.Abstractions`](../../src/Jeninnet.Localization.Abstractions/README.md) — culture and localization contracts.
- [`Jeninnet.Localization.Core`](../../src/Jeninnet.Localization.Core/README.md) — the message pipeline.
- [`Jeninnet.Localization.Integrations.Avalonia`](../Jeninnet.Localization.Integrations.Avalonia/README.md) — the desktop-host counterpart.

## License

MIT — see [`LICENSE`](../../LICENSE).
