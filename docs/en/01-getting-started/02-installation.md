# 2. Installation and First Registration

This chapter gets a working registration into an application, explains which package to add, and
calls out the registration-time behavior that surprises people most often.

## Requirements

- **.NET 8** SDK or later. Runtime libraries target `net8.0`, `net9.0`, and `net10.0`; the Roslyn generator and analyzer
  packages target `netstandard2.0` because they run in the compiler, not in your application.
- A project that can use `Microsoft.Extensions.DependencyInjection`. Console applications, ASP.NET
  Core applications, worker services, Blazor hosts, and desktop hosts all qualify.
- For the file providers, a content directory that is present at startup and deployed with the
  application.

## Availability and version

The repository is **unreleased and under active development**. No NuGet packages have been
published. The product version declared by the build is `1.0.0-preview.1`, and a local `dotnet pack`
produces packages with version `1.0.0-preview.1` in the repository's `artifacts/` directory.

That means the commands below describe the intended published workflow, and the local-feed variant
is what actually works today:

```powershell
# Produce the packages locally.
dotnet pack Jeninnet.Localization.slnx -c Release

# Consume the local feed from an application project (adjust the relative path).
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
dotnet add package Jeninnet.Localization.Providers.Json --source ..\artifacts
```

Once the packages are published, the same commands work without `--source`:

```powershell
dotnet add package Jeninnet.Localization.Core
dotnet add package Jeninnet.Localization.Providers.Json
```

Do not mix a locally packed package with a hypothetical future NuGet version in the same
solution. The version is deliberately stable at `1.0.0-preview.1` during this preview, so a stale local
package is a real hazard; clear the NuGet cache or use a fresh package folder when you rebuild the
feed.

## Package selection

There are 35 packable projects. Most applications need the core trio plus exactly one message
provider, then only the integrations they actually use.

### Core trio

| Package | Add it when |
|---|---|
| `Jeninnet.Localization.Abstractions` | You are implementing a provider, formatter, store, or tool and need only the contracts. It is a dependency of nearly everything else. |
| `Jeninnet.Localization.Core` | You want the message pipeline, the localizer, the default formatters, the store-backed `IStringLocalizer` bridge, and DI (Dependency Injection) registration. This is the normal application reference. |
| `Jeninnet.Localization.Storage` | You are implementing a provider or tool that maps catalog files into the canonical message model. Application code normally gets it transitively. |

`Core` depends on `Abstractions`; `Storage` is a leaf assembly containing message data contracts.
Referencing `Core` gives you the pipeline, but not a message source. You must add one provider.

### Message providers

| Package | Source model | Typical use |
|---|---|---|
| `Jeninnet.Localization.Providers.Memory` | One flat `key -> template` dictionary | Tests, prototypes, small static catalogs, WASM (WebAssembly) compositions |
| `Jeninnet.Localization.Providers.Json` | Flat JSON object, one file per culture for multi-culture registration | Simple file catalogs, build-time content, optional hot reload |
| `Jeninnet.Localization.Providers.Xml` | XML elements whose names are keys | Existing XML content pipelines, optional hot reload |
| `Jeninnet.Localization.Providers.ResX` | Embedded `.resx` resource sets | Existing ResX investments inside one assembly |
| `Jeninnet.Localization.Providers.Satellite` | Compiled resources plus satellite packs | Third-party or precompiled resource packs |
| `Jeninnet.Localization.Providers.Po` | Gettext PO (Portable Object) text or GNU MO (Machine Object) binary | Translator tooling, `msgctxt`, plural forms |
| `Jeninnet.Localization.Providers.Pseudo` | Transformed source templates for a synthetic culture | Overflow and untranslated-string detection |
| `Jeninnet.Localization.Providers.Database.SQLite` | `LocalizationMessages` table | Local or embedded relational storage |
| `Jeninnet.Localization.Providers.Database.SqlServer` | `LocalizationMessages` table | Central SQL Server catalog |
| `Jeninnet.Localization.Providers.Database.MySql` | `LocalizationMessages` table | Central MySQL catalog |
| `Jeninnet.Localization.Providers.Database.PostgreSQL` | `LocalizationMessages` table | Central PostgreSQL catalog |
| `Jeninnet.Localization.Providers.Database.EntityFrameworkCore` | Resource-set store over EF Core and SQL Server | Administered resource sets, history, synchronization |
| `Jeninnet.Localization.Translation.Azure` | Azure Translator v3 REST | Runtime machine translation with caching and batching |
| `Jeninnet.Localization.Translation.Google` | Google Translate v2 REST | Runtime machine translation, minimal surface |

`Jeninnet.Localization.Providers.Database.Abstractions` contains shared database provider base
types. It is a dependency of the concrete database providers; reference it directly only when you
are writing a provider.

The EF Core package is **not a message provider**. It implements `ILocalizationStore`, not
`IInnerMessageProvider`. Adding it does not give `IMessageLocalizer` a message source. That
distinction is central to the mental model and is covered in
[Resource sets and the store](../02-guides/06-resource-sets-and-store.md).

### Integrations and tooling

