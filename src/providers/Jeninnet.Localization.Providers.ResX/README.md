# Jeninnet.Localization.Providers.ResX

`Jeninnet.Localization.Providers.ResX` serves message templates from `.resx` resources compiled into an assembly. It implements `IInnerMessageProvider` through `ResXMessageProvider`, which wraps `System.Resources.ResourceManager` and asks for the exact requested culture's resource set. This is the provider for projects that already author `.resx` files in the same assembly; use `Providers.Satellite` when the strings arrive as precompiled packs instead. See [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html) for the comparison.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.ResX
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` arrives transitively.

## Registration

```csharp
using Jeninnet.Localization.Providers.ResX.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddResXLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);

services.AddResXLocalization(options => {
    options.BaseName = "MyApp.Resources.ValidationMessages";
    options.ResourceAssembly = typeof(Program).Assembly;
});
```

The provider is registered as a singleton `IInnerMessageProvider`. Registration validates `services`, the options, a non-empty `BaseName`, and a non-null `ResourceAssembly`. Null services, options, or configure delegates throw `ArgumentNullException`; an empty `BaseName` throws `ArgumentException`.

## Configuration

`ResXLocalizationOptions` is a construction snapshot.

| Property | Type | Default | Notes |
|----|----|----|----|
| `BaseName` | `string` | `""` | Required. Manifest resource base name, without culture or extension. |
| `ResourceAssembly` | `Assembly` | `typeof(ResXMessageProvider).Assembly` | Assembly that owns the compiled resource. Set it explicitly to the application or library assembly. |

```csharp
services.AddResXLocalization(options => {
    options.BaseName = "MyApp.Resources.ValidationMessages";
    options.ResourceAssembly = typeof(Program).Assembly;
});
```

The default `ResourceAssembly` is the provider assembly, not the application assembly. Leaving it unset with a local resource name fails at the first lookup with `MissingManifestResourceException`.

## Usage

A standard `.resx` entry maps `name` to `value`:

```xml
<data name="Required" xml:space="preserve">
  <value>This field is required.</value>
</data>
<data name="MinLength" xml:space="preserve">
  <value>Value must be at least {0} characters.</value>
</data>
```

At build time the `.resx` becomes an embedded `.resources` file. `BaseName` follows the `{AssemblyRootNamespace}.{ResourcePath}` convention; for a file at `Resources/ValidationMessages.resx` in a project whose root namespace is `MyApp`, use `MyApp.Resources.ValidationMessages`.

```csharp
var text = await localizer.LocalizeAsync(
    "Required",
    new Dictionary<string, object?>(),
    CultureInfo.GetCultureInfo("en"));
```

The result is `This field is required.` A missing key returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Resolution.** `ResXMessageProvider` calls `ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)` and then `GetString(key, ignoreCase: false)`. Parent probing is disabled so the localizer remains the only owner of culture fallback.
- **Culture.** The provider honors the requested culture and resolves only the corresponding exact resource set. Neutral resources are available when the invariant culture is requested. A single provider can resolve multiple embedded culture resource sets; there is no separate multi-culture registration builder.
- **Missing key.** A key absent from an existing resource set returns `null`.
- **Missing base.** A missing or misconfigured base name is not detected at registration. The first lookup throws `System.Resources.MissingManifestResourceException`.
- **Key matching.** Exact, ordinal, and case-sensitive. Case-variant names collide at build time; MSBuild reports `MSB3568` and ignores the duplicate.
- **Values.** An empty string is a valid template, distinct from a missing key. Values are not trimmed (`xml:space="preserve"`). Only string entries are supported as templates; typed/non-string `.resx` entries are outside the provider contract and require `System.Resources.Extensions` at build/runtime (`MSB3822`), which this package deliberately does not reference.
- **Loading and reload.** `ResourceManager` reads and caches resource sets lazily for the lifetime of the singleton provider. There is no file watcher, `RefreshAsync`, or acquisition event. Rebuild and redeploy to change content.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.
- **Cancellation.** A cancelled lookup throws `OperationCanceledException` before the resource set is queried.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Providers.Satellite](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Satellite/README.md) - compiled neutral resources and precompiled satellite packs.
- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Po](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md) - gettext catalogs with contexts and plurals.

## Documentation

- [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html)
- [Configuring providers](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/02-configuring-providers.html)
- [Cultures and fallback](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/03-cultures-and-fallback.html)
- [Provider reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/03-provider-reference.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)

## License

MIT. See [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
