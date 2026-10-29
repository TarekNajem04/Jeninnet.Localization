# Jeninnet.Localization.Providers.Pseudo

`Jeninnet.Localization.Providers.Pseudo` serves a synthetic pseudo-localized culture for layout overflow and untranslated-string-leak testing. It implements `IInnerMessageProvider` through `PseudoMessageProvider`, transforming source templates without I/O, timers, or randomness. It is a quality-assurance aid, not a translation: enabling it replaces the normal provider for that host. See [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md) and [Testing your localization](../../../docs/en/02-guides/15-testing-your-localization.md).

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Pseudo
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` arrives transitively.

## Registration

```csharp
using Jeninnet.Localization.Providers.Pseudo.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddPseudoLocalization(new Dictionary<string, string> {
    ["App.Title"] = "Bookstore",
    ["Greeting"] = "Hello, {Name}!"
});
```

The three overloads are:

- `AddPseudoLocalization(IReadOnlyDictionary<string, string> sourceTemplates)`
- `AddPseudoLocalization(PseudoLocalizationOptions options)`
- `AddPseudoLocalization(Action<PseudoLocalizationOptions> configure)`

The provider is registered as a singleton `IInnerMessageProvider`. Null services, options, configure delegates, or source templates throw `ArgumentNullException`. An empty or whitespace pseudo culture throws `ArgumentException`; an expansion percentage outside 0-100 throws `ArgumentOutOfRangeException`. The source dictionary is copied at construction.

## Configuration

`PseudoLocalizationOptions` is a construction snapshot.

| Property | Type | Default | Notes |
|----|----|----|----|
| `SourceTemplates` | `IReadOnlyDictionary<string, string>` | empty dictionary | Must not be null. Copied with `StringComparer.Ordinal`. |
| `PseudoCulture` | `string` | `qps-ploc` | Synthetic culture served. Must not be empty or whitespace. |
| `ExpansionPercent` | `int` | `35` | Padding added as a percentage of the transformed body length. Valid range 0-100. |

```csharp
services.AddPseudoLocalization(options => {
    options.SourceTemplates = new Dictionary<string, string> {
        ["Greeting"] = "Hello, {Name}!"
    };
    options.PseudoCulture = "qps-ploc";
    options.ExpansionPercent = 50;
});
```

## Usage

Look up the pseudo culture to receive transformed templates. Every other culture returns `null`, so the normal localizer fallback chain applies as usual.

```csharp
var pseudo = CultureInfo.GetCultureInfo("qps-ploc");

var text = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["Name"] = "Ada" },
    pseudo);
```

For the source template `Hello, {Name}!` at the default 35% expansion, the provider resolves:

```text
[!! Ĥëļļö, {Name}!~~~~~ !!]
```

Formatting then substitutes `{Name}`, producing `[!! Ĥëļļö, Ada!~~~~~ !!]`.

## Behavior and limits

- **Transform.** Latin letters map through a fixed ordinal accent table (unmapped characters pass through). Balanced `{...}` placeholder spans, including nested ICU (International Components for Unicode) constructs, are copied verbatim, so formatted output keeps its parameters. The transformed body is wrapped in `[!! ... !!]` markers and padded with `~` to `ceil(bodyLength * ExpansionPercent / 100)` characters, with placeholders included in the length.
- **Culture matching.** The requested `CultureInfo.Name` is compared ordinal-ignore-case with `PseudoCulture`. The provider never resolves the pseudo code through `CultureInfo.GetCultureInfo`, because pseudo-code recognition is host-ICU-dependent while matching itself stays exact.
- **One culture only.** Only the configured synthetic culture is served. All other cultures, including the configured default culture, return `null`; the localizer's parent and default fallback steps still run.
- **Determinism.** The transform is a pure function of `(template, ExpansionPercent)`. There are no timestamps, no randomness, and no host-culture dependence, so output is identical across machines and runs.
- **Snapshot.** Source templates are copied at construction. Changes to the original dictionary or options after registration are not observed.
- **No reload or I/O.** There is no file watcher, `RefreshAsync`, or acquisition event.
- **WASM (WebAssembly) compatible.** `PseudoMessageProvider` declares `IWasmCompatibleMessageProvider`; lookup performs no I/O.
- **Not a translation.** Pseudo-localization deliberately produces ugly, expanded text. Use it in test or staging compositions, not as a production culture.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.
- **Diagnostics.** The package emits no Roslyn diagnostics. Unmatched placeholders and malformed templates remain the formatter's concern.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Core](../../Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Memory](../Jeninnet.Localization.Providers.Memory/README.md) - deterministic source maps for tests.
- [Jeninnet.Localization.Analyzers.Localizability](../../analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) - authoring-time detection of hardcoded strings.

## Documentation

- [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Testing your localization](../../../docs/en/02-guides/15-testing-your-localization.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT. See [LICENSE](../../../LICENSE).
