# Jeninnet.Localization.Integrations.DataAnnotations

DataAnnotations validation integration for `Jeninnet.Localization`. The package provides localized replacements for the standard validation attributes. Each attribute takes a localization key, runs the framework validation semantics unchanged, and resolves its error message at validation time through the `IStringLocalizerFactory` available on the `ValidationContext`.

The package is intentionally decoupled from the runtime graph: it references only the BCL (Base Class Library)/DataAnnotations plus `Microsoft.Extensions.Localization.Abstractions`. It exposes no DI (Dependency Injection) registration API. Runtime use depends on an `IStringLocalizerFactory` being resolvable from the validation context, which in this framework is the store-backed factory registered by Core `AddLocalization()`.

See [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md) for the resource-set bridge and [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md) for the store behind it.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.DataAnnotations
```

## Registration

There is no package-specific registration method. Register the canonical localization pipeline and a store before validation runs:

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddEntityFrameworkLocalizationStore(configuration, "LocalizationDb");
```

`AddLocalization()` registers `CustomStringLocalizerFactory` as `IStringLocalizerFactory`. The attributes resolve that factory from the `ValidationContext` and create a localizer for the validated object type. If the factory is missing when a value is invalid, the attribute throws `InvalidOperationException` with a configuration-error message; it never falls back to hard-coded English text. A missing store surfaces through the factory and localizer when the message is resolved. A valid value returns before resolution, so a missing composition does not fail a successful validation.

If an MVC host already calls `AddMvcLocalizedValidation()`, that method also installs the factory as a safety net, but it does not create an `ILocalizationStore`. The store is still required.

## Configuration

There are no options and no package configuration. The localization key passed to each attribute is the catalog key; the message template is stored in the same resource set the factory maps the validated type to. Composite placeholders are positional:

- `LocalizedStringLengthAttribute` supplies `{0}` = minimum and `{1}` = maximum.
- `LocalizedRangeAttribute` supplies `{0}` = minimum and `{1}` = maximum.
- `LocalizedFileExtensionsAttribute` supplies `{0}` = the effective extension list.

Use these numeric placeholders in catalog entries, not named placeholders.

## Usage

Apply the localized attributes exactly where the framework attributes would go:

```csharp
using System.ComponentModel.DataAnnotations;
using Jeninnet.Localization.Integrations.DataAnnotations;

public sealed class CheckoutModel {
    [LocalizedRequired("Checkout.Name.Required")]
    public string Name { get; set; } = string.Empty;

    [LocalizedStringLength(3, 40, "Checkout.Name.Length")]
    public string DisplayName { get; set; } = string.Empty;

    [LocalizedRange(1, 120, "Checkout.Age.Range")]
    public int Age { get; set; }

    [LocalizedRegularExpression(@"^[A-Z]{2}\d{4}$", "Checkout.Code.Format")]
    public string Code { get; set; } = string.Empty;

    [LocalizedEmailAddress("Checkout.Email.Invalid")]
    public string Email { get; set; } = string.Empty;

    [LocalizedCompare(nameof(Email), "Checkout.Email.Mismatch")]
    public string ConfirmEmail { get; set; } = string.Empty;
}
```

Then run validation normally:

```csharp
var results = new List<ValidationResult>();
var context = new ValidationContext(model, serviceProvider, items: null);
Validator.TryValidateObject(model, context, results, validateAllProperties: true);
```

The message source can also be used directly by MVC model validation. The package does not change when validation runs or which members are reported; it only replaces the failure message.

## Behavior and limits

**Attribute family.** The package ships `LocalizedRequiredAttribute`, `LocalizedStringLengthAttribute`, `LocalizedRangeAttribute`, `LocalizedRegularExpressionAttribute`, `LocalizedCompareAttribute`, `LocalizedCustomValidationAttribute`, `LocalizedEmailAddressAttribute`, `LocalizedPhoneAttribute`, `LocalizedUrlAttribute`, `LocalizedCreditCardAttribute`, `LocalizedFileExtensionsAttribute`, `LocalizedMaxLengthAttribute`, and `LocalizedMinLengthAttribute`.

**Framework semantics preserved.** Each attribute delegates to the framework validator and replaces only the failure message. `LocalizedCompareAttribute` keeps the property comparison, `LocalizedCustomValidationAttribute` keeps validator type/method resolution, signature errors, thrown exceptions, and reported member names, and `LocalizedFileExtensionsAttribute` keeps the framework's extension matching and default list (`.png, .jpg, .jpeg, .gif`). `LocalizedRequiredAttribute` treats a null, empty, or whitespace value as missing, matching its implementation. The `DataType`-based attributes preserve their `DataType` metadata (`EmailAddress`, `PhoneNumber`, `Url`, `CreditCard`, `Upload`).

**Message resolution.** Resolution is deferred to the `IsValid` call, not metadata creation, so messages follow the UI culture of the request being validated. Keys are exact-match through the store-backed localizer. A missing key surfaces the canonical `[MISSING:key]` marker rather than silently passing validation. A missing `IStringLocalizerFactory` is a configuration error and throws `InvalidOperationException`.

**Trimming and AOT (Ahead-of-Time).** `LocalizedCompareAttribute` and the arbitrary-type `LocalizedRangeAttribute` constructor are annotated `RequiresUnreferencedCode` because they resolve members or convert values through reflection/type descriptors. Prefer the integer-bounds `LocalizedRangeAttribute` constructor when publishing trimmed, and preserve compared members explicitly.

**Security.** Validation messages are catalog content. Do not put untrusted input into keys, and treat translated messages as display text, not markup. The attributes do not encode or sanitize messages; rendering is the view's responsibility.

## AOT and trimming

Compatible with Native AOT and trimming. The assembly builds with `IsAotCompatible=true`; the attribute constructors that resolve members by reflection (`LocalizedCompare`, `LocalizedMaxLength`, `LocalizedMinLength`, `LocalizedRange(Type, ...)`, and `LocalizedCustomValidation(Type, ...)`) carry `RequiresUnreferencedCode`/`DynamicallyAccessedMembers` annotations and propagate the trim warning to dynamic call sites. Integer/string constructors remain fully trim-safe. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract and exclusions.

## Related packages

- [Jeninnet.Localization.Integrations.Mvc.Validation](../Jeninnet.Localization.Integrations.Mvc.Validation/README.md)
- [Jeninnet.Localization.Integrations.Blazor](../Jeninnet.Localization.Integrations.Blazor/README.md)
- [Jeninnet.Localization.Core](../../src/Jeninnet.Localization.Core/README.md)

## Documentation

- [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Migration from Microsoft.Extensions.Localization](../../docs/en/03-reference/08-migration-from-ms-extensions.md)

## License

MIT — see [LICENSE](../../LICENSE).
