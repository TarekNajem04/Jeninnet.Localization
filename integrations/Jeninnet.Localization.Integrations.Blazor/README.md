# Jeninnet.Localization.Integrations.Blazor

Blazor localization integration for `Jeninnet.Localization`. The package owns the active culture for a Blazor host: a circuit-scoped culture service for Blazor Server, a static-scope counterpart for client-side hosts (WebAssembly and Blazor Hybrid/WebView2), persistence through a host-wired culture store, and a WASM-compatible provider guard.

The integration adapts `CultureInfo`, DI (Dependency Injection), and `IJSRuntime`; it does not change the message pipeline, and culture fallback stays owned by `DefaultMessageLocalizer`. See [Blazor](../../docs/en/02-guides/09-blazor.md) for the conceptual model and [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md) for request culture selection on the server.

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.Blazor
```

The package declares a `FrameworkReference` on `Microsoft.AspNetCore.App` because `CircuitHandler` lives in a framework-only assembly. Consuming this package build from a browser-WASM project is therefore not supported; Blazor Hybrid and Blazor Server can use it today.

## Registration

Register the provider pipeline and exactly one provider first, then the Blazor composition, then a culture store:

```csharp
using Jeninnet.Localization.Integrations.Blazor;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
services.AddRazorComponents().AddInteractiveServerComponents();

services.AddBlazorServerLocalization(options => {
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});

services.AddScoped<IBlazorCultureStore, BlazorCookieCultureStore>();

var app = builder.Build();
app.UseRequestLocalization();
```

The cookie store is scoped to the circuit because it depends on the scoped `IJSRuntime`;
the local-storage store used by static hosts can remain a singleton with the static culture
service.

The three composition entry points select the scope:

- `AddBlazorLocalization(configure)` uses `BlazorLocalizationOptions.Mode`.
- `AddBlazorServerLocalization(configure)` sets `Mode = Server`.
- `AddBlazorWasmLocalization(configure)` sets `Mode = Wasm` and enables `RequireWasmCompatibleProviders`.

Server mode registers `BlazorCultureService` as scoped and `BlazorCircuitCultureHandler` as a `CircuitHandler`; static mode registers the service as a singleton. Without a store registration the composition uses an internal no-op store, and the choice stays in process memory only.

## Configuration

`BlazorLocalizationOptions` controls the scope and persistence:

| Option | Default | Meaning |
|----|----|----|
| `Mode` | `Server` | `Server` is per-circuit; `Wasm` is the single-user static scope. |
| `DefaultCulture` | unset | Culture used when no request or stored culture applies. |
| `SupportedCultures` | empty | Accepted cultures; empty accepts every recognized culture name. |
| `CultureCookieName` | `CookieRequestCultureProvider.DefaultCookieName` | Cookie written by `BlazorCookieCultureStore`. |
| `CultureCookieLifetime` | 365 days | Expiry applied to the culture cookie. |
| `CultureStorageKey` | `jeninnet.localization.culture` | Local-storage key used by `BlazorLocalStorageCultureStore`. |
| `DetectBrowserLanguage` | `false` | Static scope only: negotiate from `navigator.languages` when no stored choice applies. |
| `RequireWasmCompatibleProviders` | `false` | Reject registered providers that do not declare `IWasmCompatibleMessageProvider`. |

`AddBlazorWasmLocalization` sets `RequireWasmCompatibleProviders` to `true`. The guard inspects the service collection at registration time, so register the provider before the composition. A factory registration cannot be verified and is rejected by description. The Memory provider declares the marker; file, database, ResX, satellite, and translation providers do not.

## Usage

In Blazor Server, switch the circuit culture, persist it, and reload so the request pipeline applies it to server-rendered content:

```razor
@inject BlazorCultureService CultureService
@inject NavigationManager Navigation

<select @onchange="OnCultureChangedAsync">
    @foreach (var culture in new[] { "en", "ar" }) {
        <option value="@culture" selected="@(CultureService.Culture == culture)">@culture</option>
    }
</select>

@code {
    private async Task OnCultureChangedAsync(ChangeEventArgs args) {
        var culture = args.Value?.ToString();
        if (!string.IsNullOrEmpty(culture)) {
            await CultureService.SetCultureAsync(culture);
            Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
        }
    }
}
```

`CultureService.Culture` is `null` until a culture is applied. `CultureInfo` resolves `DefaultCulture` or the ambient UI culture, which makes it safe for `dir` and `lang` attributes before seeding. `CultureChanged` carries the applied name and `CultureInfo`; subscribe to it to re-render.

In a WebAssembly host, keep the static singleton registrations and restore the stored choice after
the first render because JavaScript interop is not available earlier:

```csharp
services.AddBlazorLocalization(options => {
    options.Mode = BlazorLocalizationMode.Wasm;
    options.DefaultCulture = "en";
    options.DetectBrowserLanguage = true;
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});
services.AddSingleton<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