| Package | Add it when |
|---|---|
| `Jeninnet.Localization.Integrations.Avalonia` | You run an Avalonia desktop application and need culture persistence and RTL direction. |
| `Jeninnet.Localization.Integrations.Maui` | You run a MAUI application and need preferences-backed culture persistence and RTL direction. |
| `Jeninnet.Localization.Integrations.WinUI` | You run a WinUI 3 application and need application-data culture persistence and RTL XAML direction. |
| `Jeninnet.Localization.Integrations.Mvc.Validation` | You need MVC validation metadata, display metadata, HTML-safe localizers, view localization, or the `<loc>` tag helper. |
| `Jeninnet.Localization.Integrations.RequestLocalization` | You want request cultures populated from the store and narrowed to served cultures. |
| `Jeninnet.Localization.Integrations.Blazor` | You need circuit-scoped or static-scope culture management and the WASM provider guard. |
| `Jeninnet.Localization.Integrations.JavaScript` | You need to serve allow-listed message templates to a browser client. |
| `Jeninnet.Localization.Integrations.Diagnostics` | You want health checks for the provider and store. |
| `Jeninnet.Localization.Generators.Keys` | You want compile-time key constants generated from `*.loc.json` catalogs. |
| `Jeninnet.Localization.Analyzers.Localizability` | You want authoring-time suggestions for hardcoded user-facing strings. |
| `Jeninnet.Localization.Tools.Keys` | You want the `jeninnet-localization-keys` parity tool in CI. |
| `Jeninnet.Localization.Interchange.Abstractions`, `.Csv`, `.Po`, `.ResX`, `.Xliff` | You need to read or write translation interchange formats. |

## Project setup

A minimal console project needs nothing beyond the SDK defaults:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Jeninnet.Localization.Core" Version="1.0.0-preview.1" />
    <PackageReference Include="Jeninnet.Localization.Providers.Memory" Version="1.0.0-preview.1" />
  </ItemGroup>
</Project>
```

If you use the JSON provider with files under `Localization/`, make sure they reach the output
directory. A file that exists only in the source tree may be found by `dotnet run` (which uses the
project directory as the working directory) and missing after deployment. Resolve the path
consistently, typically from `AppContext.BaseDirectory`:

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## A first registration that compiles

The following program registers the pipeline, registers a memory provider, resolves the localizer,
and formats one message. It uses the parameterless placeholder syntax supported by the default
formatter.

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;
using Microsoft.Extensions.DependencyInjection;

var messages = new Dictionary<string, string> {
    ["Greeting"] = "Hello, {name}!"
};

var services = new ServiceCollection();

// The pipeline: localizer, formatter, provider selector, and the store-backed bridge.
services.AddLocalization(options => options.DefaultCulture = "en");

// Exactly one message source.
services.AddMemoryLocalization(messages);

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var greeting = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(greeting); // Hello, Ada!
```

The registration methods live in package-specific `Extensions` namespaces. The canonical pipeline
method is `AddLocalization`; provider methods are named by capability, such as
`AddMemoryLocalization`, `AddJsonLocalization`, `AddPoLocalization`, and
`AddSqlServerLocalization`.

`AddLocalization()` without an argument registers a default `LocalizationOptions` with
`DefaultCulture = "en"`. The configuring overload is the explicit alternative. Do not do both:
if a `LocalizationOptions` instance is already registered, the configuring overload throws
`InvalidOperationException` rather than silently creating a second configuration path.

The memory provider ignores the culture argument and serves one flat map. That is why the example
is a formatting demonstration, not a culture demonstration. Multi-culture file providers are
covered in the [quick start](03-quick-start.md).

## Registration order and resolution

`AddLocalization` registers `IMessageLocalizer`, `IMessageFormatter`, and `IMessageProvider` with
`TryAddSingleton`. A pre-registered custom formatter therefore wins over the default. The
store-backed `IStringLocalizerFactory` is registered with `Replace`, because the framework must not
keep its default factory in front of the bridge.

Provider registration can come before or after `AddLocalization`; the selector resolves the
registered `IInnerMessageProvider` services when it is constructed. What matters is that exactly
one is registered by the time `IMessageProvider` or `IMessageLocalizer` is resolved. Registering
two providers is not a merge: resolution throws `InvalidOperationException` naming the resolved
provider types.

`AddLocalization` also registers the Microsoft `IStringLocalizer<T>` wrapper and a
`CustomStringLocalizerFactory` that reads from `ILocalizationStore`. Those services fail at
resolution if no store is registered. If you use only `IMessageLocalizer`, you do not need a store.

**Pitfall:** file registration is eager about existence for JSON, XML, and PO. `AddJsonLocalization`
and `AddPoLocalization` validate the path and throw `FileNotFoundException` during registration if
the file does not exist. ResX and satellite providers cannot validate the manifest resource at
registration; their first lookup throws `MissingManifestResourceException` if the base name is
wrong. Eager failures are easier to diagnose at startup, but they also mean the content must be in
place before the service collection is built.

## Where to go next

The [quick start](03-quick-start.md) builds a small multi-culture application and demonstrates
fallback. If you prefer to understand the machinery first, read
[The mental model](04-mental-model.md). For package-by-package details, see the
[package map](../appendix/02-package-map.md) and the
[provider reference](../03-reference/03-provider-reference.md).
