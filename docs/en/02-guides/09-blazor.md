# Blazor

Blazor changes what "the current culture" means. A server-side circuit is a long-lived session
multiplexed with many others on the same process; a client-side host is a single-user process where
ambient culture is application state. A culture mechanism that works for a request cannot be reused
unchanged for either.

The integration makes the scope explicit. Server mode keeps culture per circuit and never touches the
process ambient culture. Static mode applies the ambient culture and raises an event so components
re-render. The message pipeline is untouched; fallback stays in `DefaultMessageLocalizer`.

## Choosing a scope

`BlazorLocalizationOptions.Mode` selects the scope:

- **`BlazorLocalizationMode.Server`** for Blazor Server and interactive Server components. The
  culture service is scoped, one instance per circuit. The circuit handler seeds it from the request
  culture when the circuit opens. Ambient culture is never assigned, so concurrent circuits cannot
  bleed into each other.
- **`BlazorLocalizationMode.Wasm`** for client-side hosts and Blazor Hybrid. The default
  registration is a singleton and applies `CultureInfo.CurrentCulture`, `CurrentUICulture`, and the
  two `DefaultThreadCurrent*` properties. This is safe because the process serves one user. In
  WebView2, register the service and store as scoped instead (see the Hybrid section), because the
  JavaScript runtime belongs to the WebView scope.

**Note:** `AddBlazorWasmLocalization` additionally requires every registered provider to declare
`IWasmCompatibleMessageProvider`. For desktop Blazor Hybrid, use
`AddBlazorLocalization(options => options.Mode = BlazorLocalizationMode.Wasm)`; the scope is the
same, only the guard differs.

## Server registration and the circuit lifetime

```csharp
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

The composition registers `BlazorCultureService` as scoped and `BlazorCircuitCultureHandler` as a
`CircuitHandler`. Register `BlazorCookieCultureStore` as scoped too: it depends on the
circuit-scoped `IJSRuntime`, so a singleton registration fails DI (Dependency Injection) validation. The handler reads
`IRequestCultureFeature` from the connection's HTTP request and
seeds the circuit service. The request culture was resolved by `RequestLocalizationMiddleware` from
the culture cookie, the one mechanism that survives the WebSocket upgrade.

Seeding is lenient by design. An unrecognized or unsupported request culture is ignored; the circuit
then uses `DefaultCulture` or the ambient culture. A bad cookie must not fail a connection.

The circuit's culture is effectively fixed for the circuit's lifetime. `SetCultureAsync` persists the
choice through `IBlazorCultureStore`, and the host reloads the page so the request pipeline applies
the new culture to server-rendered content. A culture switch therefore creates a new circuit.

```razor
@inject BlazorCultureService CultureService
@inject NavigationManager Navigation

