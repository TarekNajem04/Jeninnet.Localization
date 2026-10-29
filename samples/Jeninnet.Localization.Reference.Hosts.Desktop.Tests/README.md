# Jeninnet.Localization.Reference.Hosts.Desktop.Tests

Composition and behavior tests for the three desktop reference hosts: WinForms, WPF, and Blazor Hybrid. The suite verifies that each desktop composition produces the same canonical stack as the web hosts, that Provider selection changes the implementation, and that the shared order flow localizes validation and messages correctly.

The project is a non-packable MSTest project (`IsPackable=false`, `IsTestProject=true`) with 15 test methods and targets `net10.0-windows`. It builds on any platform through the sample-only `EnableWindowsTargeting` setting, but every test is marked `[OSCondition(OperatingSystems.Windows)]` and only runs on Windows.

## What the suite covers

| Test file | Scope |
|----|----|
| `DesktopHostCompositionTests.cs` | Composition matrix and culture behavior for WinForms, WPF, and Blazor Hybrid |
| `DesktopHostOrderBehaviourTests.cs` | Headless order flow, localized validation, and message localization |
| `DesktopHostTestPaths.cs` | Temporary content staging and repository-root discovery |

The composition matrix:

- **Exactly one inner Provider** for every build-safe Provider (`Memory`, `Json`, `Xml`, `ResX`, `SQLite`) across all three desktop hosts.
- **Provider selection changes the implementation**: `CultureAwareMessageProvider` for JSON/XML, `MemoryMessageProvider`, `ResXMessageProvider`, and `SqliteMessageProvider`.
- **Canonical services resolve** from every host: `IMessageLocalizer`, `IMessageFormatter`, `ILocalizationStore`, and `IStringLocalizer<CreateOrderRequest>`.
- **Blazor Hybrid static culture composition** registers a WebView-scoped `BlazorCultureService` and `BlazorLocalStorageCultureStore`, so JavaScript interop runs inside the WebView context.
- **Bridge culture awareness** through every desktop composition.
- **Memory stays culture-agnostic** in every desktop host.
- **Unsupported switches fail eagerly**: unknown Providers are rejected; server and translation Providers are classified as configuration errors for desktop hosts; a second composition or provider is rejected.
- **SQLite selection** registers only the SQLite inner Provider.

The behavior matrix:

- the shared `OrderService` produces the canonical order shape and total;
- the shared validation model localizes `[LocalizedRange]` output through a Dependency Injection (DI)-backed `ValidationContext`;
- `IMessageLocalizer` resolves the current culture through every desktop composition;
- the complete order flow returns the localized confirmation through every desktop composition.

## Prerequisites

- .NET 10 SDK.
- Windows to run the tests. No external service, credential, or database is required; SQLite uses a local file created by the test composition.

## Run

On Windows:

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Desktop.Tests \
    --configuration Release
```

Run a focused class:

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Desktop.Tests \
    --configuration Release \
    --filter "FullyQualifiedName~DesktopHostOrderBehaviourTests"
```

The project is part of the main solution, so a solution-wide `dotnet test` on Windows also runs it. On Linux and macOS the tests are skipped because of the OS condition.

## Test infrastructure

- `DesktopHostTestPaths.CreateLocalizationDirectory` copies the committed JSON/XML content into a temporary `Localization/` directory so file-based Providers can be composed without touching the working tree.
- The repository root is discovered by walking upward to `Directory.Packages.props`.
- The tests compose the desktop host services directly; they never create a visible window and never require WebView2.
- The desktop hosts' own `--smoke` modes remain the end-to-end headless check for the UI-facing composition; this project verifies the same composition at the service level.

## Related documentation

- [Reference application overview](../README.md)
- [WinForms host](../Jeninnet.Localization.Reference.WinForms/README.md)
- [WPF host](../Jeninnet.Localization.Reference.Wpf/README.md)
- [Blazor Hybrid host](../Jeninnet.Localization.Reference.BlazorHybrid/README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Blazor guide](../../docs/en/02-guides/09-blazor.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
