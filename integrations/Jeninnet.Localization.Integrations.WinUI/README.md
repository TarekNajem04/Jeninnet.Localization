# Jeninnet.Localization.Integrations.WinUI

WinUI 3 host integration for `Jeninnet.Localization`. The package owns the active culture for a
WinUI application, persists the user's choice in Windows application data, and exposes the
direction needed for right-to-left XAML layouts.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.WinUI
```

The package targets `net10.0-windows10.0.19041.0`. It depends on
`Jeninnet.Localization.Abstractions`, `Microsoft.WindowsAppSDK`, and
`Microsoft.Extensions.DependencyInjection.Abstractions`; it does not depend on `Core`, so the host
chooses its message provider and store separately.

## Registration

```csharp
using Jeninnet.Localization.Integrations.WinUI;

services.AddWinUiLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.SettingsKey = "MyApp.Culture";
});
```

`AddWinUiLocalization` registers:

- `WinUiLocalizationOptions` and its `HostCultureOptions` base;
- `IHostCultureStore`, backed by `WinUiApplicationDataHostCultureStore` unless the host registers one first;
- `HostCultureService`.

## Usage

Call `InitializeAsync` once during startup, then apply the selected culture on the UI thread:

```csharp
var culture = services.GetRequiredService<HostCultureService>();
await culture.InitializeAsync();

culture.CultureChanged += (_, change) => {
    // Re-render the page; change.NewCulture is the applied culture.
    ApplyDirection();
};

await culture.SetCultureAsync("ar");
```

For a WinUI page or window:

```csharp
FlowDirection direction = culture.ToFlowDirection();
```

The host remains responsible for applying `FlowDirection` to each XAML element or window and for
any layout-specific RTL rules.

## Store

`WinUiApplicationDataHostCultureStore` stores one culture name under a configurable key through
`Microsoft.Windows.Storage.ApplicationData.GetDefault().LocalSettings`, which works for packaged
and unpackaged WinUI applications. A host that already uses application data can register its own
`IHostCultureStore` before calling `AddWinUiLocalization`; the registration uses `TryAdd`, so the
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

The integration uses no reflection and no dynamic code. Settings keys and culture names are
ordinary strings, so the package is safe under trimming and Native AOT. WinUI and the Windows App
SDK determine whether a trimmed or AOT-published application is viable; the host must keep the XAML
resources and controls it uses.

## Related packages

- [`Jeninnet.Localization.Abstractions`](../../src/Jeninnet.Localization.Abstractions/README.md) — culture and localization contracts.
- [`Jeninnet.Localization.Core`](../../src/Jeninnet.Localization.Core/README.md) — the message pipeline.
- [`Jeninnet.Localization.Integrations.Maui`](../Jeninnet.Localization.Integrations.Maui/README.md) — the cross-platform host counterpart.

## License

MIT — see [`LICENSE`](../../LICENSE).
