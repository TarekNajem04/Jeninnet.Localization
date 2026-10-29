# Jeninnet.Localization.Integrations.Avalonia

Avalonia host integration for `Jeninnet.Localization`. The package owns the active culture for an
Avalonia application, persists the user's choice in a small file, and exposes the direction needed
for right-to-left layouts.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.Avalonia
```

The package targets .NET 8, .NET 9, and .NET 10. It depends on `Jeninnet.Localization.Abstractions`,
Avalonia, and `Microsoft.Extensions.DependencyInjection.Abstractions`; it does not depend on `Core`,
so the host chooses its message provider and store separately.

## Registration

```csharp
using Jeninnet.Localization.Integrations.Avalonia;

services.AddAvaloniaLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.CultureStoreFilePath = "Localization/culture.txt";
});
```

`AddAvaloniaLocalization` registers:

- `AvaloniaLocalizationOptions` and its `HostCultureOptions` base;
- `IHostCultureStore`, backed by `FileHostCultureStore` unless the host registers one first;
- `HostCultureService`.

## Usage

Call `InitializeAsync` once during startup, then apply the selected culture on the UI thread:

```csharp
var culture = services.GetRequiredService<HostCultureService>();
await culture.InitializeAsync();

culture.CultureChanged += (_, change) => {
    // Re-render the view; change.NewCulture is the applied culture.
};

await culture.SetCultureAsync("ar");
```

For an Avalonia control, bind the layout direction through the helper:

```csharp
FlowDirection direction = culture.ToFlowDirection();
```

`CultureInfo.TextInfo.IsRightToLeft` is also exposed through `HostCultureService.IsRightToLeft`.
The host remains responsible for applying `FlowDirection` to individual controls or the window and
for any layout-specific RTL rules.

## Store

`FileHostCultureStore` writes one UTF-8 culture name to a text file. It creates the parent
directory and ignores a missing file. A host that already uses platform preferences can register
its own `IHostCultureStore` before calling `AddAvaloniaLocalization`; the registration uses
`TryAdd`, so the host store wins.

## Behavior

- A persisted choice wins over the configured default culture.
- `InitializeAsync` ignores an unknown or unsupported persisted culture and falls back to the
  default culture.
- `SetCultureAsync` validates the culture name and the supported-culture list before writing.
- The service applies `CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, and
  `DefaultThreadCurrentUICulture`.
- Call the service on the UI thread; the package does not marshal to a dispatcher.

## AOT and trimming

The integration uses no reflection and no dynamic code. Culture names are ordinary strings, and
the file store uses `System.IO` and UTF-8 encoding, so the package is safe under trimming and
Native AOT. Avalonia itself determines whether a trimmed or AOT-published application is viable;
the host must keep the Avalonia controls and bindings it uses.

## Related packages

- [`Jeninnet.Localization.Abstractions`](../../src/Jeninnet.Localization.Abstractions/README.md) — culture and localization contracts.
- [`Jeninnet.Localization.Core`](../../src/Jeninnet.Localization.Core/README.md) — the message pipeline.
- [`Jeninnet.Localization.Integrations.Blazor`](../Jeninnet.Localization.Integrations.Blazor/README.md) — the browser-host counterpart.

## License

MIT — see [`LICENSE`](../../LICENSE).
