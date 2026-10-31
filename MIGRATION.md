# Migrating to Jeninnet.Localization 1.0.0-preview.1

`1.0.0-preview.1` is the first public release of Jeninnet.Localization. There is no earlier
Jeninnet.Localization package to upgrade from.

This guide is for teams adopting the framework in an application that already localizes with
`Microsoft.Extensions.Localization`, ResX resource files, `ResourceManager`, or a hand-rolled
localization layer. It explains the model, maps the existing concepts, shows the registration swap,
migrates the data, and lists the behavioral differences that need a decision before you delete the
old setup.

If you are starting a new application, read
[Installation](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/02-installation.html)
and [Quick start](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/03-quick-start.html)
instead. Most of this guide does not apply to a greenfield project.

> **Preview release.** `1.0.0-preview.1` is complete enough to build and evaluate against, but it is
> not a stable `1.0.0`. Pin the exact preview version in your project files, review
> [CHANGELOG.md](CHANGELOG.md) before every upgrade, and assume that a later preview may adjust the
> public surface.

---

## 1. Understand the two layers first

The largest migration mistake is treating Jeninnet.Localization as a drop-in replacement for
`ResourceManager`. It keeps the `IStringLocalizer` interfaces, but the behavior behind them belongs
to a different model.

| Layer | Contract | What it owns |
|---|---|---|
| **Message pipeline** | `IMessageLocalizer` + `IMessageProvider` | Key lookup, the fallback chain, formatting, plurals, and the optional machine-translation providers. |
| **Resource-set store** | `ILocalizationStore` + the `IStringLocalizer` bridge | Named, mutable resource sets administered through a governed API, with audit, history, notes, and versioning. |

The pipeline resolves exactly one registered message provider. Adding a second provider is a
configuration error, not a fallback layer. Culture fallback lives in `DefaultMessageLocalizer`, not in
the provider.

The store is a separate dependency. `AddLocalization()` registers the `IStringLocalizerFactory`
bridge, but it does not register a store. Without a store, `IStringLocalizer<T>` cannot resolve a
resource set. If you only use `IMessageLocalizer`, you do not need a store at all.

The two surfaces do not share data. A JSON catalog registered with the pipeline does not feed
`IStringLocalizer`, and a store entry does not feed `IMessageLocalizer`. Keep both catalogs aligned
deliberately if you use both surfaces.

Read [The mental model](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/04-mental-model.html)
before changing production code.

---

## 2. Choose the target composition

| Your application | Packages to add | Store required? |
|---|---|---|
| Key-driven messages through `IMessageLocalizer` | `Jeninnet.Localization.Core` + exactly one message provider | No |
| Existing `IStringLocalizer` / `IStringLocalizer<T>` code | `Core` + a store (`Providers.Database.EntityFrameworkCore`) | Yes |
| ASP.NET Core request cultures | `Core` + `Integrations.RequestLocalization` (+ store or explicit cultures) | Usually |
| MVC validation, HTML localizers, Razor views | `Core` + `Integrations.Mvc.Validation` (+ store) | For `IStringLocalizer` yes |
| Blazor Server / WASM / Hybrid | `Core` + `Integrations.Blazor` (+ provider and store) | Depends on the surface |
| Avalonia / MAUI / WinUI culture persistence | `Core` + the host integration | No |
| Machine translation | `Core` + `Translation.Azure` or `Translation.Google` as the single provider | No |
| Translator hand-off | the matching `Interchange.*` package | No |

The complete inventory, dependencies, and "which package do I need?" guidance are in the
[package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html).

---

## 3. Install the packages

Pin the exact preview version. A floating version can silently pick up a later preview:

```powershell
dotnet add package Jeninnet.Localization.Core --version 1.0.0-preview.1
dotnet add package Jeninnet.Localization.Providers.Json --version 1.0.0-preview.1
```

For a store-backed web application:

```powershell
dotnet add package Jeninnet.Localization.Providers.Database.EntityFrameworkCore --version 1.0.0-preview.1
dotnet add package Jeninnet.Localization.Integrations.RequestLocalization --version 1.0.0-preview.1
dotnet add package Jeninnet.Localization.Integrations.Mvc.Validation --version 1.0.0-preview.1
```

`dotnet add package` needs `--prerelease` when you do not pass an exact version. Passing the exact
version is the recommended form for a preview.

---

## 4. Replace the registration

