# Jeninnet.Localization.Integrations.Mvc.Validation

MVC validation, display metadata, and Razor view integration for `Jeninnet.Localization`. The package connects the framework's MVC metadata pipeline and Razor localizer contracts to the canonical message pipeline and the store-backed `IStringLocalizerFactory`, so controllers, views, validation messages, and display names all resolve from the same catalog.

The integration is the single documented `Integration → Core` dependency: it registers Core's `CustomStringLocalizerFactory` as `IStringLocalizerFactory` and depends on `Microsoft.AspNetCore.App` for the MVC metadata pipeline. See [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md) for the full composition and the relationship between `IMessageLocalizer` and `IStringLocalizer`.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.Mvc.Validation
```

## Registration

Call the MVC registration after `AddControllers()`, `AddControllersWithViews()`, or `AddMvcCore()` so the localized metadata providers are appended after the framework's DataAnnotations provider:

```csharp
using Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;

services.AddControllersWithViews();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddEntityFrameworkLocalizationStore(configuration, "LocalizationDb");
services.AddMvcLocalizedValidation();
```

The package also ships two explicit opt-ins:

```csharp
services.AddHtmlMessageLocalizer();   // IHtmlLocalizer / IHtmlLocalizerFactory bridges
services.AddViewMessageLocalizer();   // IViewLocalizer bridge with view-path scoping
```

The `<loc>` tag helper needs only `AddLocalization()` plus one provider; it does not require `AddHtmlMessageLocalizer()`.

`AddMvcLocalizedValidation` replaces `IStringLocalizerFactory` with `CustomStringLocalizerFactory` and registers the standard `IStringLocalizer<T>` wrapper. Register an `ILocalizationStore` before resolving either abstraction. The MVC extension supplies the bridge registrations as a fallback when it is used without Core setup, but it does not create a store.

## Configuration

The package has no options type. Its configuration surface is registration order and the framework metadata model:

- `AddMvcLocalizedValidation` appends `LocalizedValidationMetadataProvider` and `LocalizedDisplayMetadataProvider` to `MvcOptions.ModelMetadataDetailsProviders`.
- Providers run in registration order. Appending them after the framework's DataAnnotations provider is what makes the localized values take precedence.
- `LocalizedValidationMetadataProvider` localizes only a plain framework `RequiredAttribute` whose `ErrorMessage` is empty, using the property name as the key. Attributes derived from `RequiredAttribute`, including the localized DataAnnotations family, are left untouched because they own their localization.
- `LocalizedDisplayMetadataProvider` localizes `DisplayAttribute.Name`, `Description`, and `Prompt` using the literal attribute values as keys. If `ResourceType` is set, the attribute is left untouched so the built-in resource-manager mechanism keeps working. `DisplayAttribute.ShortName` has no destination in the MVC display-metadata model on the supported ASP.NET Core versions and cannot be localized through this pipeline.

## Usage

A model with framework `Required` and `Display` attributes resolves metadata from the catalog:

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class CheckoutModel {
    [Required]
    [Display(Name = "Checkout.Name", Description = "Checkout.Name.Help", Prompt = "Checkout.Name.Prompt")]
    public string Name { get; set; } = string.Empty;
}
```

The property name (`Name`) is the validation key for the plain `Required` attribute; the literal display strings are the keys for the display metadata. For messages that must follow every request culture, use the localized DataAnnotations attribute instead:

```csharp
using Jeninnet.Localization.Integrations.DataAnnotations;

public sealed class CheckoutModel {
    [LocalizedRequired("Checkout.Name.Required")]
    public string Name { get; set; } = string.Empty;
}
```

HTML-safe view localization encodes arguments while passing trusted template markup through:

```razor
@inject IHtmlLocalizer HtmlLocalizer
@HtmlLocalizer["Greeting", userName]
```

With a `Greeting` template of `<b>Hello {0}!</b>`, markup in `userName` renders inert while the template markup passes through.

View-scoped keys use the executing view path as a gettext context:

```csharp
services.AddViewMessageLocalizer();
```

```razor
@inject IViewLocalizer Localizer
@Localizer["Heading"]
```

`/Views/Catalog/Index.cshtml` becomes the context `Views.Catalog.Index`, so the lookup first tries `Views.Catalog.Index` + EOT (End of Transmission) + `Heading`, then the shared `Heading` entry, each through the canonical culture fallback.

Inline content uses the tag helper:

```razor
@addTagHelper *, Jeninnet.Localization.Integrations.Mvc.Validation

<loc key="Cart.Title">Shopping Cart</loc>
<loc key="Greeting" arguments="@(new object[] { userName })">Hello {0}!</loc>
```

On a miss the element body renders as the fallback. The `<loc>` element and its attributes are removed from the output. `arguments` binds an `object[]` from a Razor expression, applies to resolved templates only, and is HTML-encoded exactly as in `AddHtmlMessageLocalizer()`.

## Behavior and limits

**Validation metadata is culture-frozen.** The localized validation provider attaches the message when MVC creates the metadata, and MVC caches that metadata for the application lifetime. The `ErrorMessage` therefore reflects `CultureInfo.CurrentUICulture` at first metadata creation. Applications that need culture-correct "required" messages across request cultures should use the localized DataAnnotations family, which resolves at validation time.

**Display metadata is deferred.** The display providers assign delegates rather than values. Each evaluation creates a fresh localizer, so cached MVC metadata stays culture-correct when the current culture changes between requests. Missing keys surface the canonical `[MISSING:key]` marker.

**Localizer contracts.** `HtmlMessageLocalizer` encodes argument values before lookup and formatting, passes trusted template markup through, and returns framework `LocalizedHtmlString` values. A missing key follows the framework convention: the value is the name and the result is flagged not found. Key enumeration throws `NotSupportedException` because the message pipeline has no enumeration surface. `HtmlMessageLocalizerFactory` provides Base Class Library (BCL)-surface parity for components that resolve the factory; `Create(Type)` and `Create(baseName, location)` validate inputs but do not scope the lookup, because the HTML bridge is key-based.

**View scoping.** `ViewMessageLocalizer` uses `ViewContext.ExecutingFilePath` over `ViewContext.View.Path`, so partials and themed or area-expanded locations resolve under their own path. A key resolves view-scoped first and shared second, each with the full canonical culture fallback. Without a usable view path the bridge degrades to shared-only behavior. Plural and context lookups are not part of the `IViewLocalizer` surface.

**Trusted templates.** Templates are developer content and are never sanitized. Argument values are encoded. If a catalog can be edited by untrusted users, review the markup path before rendering it as HTML. See the [security chapter](../../docs/en/04-operations/02-security.md).

**Trimming and AOT (Ahead-of-Time).** The metadata providers use the `IStringLocalizerFactory` contract and no reflection-based binding. The MVC pipeline itself is framework-owned; follow the host's trimming guidance for MVC.

## AOT and trimming

Compatible with Native AOT and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Integrations.DataAnnotations](../Jeninnet.Localization.Integrations.DataAnnotations/README.md)
- [Jeninnet.Localization.Integrations.RequestLocalization](../Jeninnet.Localization.Integrations.RequestLocalization/README.md)
- [Jeninnet.Localization.Core](../../src/Jeninnet.Localization.Core/README.md)
- [Jeninnet.Localization.Storage](../../src/Jeninnet.Localization.Storage/README.md)

## Documentation

- [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Migration from Microsoft.Extensions.Localization](../../docs/en/03-reference/08-migration-from-ms-extensions.md)
- [Security](../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../LICENSE).
