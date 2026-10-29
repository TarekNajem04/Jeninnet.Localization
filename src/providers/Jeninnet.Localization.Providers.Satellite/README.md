# Jeninnet.Localization.Providers.Satellite

`Jeninnet.Localization.Providers.Satellite` serves message templates from compiled .NET resources: the neutral resources embedded in an assembly plus its satellite packs (`{culture}/{AssemblyName}.resources.dll`). It implements `IInnerMessageProvider` through `SatelliteMessageProvider`, which delegates satellite probing to `ResourceManager`. Use it when the strings arrive as precompiled packs, including packs authored outside this repository; use `Providers.ResX` when `.resx` files are authored in the same project. See [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md).

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Satellite
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` arrives transitively.

## Registration

```csharp
using Jeninnet.Localization.Providers.Satellite.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddSatelliteLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);

services.AddSatelliteLocalization(options => {
    options.BaseName = "MyApp.Resources.ValidationMessages";
    options.ResourceAssembly = typeof(Program).Assembly;
});
```

The provider is registered as a singleton `IInnerMessageProvider`. Registration validates `services`, the options, a non-empty `BaseName`, and a non-null `ResourceAssembly`. Null services, options, or configure delegates throw `ArgumentNullException`; an empty `BaseName` throws `ArgumentException`.

## Configuration

`SatelliteLocalizationOptions` is a construction snapshot.

| Property | Type | Default | Notes |
|----|----|----|----|
| `BaseName` | `string` | `""` | Required. Manifest resource base name, without culture or extension. |
| `ResourceAssembly` | `Assembly` | `typeof(SatelliteMessageProvider).Assembly` | Assembly that owns the resources. Satellite packs are probed next to it. |

```csharp
services.AddSatelliteLocalization(options => {
    options.BaseName = "MyApp.Resources.ValidationMessages";
    options.ResourceAssembly = typeof(Program).Assembly;
});
```

The default `ResourceAssembly` is the provider assembly, not the application assembly. Set it explicitly. Library packs and host packs are addressed identically as a `(BaseName, assembly)` pair.

## Usage

A typical deployment has a neutral resource in the main assembly and per-culture satellite packs next to it:

```text
MyApp.dll
fr/MyApp.resources.dll
ar/MyApp.resources.dll
```

Each satellite contains the culture-specific `.resources` stream for the same base name. The provider resolves the exact requested culture, so a request for `fr` reads the `fr` pack and a request for `fr-CA` first looks for a `fr-CA` pack.

```csharp
var text = await localizer.LocalizeAsync(
    "Required",
    new Dictionary<string, object?>(),
    CultureInfo.GetCultureInfo("fr"));
```

A missing satellite is a clean miss (`null`); the localizer then continues its own parent-culture and default-culture fallback. A resource base that does not exist is a configuration error and throws `MissingManifestResourceException` on the first lookup.

## Behavior and limits

- **Exact matching.** The provider calls `ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)`, then `GetString(key, ignoreCase: false)`. Parent probing is disabled so the localizer remains the only owner of culture fallback.
- **Satellite probing.** Neutral resources come from the main assembly; per-culture strings come from satellite packs. Probing is delegated entirely to `ResourceManager`; the provider does not invent a file layout.
- **Missing satellite.** A culture without a pack is a clean miss (`null`), never an exception.
- **Missing resource base.** A misconfigured base name throws `MissingManifestResourceException`, preserving `ResourceManager.GetString` semantics instead of silently missing.
- **Strings only.** A non-string resource resolves to the documented miss result (`null`); nothing is converted or serialized. Typed resources are outside the contract.
- **Key matching.** Exact, ordinal, and case-sensitive (BCL (Base Class Library) `ResourceSet` semantics).
- **Culture.** A single provider resolves multiple cultures through `ResourceManager`; there is no multi-culture registration builder. The localizer still owns parent and default-culture fallback.
- **Loading and reload.** `ResourceManager` caches resource sets for the lifetime of the singleton provider. There is no file watcher, `RefreshAsync`, or acquisition event; rebuild and redeploy packs to change content.
- **Authoring.** Out of scope. Author neutral and translated resources with your normal ResX or resource-pack tooling, then ship the compiled assemblies.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.
- **Cancellation.** A cancelled lookup throws `OperationCanceledException` before the resource set is queried.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Providers.ResX](../Jeninnet.Localization.Providers.ResX/README.md) - `.resx` authoring in the same assembly.
- [Jeninnet.Localization.Core](../../Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Po](../Jeninnet.Localization.Providers.Po/README.md) - gettext catalogs with contexts and plurals.

## Documentation

- [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Cultures and fallback](../../../docs/en/02-guides/03-cultures-and-fallback.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT. See [LICENSE](../../../LICENSE).