### Before (Microsoft.Extensions.Localization)

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews();
```

### After (Jeninnet.Localization)

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;

builder.Services.AddControllersWithViews();

// The message pipeline and the store-backed IStringLocalizer factory.
builder.Services.AddLocalization(options => options.DefaultCulture = "en");

// The store is a separate registration and is what makes IStringLocalizer<T> resolvable.
builder.Services.AddEntityFrameworkLocalizationStore(
    builder.Configuration,
    connectionStringName: "LocalizationDb");

// MVC validation/display metadata, HTML localizers, and view localization.
builder.Services.AddMvcLocalizedValidation();

// Request cultures from the store, narrowed to the served set.
builder.Services.AddLocalizationRequestCultures();

var app = builder.Build();
app.UseRequestLocalization();
```

Three details matter here:

1. **`AddLocalization` is ambiguous by name.** It exists in both
   `Microsoft.Extensions.DependencyInjection` and `Jeninnet.Localization.Core.Extensions`. The
   configuring overload above is unambiguous because `DefaultCulture` exists only on the Jeninnet
   options type. For the parameterless overload, call the static class explicitly:
   `Jeninnet.Localization.Core.Extensions.ServiceCollectionExtensions.AddLocalization(services)`.
   Do not call both registrations and expect them to merge; the last factory registration wins.
2. **`AddLocalization` does not create a store.** Add `Providers.Database.EntityFrameworkCore` (or
   your own `ILocalizationStore` implementation) before resolving `IStringLocalizer`.
3. **`UseRequestLocalization()` still belongs in the pipeline.** `AddLocalizationRequestCultures`
   configures the framework middleware; it does not install it.

If the application only uses `IMessageLocalizer`, drop the store and add one provider instead:

```csharp
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
```

---

## 5. Move the resource data

### 5.1 Map ResX concepts

| ResX / ResourceManager | Jeninnet.Localization |
|---|---|
| One `.resx` file per culture | One `(resourceSet, culture)` pair |
| ResX entry name | Message key, unchanged |
| ResX value | Message template |
| ResX comment | Translator note (optional) |
| Neutral ResX file | Usually the default culture |
| `ResourcesPath` | No equivalent; paths are explicit in provider or store registration |
| `Create(Type)` uses the type's full name | `Create(Type)` uses `Type.Name`; namespaces are not part of the resource-set name |
| `Create(baseName, location)` scopes by location | The base name is the whole identity; the location is validated and ignored |
| Parent-culture lookup in `ResourceManager` | The pipeline walks parents; the store bridge does **not** |

Choose the resource-set name before importing anything. `Create(Type)` maps `HomeController` to the
resource set `HomeController`. Two types with the same name in different namespaces collide; give one
of them an explicit base name or rename the resource set.

### 5.2 Import ResX into the store

`Jeninnet.Localization.Interchange.ResX` parses and validates ResX without touching the database.
Apply the parsed entries through the governed admin API, preview first, then commit with a real
actor:

```csharp
using Jeninnet.Localization.Abstractions.Store;
using Jeninnet.Localization.Interchange.ResX;

var resxPath = "Resources/HomeController.ar.resx";
var resxText = await File.ReadAllTextAsync(resxPath);
var document = ResXCatalogInterchange.ParseDocument(resxText, resxPath);

var admin = store.RequireAdmin();
var options = new ResourceAdminOptions { Actor = "resx-migration", DryRun = true };

foreach (var entry in document.Entries.Where(e => e.IsStringResource && e.Value is not null)) {
    var preview = await admin.CreateEntryAsync(
        "HomeController", "ar", entry.Name, entry.Value!, options);

    Console.WriteLine(
        $"{entry.Name}: applied={preview.Applied}, conflict={preview.Conflict?.Kind}");
}

// Review the preview, then set DryRun = false and run the same loop.
```

`CreateEntryAsync` reports `AlreadyExists` rather than throwing, so a second run is idempotent. A
production migration should keep the actor, keep the dry-run output as an artifact, and only then
apply.

If you need translator notes from ResX comments, call `SetNoteAsync` after the entries exist:

```csharp
foreach (var entry in document.Entries.Where(e => e.Comment is not null)) {
    await admin.SetNoteAsync("HomeController", entry.Name, entry.Comment!, new ResourceAdminOptions {
        Actor = "resx-migration"
    });
}
```

Notes are key-level and culture-agnostic. They follow renames and key deletion automatically; see
[Audit and history](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/07-audit-and-history.html).

### 5.3 Or move to a file catalog

If you do not need runtime administration, a flat JSON catalog is simpler. One file per culture:

