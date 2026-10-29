# ASP.NET Core and MVC

An ASP.NET Core application can consume localization through two surfaces. The message pipeline
returns formatted strings from `IMessageLocalizer`; the resource-set bridge returns
`LocalizedString` values from `IStringLocalizer` and `IStringLocalizer<T>`. MVC, DataAnnotations,
Razor views, and validation metadata use the second surface. This chapter wires both, explains
request-culture selection, and calls out the lifetime traps behind "works locally, wrong language in
production" failures.

## The two surfaces

`IMessageLocalizer` owns culture fallback, exact-match provider queries, and formatting through
`IMessageFormatter`. It knows nothing about resource sets, MVC, or `IStringLocalizer`.

`IStringLocalizer` is the Microsoft.Extensions.Localization contract. Here it is backed by
`CustomStringLocalizerFactory` over an `ILocalizationStore`. The factory maps a type name (or an
explicit base name) to a resource-set name, captures the current UI culture when the localizer is
created, and re-probes the store on every lookup. It does not walk parent cultures, and it returns
`[MISSING:key]` with `ResourceNotFound = true` for an absent entry.

The surfaces do not share data. Keep both catalogs aligned deliberately. See
[Resource sets and the store](06-resource-sets-and-store.md).

## A working composition

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddLocalization(options => options.DefaultCulture = "en");
builder.Services.AddEntityFrameworkLocalizationStore(
    builder.Configuration, connectionStringName: "LocalizationDb");
builder.Services.AddMvcLocalizedValidation();
builder.Services.AddLocalizationRequestCultures();

var app = builder.Build();
app.UseRequestLocalization();
app.UseRouting();
app.MapDefaultControllerRoute();
app.Run();
```

`AddLocalization` registers the pipeline, the formatter, the provider selector, and the
store-backed `IStringLocalizerFactory`. It does not register an `ILocalizationStore`; the store
registration above is what makes `IStringLocalizer<T>` resolvable. `AddMvcLocalizedValidation` also
replaces the factory as a safety net, but it does not create a store.

If the application only uses `IMessageLocalizer`, it does not need a store. Adding `IStringLocalizer`
is the point where the store becomes mandatory.

## Request culture selection

`AddLocalizationRequestCultures` fills the framework middleware options:

- **Served cultures** from explicit `LocalizationRequestCultureOptions.SupportedCultures` when set;
  otherwise from `ILocalizationStore.GetSupportedCulturesAsync()`. The default culture is always
  unioned into the set.
- **Default request culture** from `LocalizationRequestCultureOptions.DefaultCulture`, then the
  registered `LocalizationOptions.DefaultCulture`, then `"en"`.
- **Providers** in the order route value, query string, cookie, `Accept-Language`. The route source
  is `RouteValueRequestCultureProvider` with a configurable key (default `"culture"`).
- **Narrowing** through `ServedCultureRequestCultureProvider`: exact served name first, then the
  framework-configured bounded parent fallback.

The bridge reads the store, not the message provider. An application with JSON catalogs for `en`,
`ar`, and `de` but no store still runs, but the served set collapses to the default culture and
`Accept-Language: ar` is ignored. Register the store, or declare the served cultures explicitly.

A reachable store that reports zero cultures fails startup. An unreachable or absent store falls back
to the default culture alone and logs a warning when logging is available. An unrecognized culture
name fails startup. A default culture missing from the served set is added to it.

Keep `app.UseRequestLocalization()` in the pipeline. The bridge configures the middleware; it does
not install it.

## The `IStringLocalizer` bridge

The factory maps a type to a resource-set name through `Type.Name`:

```csharp
public class HomeController : Controller {
    private readonly IStringLocalizer<HomeController> _localizer;
    public HomeController(IStringLocalizer<HomeController> localizer) => _localizer = localizer;
    public IActionResult Index() => View(_localizer["Home.Title"]);
}
```

The lookup resolves the resource set `HomeController` for the UI culture captured at creation.
`Create(baseName, location)` uses `baseName` verbatim and validates `location` only; the location
does not scope the lookup. A namespace-qualified Microsoft resource name does not map automatically.

The generic wrapper is transient. It gets a fresh factory result per resolution, so injecting
`IStringLocalizer<T>` into a controller or Razor page is safe: the localizer is created after request
localization applied the request culture. The factory and store are singletons.

**Pitfall:** injecting `IStringLocalizer<T>` into a singleton. The localizer captured the first
request's culture, and the singleton serves every request. Resolve per request, or pass the culture
explicitly to `IMessageLocalizer`.

**Pitfall:** resolving `IStringLocalizer<T>` without an `ILocalizationStore`. The pipeline can be
fully registered and still fail here.

## DataAnnotations integration

`Integrations.DataAnnotations` contains localized replacements for the standard validation
attributes: `LocalizedRequiredAttribute`, `LocalizedStringLengthAttribute`, `LocalizedRangeAttribute`,
`LocalizedRegularExpressionAttribute`, `LocalizedCompareAttribute`, `LocalizedCustomValidationAttribute`,
and the rest of the family. Each takes a localization key and resolves at validation time through the
`IStringLocalizerFactory` on the `ValidationContext`.

```csharp
using System.ComponentModel.DataAnnotations;
using Jeninnet.Localization.Integrations.DataAnnotations;

public class CheckoutModel {
    [LocalizedRequired("Checkout.Name.Required")]
    public string Name { get; set; } = string.Empty;

