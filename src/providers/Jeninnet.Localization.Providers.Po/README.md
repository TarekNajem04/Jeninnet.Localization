# Jeninnet.Localization.Providers.Po

`Jeninnet.Localization.Providers.Po` serves message templates from gettext catalogs: PO (Portable Object) text files or compiled GNU MO (Machine Object) binaries. It implements `IInnerMessageProvider` and `IPluralFormMessageProvider` through `PoMessageProvider`, so it supports exact-match templates, `msgctxt` contexts, and count-aware plural forms. The provider follows the file/static contract: one catalog is one culture, and `DefaultMessageLocalizer` owns culture fallback. See [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md) for the translator-tooling scenario this package targets.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Po
dotnet add package Jeninnet.Localization.Core
```

`Core` supplies the pipeline; `Abstractions` and `Storage` arrive transitively.

## Registration

Single culture:

```csharp
using Jeninnet.Localization.Providers.Po.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddPoLocalization("Localization/en.po");

services.AddPoLocalization(options => {
    options.FilePath = "Localization/en.mo";
    options.Format = PoCatalogFormat.Mo;
});
```

Multiple cultures with the convention `{baseDirectory}/{subdirectory}/{culture}.po`:

```csharp
services.AddPoLocalizationProviders(AppContext.BaseDirectory, new[] { "en", "ar" });
services.AddPoLocalizationProviders(AppContext.BaseDirectory);
```

Explicit paths through the builder, or a custom `IPoFileLocationProvider`:

```csharp
services.AddPoLocalizationProviders(builder => builder
    .AddCulture("en", "Localization/en.po")
    .AddCulture("ar", "Localization/ar.mo"));

services.AddPoLocalizationProviders(locationProvider, new[] { "en", "ar" });
```

Every overload registers one singleton `IInnerMessageProvider`. The multi-culture overloads compose a `CultureAwareMessageProvider` that dispatches by full culture name and then by two-letter ISO language name. The default location provider `DefaultPoFileLocationProvider` resolves `{baseDirectory}/{subdirectory}/{culture}.po` and never probes for `.mo` files.

Registration validates `services`, the options, a non-empty `FilePath`, and that the file exists. Missing files throw `FileNotFoundException`. Auto-discovery throws `DirectoryNotFoundException` when the directory is missing and `InvalidOperationException` when no PO files are found.

## Configuration

`PoLocalizationOptions` is a construction snapshot.

| Property | Type | Default | Notes |
|----|----|----|----|
| `FilePath` | `string` | `""` | Required. PO text or MO binary path; must exist at registration. |
| `Format` | `PoCatalogFormat` | `Auto` | `Auto` reads `.mo` as MO (case-insensitive extension) and everything else as PO text. `Po` and `Mo` force a reader. |
| `WatchForChanges` | `bool` | `false` | Enables the opt-in file watcher. |
| `ReloadDebounce` | `TimeSpan` | 500 ms | Quiet window before reload. Must be positive when watching. |

```csharp
services.AddPoLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.po");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

An undefined `Format` value throws `ArgumentOutOfRangeException`. A non-positive `ReloadDebounce` is rejected only when `WatchForChanges` is enabled.

## Usage

A PO catalog holds `msgid`, optional `msgid_plural`, `msgctxt`, and `msgstr` entries:

```po
msgid ""
msgstr ""
"Content-Type: text/plain; charset=UTF-8\n"
"Plural-Forms: nplurals=2; plural=(n != 1);\n"

msgid "Required"
msgstr "This field is required."

msgctxt "menu"
msgid "Open"
msgstr "Ouvrir"

msgid "book"
msgid_plural "books"
msgstr[0] "livre"
msgstr[1] "livres"
```

```csharp
var text = await localizer.LocalizePluralAsync(
    "book",
    "books",
    3,
    new Dictionary<string, object?>(),
    CultureInfo.GetCultureInfo("fr"));
```

The provider selects the plural form from the catalog's `Plural-Forms` header. A miss returns `null` after the localizer's fallback chain has been exhausted.

## Behavior and limits

- **Loading.** The catalog is read lazily on the first lookup and parsed into `PoCatalog`, then mapped into the canonical Storage model. A failed load is not cached, so a later lookup retries.
- **Formats.** PO text supports multiline strings and C-style escapes (`\"`, `\\`, `\n`, `\t`, `\r`, `\b`, `\f`, `\v`, `\0`). MO binaries support magic `0x950412de` (little-endian) or `0xde120495` (big-endian), revisions 0.x/1.x; the hash table is ignored. MO strings are strict UTF-8 (Unicode Transformation Format, 8-bit); invalid bytes fail rather than being replacement-decoded.
- **Context.** `msgctxt` entries use the gettext EOT (End of Transmission) composite key `msgctxt + '\u0004' + msgid`, so `LocalizeWithContextAsync` interoperates without a second lookup path.
- **Plurals.** With a `Plural-Forms` header, `GetPluralTemplateAsync` and `GetPluralTemplateWithContextAsync` select the catalog's `msgstr[n]` form. Without one, the `PluralRules` pair mapping applies: `one` selects form 0 and every other category selects form 1. Plain `GetMessageTemplateAsync` exposes `msgid -> msgstr[0]` and `msgid_plural -> msgstr[1]`.
- **Empty translations.** `msgstr ""` is treated as untranslated, so the lookup returns the miss result and the fallback chain applies. This differs from JSON/XML, where an empty string is a valid template, and matches gettext semantics.
- **Fuzzy entries.** Entries marked `#, fuzzy` are skipped, matching gettext runtime behavior.
- **Malformed catalogs.** Bad keywords, unterminated strings, missing `msgstr`, non-contiguous `msgstr[n]`, duplicate keys, invalid `Plural-Forms`, `nplurals` mismatches, bad MO tables, and invalid UTF-8 throw `PoParseException` with the file path and a 1-based line number (0 for binaries). Malformed input is never converted into a partial table.
- **Key matching.** Exact, ordinal, and case-sensitive.
- **Hot reload.** With `WatchForChanges = true` the provider watches the file after the first successful load and swaps the whole catalog, including plural tables, on change. Reloads are debounced and atomic. A failed reload keeps the last-good catalog and raises `AcquisitionFailed`; a success raises `TranslationAcquired`. Both events carry `CultureInfo.InvariantCulture`.
- **Refresh.** The concrete `PoMessageProvider` implements `IRefreshableMessageProvider`. `RefreshAsync` reloads immediately and throws on failure.
- **Culture.** A single `PoMessageProvider` ignores the supplied culture for exact-match lookup and passes it through only for plural fallback selection. The multi-culture overloads supply culture dispatch; the localizer still owns parent and default-culture fallback.
- **One provider per composition.** Core resolves exactly one `IInnerMessageProvider`; registering a second source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Core](../../Jeninnet.Localization.Core/README.md) - pipeline and fallback.
- [Jeninnet.Localization.Providers.Json](../Jeninnet.Localization.Providers.Json/README.md) and [Jeninnet.Localization.Providers.Xml](../Jeninnet.Localization.Providers.Xml/README.md) - flat catalogs without contexts or plural tables.
- [Jeninnet.Localization.Interchange.Po](../../interchange/Jeninnet.Localization.Interchange.Po/README.md) - export and import for translator round trips.

## Documentation

- [Choosing a provider](../../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Formatting and plurals](../../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Translator workflow](../../../docs/en/02-guides/11-translator-workflow.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT. See [LICENSE](../../../LICENSE).