```json
{
  "HomeController.Title": "Home",
  "HomeController.Welcome": "Welcome, {name}!"
}
```

Register it explicitly:

```csharp
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
```

The JSON catalog is pipeline data only. It does not feed `IStringLocalizer`. If the application has
both surfaces, seed both deliberately.

### 5.4 Keep the runtime ResX path if you need it

`Jeninnet.Localization.Providers.ResX` serves embedded `.resx` resources through `ResourceManager`
as a message provider. It is a legitimate runtime option when a large compiled ResX investment must
stay in place. It does not feed `IStringLocalizer`; the bridge still needs the store. Do not confuse
the runtime provider with the interchange package, which is a tooling hand-off format.

---

## 6. Decide the key and fallback strategy

### Keys

ResX workflows often use English text or property names as keys. That becomes a liability in a
key-driven catalog:

- translators see the key in PO/XLIFF and may translate it;
- renaming a text key becomes a catalog migration;
- two screens with the same text collapse into one entry;
- keys appear in traces, missing-key sinks, and parity reports.

Adopt dotted keys such as `Checkout.Name.Required` and `Cart.Items.Empty`, and use contexts where the
same key has different meanings:

```csharp
var open = await localizer.LocalizeWithContextAsync(
    "Open", "FileMenu", parameters, culture);
```

The context is namespaced as `context + EOT + key`, which matches gettext `msgctxt` namespacing.

### Fallback

The pipeline resolves the requested culture, its parent cultures, then the configured default
culture. The invariant culture is never queried. Providers are exact-match only; they never walk
parents themselves.

The store bridge is different: it captures the current UI culture when the localizer is created and
resolves that exact resource set on every lookup. It does not walk parent cultures. A missing entry
returns `[MISSING:key]` with `ResourceNotFound = true`.

Practical consequences:

- If the application depends on parent-culture fallback for `IStringLocalizer`, seed every served
  culture or move those lookups to `IMessageLocalizer`.
- UI tests that expected the key name on a miss must expect `[MISSING:key]`.
- `GetAllStrings` enumerates the exact culture only.
- `LocalizationOptions.DefaultCulture` is captured when the localizer is constructed; mutating the
  options object later has no effect.

### Plural and context APIs

The pipeline adds gettext-style plural selection and context lookup:

```csharp
var items = await localizer.LocalizePluralAsync(
    "Cart.Item.One", "Cart.Item.Other", count, parameters, culture);

var open = await localizer.LocalizeWithContextAsync(
    "Open", "FileMenu", parameters, culture);
```

Plural selection uses the CLDR cardinal category for the requested culture. When only one of the two
keys exists, the available form is returned; when neither exists, the result is `null`. The count is
not injected into the parameters, so templates that render it must receive it explicitly.

---

## 7. Move formatting to the canonical pipeline

| Concern | Microsoft path | Jeninnet path |
|---|---|---|
| Placeholder syntax on `IStringLocalizer` | Composite `{0}`, `{1}` | Unchanged: the bridge uses composite formatting |
| Placeholder syntax on `IMessageLocalizer` | — | Named `{name}` by default |
| ICU MessageFormat | — | Opt-in `IcuMessageFormatter` (`plural`, `select`, `selectordinal`, `number`, `date`, `time`, nesting) |
| CLDR plural rules | — | `LocalizePluralAsync` / `PluralRules` (51 languages plus English ordinals) |
| HTML-safe arguments | — | `HtmlSafeMessageFormatter`, `IHtmlLocalizer` bridge |
| Markdown subset | — | `MarkdownMessageFormatter` |
| Razor inline localization | — | `<loc key="...">` tag helper |

Register a formatter before `AddLocalization()` because the default is registered with
`TryAddSingleton`:

```csharp
services.AddSingleton<IMessageFormatter>(
    new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter())));
services.AddLocalization(options => options.DefaultCulture = "en");
```

ICU is opt-in. Templates without ICU constructs render exactly as before. Malformed ICU templates
throw a descriptive `FormatException`; they do not silently fall back.

