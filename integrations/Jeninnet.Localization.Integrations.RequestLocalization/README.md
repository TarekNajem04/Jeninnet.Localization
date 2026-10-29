# Jeninnet.Localization.Integrations.RequestLocalization

ASP.NET Core request-localization bridge for `Jeninnet.Localization`. The package populates `RequestLocalizationOptions` from the localization store (supported cultures and default culture) and selects request cultures only from the set the application can actually serve. The host keeps calling the framework middleware; the bridge supplies a working, truthful configuration.

Request culture selection is an adapter concern. The bridge never mutates ambient culture and never reimplements message lookup or the culture-fallback chain, which remain owned by `DefaultMessageLocalizer`. See [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md) and [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md).

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.RequestLocalization
```

The package declares a `FrameworkReference` on `Microsoft.AspNetCore.App` for `RequestLocalizationMiddleware`, `RequestCultureProvider`, and `RequestLocalizationOptions`.

## Registration

Register the store and provider first, then the bridge:

```csharp
using Jeninnet.Localization.Integrations.RequestLocalization.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddEntityFrameworkLocalizationStore(configuration, "LocalizationDb");
services.AddLocalizationRequestCultures();

var app = builder.Build();
app.UseRequestLocalization();
```

The host still installs the framework middleware. The bridge configures it; it does not add the middleware to the pipeline.

Explicit overrides are available when the served set should not come from the store:

```csharp
services.AddLocalizationRequestCultures(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.RouteCultureKey = "culture";
});
```

Registering the bridge twice throws.

## Configuration

`LocalizationRequestCultureOptions` exposes:

| Option | Default | Meaning |
|----|----|----|
| `DefaultCulture` | unset | Fallback request culture. When unset, the registered `LocalizationOptions.DefaultCulture` is used, then `"en"`. |
| `SupportedCultures` | empty | Explicit served cultures. When empty, the store is queried. |
| `RouteCultureKey` | `culture` | Route value key that carries the request culture. |

The default culture is always unioned into the served set, so the middleware's fallback is truthful even when the configured default is not listed explicitly. Explicit culture names are validated at registration; an unrecognized name fails startup.

## Usage

A route, query string, cookie, or `Accept-Language` header can select the culture:

```text
GET /en/catalog
GET /catalog?culture=ar
Cookie: .AspNetCore.Culture=c%3Dar%7Cuic%3Dar
Accept-Language: ar-SY,ar;q=0.9
```

With the default route key, a controller route such as `{culture}/catalog` supplies the value. The framework providers handle query string, cookie, and `Accept-Language`; the package adds `RouteValueRequestCultureProvider` for the route source the framework lacks. Every provider result is wrapped by `ServedCultureRequestCultureProvider`, so an unserved culture resolves to the default request culture instead of leaking through.

The bridge can also be used directly with a custom provider:

```csharp
var provider = new ServedCultureRequestCultureProvider(customProvider) {
    Options = requestLocalizationOptions,
};
```

Assign `Options` before use. The wrapper needs the current `RequestLocalizationOptions` to read the served set.

## Behavior and limits

**Provider order.** `RequestCultureProviders` is set to route value, query string, cookie, then `Accept-Language`, in that order. The framework providers remain the parsing mechanism; the route provider only reads a route value.

**Narrowing.** `ServedCultureRequestCultureProvider` first tries an exact culture-name match (ordinal, case-insensitive), then the framework-configured bounded parent fallback when `FallBackToParentCultures` or `FallBackToParentUICultures` is enabled. A result that matches nothing yields `null`, so the middleware applies `DefaultRequestCulture`. A side whose supported list is `null` passes through unchanged.

**Failure behavior.** The bridge never produces a silent empty culture list:

| Condition | Behavior |
|----|----|
| Store reachable and reports zero cultures | Startup fails with `InvalidOperationException`; seed the store or declare `SupportedCultures`. |
| Store unreachable or no store registered | The served set falls back to the default culture alone; a warning is logged when logging is available. |
| Explicit or discovered culture name not recognized | Startup fails naming the offending value. |
| Default culture not in the served set | The default is unioned into the served set. |

The store read happens at options configuration time through a single intentional synchronous boundary, because the framework's `IConfigureOptions<T>` has no asynchronous form. The bridge reads `ILocalizationStore`, not the message provider, so an application with JSON catalogs but no store collapses to the default culture and ignores `Accept-Language`. Register the store or declare the served cultures explicitly.

**Scope.** The bridge selects the request culture only. It does not mutate ambient culture; the framework middleware applies the resolved culture per request. Message lookup and fallback stay in the canonical pipeline. Browser-WASM browser-language detection is out of scope; the Blazor integration owns client-side culture selection.

**Logging.** An unreachable store logs one warning with event id `1` and event name `LocalizationStoreUnreachable`. That line is the quickest explanation for "the site ignores `Accept-Language` in this environment."

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Integrations.Blazor](../Jeninnet.Localization.Integrations.Blazor/README.md)
- [Jeninnet.Localization.Integrations.Mvc.Validation](../Jeninnet.Localization.Integrations.Mvc.Validation/README.md)
- [Jeninnet.Localization.Integrations.JavaScript](../Jeninnet.Localization.Integrations.JavaScript/README.md)
- [Jeninnet.Localization.Abstractions](../../src/Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [Configuring providers](../../docs/en/02-guides/02-configuring-providers.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [LICENSE](../../LICENSE).
