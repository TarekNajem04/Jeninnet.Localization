# Migration from Microsoft.Extensions.Localization

An application already using `Microsoft.Extensions.Localization` has `IStringLocalizer`,
`IStringLocalizer<T>`, `IStringLocalizerFactory`, and usually ResX files. The interfaces are the same
ones this framework bridges to, which makes the migration look smaller than it is. The behavior behind
them changes.

This chapter maps the concepts, swaps the registration, and lists the differences that break code
silently. It assumes the application can tolerate a migration in one release; a per-type hybrid is
not built in.

## Concept mapping

| Microsoft.Extensions.Localization | Jeninnet.Localization |
|---|---|
| `IStringLocalizer` / `IStringLocalizer<T>` | Same interfaces, backed by `CustomStringLocalizerFactory` over `ILocalizationStore`. |
| `IStringLocalizerFactory` | Replaced with `CustomStringLocalizerFactory` by `AddLocalization()`. |
| `ResourceManagerStringLocalizerFactory` | No equivalent in the bridge; ResX remains a runtime option only through the message pipeline. |
| ResX base name / `ResourcesPath` | Resource-set name. `Create(Type)` uses `Type.Name`; `Create(baseName, location)` uses the exact base name. There is no `ResourcesPath` option. |
| `LocalizationOptions.ResourcesPath` | No equivalent. Paths are explicit in provider registration. |
| `LocalizationOptions` (Microsoft) | `Jeninnet.Localization.Abstractions.Options.LocalizationOptions` with `DefaultCulture` and `MissingKeySink`. |
| Satellite assemblies / `ResourceManager` | `Providers.ResX` or `Providers.Satellite` for the pipeline; the store bridge is separate. |
| `RequestLocalizationMiddleware` | Same middleware; `AddLocalizationRequestCultures` can populate its options. |
| `[Required(ErrorMessage = "...")]` | `[LocalizedRequired("Key")]` or the MVC metadata provider for plain `RequiredAttribute`. |
| Missing value: the name, `ResourceNotFound = true` | `[MISSING:key]`, `ResourceNotFound = true` through the store bridge. |
| Parent-culture fallback in ResourceManager | No fallback in the store bridge; fallback lives in `IMessageLocalizer`. |

The largest behavioral difference is fallback. Microsoft's resource manager can resolve a parent
culture's resource when the exact culture is absent. The store bridge resolves exactly the culture
captured at localizer creation. If the application depends on parent fallback, either seed every
served culture or move those lookups to the pipeline.

## Registration swap

A typical Microsoft composition looks like this:

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews();
```

The Jeninnet composition has a different shape because the store is a separate dependency:

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;
using JeninnetLocalizationOptions = Jeninnet.Localization.Abstractions.Options.LocalizationOptions;

builder.Services.AddControllersWithViews();

builder.Services.AddLocalization(options => options.DefaultCulture = "en");

builder.Services.AddEntityFrameworkLocalizationStore(
    builder.Configuration,
    connectionStringName: "LocalizationDb");

builder.Services.AddMvcLocalizedValidation();
builder.Services.AddLocalizationRequestCultures();
```

The store registration is mandatory for `IStringLocalizer<T>`. Without it, the factory is registered
but cannot resolve a resource set. The message pipeline alone does not need a store.

**Pitfall:** `AddLocalization` exists in both `Microsoft.Extensions.DependencyInjection` and
`Jeninnet.Localization.Core.Extensions`. If both namespaces are imported, the call is ambiguous. Use
an alias for the options type and qualify the extension call, or keep the namespaces separated.

## Data migration

ResX content is keyed by `data name` and carries a value and optional comment. A resource set is keyed
by `(resourceSet, culture, name)`. The mapping is mechanical but needs decisions:

1. **Choose the resource-set name.** `Create(Type)` uses `Type.Name`, so the natural mapping is the
   type name without its namespace. If two types share a name in different namespaces, they collide.
   Choose explicit base names or rename the resource sets.
2. **Choose the key.** The ResX entry name becomes the message key unchanged. This is where
   text-as-key catalogs become awkward; consider introducing dotted keys now rather than later.
3. **Choose the culture.** One ResX file per culture maps to one `(set, culture)` pair. The neutral
   ResX file usually maps to the default culture.
4. **Choose the store or a catalog.** A runtime-editable store takes entries through the governed
   API; a file provider takes a flat JSON or XML catalog. Both are valid, and they are different
   paths.

A small seeding utility can parse a ResX file and create entries:

```csharp
var document = ResXCatalogInterchange.ParseDocument(
    await File.ReadAllTextAsync("Resources/HomeController.ar.resx"),
    "HomeController.ar.resx");

var admin = store.RequireAdmin();
foreach (var entry in document.Entries.Where(e => e.IsStringResource && e.Value is not null)) {
    await admin.CreateEntryAsync(
        "HomeController", "ar", entry.Name, entry.Value!,
        new ResourceAdminOptions { Actor = "resx-migration" });
}
```

`CreateEntryAsync` reports `AlreadyExists` rather than throwing, so a second run can be treated as
idempotent. A production migration should preview with `DryRun = true` first and keep the actor.

If the target is a JSON catalog instead, write the same entries into a flat object. The key generator
can consume that file as `*.loc.json` if it is declared as an additional file.

## Key strategy

Microsoft ResX workflows often use English text or property names as keys. That works in ResX because
the key is rarely read by a human outside the editor. In a key-driven pipeline it becomes a liability:

