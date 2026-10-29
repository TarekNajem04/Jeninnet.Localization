# Jeninnet.Localization.Reference.BlazorHybrid

The Blazor Hybrid host of the Jeninnet.Localization reference application. It hosts Blazor components inside a Windows Forms shell through `BlazorWebView` and demonstrates the static-scope Blazor localization mode: in-app culture switching with local-storage persistence, local/offline Providers, and no ASP.NET Core request pipeline.

The project is a **sample**, not a package: `IsPackable=false`, never published. It targets `net10.0-windows`, requires the Windows Desktop runtime to run, and requires the Microsoft Edge WebView2 runtime for the embedded browser.

## What this host demonstrates

- **Blazor components in a desktop shell** using `Microsoft.AspNetCore.Components.WebView.WindowsForms`.
- **Static culture scope** through `AddBlazorLocalization` with `BlazorLocalizationMode.Wasm`, appropriate for a single-user desktop process.
- **Culture persisted to local storage** through `BlazorLocalStorageCultureStore`; the selector changes the culture and the components re-render.
- **Local/offline Providers**: Memory, JSON, XML, RESX, and SQLite are the intended desktop choices; network translation and server databases are not reference scenarios for this host.
- **Localized order flow** through `IStringLocalizer<PageResources>` and `IMessageLocalizer`; culture switching keeps the current form and re-localizes the last order confirmation.
- **Headless verification** through a `--smoke` mode that needs no WebView2 or GUI session.

## Prerequisites

- .NET 10 SDK to build.
- Windows with the .NET Desktop Runtime to run the GUI.
- Microsoft Edge WebView2 Runtime for the embedded browser.
- No server, database, or credential for the default `Json` Provider.
- SQLite writes only local data.

## Run

Build from any platform:

```bash
dotnet build samples/Jeninnet.Localization.Reference.BlazorHybrid -c Release
```

Run the GUI on Windows:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorHybrid
```

The WinForms window hosts the Blazor application. Use the culture selector at the top of the page to switch between `en`, `ar`, and `ar-SY`; the selection is persisted in local storage through the Jeninnet culture service.

Run the headless smoke verification:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorHybrid -- --smoke
```

The smoke mode requires neither WebView2 nor a GUI session. It prints one block per supported culture with the heading, application title, demo keys, missing-key marker, and localized order message, then exits with `0` when the composition and lookups complete.

## Project layout

| File | Responsibility |
|----|----|
| `Program.cs` | Entry point; selects `--smoke` or runs the WinForms shell |
| `ReferenceBlazorHybridComposition.cs` | Registers Provider selection, Core, Blazor integration, and the local-storage culture store |
| `MainForm.cs` | Creates the `BlazorWebView`, sets the host page, and mounts `App` at `#app` |
| `Components/App.razor` | Root component and router |
| `Components/Catalog.razor` | Localized catalog component |
| `Components/Orders.razor` | Localized order form and feedback |
| `Components/CultureSelector.razor` | In-app culture switcher backed by `BlazorCultureService` |
| `wwwroot/index.html` | WebView2 host page and styles |
| `ReferenceBlazorHybridSmoke.cs` | Headless composition and culture verification |
| `PageResources.cs` | Anchor type for `IStringLocalizer<PageResources>` |

## Composition

```text
Program.cs
    -> ApplicationConfiguration.Initialize()
    -> default ambient culture (en)
    -> ReferenceBlazorHybridComposition.BuildServiceProvider()
    -> new MainForm(services)
    -> Application.Run(form)
```

`ReferenceBlazorHybridComposition.AddReferenceBlazorHybrid` registers:

```text
AddReferenceLocalizationProvider(...)   -> shared Provider selection and Core pipeline
AddBlazorLocalization(options => {      -> static-scope culture service
    options.Mode = BlazorLocalizationMode.Wasm;
    options.DefaultCulture = "en";
    options.SupportedCultures = ["en", "ar", "ar-SY"];
})
AddScoped<BlazorCultureService>()       -> WebView-scoped culture service
AddScoped<IBlazorCultureStore,
          BlazorLocalStorageCultureStore>() -> local-storage persistence
```

`MainForm` resolves `IStringLocalizer<PageResources>` for the window title, builds the `BlazorWebView`, and exposes the same service provider to the Blazor components. The culture service and local-storage store are scoped to the WebView, because WebView2 owns the `IJSRuntime` that backs the JavaScript calls.

## Provider selection

The desktop hosts read `ReferenceLocalization__Provider` directly from the environment; there is no `appsettings.json` in this project.

```powershell
$env:ReferenceLocalization__Provider = "SQLite"
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorHybrid
```

```bash
ReferenceLocalization__Provider=Xml \
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorHybrid -- --smoke
```

Build-safe values: `Memory`, `Json` (default), `Xml`, `ResX`, `SQLite`. Server and translation Providers are not reference scenarios for desktop hosts; see the matrix in [`samples/README.md`](../README.md).

SQLite creates and seeds `<host output>/Data/reference-localization.db` on first start. Delete the `Data/` directory while the app is stopped to reset it.

## Testing

The Blazor Hybrid composition is covered on Windows by [`Jeninnet.Localization.Reference.Hosts.Desktop.Tests`](../Jeninnet.Localization.Reference.Hosts.Desktop.Tests/README.md):

- exactly one inner Provider for every build-safe Provider;
- the expected Provider implementation after selection;
- the canonical pipeline services and bridge;
- the WebView-scoped `BlazorCultureService` registration and the local-storage culture store;
- localized order behavior and DataAnnotations validation.

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Desktop.Tests \
    --configuration Release
```

The desktop test project builds on any platform but its tests are marked `[OSCondition(OperatingSystems.Windows)]`.

## Troubleshooting

| Symptom | Cause | Remedy |
|----|----|----|
| The window opens but the Blazor content is blank | WebView2 Runtime is missing or broken | Install the Microsoft Edge WebView2 Runtime |
| The app fails to start with a Windows runtime error | Running a `net10.0-windows` app on a non-Windows OS | Run the GUI on Windows; use `--smoke` only on Windows as well |
| Startup fails naming a missing configuration key | A gated Provider was selected without its value | Use a build-safe Provider or provide the value through the environment |
| A label shows `[MISSING:<key>]` | The key is absent in every culture, or the Provider returned no value | Expected for `Demo.MissingEverywhere`; otherwise verify the selected content source |
| The culture is not remembered between runs | Local storage was cleared or the app data folder was reset | Reselect the culture; persistence is per user profile |
| SQLite keeps old content | The local database was not reset | Stop the app and delete the `Data/` directory under the output folder |

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Blazor integration](../../integrations/Jeninnet.Localization.Integrations.Blazor/README.md)
- [Blazor guide](../../docs/en/02-guides/09-blazor.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [RTL (Right-to-Left) and bidirectional text](../../docs/en/02-guides/16-rtl-and-bidi.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
