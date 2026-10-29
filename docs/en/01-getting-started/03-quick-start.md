# 3. Quick Start

This walkthrough builds a small console application with two cultures, resolves messages by key,
switches the current UI culture, and demonstrates both parent-culture and default-culture fallback.
It uses the JSON provider because its multi-culture registration makes the culture behavior
visible without writing a custom provider.

## 1. Create the project

```powershell
dotnet new console -n LocalizationDemo
cd LocalizationDemo
dotnet add package Jeninnet.Localization.Core --source ..\artifacts
dotnet add package Jeninnet.Localization.Providers.Json --source ..\artifacts
```

Adjust the `--source` path if your local feed is elsewhere. If the packages have been published,
omit `--source`.

## 2. Create the catalogs

Create a `Localization` directory at the project root with two files.

`Localization/en.json`:

```json
{
  "App.Title": "Bookstore",
  "Greeting": "Hello, {name}!",
  "Cart.Items": "You have {count} items."
}
```

`Localization/ar.json`:

```json
{
  "App.Title": "مكتبة",
  "Greeting": "مرحبا، {name}!"
}
```

The Arabic catalog deliberately omits `Cart.Items`. That omission is what makes the fallback
demonstration meaningful. Each file is a flat object: keys are strings and values are templates.
There is no nesting, no metadata, and no culture hierarchy inside a file.

Make sure the files are copied to the output directory:

```xml
<ItemGroup>
  <None Update="Localization\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## 3. Register the pipeline and the provider

Replace `Program.cs` with the following program:

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Json.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// The canonical pipeline. The default culture is the last fallback step.
services.AddLocalization(options => options.DefaultCulture = "en");

// One provider registration. The multi-culture overload wraps one JSON provider
// per culture in a CultureAwareMessageProvider.
services.AddJsonLocalizationProviders(
    AppContext.BaseDirectory,
    ["en", "ar"]);

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var noParameters = new Dictionary<string, object?>();

// Exact culture: English.
Console.WriteLine(await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en")));

// Exact culture: Arabic.
Console.WriteLine(await localizer.LocalizeAsync(
    "App.Title",
    noParameters,
    new CultureInfo("ar")));

// Missing in Arabic, present in the default culture: fallback to English.
Console.WriteLine(await localizer.LocalizeAsync(
    "Cart.Items",
    new Dictionary<string, object?> { ["count"] = 3 },
    new CultureInfo("ar")));

// Switch the ambient UI culture and use the convenience overload.
CultureInfo.CurrentUICulture = new CultureInfo("ar");
Console.WriteLine(await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?> { ["name"] = "Ada" }));

// A complete miss returns null. No key, placeholder, or empty string is fabricated.
var missing = await localizer.LocalizeAsync("Missing.Key", noParameters, new CultureInfo("ar"));
Console.WriteLine(missing is null ? "(missing)" : missing);
```

Run it:

```powershell
dotnet run
```

Expected output:

```text
Hello, Ada!
مكتبة
You have 3 items.
مرحبا، Ada!
(missing)
```

The placeholders are substituted by `DefaultMessageFormatter`. `{name}` maps to the `name`
dictionary entry and `{count}` maps to `count`. Placeholders that are not present in the
dictionary are left as written; see
[Formatting and plurals](../02-guides/04-formatting-and-plurals.md).

## 4. Understand the fallback demonstration

The Arabic catalog has no `Cart.Items` entry. The lookup for `ar` therefore does this:

1. Ask the provider for `Cart.Items` in `ar`. The provider is exact-match and returns `null`.
2. The parent of `ar` is the invariant culture. The localizer never queries invariant, so the
   parent walk stops.
3. Ask the provider for `Cart.Items` in the configured default culture, `en`. It returns the
   English template.
4. Format the English template with the caller's parameters.

The result is English text on an Arabic request. That is the documented fallback behavior, not an
error. If you want to detect it, use the parity tooling, pseudo-localization, or the diagnostics
described in [Diagnostics and observability](../02-guides/14-diagnostics-and-observability.md).

A few more lookups make the chain concrete:

| Requested culture | Key | Provider queries, in order | Result |
|---|---|---|---|
| `ar` | `App.Title` | `ar` | Arabic title |
| `ar` | `Cart.Items` | `ar`, `en` | English template formatted |
| `fr-CA` | `App.Title` | `fr-CA`, `fr`, `en` | English title |
| `ar` | `Missing.Key` | `ar`, `en` | `null` |

The `fr-CA` row shows a multi-level parent walk. French is not configured, but the localizer still
queries the requested culture and each parent before reaching the default culture. The provider
does not perform that walk; the localizer does.

The `ar` row for `Missing.Key` shows the full miss path. The default culture is queried even though
the key was already missing in the requested culture, because "missing in Arabic" says nothing
about English. The result is `null`, not an exception.

## 5. Switch culture at runtime

The convenience overload uses `CultureInfo.CurrentUICulture`, not `CultureInfo.CurrentCulture`.
Those are different concepts: UI culture selects resources, culture controls formatting such as
numbers and dates. In a web application the request-localization middleware sets both per request.
In a console application you can set the UI culture directly:

```csharp
CultureInfo.CurrentUICulture = new CultureInfo("ar");
```

If you use `IMessageLocalizer` in a background job or a long-lived service, prefer the explicit
culture overload. An ambient culture can change between asynchronous operations, and a lookup
should use the culture the caller actually intended.

## 6. What just happened

The registration composed three things:

- `AddLocalization` registered `DefaultMessageLocalizer`, `DefaultMessageFormatter`,
  `LocalizationProviderSelector`, and the store-backed Microsoft localizer bridge.
- `AddJsonLocalizationProviders` created one `JsonMessageProvider` per culture and wrapped them in
  a single `CultureAwareMessageProvider`, registered as the one `IInnerMessageProvider`.
- `DefaultMessageLocalizer` performed the fallback chain and invoked the formatter only after a
  template resolved.

The store-backed bridge was registered but is unused here. It requires an `ILocalizationStore` and
is the path used by `IStringLocalizer`, MVC validation metadata, and the administration APIs. It is
not the path used by `IMessageLocalizer`.

**Pitfall:** do not add a second provider to "cover" the Arabic gaps. Two providers do not merge;
resolving `IMessageProvider` throws `InvalidOperationException`. Fill the catalog, change the
single provider, or consolidate sources before runtime.

**Pitfall:** the JSON file name is the culture code in the multi-culture convention. A file named
`ar.json` is culture `ar`; a file named `ar-SY.json` is culture `ar-SY`. The dispatcher first tries
the full requested name and then the two-letter language name, but it does not perform a full
parent walk. That remains the localizer's job. See
[Cultures and fallback](../02-guides/03-cultures-and-fallback.md).

The JSON walkthrough on this page is compiled and executed by
[`samples/Jeninnet.Localization.Docs`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Docs/README.md) as part of
the solution and CI, so the snippet is checked against the real packages.

## Where to go next

- Read [The mental model](04-mental-model.md) for the complete lookup lifecycle and the exactly-one
  provider rule.
- Read [Choosing a provider](../02-guides/01-choosing-a-provider.md) before committing to JSON for
  a production catalog.
- If you need runtime-editable catalogs, read
  [Resource sets and the store](../02-guides/06-resource-sets-and-store.md); it is a different
  lookup path from the one in this walkthrough.
- If you want compile-time key constants, read
  [Strongly typed keys](../02-guides/05-strongly-typed-keys.md).