<select @onchange="OnCultureChangedAsync">
    @foreach (var culture in ReferenceCultures.SupportedCultures) {
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

`CultureService.Culture` is `null` until a culture is applied. `CultureService.CultureInfo` resolves
`DefaultCulture` or the ambient UI culture, which makes it safe to use for `dir` and `lang`
attributes before seeding.

## The Server store

`BlazorCookieCultureStore` writes the RequestLocalization cookie through
`CookieRequestCultureProvider.MakeCookieValue` and reads it with `ParseCookieValue`. The standard
cookie provider therefore reads exactly what the store writes. The cookie name defaults to
`CookieRequestCultureProvider.DefaultCookieName`; the lifetime defaults to 365 days.

The host must register a store. Without one, the composition registers an internal no-op store and
the choice stays in process memory only, which means it disappears with the circuit. The package
deliberately does not choose storage for the host.

`BlazorCultureService.SetCultureAsync` applies the culture first, then writes the store. If the write
fails, the in-memory culture has already changed. Treat a persistence failure as a request failure
and let the page reload path recover; do not report success based on the method returning.

## Hybrid and client-side hosts

```csharp
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);
services.AddBlazorLocalization(options => {
    options.Mode = BlazorLocalizationMode.Wasm;
    options.DefaultCulture = "en";
    options.DetectBrowserLanguage = true;
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
});

// WebAssembly: the JavaScript runtime is a singleton.
services.AddSingleton<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

In Blazor Hybrid/WebView2 the JavaScript runtime comes from the WebView scope. Override the
static registrations with scoped ones so the service does not capture the root runtime:

```csharp
services.AddScoped<BlazorCultureService>();
services.AddScoped<IBlazorCultureStore, BlazorLocalStorageCultureStore>();
```

The local-storage store reads and writes one key, `CultureStorageKey` (default
`jeninnet.localization.culture`). Call `InitializeAsync` once after the first render; JavaScript
interop is not available before then in client-side hosts.

```razor
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            await CultureService.InitializeAsync();
        }
    }
}
```

`InitializeAsync` reads the stored choice and applies it when it differs from the current one. With
`DetectBrowserLanguage` enabled it then falls back to browser negotiation: `navigator.languages`,
then `navigator.language`, mapped against `SupportedCultures` by exact tag first and then the
browser tag's parent chain (`de-DE` to `de`). Detection is static-scope only, read-only, and
best-effort; interop failure or an empty list means no detection, not an exception. An empty supported
list accepts the first recognized browser preference. The choice is never persisted by detection, so
the store keeps explicit user choices only.

Subscribe to `CultureChanged` to re-render; the event carries the applied name and `CultureInfo`.

## Dynamic components and the pipeline

The integration is an adapter over `CultureInfo`, DI, and `IJSRuntime`; it does not wrap the provider
pipeline. In Server mode the pipeline formats with the culture passed by the caller. The convenience
overload of `IMessageLocalizer` uses ambient UI culture, which Server mode deliberately does not
change, so a component that wants the circuit's culture passes `CultureService.CultureInfo`:

```csharp
var text = await Localizer.LocalizeAsync("Cart.Items", parameters, CultureService.CultureInfo);
```

Reading the circuit service is cheap and explicit; mutating ambient culture would trade correctness
for convenience.

`IStringLocalizer<T>` is different: the store bridge captures the ambient UI culture at creation.
In Server mode that is the request culture, which is the right value for server-rendered content. Do
not hold such a localizer in a singleton; resolve it per component scope or use the pipeline with
the circuit culture. See [ASP.NET Core and MVC](08-aspnet-core-and-mvc.md).

## The WASM provider guard

Browser WebAssembly cannot perform synchronous file or database I/O, and many server-oriented
providers would fail at the first lookup. `AddBlazorWasmLocalization` enforces the provider subset at
registration time:

- Every registered `IInnerMessageProvider` must implement `IWasmCompatibleMessageProvider`.
- Factory-registered providers cannot be verified and are rejected by description.
- The Memory and Pseudo providers declare the marker; file, database, ResX, satellite, and
  translation providers do not.
- Register the provider before calling the composition; the guard inspects the service collection.

**Note:** the package currently declares a `FrameworkReference` on `Microsoft.AspNetCore.App`
because `CircuitHandler` lives in a framework-only assembly. A browser-WASM (WebAssembly) project cannot consume
that package build directly. The static-scope semantics and the provider guard are still the
contracts a components-only build would reuse. Blazor Hybrid can use the package today.

## JavaScript interop

`BlazorCultureJsInterop` is self-contained. It invokes small `eval` scripts for cookies, local
storage, and browser languages; dynamic values are serialized through a source-generated JSON context
so a value cannot break out of the generated script. No host JavaScript file is required.

The trade-off is a strict Content Security Policy. `eval` is blocked unless the policy allows it,
and allowing it weakens the policy for the whole page. Hosts with a strict CSP should either provide
a small named JavaScript module and a custom `IBlazorCultureStore`, or accept the policy exception.
The package does not hide this choice.

## Worked example: two concurrent circuits

Two users open the application at the same time. One arrives with an Arabic cookie, the other with
English. The middleware resolves `ar` and `en`; each connection opens its own circuit; each
`BlazorCircuitCultureHandler` seeds its scoped `BlazorCultureService`. A component in the Arabic
circuit calls `CultureService.CultureInfo` and gets `ar`; a component in the English circuit gets
`en`. Neither assignment touches `CultureInfo.CurrentUICulture`, so there is no cross-talk even
though both circuits run on the same process.

The Arabic user switches to English. `SetCultureAsync("en")` writes the cookie, the page reloads, the
middleware applies `en`, and a new English circuit opens. The old circuit is gone. The store now
records `en` for the next visit.

## Pitfalls

**Pitfall:** mutating ambient culture in Server mode to make convenience lookups work. That is
exactly the cross-circuit leak the scope exists to prevent.

**Pitfall:** forgetting `IBlazorCultureStore`. The choice works for the current session and vanishes
on the next circuit or process start. The no-op store is silent by design.

**Pitfall:** enabling `AddBlazorWasmLocalization` with a file provider. Registration fails, and the
error lists the provider. Use the static scope without the guard for Blazor Hybrid.

**Pitfall:** treating `SetCultureAsync` as transactional across storage. The in-memory culture is
applied before the store write.

## Where to go next

- [ASP.NET Core and MVC](08-aspnet-core-and-mvc.md) for request localization and the store bridge.
- [Cultures and fallback](03-cultures-and-fallback.md) for how the circuit culture meets fallback.
- [RTL (Right-to-Left) and bidirectional text](16-rtl-and-bidi.md) for direction and bidi isolation in Blazor hosts.
- [JavaScript and client delivery](10-javascript-and-client.md) for browser payloads outside Blazor.
