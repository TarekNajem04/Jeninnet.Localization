# Jeninnet.Localization.Reference.Wpf

The WPF/XAML host of the Jeninnet.Localization reference application. It demonstrates the canonical localization pipeline through lightweight MVVM: localized display properties, runtime culture switching, localized DataAnnotations validation, and localized order messages, all resolved by the shared `IMessageLocalizer` and `IStringLocalizer<T>` services.

The project is a **sample**, not a package: `IsPackable=false`, never published. It targets `net10.0-windows` and requires the Windows Desktop runtime to run; it builds cross-platform through the sample-only `EnableWindowsTargeting` setting.

## What this host demonstrates

- **MVVM consumption** of `IMessageLocalizer` and `IStringLocalizer<PageResources>` through an observable view model.
- **Standard XAML bindings** for every localized label, heading, catalog item, detail value, and feedback message.
- **Runtime culture switching** through a bound `ComboBox`; the view model reapplies the culture and raises change notifications for every localized property.
- **RTL layout** — the window's `FlowDirection` is bound to the active culture, so Arabic switches the whole form to right-to-left.
- **Localized DataAnnotations validation** through a Dependency Injection (DI)-backed `ValidationContext`.
- **Localized order feedback**, including both the formatted confirmation and business errors; the last confirmation survives a culture switch and is re-localized.
- **No custom markup extension**: the host deliberately uses ordinary bindings instead of inventing a XAML localization extension that the library does not provide.
- **Exactly one Provider**, selected through an environment variable.

## Prerequisites

- .NET 10 SDK to build.
- Windows with the .NET Desktop Runtime to run the GUI.
- No server, database, or credential for the default `Json` Provider.
- SQLite writes only local data; the other build-safe Providers need no external prerequisites.

## Run

Build from any platform:

```bash
dotnet build samples/Jeninnet.Localization.Reference.Wpf -c Release
```

Run the GUI on Windows:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.Wpf
```

Choose a culture, select a book, set a quantity, and place the order. The title, labels, catalog entries, details, layout direction, and message update through property-change notifications.

Run the headless smoke verification:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.Wpf -- --smoke
```

The smoke mode needs no GUI session or UI automation. It prints one block per supported culture with the heading, application title, demo keys, missing-key marker, and localized order message, then exits with `0` when the composition and lookups complete.

## Project layout

| File | Responsibility |
|----|----|
| `Program.cs` | Entry point; selects `--smoke` or runs the WPF application |
| `ReferenceWpfComposition.cs` | Registers Provider selection, the Core pipeline, and `BookStoreViewModel` |
| `BookStoreViewModel.cs` | Observable view model exposing localized properties and order commands |
| `BookListItem.cs` | Display wrapper for a catalog entry and its localized title |
| `MainWindow.xaml` / `.xaml.cs` | Bindings and the code-behind click handler |
| `ReferenceWpfSmoke.cs` | Headless composition and culture verification |
| `PageResources.cs` | Anchor type for `IStringLocalizer<PageResources>` |

## Composition

```text
Program.cs
    -> ReferenceWpfComposition.BuildServiceProvider()
    -> new Application { ShutdownMode = OnMainWindowClose }
    -> new MainWindow(services.GetRequiredService<BookStoreViewModel>())
    -> application.Run(window)
```

`ReferenceWpfComposition.AddReferenceWpf` registers the shared Provider selection, the Core pipeline, and `BookStoreViewModel` as a singleton. The view model resolves `IMessageLocalizer` and a current `IStringLocalizer<PageResources>` from the container.

When `CurrentCulture` changes, the view model:

1.  sets `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture`;
2.  resolves a fresh `IStringLocalizer<PageResources>` (the bridge captures the UI culture at construction);
3.  reloads the catalog with localized titles;
4.  raises change notifications for every localized property, the layout `FlowDirection`, and the message.

## Provider selection

The desktop hosts read `ReferenceLocalization__Provider` directly from the environment; there is no `appsettings.json` in this project.

```powershell
$env:ReferenceLocalization__Provider = "Memory"
dotnet run --project samples/Jeninnet.Localization.Reference.Wpf
```

```bash
ReferenceLocalization__Provider=SQLite \
dotnet run --project samples/Jeninnet.Localization.Reference.Wpf -- --smoke
```

Build-safe values: `Memory`, `Json` (default), `Xml`, `ResX`, `SQLite`. Server and translation Providers are not reference scenarios for desktop hosts; see the matrix in [`samples/README.md`](../README.md).

SQLite creates and seeds `<host output>/Data/reference-localization.db` on first start. Delete the `Data/` directory while the app is stopped to reset it.

## Testing

The WPF composition is covered on Windows by [`Jeninnet.Localization.Reference.Hosts.Desktop.Tests`](../Jeninnet.Localization.Reference.Hosts.Desktop.Tests/README.md):

- exactly one inner Provider for every build-safe Provider;
- the expected Provider implementation after selection;
- the canonical pipeline services and bridge;
- localized order behavior and DataAnnotations validation;
- culture-aware message localization through every desktop composition;
- the WPF view model's `RightToLeft` layout direction when Arabic is selected.

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Desktop.Tests \
    --configuration Release
```

The desktop test project builds on any platform but its tests are marked `[OSCondition(OperatingSystems.Windows)]`.

## Troubleshooting

| Symptom | Cause | Remedy |
|----|----|----|
| The app fails to start with a Windows runtime error | Running a `net10.0-windows` app on a non-Windows OS | Run the GUI on Windows; use `--smoke` only on Windows as well |
| Startup fails naming a missing configuration key | A gated Provider was selected without its value | Use a build-safe Provider or provide the value through the environment |
| A label shows `[MISSING:<key>]` | The key is absent in every culture, or the Provider returned no value | Expected for `Demo.MissingEverywhere`; otherwise verify the selected content source |
| The language does not change | The bound culture value did not change | Select a different supported culture; the view model reapplies all localized properties |
| SQLite keeps old content | The local database was not reset | Stop the app and delete the `Data/` directory under the output folder |

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [Formatting and plurals](../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
