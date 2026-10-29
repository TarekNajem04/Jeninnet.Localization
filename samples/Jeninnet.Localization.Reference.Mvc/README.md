# Jeninnet.Localization.Reference.Mvc

The ASP.NET Core MVC host of the Jeninnet.Localization reference application. It renders the shared BookStore catalog and order form as server-side Razor pages and demonstrates the framework-specific localization surface: display metadata, localized validation attributes, `IViewLocalizer`, the `IStringLocalizer<T>` bridge, and request culture switching.

The project is a **sample**, not a package: `IsPackable=false`, never published. It is the only reference host that references the MVC validation integration directly, because that integration is what the host is meant to teach.

## What this host demonstrates

- **Localized display metadata** for the checkout form through `DisplayAttribute` name, description, and prompt values.
- **Localized DataAnnotations validation** through `AddMvcLocalizedValidation()` and the shared `CreateOrderRequest` model.
- **View-scoped localization** with `IViewLocalizer` (and the `<loc>` tag helper) in Razor views.
- **Controller localization** through `IStringLocalizer<PageResources>` and `IMessageLocalizer` for composed order messages.
- **Culture preservation across requests** via `?culture=`, cookies, and culture-aware form actions and links; the last order confirmation is kept in session and re-localized.
- **RTL page direction** — the layout applies `dir="rtl"` and the matching `lang` for right-to-left cultures.
- **Exactly one Provider** selected through configuration, with no application-code changes when the Provider changes.

## Prerequisites

- .NET 10 SDK.
- No external service, database, or credential for the default `Json` Provider.
- Optional gated Providers follow the same rules as the API host: server providers need a connection string; translation providers need an API key.

## Run

From the repository root:

```bash
dotnet restore Jeninnet.Localization.slnx
dotnet build Jeninnet.Localization.slnx -c Release

ASPNETCORE_URLS=http://localhost:5062 dotnet run \
    --project samples/Jeninnet.Localization.Reference.Mvc
```

PowerShell equivalent:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5062"
dotnet run --project samples/Jeninnet.Localization.Reference.Mvc
```

The launch profile binds `https://localhost:2927` and `http://localhost:2929`. The commands above pin port 5062 to match the reference runbook in [`samples/README.md`](../README.md).

Open the localized pages:

```text
http://localhost:5062/catalog?culture=ar
http://localhost:5062/catalog/book-001?culture=ar
http://localhost:5062/orders/create?culture=ar
```

Submit the order form with an out-of-range quantity to see the localized validation messages, and with a valid quantity to see the localized confirmation stored in `TempData`.

## Pages and routes

| Method | Route | Purpose |
|----|----|----|
| `GET` | `/` | Redirects to `/catalog` and preserves the query string |
| `GET` | `/catalog` | Catalog page with localized heading, titles, descriptions, prices, and actions |
| `GET` | `/catalog/{id}` | Book detail page with localized description and price label |
| `GET` | `/orders/create` | Order form with localized display metadata, labels, and options |
| `POST` | `/orders/create` | Validates and places the order; re-renders with localized errors or redirects with a localized confirmation |

`CatalogController` renders the shared `BookCatalog`. `OrdersController` builds the book options with `IStringLocalizer<PageResources>`, places the order through `OrderService`, and localizes business errors through `IMessageLocalizer`. The POST redirect carries the current culture in the route so the confirmation is rendered in the culture that placed the order.

## Localization integrations

The host composition in `ReferenceMvcComposition.AddReferenceMvc` performs:

```text
AddReferenceLocalizationProvider(...)   -> shared Provider selection and Core pipeline
AddControllersWithViews()               -> MVC controllers and Razor views
AddMvcLocalizedValidation()             -> localized validation metadata for MVC
AddViewMessageLocalizer()               -> view-scoped IViewLocalizer bridge
```

The views use `@inject IViewLocalizer` for view strings and `IStringLocalizer<PageResources>` for controller strings. `PageResources` is an anchor type only; its resource set resolves through the shared store-backed content, not through a dedicated `.resx` file.

Validation messages come from the shared `CreateOrderRequest` model, whose DataAnnotations attributes reference the canonical key constants. Because the validation context is Dependency Injection (DI)-backed, the localized messages resolve from the same pipeline as every other string.

## Configuration

`appsettings.json` defaults to the JSON Provider:

```json
{
  "ReferenceLocalization": {
    "Provider": "Json"
  }
}
```

Override it with `ReferenceLocalization__Provider`:

```bash
ASPNETCORE_URLS=http://localhost:5062 \
ReferenceLocalization__Provider=ResX \
dotnet run --project samples/Jeninnet.Localization.Reference.Mvc
```

Supported build-safe values are `Memory`, `Json`, `Xml`, `ResX`, and `SQLite`. The web host also supports the infrastructure-gated `SqlServer`, `PostgreSql`, and `MySql` values and the credential-gated `AzureTranslation` and `GoogleTranslation` values. The full matrix, secret setup, and cleanup instructions are in [`samples/README.md`](../README.md) sections 5–8 and 25–28.

The host declares a `UserSecretsId`, so gated values can be supplied with `dotnet user-secrets` during local development and never enter the repository.

## Culture selection

`Program.cs` configures the same request-localization model as the API host:

- default culture `en`;
- supported cultures and UI cultures `en`, `ar`, `ar-SY`;
- query-string provider first, cookie provider second.

Links and forms preserve the current culture explicitly, so navigation does not silently reset to the default culture. The layout applies `lang` and `dir` from the request UI culture, so `?culture=ar` and `?culture=ar-SY` render the page right-to-left. Core owns fallback: exact culture, parent cultures, then `en`; unsupported cultures such as `fr` fall back to `en`.

## Testing

The MVC composition is covered by [`Jeninnet.Localization.Reference.Hosts.Tests`](../Jeninnet.Localization.Reference.Hosts.Tests/README.md), including:

- exactly one inner Provider for every build-safe Provider;
- the expected Provider implementation after selection;
- the canonical pipeline services and bridge;
- registration of the Jeninnet view localizer (`ViewMessageLocalizer`);
- culture-aware bridge lookups.

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Tests \
    --configuration Release
```

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [MVC validation integration](../../integrations/Jeninnet.Localization.Integrations.Mvc.Validation/README.md)
- [DataAnnotations integration](../../integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md)
- [ASP.NET Core and MVC guide](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
