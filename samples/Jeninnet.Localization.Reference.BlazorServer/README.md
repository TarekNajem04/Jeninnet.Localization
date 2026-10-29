# Jeninnet.Localization.Reference.BlazorServer

The Blazor Server host of the Jeninnet.Localization reference application. It renders the shared BookStore catalog and order pages with Blazor components, supports interactive server rendering, and demonstrates the circuit-scoped Blazor localization integration provided by `Jeninnet.Localization.Integrations.Blazor`.

The project is a **sample**, not a package: `IsPackable=false`, never published. It references the shared reference project and the Blazor integration package.

## What this host demonstrates

- **Blazor components over the canonical pipeline**: `IStringLocalizer<PageResources>` and `IMessageLocalizer` injected into Razor components.
- **Server-side rendering plus interactive server mode** through `MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
- **Circuit-scoped culture state** through `AddBlazorServerLocalization` and `BlazorCultureService`; each circuit gets its own culture, seeded from the request.
- **Cookie-persisted culture selection** through `BlazorCookieCultureStore` and the in-app `CultureSelector` component.
- **RTL page direction** — the root document applies `dir="rtl"` and the matching `lang` for right-to-left cultures.
- **Exactly one Provider**, selected through configuration, with no component changes when the Provider changes.
- **Localized validation and order feedback** rendered from the shared order service and message pipeline; the last confirmation is kept in protected session storage across a language reload.

## Prerequisites

- .NET 10 SDK.
- A browser to exercise the interactive circuit; no external service, database, or credential for the default `Json` Provider.
- Optional gated Providers follow the same rules as the other web hosts.

## Run

From the repository root:

```bash
dotnet restore Jeninnet.Localization.slnx
dotnet build Jeninnet.Localization.slnx -c Release

ASPNETCORE_URLS=http://localhost:5064 dotnet run \
    --project samples/Jeninnet.Localization.Reference.BlazorServer
```

PowerShell equivalent:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5064"
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorServer
```

The launch profile binds `https://localhost:2928` and `http://localhost:2931`. The commands above pin port 5064 to match the reference runbook in [`samples/README.md`](../README.md).

Open the localized pages:

```text
http://localhost:5064/?culture=ar
http://localhost:5064/orders?culture=ar
```

Use the culture selector in the layout to switch between `en`, `ar`, and `ar-SY`. The selection is persisted through the Jeninnet culture service and a cookie, then the page reloads so the request pipeline applies the new culture to the next circuit.

## Components and routes

| Route | Component | Purpose |
|----|----|----|
| `/` | `Components/Pages/Catalog.razor` | Localized catalog table with titles, authors, prices, and descriptions |
| `/orders` | `Components/Pages/Orders.razor` | Localized order form with validation and a localized confirmation or error message |
| layout | `Components/Layout/CultureSelector.razor` | Culture switcher backed by `BlazorCultureService` and the cookie store |

`Catalog.razor` resolves text through `IStringLocalizer<PageResources>` and formats prices with `CultureInfo.CurrentUICulture`. `Orders.razor` performs client-side validation of the book and quantity before calling the shared `OrderService`, then localizes the result with `IMessageLocalizer`. `CultureSelector.razor` calls `BlazorCultureService.SetCultureAsync` and forces a reload so the circuit and request culture stay aligned.

## Composition

`ReferenceBlazorComposition.AddReferenceBlazor` registers:

```text
AddReferenceLocalizationProvider(...)  -> shared Provider selection and Core pipeline
AddRazorComponents()
    .AddInteractiveServerComponents()  -> Blazor Server rendering
AddBlazorServerLocalization(...)       -> circuit-scoped culture service and handler
AddScoped<IBlazorCultureStore,
          BlazorCookieCultureStore>() -> cookie persistence
```

`Program.cs` adds `UseRequestLocalization` with the same supported cultures as the other web hosts, then `UseAntiforgery`, then maps the root components with interactive server rendering.

The Blazor integration is documented in [`Jeninnet.Localization.Integrations.Blazor`](../../integrations/Jeninnet.Localization.Integrations.Blazor/README.md).

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
ASPNETCORE_URLS=http://localhost:5064 \
ReferenceLocalization__Provider=Xml \
dotnet run --project samples/Jeninnet.Localization.Reference.BlazorServer
```

Supported build-safe values are `Memory`, `Json`, `Xml`, `ResX`, and `SQLite`. The infrastructure and credential-gated values are recognized exactly as in the API and MVC hosts; see [`samples/README.md`](../README.md) sections 5–8 and 25–28.

The host declares a `UserSecretsId` for local development with gated Providers. Secrets are never committed.

## Culture selection

- Supported cultures: `en`, `ar`, `ar-SY`; default: `en`.
- The request pipeline uses the query string first and the cookie second, matching the other web hosts.
- Interactive culture changes go through `BlazorCultureService`, which is circuit-scoped, and are persisted by `BlazorCookieCultureStore`.
- The root document applies `lang` and `dir` from the request UI culture; the selector forces a reload so the cookie-backed request culture and the page direction stay aligned.
- Core owns fallback: exact culture, parent cultures, then the default culture. Providers remain exact-match.

## Testing

There is no headless `--smoke` mode for this host because interactive components run over a SignalR circuit in a browser. The server-side composition and routes are covered by [`Jeninnet.Localization.Reference.Hosts.Tests`](../Jeninnet.Localization.Reference.Hosts.Tests/README.md):

- exactly one inner Provider for every build-safe Provider;
- the canonical pipeline services and bridge;
- the circuit-scoped `BlazorCultureService` registration;
- the `BlazorCircuitCultureHandler` in the `CircuitHandler` set;
- the scoped `BlazorCookieCultureStore` registration;
- culture-aware bridge lookups.

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Tests \
    --configuration Release
```

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Blazor integration](../../integrations/Jeninnet.Localization.Integrations.Blazor/README.md)
- [Blazor guide](../../docs/en/02-guides/09-blazor.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
