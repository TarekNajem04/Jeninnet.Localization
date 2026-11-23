# Jeninnet.Localization.Providers.Toml

File message provider that serves localized templates from flat TOML catalogs: one section-less or sectioned file per culture, `key = "value"` entries with string-only values, no nesting, no dependencies beyond BCL.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Toml --version 1.0.0-preview.3
```

## Registration

```csharp
services.AddTomlLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.toml");
});
```

## Configuration

`TomlLocalizationOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `FilePath` | `string` | `""` | Required. Must be non-empty and exist when the provider is constructed. |
| `WatchForChanges` | `bool` | `false` | Enables the opt-in file watcher. |
| `ReloadDebounce` | `TimeSpan` | 500 ms | Quiet window before reload. Must be positive when watching. |

```csharp
services.AddTomlLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.toml");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

Mutating the options object after registration does not affect the provider. A non-positive `ReloadDebounce` is rejected only when `WatchForChanges` is enabled.

## Usage

A catalog is a flat list of entries. Keys are message identifiers; values are templates:

```toml
# Store front strings
App.Title = "Bookstore"
Cart.Items = "You have {count} items."  # trailing comments are allowed

[Validation]
Required = "This field is required."
```

`[Validation]` qualifies the keys that follow it, so `Required` above is served as `Validation.Required`. Keys outside any section stay bare.

```csharp
services.AddTomlLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.toml")
    .AddCulture("ar", "Localization/ar.toml"));

var text = await localizer.LocalizeAsync(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = 3 },
    CultureInfo.GetCultureInfo("en"));
```

The result is `You have 3 items.` in the default formatter. A miss returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Loading.** The file is read lazily on the first lookup under synchronized one-shot initialization and parsed into the canonical Storage model plus an ordinal dictionary. A failed load is not cached, so a later lookup retries.
- **Exact matching.** Keys (including any `Section.` qualifier) are ordinal and case-sensitive. There is no normalization beyond surrounding-whitespace trimming.
- **Shape.** Only flat `key = value` entries with string values and optional `[Section]` headers are supported. There is no nesting, no dotted keys, no dotted or quoted sections, no multi-line values, no metadata, no notes, no contexts, and no plural table.
- **Values.** Basic strings (`"..."`) honor the escapes `\\`, `\"`, `\b`, `\f`, `\n`, `\r`, `\t`, `\uXXXX`, and `\UXXXXXXXX`; literal strings (`'...'`) are verbatim. Non-string values (numbers, booleans, dates, arrays, inline tables) are rejected. An empty value (`""`) is a valid empty template.
- **Comments.** Full-line `#` comments and trailing `#` comments after a value are ignored. A `#` inside a string is literal.
- **Malformed input.** A missing `=`, an empty or dotted key, an unclosed or non-bare section header, a non-string value, trailing content that is neither whitespace nor a comment, and a duplicate qualified key throw `System.IO.InvalidDataException` carrying the file path and the 1-based line number, on the first lookup; they are never converted into an empty provider. An empty file is a valid empty provider.
- **Duplicate keys.** Duplicates are rejected: the first occurrence wins nothing, the import fails with the offending line number instead.
- **Encoding.** UTF-8 (Unicode Transformation Format, 8-bit); a UTF-8 BOM (Byte Order Mark) is tolerated.
- **Hot reload.** With `WatchForChanges = true` the provider watches the file after the first successful load and swaps the whole template table on change. Reloads are debounced and atomic, never partial. A failed reload keeps the last-good table and raises `AcquisitionFailed`; a success raises `TranslationAcquired`. Both events carry `CultureInfo.InvariantCulture` because a file reload is not culture-scoped. Watching never throws onto the lookup path.
- **Refresh.** The concrete `TomlMessageProvider` implements `IRefreshableMessageProvider`. `RefreshAsync` reloads immediately and throws on failure.
- **Culture.** A single `TomlMessageProvider` ignores the supplied culture for exact-match lookup. The multi-culture overloads supply the culture dispatch layer; the localizer still owns parent and default-culture fallback.
- **Convention pitfall.** Auto-discovery treats the file stem as the culture code. A file named `en.final.toml` is culture `en.final`, not `en`; register it through the builder with an explicit path instead.
- **INI vs TOML.** The INI provider accepts `=` or `:` separators and treats trailing text as part of the value; the TOML provider accepts only `=` and treats trailing `#` text as a comment. Values are always quoted in TOML; non-string values are rejected.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md)
- [Jeninnet.Localization.Storage](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md)
- [Jeninnet.Localization.Providers.Json](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md)
- [Jeninnet.Localization.Providers.Ini](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Ini/README.md)
- [Jeninnet.Localization.Providers.Po](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/README.md)
- [Jeninnet.Localization.Providers.Xml](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/README.md)

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
