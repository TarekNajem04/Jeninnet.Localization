# Solutions in Jeninnet.Localization

The repository ships two `.slnx` solutions. They are complementary: the main solution is the
authoritative cross-platform entry point, and the Windows solution is a focused companion for the
Windows-only host integrations, samples, and tests.

| Solution | Scope | Platforms and prerequisites | Use it for |
| --- | --- | --- | --- |
| `Jeninnet.Localization.slnx` | The complete cross-platform solution: core libraries, providers, translation, interchange, cross-platform integrations, tooling, the main test suite, the full reference application, and the docs/scripts view items. | Windows, Linux, and macOS. The desktop reference hosts build on all three using the sample-only `EnableWindowsTargeting` setting and run on Windows. | Library development, the complete reference application, and the normal CI matrix. |
| `Jeninnet.Localization.Windows.slnx` | The Windows companion: WinUI 3 integration, MAUI integration used by the Windows sample, the shared host-sample project, the MAUI and WinUI reference hosts, the Windows host-integration tests, and the four core library projects those heads build (`Abstractions`, `Core`, `Storage`, `Providers.Json`). | Windows with the WinUI/MAUI workloads and Windows App SDK. The WinUI reference host is x64-only; the solution maps it to `x64`. | Windows host development, running the Windows-only samples/tests, and packing the Windows-only host packages. |

## Why the Windows solution exists

WinUI and MAUI Windows heads need Windows-only SDK workloads, Visual Studio components, and the
Windows App SDK. The WinUI reference host is also x64-only. Keeping those heads in the main solution
would force every Linux/macOS contributor and CI leg to install Windows workloads and carry
Windows-only configuration.

The Windows solution provides a focused entry point for those heads without making the main
solution platform-specific. It references the same source projects, so no code is duplicated. The
main solution remains the single authoritative solution for the cross-platform libraries and the
complete reference application.

The main solution already contains the WinForms, WPF, and Blazor Hybrid desktop hosts (they build
cross-platform and run on Windows). The Windows solution is specifically for the WinUI/MAUI Windows
heads and the Windows host test suite; it is not a replacement for the main solution.

## What lives in the main solution

| Area | Projects and purpose |
| --- | --- |
| Core (`src/`) | `Jeninnet.Localization.Abstractions` (contracts and options), `Jeninnet.Localization.Core` (localizer pipeline, formatting, missing-key handling), `Jeninnet.Localization.Storage` (resource-set storage model). |
| Providers (`src/providers/`) | Memory, JSON, XML, ResX, Satellite, PO, Pseudo, and the database providers (SQL Server, MySQL, PostgreSQL, SQLite, EF Core, database abstractions). Each provider implements one message-source family. |
| Translation (`src/translation/`) | Azure Translator and Google Translate providers. |
| Interchange (`src/interchange/`) | CSV, PO, ResX, XLIFF, and the interchange abstractions used to import/export catalogs. |
| Integrations (`integrations/`) | Avalonia, Blazor, DataAnnotations, Diagnostics, JavaScript, MAUI (`net10.0`), MVC validation, and request-localization bridges. |
| Tooling (`src/generators/`, `src/analyzers/`, `src/tooling/`) | The localization-key source generator, the localizability analyzer and code fixes, and the `Tools.Keys` CLI. |
| Tests (`tests/`) | `Jeninnet.Localization.Tests` — the cross-platform MSTest suite (net8.0/net9.0/net10.0). |
| Reference application (`samples/`) | `Jeninnet.Localization.Docs`, `HostSamples.Shared`, the BookStore `Reference.Shared`, the six hosts (`Api`, `Mvc`, `BlazorServer`, `BlazorHybrid`, `WinForms`, `Wpf`), and the shared/web/desktop test projects. The Avalonia host sample is also included. |
| Consumers (`consumers/`) | `Jeninnet.Localization.Consumer` + its tests — package validation against the packed local feed; this has its own solution. |
| Docs, scripts, workflows | View items under `/doc/`, `/scripts/`, and `/.github/` for navigation; they do not affect compilation. |

## What lives in the Windows solution

| Project | Purpose |
| --- | --- |
| `integrations/Jeninnet.Localization.Integrations.WinUI` | WinUI 3 integration package: culture service and Windows application-data persistence. |
| `integrations/Jeninnet.Localization.Integrations.Maui` | MAUI integration package (`net10.0`); included here because the Windows MAUI sample and host tests use it. |
| `samples/Jeninnet.Localization.HostSamples.Shared` | Shared host-sample model and English/Arabic content used by the MAUI and WinUI samples. |
| `samples/Jeninnet.Localization.Reference.Maui` | MAUI Windows reference host sample: preferences-backed culture persistence and RTL direction. |
| `samples/Jeninnet.Localization.Reference.WinUI` | WinUI 3 reference host sample: Windows application-data persistence and RTL XAML. The project supports `x64` only. |
| `src/Jeninnet.Localization.Abstractions`, `src/Jeninnet.Localization.Core`, `src/Jeninnet.Localization.Storage`, `src/providers/Jeninnet.Localization.Providers.Json` | The core library dependencies of the Windows heads. They are the same projects used by the main solution; listing them ensures the selected Debug/Release configuration applies to the whole dependency graph. |
| `tests/Jeninnet.Localization.HostIntegrations.Tests` | Windows-only MSTest suite validating the MAUI and WinUI host integrations. |

The Windows solution also displays the same `/src/Shared/` view items as the main solution. Those
files are shared source compiled into the packages through `.projitems`; they are not a separate
project or assembly.

The WinUI project declares `<Platforms>x64</Platforms>`. The Windows solution declares `Any CPU` as
its solution platform and maps the WinUI project to `x64`:

```xml
<Project Path="samples/Jeninnet.Localization.Reference.WinUI/Jeninnet.Localization.Reference.WinUI.csproj">
    <Platform Project="x64" />
</Project>
```

This mapping is what keeps Visual Studio's configuration manager consistent and avoids the
"project configuration does not exist" warning.

## Shared source and solution folders

`src/shared` contains source-only shared items (`.cs` files plus `.projitems` import files). They are
compiled directly into the projects that import them, so there is no runtime shared assembly and no
extra package dependency. The `/src/Shared/` folders in both `.slnx` files are **solution view
items**: listing a file there only makes it visible in Solution Explorer; it does not change
compilation. Project source files are included automatically through their `.csproj` files.

## Building and testing

Main solution (all operating systems):

```bash
dotnet build Jeninnet.Localization.slnx -c Release
dotnet test  Jeninnet.Localization.slnx -c Release
```

Windows solution (Windows with the WinUI/MAUI workloads):

```bash
dotnet workload install maui-windows
dotnet build Jeninnet.Localization.Windows.slnx -c Release
dotnet test  tests/Jeninnet.Localization.HostIntegrations.Tests -c Release
```

CI: `.github/workflows/ci.yml` builds and tests the main solution on Windows, Ubuntu, and macOS, and
builds/tests the Windows solution on Windows. `.github/workflows/nuget-publish.yml` packs both
solutions before publishing.
