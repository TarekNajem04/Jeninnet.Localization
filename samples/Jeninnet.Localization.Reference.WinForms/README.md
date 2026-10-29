# Jeninnet.Localization.Reference.WinForms

The Windows Forms host of the Jeninnet.Localization reference application. It shows that the canonical message pipeline is not tied to ASP.NET Core: a classic desktop UI resolves localized catalog text through `IStringLocalizer<T>`, formats order messages through `IMessageLocalizer`, and switches culture at runtime without restarting the application.

The project is a **sample**, not a package: `IsPackable=false`, never published. It targets `net10.0-windows` and requires the Windows Desktop runtime to run; it builds cross-platform through the sample-only `EnableWindowsTargeting` setting.

## What this host demonstrates

- **Localization from classic UI code** using dependency injection and the shared BookStore domain.
- **Runtime culture switching** through a `ComboBox` that sets `CultureInfo.CurrentUICulture` and re-renders every label and catalog item.
- **Right-to-left layout** for Arabic cultures through `RightToLeft` on the form.
- **Localized DataAnnotations validation** through a Dependency Injection (DI)-backed `ValidationContext` over the shared `CreateOrderRequest` model.
- **Localized order feedback** including the formatted confirmation and business error messages; the last confirmation survives a culture switch and is re-localized.
- **Exactly one Provider**, selected through an environment variable, with no UI-code changes when the Provider changes.

## Prerequisites

- .NET 10 SDK to build.
- Windows with the .NET Desktop Runtime to run the GUI.
- No server, database, or credential for the default `Json` Provider.
- SQLite writes only local data; the other build-safe Providers need no external prerequisites.

## Run

Build from any platform:

```bash
dotnet build samples/Jeninnet.Localization.Reference.WinForms -c Release
```

Run the GUI on Windows:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.WinForms
```

Choose a culture from the combo box, select a book, set a quantity, and place the order. The window title, labels, catalog entries, details, and messages update immediately after a culture change.

Run the headless smoke verification:

```bash
dotnet run --project samples/Jeninnet.Localization.Reference.WinForms -- --smoke
```

The smoke mode needs no GUI session or UI automation. It prints one block per supported culture:

```text
[en] heading          : Catalog
[en] title            : Jeninnet Localization Reference
[en] demoShowcase     : ...
[en] regionalNote     : ...
[en] missingEverywhere: [MISSING:Demo.MissingEverywhere]
[en] orderMessage     : ...
```

It exits with `0` when the composition and lookups complete. A failed composition or lookup throws and returns a non-zero exit code.

## Provider selection

The desktop hosts read `ReferenceLocalization__Provider` directly from the environment; there is no `appsettings.json` in this project.

```powershell
$env:ReferenceLocalization__Provider = "SQLite"
dotnet run --project samples/Jeninnet.Localization.Reference.WinForms
```

```bash
ReferenceLocalization__Provider=Xml \
dotnet run --project samples/Jeninnet.Localization.Reference.WinForms -- --smoke
```

Build-safe values: `Memory`, `Json` (default), `Xml`, `ResX`, `SQLite`. Server and translation Providers are not reference scenarios for desktop hosts; the matrix in [`samples/README.md`](../README.md) marks those cells as not appropriate or deferred.

SQLite creates and seeds `<host output>/Data/reference-localization.db` on first start. Delete the `Data/` directory while the app is stopped to reset it.

## Composition and UI

```text
Program.cs
    -> ReferenceWinFormsComposition.BuildServiceProvider()
    -> ApplicationConfiguration.Initialize()
    -> new MainForm(services)
```

`ReferenceWinFormsComposition.AddReferenceWinForms` registers the shared Provider selection and the Core pipeline. `MainForm` resolves `IMessageLocalizer` and `IStringLocalizer<PageResources>` from the container, builds the layout in code, and never hard-codes culture-specific text.

The form contains:

- a culture selector seeded from `ReferenceCultures.SupportedCultures`;
- a catalog list with localized titles;
- a details panel with localized author, description, and price labels;
- a quantity control bounded by `OrderLimits`;
- an order button that validates through DataAnnotations and then calls the shared `OrderService`;
- a feedback label for the localized confirmation or error.

## Testing

The WinForms composition is covered on Windows by [`Jeninnet.Localization.Reference.Hosts.Desktop.Tests`](../Jeninnet.Localization.Reference.Hosts.Desktop.Tests/README.md):

- exactly one inner Provider for every build-safe Provider;
- the expected Provider implementation after selection;
- the canonical pipeline services and bridge;
- localized order behavior and DataAnnotations validation;
- culture-aware message localization.

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
| The window shows `[MISSING:<key>]` | The key is absent in every culture, or the Provider returned no value | Expected for `Demo.MissingEverywhere`; otherwise verify the selected content source |
| The language does not change | The combo box selection was not applied | Select a different supported culture; the form reapplies labels immediately |
| SQLite keeps old content | The local database was not reset | Stop the app and delete the `Data/` directory under the output folder |

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [RTL (Right-to-Left) and bidirectional text](../../docs/en/02-guides/16-rtl-and-bidi.md)
- [Formatting and plurals](../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