- Translators see the key in PO (Portable Object)/XLIFF (XML Localization Interchange File Format) and may translate it by mistake.
- Renaming a key is a catalog migration; text keys make that migration constant.
- Duplicate meanings collapse. Two screens with the same text often need different translations.
- Keys appear in traces, missing-key sinks, and parity reports. Dotted identifiers are easier to read
  and safer than sentences.

Adopt dotted keys (`Checkout.Name.Required`, `Cart.Items.Empty`) and use contexts where the same key
has different meanings. This is the same advice as [Strongly typed keys](../02-guides/05-strongly-typed-keys.md),
but it matters more during migration because changing keys later touches every catalog.

## Coexistence

The factory is a single service. Once `CustomStringLocalizerFactory` replaces
`ResourceManagerStringLocalizerFactory`, every `IStringLocalizer<T>` resolution goes through the
store. There is no per-type hybrid in the box.

Practical coexistence options:

- **Process-level split.** One host uses the Jeninnet bridge; another still uses Microsoft
  localization. This is clean but does not help a single application.
- **Migrate all `IStringLocalizer` consumers at once.** Keep the message pipeline separate for
  `IMessageLocalizer` consumers. Both can run in one process; they read different sources.
- **Keep ResX on the pipeline.** `Providers.ResX` serves `IMessageLocalizer` from compiled resources.
  It does not feed `IStringLocalizer`. This preserves ResX authoring for pipeline consumers while the
  bridge uses the store, at the cost of two catalogs.
- **Wrap a legacy factory.** A custom `IStringLocalizerFactory` can delegate some types to a
  Microsoft factory and others to the Jeninnet factory. That is bespoke code and outside the
  supported composition; test it carefully if you need it.

**Pitfall:** calling the Microsoft `AddLocalization()` after the Jeninnet one, or the reverse, and
assuming the factory is merged. Both register a factory; the last replacement wins. Decide which
surface owns `IStringLocalizer` and keep one owner.

## Validation checklist

Run through this list before deleting the old registration:

- [ ] Every `IStringLocalizer<T>` resolution has an `ILocalizationStore`.
- [ ] Resource-set names match `Type.Name` or the explicit base name used at the call site.
- [ ] Every served culture has entries for the keys its users need; parent fallback is not assumed.
- [ ] Missing entries render `[MISSING:key]`; UI tests and snapshots are updated.
- [ ] Arguments use composite `{0}` formatting, not named placeholders, on the `IStringLocalizer`
      path.
- [ ] Validation attributes use the localized family or the MVC metadata provider, and their keys
      exist in the store.
- [ ] `DisplayAttribute` names, descriptions, and prompts are either keys in the store or use
      `ResourceType` deliberately.
- [ ] Request localization has a store or explicit supported cultures; `Accept-Language` still
      selects the expected culture.
- [ ] Scoped and transient consumers do not capture a localizer in a singleton.
- [ ] `GetAllStrings` behavior is acceptable: the bridge enumerates the exact culture only.
- [ ] The parity tool reports no missing keys or placeholder drifts for the migrated catalog.
- [ ] A rollback plan exists: keep the old registration and catalogs until one release has shipped.

## Worked example: one controller

Before:

```csharp
public class HomeController : Controller {
    private readonly IStringLocalizer<HomeController> _localizer;
    public HomeController(IStringLocalizer<HomeController> localizer) => _localizer = localizer;

    public IActionResult Index() => View(_localizer["Welcome", User.Identity?.Name ?? "guest"]);
}
```

The ResX file is `Resources/HomeController.resx` with an entry named `Welcome` and value
`Welcome, {0}!`.

After:

1. Create the store (or a JSON catalog imported into it) with resource set `HomeController`, culture
   `en`, key `Welcome`, template `Welcome, {0}!`.
2. Register the store, `AddLocalization`, and the provider.
3. Keep the controller unchanged. The transient `IStringLocalizer<HomeController>` now resolves
   through the store.
4. Add the `ar` entries for every served key. Do not rely on `en` fallback unless the application
   deliberately serves English to Arabic users.
5. Replace hardcoded validation messages with `LocalizedRequired` and friends, or register
   `AddMvcLocalizedValidation` for plain `RequiredAttribute` metadata.
6. Run the parity tool and the existing UI tests. The missing marker and fallback differences are the
   two most likely test failures.

The controller code did not change. The registration, the data, and the fallback expectations did.

## Pitfalls

**Pitfall:** assuming `Create(Type)` uses the full type name. It uses `Type.Name`; namespaces are not
part of the resource-set name.

**Pitfall:** assuming `Create(baseName, location)` scopes by location. The location is validated and
ignored; the base name is the whole identity.

**Pitfall:** expecting `ResourcesPath` semantics or parent-culture fallback from the store. There is no
`ResourcesPath` option, and the bridge resolves the exact captured culture. Seed the served cultures
or use the pipeline.

**Pitfall:** comparing missing values to the old behavior. The old localizer returned the name; the
bridge returns `[MISSING:key]` and flags `ResourceNotFound`.

**Pitfall:** forgetting that the localized DataAnnotations attributes also need the store-backed
factory. A pipeline-only composition compiles and then fails validation at runtime.

## Where to go next

- [ASP.NET Core and MVC](../02-guides/08-aspnet-core-and-mvc.md) for the full web composition.
- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md) for the bridge.
- [Provider reference](03-provider-reference.md) for ResX and satellite runtime options.