Do not pass untrusted catalog content or user-authored templates into the pipeline. Templates are
trusted content. See [Security](https://tareknajem04.github.io/Jeninnet.Localization/en/04-operations/02-security.html).

---

## 8. Replace validation, views, and request cultures

### Validation attributes

Replace framework attributes with the localized family, or keep the framework attributes and let
the MVC metadata provider localize them:

```csharp
using Jeninnet.Localization.Integrations.DataAnnotations;

public sealed class CheckoutModel {
    [LocalizedRequired("Checkout.Name.Required")]
    public string Name { get; set; } = string.Empty;

    [LocalizedStringLength(3, 40, "Checkout.Name.Length")]
    public string DisplayName { get; set; } = string.Empty;
}
```

The localized attributes resolve their messages through the store-backed
`IStringLocalizerFactory`. They need the store; a pipeline-only composition compiles and then fails
at validation time. If the factory is missing, the attribute throws a configuration error rather
than falling back to hard-coded English.

### Razor views

Keep the view code and let the bridge resolve keys:

```razor
@inject IViewLocalizer Localizer

<h1>@Localizer["Home.Title"]</h1>

<loc key="Cart.Title">Shopping Cart</loc>
```

`AddViewMessageLocalizer()` installs the view-scoped localizer; `AddHtmlMessageLocalizer()` installs
the `IHtmlLocalizer` factory and generic localizer. The tag helper removes the `<loc>` element and
falls back to the authored body on a miss.

### Request cultures

```csharp
builder.Services.AddLocalizationRequestCultures();

var app = builder.Build();
app.UseRequestLocalization();
```

The provider order is route value, query string, cookie, then `Accept-Language`. Results are narrowed
to the served set. The served set comes from explicit `SupportedCultures` or from
`ILocalizationStore.GetSupportedCulturesAsync()`. An application with pipeline catalogs but no store
collapses to the default culture and ignores `Accept-Language`; register a store or declare the
served cultures explicitly. A reachable store that reports zero cultures fails startup, which is
intentional.

---

## 9. Desktop and Blazor hosts

### Avalonia, MAUI, WinUI

The host integrations provide a shared culture contract, platform persistence, initialization,
change notifications, and RTL helpers:

```csharp
services.AddAvaloniaLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});
```

The exact registration name follows the host package (`AddAvaloniaLocalization`,
`AddMauiLocalization`, `AddWinUiLocalization`). The service applies the selected culture to the host
and persists the user's explicit choice through the platform store.

### Blazor

Choose the scope explicitly:

- `AddBlazorServerLocalization(...)` for Blazor Server; culture is circuit-scoped and the ambient
  culture is never mutated.
- `AddBlazorWasmLocalization(...)` for client-side hosts; it also rejects providers that do not
  declare `IWasmCompatibleMessageProvider` (file and database providers do not).
- For Blazor Hybrid/WebView2, register the culture service and store as scoped and use
  `AddBlazorLocalization(options => options.Mode = BlazorLocalizationMode.Wasm)` so the scope
  follows the WebView.

The Blazor integration persists explicit user choices through an `IBlazorCultureStore`. It ships
cookie and local-storage stores. Browser-language detection is opt-in and only applies in static
scope; server mode stays seeded from the resolved request culture.

---

## 10. Add the tooling and CI gates

### Strongly typed keys

Reference the generator as a build-time analyzer and declare `.loc.json` catalogs:

```xml
<ItemGroup>
  <PackageReference Include="Jeninnet.Localization.Generators.Keys"
                    Version="1.0.0-preview.1"
                    PrivateAssets="all" />
  <AdditionalFiles Include="Localization\en.loc.json" />
  <AdditionalFiles Include="Localization\ar.loc.json" />
</ItemGroup>
```

The generator emits `const string` keys. Typos become build errors, and renames produce
solution-wide compile errors instead of silent misses.

### Localizability analyzer

`Jeninnet.Localization.Analyzers.Localizability` reports `JLNLOC001` for hardcoded user-facing
strings and offers a code fix when the literal matches a generated key.

### Parity tool

`Jeninnet.Localization.Tools.Keys` is the `jeninnet-localization-keys` command:

```powershell
dotnet tool install --global Jeninnet.Localization.Tools.Keys --version 1.0.0-preview.1

jeninnet-localization-keys `
  --catalog ./Localization `
  --source ./src `
  --default-culture en `
  --report artifacts/parity.json
```

It reports missing keys, unused keys, template drift, format gaps, placeholder drift, unresolved
references, definition conflicts, and unreadable catalogs with file/line precision. The report
schema is version 2. The tool is report-only and exits non-zero when findings exist, so it fits a CI
gate. See [Parity and quality gates](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/13-parity-and-quality-gates.html).

---

## 11. Verify before deleting the old setup

Run through this list in a branch:

- [ ] Every `IStringLocalizer<T>` resolution has an `ILocalizationStore`.
- [ ] Resource-set names match `Type.Name` or the explicit base name used at the call site.
- [ ] Every served culture has entries for the keys its users need; parent fallback is not assumed.
- [ ] Missing entries render `[MISSING:key]`; UI tests and snapshots are updated.
- [ ] Arguments on the `IStringLocalizer` path use composite `{0}` formatting, not named placeholders.
- [ ] Pipeline templates use the syntax of the formatter you registered (named `{name}` by default).
- [ ] Validation attributes use the localized family or the MVC metadata provider, and their keys
      exist in the store.
- [ ] `DisplayAttribute` names, descriptions, and prompts are keys in the store or use
      `ResourceType` deliberately.
- [ ] Request localization has a store or explicit supported cultures; `Accept-Language` selects the
      expected culture.
- [ ] Scoped and transient consumers do not capture a localizer in a singleton.
- [ ] `GetAllStrings` behavior is acceptable: the bridge enumerates the exact culture only.
- [ ] The parity tool reports no missing keys or placeholder drift for the migrated catalog.
- [ ] The startup registration is exercised by an integration test, not only by a unit test with a
      hand-built service provider.
- [ ] A rollback plan exists: keep the old registration and catalogs until one release has shipped.

A production migration should also:

- back up the store database before the first import;
- run the import with `DryRun = true` and keep the report;
- keep the actor identity on every mutation so the audit trail names the migration;
- deploy content files with `CopyToOutputDirectory` or an equivalent packaging rule;
- verify the health checks (`AddLocalizationHealthChecks`) and the diagnostics names before turning
  on dashboards.

---

## 12. Troubleshooting

| Symptom | Likely cause | Action |
|---|---|---|
| `InvalidOperationException` naming two providers | Two message providers registered | Keep exactly one provider registration |
| `InvalidOperationException` about `LocalizationOptions` | Both `AddLocalization` overloads called | Use one overload, or pre-register a single options instance |
| `IStringLocalizer<T>` cannot resolve a resource set | No `ILocalizationStore` registered | Add the EF store or a custom store |
| Everything renders in the default culture | No store and no explicit `SupportedCultures` for request localization | Register the store or declare the served cultures |
| `[MISSING:key]` where ResX used to fall back | The store bridge does not walk parents | Seed the served culture or use `IMessageLocalizer` |
| Validation messages are English or a configuration error | Localized attributes cannot find the factory | Register `AddLocalization` and the store; use `AddMvcLocalizedValidation` for plain attributes |
| `FileNotFoundException` at startup | JSON/XML/PO path validated eagerly | Fix the content path and the copy-to-output rule |
| `MissingManifestResourceException` on first lookup | ResX base name does not match the embedded resource | Correct the base name; ResX cannot validate at registration |
| Templates render `{name}` literally | The lookup went through `IStringLocalizer` (composite) instead of `IMessageLocalizer` | Use `{0}` on the bridge, or move the lookup to the pipeline |
| ICU template throws `FormatException` | Malformed or unsupported ICU construct | Fix the template; the formatter does not silently fall back |

---

## 13. Rollback

The migration is designed to be reversible while the old registration and catalogs still exist:

1. Keep the previous `Microsoft.Extensions.Localization` registration and ResX files in source
   control until one release has shipped.
2. Keep the store import script and its dry-run report.
3. If the new composition fails, revert the registration commit first; the data import is additive
   and does not modify ResX files.
4. If the importer created entries that must be removed, use the governed delete path with the same
   actor and keep the audit record. Do not delete rows directly.

---

## 14. Where to read next

- [Installation and first registration](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/02-installation.html)
- [Quick start](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/03-quick-start.html)
- [The mental model](https://tareknajem04.github.io/Jeninnet.Localization/en/01-getting-started/04-mental-model.html)
- [Cultures and fallback](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/03-cultures-and-fallback.html)
- [Formatting and plurals](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/04-formatting-and-plurals.html)
- [Strongly typed keys](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/05-strongly-typed-keys.html)
- [Resource sets and the store](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/06-resource-sets-and-store.html)
- [ASP.NET Core and MVC](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/08-aspnet-core-and-mvc.html)
- [Blazor](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/09-blazor.html)
- [Translator workflow](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html)
- [Parity and quality gates](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/13-parity-and-quality-gates.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)
- [Known limitations](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/10-known-limitations.html)
- [Full documentation](https://tareknajem04.github.io/Jeninnet.Localization/)
- [Release notes](https://github.com/TarekNajem04/Jeninnet.Localization/releases/tag/v1.0.0-preview.1)
- [Changelog](CHANGELOG.md)