Blazor Hybrid/WebView2 also uses the static culture semantics, but WebView2 supplies `IJSRuntime`
from the WebView scope. Override the culture service and store to scoped in that host; a singleton
service would capture the root JavaScript runtime and fail with "Cannot invoke JavaScript outside
of a WebView context":

```csharp
services.AddBlazorLocalization(options => {
    options.Mode = BlazorLocalizationMode.Wasm;
    options.DefaultCulture = "en";
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});
services.AddScoped<BlazorCultureService>();
services.AddScoped<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

```razor
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await CultureService.InitializeAsync();
        }
    }
}
```

## Behavior and limits

**Server scope.** The culture service is scoped to the circuit and is seeded when the circuit opens from `IRequestCultureFeature`, which the request pipeline resolved from the cookie or another provider. Ambient culture is never mutated, so concurrent circuits cannot bleed culture into each other. Seeding is lenient: an unrecognized or unsupported request culture is ignored rather than failing the connection. The circuit culture is effectively fixed for the circuit lifetime; a switch persists and reloads the page, which creates a new circuit.

**Static scope.** Applying a culture sets `CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, and `DefaultThreadCurrentUICulture`, then raises `CultureChanged`. This is safe because the process serves one user. It is the correct semantics for Blazor Hybrid/WebView2, where file and database access is legitimate; use `AddBlazorLocalization` with `Mode = Wasm` and no guard there. In WebAssembly the service and store can stay singleton; in WebView2 register both as scoped because the JavaScript runtime belongs to the WebView scope.

**Persistence.** `SetCultureAsync` applies the in-memory culture before writing the store. If the write fails, the in-memory culture has already changed; treat that as a failed request rather than reporting success. `BlazorCookieCultureStore` writes the canonical RequestLocalization cookie value, so the standard cookie provider reads exactly what it writes and the cookie survives the WebSocket upgrade. `BlazorLocalStorageCultureStore` uses one local-storage key. Without a store, the no-op store silently keeps the choice in memory only.

**Browser-language detection.** `DetectBrowserLanguage` applies only in the static scope, is read-only (never persisted), and is best-effort. `InitializeAsync` checks the stored choice first, then `navigator.languages` (falling back to `navigator.language`), mapping each preference by exact tag and then its parent chain against `SupportedCultures`. An empty supported list accepts the first recognized browser preference. Interop unavailability or an empty language list means no detection, not an exception.

**JavaScript interop and CSP.** `BlazorCultureJsInterop` is self-contained: it invokes small `eval` scripts for cookies, local storage, and browser languages, with dynamic values serialized through a source-generated JSON context so a value cannot break out of the script. No host JavaScript file is required. A strict Content Security Policy blocks `eval`; hosts that cannot allow it should supply a named JavaScript module and a custom `IBlazorCultureStore` instead.

**WASM (WebAssembly) provider guard.** `AddBlazorWasmLocalization` requires every registered `IInnerMessageProvider` to implement `IWasmCompatibleMessageProvider` (template resolution without file, database, or network I/O). Browser WebAssembly cannot perform synchronous file or database I/O, so server-oriented providers would fail at the first lookup. The guard rejects them at registration with an actionable error listing the offenders.

**Framework constraint.** Because `CircuitHandler` requires `Microsoft.AspNetCore.App`, this package build is not consumable from browser WASM; the static-scope contracts remain reusable.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Integrations.RequestLocalization](../Jeninnet.Localization.Integrations.RequestLocalization/README.md)
- [Jeninnet.Localization.Integrations.JavaScript](../Jeninnet.Localization.Integrations.JavaScript/README.md)
- [Jeninnet.Localization.Integrations.Diagnostics](../Jeninnet.Localization.Integrations.Diagnostics/README.md)
- [Jeninnet.Localization.Abstractions](../../src/Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [Blazor](../../docs/en/02-guides/09-blazor.md)
- [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [JavaScript and client delivery](../../docs/en/02-guides/10-javascript-and-client.md)

## License

MIT — see [LICENSE](../../LICENSE).
