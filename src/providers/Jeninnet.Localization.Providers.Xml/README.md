# Jeninnet.Localization.Providers.Xml

`Jeninnet.Localization.Providers.Xml` serves message templates from XML documents. It implements `IInnerMessageProvider` through `XmlMessageProvider` and follows the file/static provider contract: one document is one flat `key -> template` map, lookups are exact-match, and culture fallback remains the responsibility of `DefaultMessageLocalizer`. The package also ships a multi-culture builder that wraps one provider per culture in `CultureAwareMessageProvider`. See [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md) for when an XML catalog fits an existing content pipeline.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Xml
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` and `Storage` arrive transitively.

## Registration

Single culture:

```csharp
using Jeninnet.Localization.Providers.Xml.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddXmlLocalization("Localization/en.xml");

services.AddXmlLocalization(options => {
    options.FilePath = "Localization/en.xml";
});
```

Multiple cultures with the convention `{baseDirectory}/{subdirectory}/{culture}.xml`:

```csharp
services.AddXmlLocalizationProviders(AppContext.BaseDirectory, new[] { "en", "ar" });
services.AddXmlLocalizationProviders(AppContext.BaseDirectory, new[] { "en", "ar" }, "Messages");
```

Auto-discovery of `*.xml` files under the subdirectory (the file stem becomes the culture code):

```csharp
services.AddXmlLocalizationProviders(AppContext.BaseDirectory);
```

Explicit per-culture paths through the builder:

```csharp
services.AddXmlLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.xml")
    .AddCulture("ar", "Localization/ar.xml"));
```

Every overload registers one singleton `IInnerMessageProvider`. A single-culture registration becomes one `XmlMessageProvider`; the multi-culture overloads register a `CultureAwareMessageProvider` that dispatches by the full culture name and then by the two-letter ISO language name.

Registration validates `services`, the options, and a non-empty `FilePath`. The provider constructor checks that the file exists, so a missing single-culture file throws `FileNotFoundException` during registration. Auto-discovery throws `DirectoryNotFoundException` when the directory is missing and `InvalidOperationException` when no XML files are found. The builder throws `InvalidOperationException` when no culture was added before `Build`.

## Configuration

`XmlLocalizationOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `FilePath` | `string` | `""` | Required. Must be non-empty and exist when the provider is constructed. |
| `WatchForChanges` | `bool` | `false` | Enables the opt-in file watcher. |
| `ReloadDebounce` | `TimeSpan` | 500 ms | Quiet window before reload. Must be positive when watching. |

```csharp
services.AddXmlLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.xml");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

Mutating the options object after registration does not affect the provider. A non-positive `ReloadDebounce` is rejected only when `WatchForChanges` is enabled.

## Usage

Each direct child element of the root is one entry. The element local name is the key; its text content is the template:

```xml
<?xml version="1.0" encoding="utf-8"?>
<messages>
  <App.Title>Bookstore</App.Title>
  <Cart.Items>You have {count} items.</Cart.Items>
  <Required>This field is required.</Required>
</messages>
```

```csharp
services.AddXmlLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.xml")
    .AddCulture("ar", "Localization/ar.xml"));

var text = await localizer.LocalizeAsync(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = 3 },
    CultureInfo.GetCultureInfo("en"));
```

The result is `You have 3 items.` in the default formatter. A miss returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Loading.** The document is parsed lazily on the first lookup with `System.Xml.Linq.XDocument.Load` under synchronized one-shot initialization, then mapped into the canonical Storage model plus an ordinal dictionary. A failed load is not cached, so a later lookup retries.
- **Key shape.** Keys are the child element `LocalName` values, so namespace prefixes do not change the key. Keys must be valid XML names. The root element name is not enforced.
- **Exact matching.** Keys are ordinal and case-sensitive. Keys and templates are not trimmed; element text is preserved verbatim.
- **Duplicate keys.** Duplicate child element names throw `ArgumentException` on the first lookup when the dictionary is built. This differs from JSON's last-wins parser behavior.
- **Malformed input.** Malformed XML, including a completely empty file, throws `System.Xml.XmlException` on the first lookup. A valid document with no child elements is an empty provider.
- **Content.** Nested elements inside a message are flattened into the element's text value by the platform parser. Comments are ignored. XML entities and numeric character references are decoded.
- **Encoding.** UTF-8 (Unicode Transformation Format, 8-bit), including a UTF-8 BOM (Byte Order Mark) and an XML declaration, and non-ASCII content.
- **Hot reload.** With `WatchForChanges = true` the provider watches the file after the first successful load and swaps the whole template table on change. Reloads are debounced and atomic, never partial. A failed reload keeps the last-good table and raises `AcquisitionFailed`; a success raises `TranslationAcquired`. Both events carry `CultureInfo.InvariantCulture`. Watching never throws onto the lookup path.
- **Refresh.** The concrete `XmlMessageProvider` implements `IRefreshableMessageProvider`. `RefreshAsync` reloads immediately and throws on failure.
- **Security.** External entity and external DTD resolution are not enabled; a declaration that references an external resource is not fetched during load.
- **Culture.** A single `XmlMessageProvider` ignores the supplied culture for exact-match lookup. The multi-culture overloads supply the culture dispatch layer; the localizer still owns parent and default-culture fallback.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Core](../../Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Json](../Jeninnet.Localization.Providers.Json/README.md) - the same lifecycle with JSON files.
- [Jeninnet.Localization.Providers.Po](../Jeninnet.Localization.Providers.Po/README.md) - contexts and plural forms.
- [Jeninnet.Localization.Providers.Satellite](../Jeninnet.Localization.Providers.Satellite/README.md) - compiled resources instead of source files.

## Documentation

- [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT. See [LICENSE](../../../LICENSE).
