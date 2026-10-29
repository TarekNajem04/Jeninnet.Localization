# Jeninnet.Localization.Providers.Json

`Jeninnet.Localization.Providers.Json` serves message templates from flat JSON files. It implements `IInnerMessageProvider` through `JsonMessageProvider` and follows the file/static provider contract: one catalog is one flat `key -> template` map, lookups are exact-match, and culture fallback remains the responsibility of `DefaultMessageLocalizer`. The package also ships a multi-culture builder that wraps one provider per culture in `CultureAwareMessageProvider`. See [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md) for when a repository-owned JSON catalog is the right source.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Json
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` and `Storage` arrive transitively.

## Registration

Single culture:

```csharp
using Jeninnet.Localization.Providers.Json.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalization("Localization/en.json");

services.AddJsonLocalization(options => {
    options.FilePath = "Localization/en.json";
});
```

Multiple cultures with the convention `{baseDirectory}/{subdirectory}/{culture}.json`:

```csharp
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, new[] { "en", "ar" });
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, new[] { "en", "ar" }, "Messages");
```

Auto-discovery of `*.json` files under the subdirectory (the file stem becomes the culture code):

```csharp
services.AddJsonLocalizationProviders(AppContext.BaseDirectory);
```

Explicit per-culture paths through the builder:

```csharp
services.AddJsonLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.json")
    .AddCulture("ar", "Localization/ar.json"));
```

Every overload registers one singleton `IInnerMessageProvider`. A single-culture registration becomes one `JsonMessageProvider`; the multi-culture overloads register a `CultureAwareMessageProvider` that dispatches by the full culture name and then by the two-letter ISO language name.

Registration validates `services`, the options, and a non-empty `FilePath`. A missing single-culture file throws `FileNotFoundException`. Auto-discovery throws `DirectoryNotFoundException` when the directory is missing and `InvalidOperationException` when no JSON files are found. The builder throws `InvalidOperationException` when no culture was added before `Build`.

## Configuration

`JsonLocalizationOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `FilePath` | `string` | `""` | Required. Must be non-empty and exist when the provider is constructed. |
| `WatchForChanges` | `bool` | `false` | Enables the opt-in file watcher. |
| `ReloadDebounce` | `TimeSpan` | 500 ms | Quiet window before reload. Must be positive when watching. |

```csharp
services.AddJsonLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

Mutating the options object after registration does not affect the provider. A non-positive `ReloadDebounce` is rejected only when `WatchForChanges` is enabled.

## Usage

A catalog is a flat JSON object. Keys are message identifiers; values are templates:

```json
{
  "App.Title": "Bookstore",
  "Cart.Items": "You have {count} items.",
  "Required": "This field is required."
}
```

```csharp
services.AddJsonLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.json")
    .AddCulture("ar", "Localization/ar.json"));

var text = await localizer.LocalizeAsync(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = 3 },
    CultureInfo.GetCultureInfo("en"));
```

The result is `You have 3 items.` in the default formatter. A miss returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Loading.** The file is read lazily on the first lookup under synchronized one-shot initialization and parsed into the canonical Storage model plus an ordinal dictionary. A failed load is not cached, so a later lookup retries.
- **Exact matching.** Keys are ordinal and case-sensitive. There is no normalization or trimming.
- **Shape.** Only a flat JSON object of string keys to string templates is supported. There is no nested structure, metadata, notes, contexts, or plural table.
- **Malformed input.** Malformed JSON and non-string values throw `System.Text.Json.JsonException` on the first lookup; they are never converted into an empty provider. A JSON `null` value behaves as an absent key. An empty object is a valid empty provider. An empty string is a valid template.
- **Duplicate keys.** The last occurrence wins for the platform dictionary parser.
- **Encoding.** UTF-8 (Unicode Transformation Format, 8-bit); a UTF-8 BOM (Byte Order Mark) is tolerated.
- **Hot reload.** With `WatchForChanges = true` the provider watches the file after the first successful load and swaps the whole template table on change. Reloads are debounced and atomic, never partial. A failed reload keeps the last-good table and raises `AcquisitionFailed`; a success raises `TranslationAcquired`. Both events carry `CultureInfo.InvariantCulture` because a file reload is not culture-scoped. Watching never throws onto the lookup path.
- **Refresh.** The concrete `JsonMessageProvider` implements `IRefreshableMessageProvider`. `RefreshAsync` reloads immediately and throws on failure.
- **Culture.** A single `JsonMessageProvider` ignores the supplied culture for exact-match lookup. The multi-culture overloads supply the culture dispatch layer; the localizer still owns parent and default-culture fallback.
- **Convention pitfall.** Auto-discovery treats the file stem as the culture code. A generator input named `en.loc.json` is culture `en.loc`, not `en`; register it through the builder with an explicit path instead.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Core](../../Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Xml](../Jeninnet.Localization.Providers.Xml/README.md) - the same lifecycle with XML documents.
- [Jeninnet.Localization.Providers.Po](../Jeninnet.Localization.Providers.Po/README.md) - contexts and plural forms.
- [Jeninnet.Localization.Generators.Keys](../../generators/Jeninnet.Localization.Generators.Keys/README.md) - compile-time constants from `*.loc.json` catalogs.

## Documentation

- [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Strongly typed keys](../../../docs/en/02-guides/05-strongly-typed-keys.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT. See [LICENSE](../../../LICENSE).
