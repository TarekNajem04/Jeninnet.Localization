# Jeninnet.Localization.Providers.Memory

`Jeninnet.Localization.Providers.Memory` serves message templates from an in-memory `IReadOnlyDictionary<string, string>`. It is an `IInnerMessageProvider` implementation for tests, prototypes, small static catalogs, and browser WebAssembly compositions where no file or network I/O is acceptable. The provider sits at the edge of the message pipeline: `DefaultMessageLocalizer` owns culture fallback and calls the single registered provider for each step. See [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html) for where Memory fits among the other source families.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Memory
dotnet add package Jeninnet.Localization.Core
```

`Jeninnet.Localization.Core` supplies the localizer, formatter, and provider selector; `Jeninnet.Localization.Abstractions` arrives transitively.

## Registration

```csharp
using Jeninnet.Localization.Providers.Memory.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["App.Title"] = "Bookstore",
    ["Greeting"] = "Hello, {name}!"
});
```

The provider is registered as a singleton `IInnerMessageProvider`. The three overloads are:

- `AddMemoryLocalization(IReadOnlyDictionary<string, string> messages)`
- `AddMemoryLocalization(MemoryLocalizationOptions options)`
- `AddMemoryLocalization(Action<MemoryLocalizationOptions> configure)`

Null `services`, options, configure delegates, or `Messages` values throw `ArgumentNullException`. The provider copies the dictionary at construction, so mutating the caller-owned dictionary afterwards has no effect.

## Configuration

`MemoryLocalizationOptions` exposes one property:

| Property | Type | Default | Notes |
|----|----|----|----|
| `Messages` | `IReadOnlyDictionary<string, string>` | empty dictionary | Must not be null. Copied with `StringComparer.Ordinal`. |

```csharp
services.AddMemoryLocalization(options => {
    options.Messages = new Dictionary<string, string> {
        ["App.Title"] = "Bookstore"
    };
});
```

## Usage

Resolve `IMessageLocalizer` from the container and pass the key and parameters. The localizer formats the template after the provider resolves it.

```csharp
public sealed class WelcomeService(IMessageLocalizer localizer) {
    public ValueTask<string?> GreetAsync(CultureInfo culture) =>
        localizer.LocalizeAsync(
            "Greeting",
            new Dictionary<string, object?> { ["name"] = "Ada" },
            culture);
}
```

With the registration above and the `en` culture, the result is `Hello, Ada!`. A key that is absent from the dictionary returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Exact matching.** Keys are compared ordinally and case-sensitively. `App.Title` and `app.title` are different keys.
- **Construction snapshot.** The provider copies the supplied dictionary into a new `Dictionary<string, string>` using `StringComparer.Ordinal`. Later mutation of the options object or the original dictionary is not observed.
- **Culture is ignored.** One `MemoryMessageProvider` owns one flat `key -> template` map. The culture argument does not select or translate messages. Multi-culture compositions need one map per culture behind `CultureAwareMessageProvider`; Memory has no multi-culture registration overload.
- **No provider fallback.** The provider never walks parent cultures, applies a default culture, queries another provider, or synthesizes a message. Culture fallback belongs exclusively to `DefaultMessageLocalizer`.
- **No reload or I/O.** There is no file watcher, `RefreshAsync`, or acquisition event. Changing the catalog requires a new registration or a process restart.
- **WASM (WebAssembly) compatible.** `MemoryMessageProvider` declares `IWasmCompatibleMessageProvider`; lookup performs no I/O and the table is an immutable construction snapshot.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`. Resolving a second provider throws `InvalidOperationException`; with zero providers, lookups return `null`.
- **Cancellation.** A cancelled lookup throws `OperationCanceledException` before the dictionary is read.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) - pipeline and registration entry point.
- [Jeninnet.Localization.Providers.Json](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md) and [Jeninnet.Localization.Providers.Xml](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md) - repository-owned file catalogs.
- [Jeninnet.Localization.Providers.Pseudo](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Pseudo/README.md) - QA-only synthetic culture.

## Documentation

- [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html)
- [Configuring providers](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/02-configuring-providers.html)
- [Testing your localization](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/15-testing-your-localization.html)
- [Provider reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/03-provider-reference.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)

## License

MIT. See [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