    [LocalizedStringLength(3, 40, "Checkout.Name.Length")]
    public string DisplayName { get; set; } = string.Empty;
}
```

The lookup is the store-backed `IStringLocalizer` indexer. Arguments use composite formatting:
`LocalizedStringLength` supplies `{0}` (minimum) and `{1}` (maximum), and `LocalizedRangeAttribute`
does the same for its bounds. Catalog entries must use `{0}`/`{1}`, not named placeholders.

The package depends only on the BCL (Base Class Library), DataAnnotations, and
`Microsoft.Extensions.Localization.Abstractions`. Runtime composition still requires the factory and
store. When the composition is missing, the attribute throws `InvalidOperationException` instead of
falling back to hard-coded text: a configuration error, not a localization miss.

## MVC validation and display metadata

`AddMvcLocalizedValidation` registers two metadata providers after the framework's DataAnnotations
provider:

- `LocalizedValidationMetadataProvider` attaches a localized message to a plain framework
  `RequiredAttribute` with no `ErrorMessage`, using the property name as the key. Attributes derived
  from `RequiredAttribute`, including the localized family, are untouched because they own their
  localization.
- `LocalizedDisplayMetadataProvider` localizes `DisplayAttribute.Name`, `Description`, and `Prompt`.
  The literal attribute values are the keys. `DisplayAttribute.ResourceType` disables the provider for
  that attribute, preserving the built-in resource-manager mechanism.

Registration order matters: the localized providers must be appended after `AddControllers()` or
`AddMvcCore()` so they run after the framework provider.

**Display metadata is deferred.** The metadata delegates create a fresh localizer each time the value
is consumed, so cached MVC metadata stays culture-correct across requests. **Framework
`RequiredAttribute` metadata is not deferred.** The message is attached when MVC creates the metadata,
which MVC caches for the application lifetime; it reflects the UI culture at first creation. Use the
localized DataAnnotations family when validation messages must follow every request culture.

`DisplayAttribute.ShortName` has no destination in the MVC display-metadata model on the supported ASP.NET Core versions. It
cannot be localized through this pipeline; this is a framework constraint.

## Razor views

`AddHtmlMessageLocalizer()` replaces `IHtmlLocalizer` and `IHtmlLocalizerFactory` with HTML-safe
bridges over the pipeline. Template markup passes through as trusted developer content; argument
values are encoded through `HtmlSafeMessageFormatter.EncodeArgument`. A missing key follows the
framework convention: the value is the name and `IsResourceNotFound` is true. `GetAllStrings` throws
`NotSupportedException` because the message pipeline has no key enumeration.

`AddViewMessageLocalizer()` replaces `IViewLocalizer`. The executing view path becomes a gettext
context (`/Views/Catalog/Index.cshtml` becomes `Views.Catalog.Index`). A key resolves view-scoped
first and shared second, each through the canonical fallback chain. `ViewContext.ExecutingFilePath`
wins over `ViewContext.View.Path`, so partials and themed or area-expanded views resolve under their
own location. Plural and context lookups are not part of the `IViewLocalizer` surface and throw.

The `<loc>` tag helper localizes inline content without a code-behind lookup:

```razor
@addTagHelper *, Jeninnet.Localization.Integrations.Mvc.Validation

<loc key="Cart.Title">Shopping Cart</loc>
<loc key="Greeting" arguments="@(new object[] { userName })">Hello {0}!</loc>
```

On a miss the element body renders as the fallback. The `<loc>` element and its attributes are removed
from the output. The helper needs `AddLocalization()` and a provider, but not
`AddHtmlMessageLocalizer()`.

## Worked example: from request to validation message

A request arrives with `Accept-Language: ar-SY`. The middleware selects `ar-SY`. A controller injects
`IStringLocalizer<CheckoutController>`; the transient wrapper captures `ar-SY`. The store has no
`CheckoutController`/`ar-SY` entry and no fallback, so the indexer returns
`[MISSING:Checkout.Title]`. The marker is visible in staging and in review.

A form post validates two fields. `LocalizedRequiredAttribute("Checkout.Name.Required")` resolves the
same store exactly for `ar-SY`. A missing entry produces `[MISSING:Checkout.Name.Required]`. The
parity tool reports the gap before deployment. See
[Parity and quality gates](13-parity-and-quality-gates.md).

If the team wants parent-culture fallback, it must store entries for every served culture or route
those strings through `IMessageLocalizer`.

## Pitfalls

**Pitfall:** assuming `AddLocalization()` also registers a store. It does not; `IStringLocalizer<T>`
resolution fails until an `ILocalizationStore` is registered.

**Pitfall:** injecting `IStringLocalizer<T>` into a singleton. It captured the first request's
culture; resolve it per request.

**Pitfall:** calling both `AddLocalization()` methods without controlling order. The factory
registration is a replacement, not a merge. Keep one owner. See
[Migration from Microsoft.Extensions.Localization](../03-reference/08-migration-from-ms-extensions.md).

## Where to go next

- [Resource sets and the store](06-resource-sets-and-store.md) for the bridge and the EF store.
- [Blazor](09-blazor.md) for the circuit and static client scopes.
- [RTL (Right-to-Left) and bidirectional text](16-rtl-and-bidi.md) for direction, logical CSS, and bidi isolation.
- [Storage API](../03-reference/02-storage-api.md) for the complete store surface.
