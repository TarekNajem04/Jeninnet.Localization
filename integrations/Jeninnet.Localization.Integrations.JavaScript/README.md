# Jeninnet.Localization.Integrations.JavaScript

JavaScript resource delivery for `Jeninnet.Localization`. The package serves an explicit allow-list of message templates for one culture as a flat JSON object (`{ key: template }`), either from a versioned minimal-API endpoint or from static `{culture}.json` files. It is the `GetResourcesAsJavascriptObject` capability rebuilt on the canonical provider pipeline: exact-match lookups, no store dump, and content-hash ETag revalidation.

The integration is deliberately small. It ships no JavaScript client library, no formatting engine, and no culture switcher. It defines the payload contract and the HTTP semantics; the client chooses the culture and owns rendering. See [JavaScript and client delivery](../../docs/en/02-guides/10-javascript-and-client.md) and [Security](../../docs/en/04-operations/02-security.md).

## Installation

```bash
dotnet add package Jeninnet.Localization.Integrations.JavaScript
```

The package declares a `FrameworkReference` on `Microsoft.AspNetCore.App` for minimal-API mapping and results.

## Registration

Register the provider pipeline first, then the integration, then map the endpoint:

```csharp
using Jeninnet.Localization.Integrations.JavaScript.Extensions;

services.AddLocalization(options => options.DefaultCulture = "en");
services.AddJsonLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"]);

services.AddJavaScriptLocalization(options => {
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.AllowedKeys.Add("App.Title");
    options.AllowedKeys.Add("Catalog.Heading");
});

var app = builder.Build();
app.MapJavaScriptLocalization();
```

`AddJavaScriptLocalization` registers the options snapshot and the `JavaScriptResourcePayloadBuilder`. `MapJavaScriptLocalization` maps `GET` on the configured route pattern. Mapping before registration throws with an actionable message; registering the composition twice throws.

## Configuration

`JavaScriptLocalizationOptions` exposes:

| Option | Default | Meaning |
|----|----|----|
| `RoutePattern` | `/localization/v1/{culture}` | Endpoint route; must contain the `{culture}` parameter. The version is the URL segment. |
| `CacheControlHeader` | `public, max-age=0, must-revalidate` | Response `Cache-Control`; null or empty omits the header. |
| `AllowedKeys` | empty | Explicit key allow-list. Must be non-empty, non-blank, and duplicate-free. |
| `SupportedCultures` | empty | Served cultures; empty serves any recognized culture. |

The registration validates all of these immediately. Every supported culture name must be recognized. `AllowedKeys` is mandatory because whole stores are never dumped; only the listed keys are ever requested from the provider.

## Usage

The endpoint response is the payload itself:

```text
GET /localization/v1/ar
ETag: "3ab6…"
Cache-Control: public, max-age=0, must-revalidate

{ "App.Title": "مكتبة …", "Catalog.Heading": "الفهرس" }
```

A client fetches the payload for the selected culture:

```js
const culture = localStorage.getItem("culture") ?? "en";
const response = await fetch(`/localization/v1/${encodeURIComponent(culture)}`, {
  headers: { "Accept": "application/json" }
});

if (!response.ok) {
    throw new Error(`Localization payload unavailable for ${culture}`);
}

const messages = await response.json();
const t = (key) => Object.hasOwn(messages, key) ? messages[key] : key;
```

For build-time hosting, export the same bytes to static files:

```csharp
using Jeninnet.Localization.Integrations.JavaScript;

var builder = new JavaScriptResourcePayloadBuilder(
    provider.GetRequiredService<IMessageProvider>(),
    options);

await builder.ExportToDirectoryAsync("wwwroot/localization", ["en", "ar", "ar-SY"]);
```

Each file is named `{culture}.json` and is byte-identical to the endpoint response body. Cultures that are unrecognized or outside `SupportedCultures` are skipped by export and produce `404` from the endpoint.

## Behavior and limits

**Payload contract.** A payload is a flat JSON object of allow-listed keys in allow-list order. `JavaScriptResourcePayload` exposes the normalized `Culture`, the JSON string, the exact UTF-8 (Unicode Transformation Format, 8-bit) bytes (`Utf8Json`), and a quoted strong `ETag` computed as the lowercase SHA-256 (Secure Hash Algorithm, 256-bit) hex digest of those bytes. One serialization feeds both the endpoint and the static export, so the bytes cannot disagree.

**Exact match, no fallback.** Keys resolve through the exact requested culture only. A key the provider does not serve for that culture is omitted; it is not filled from a parent culture. Culture fallback remains owned by `DefaultMessageLocalizer` and is not reimplemented here. Hosts that want fallback values to reach clients should serve the fallback culture or allow-list the parent culture's keys separately.

**HTTP semantics.** A matching `If-None-Match` returns `304 Not Modified` with no body. Comparison is the RFC 9110 weak comparison: `W/"…"` forms are accepted and `*` matches. `Cache-Control` defaults to revalidation rather than long-lived immutable caching. Because payloads are culture-level and not per-user, a shared cache or CDN may hold them. The endpoint builds the payload on every request and has no provider-level cache of its own; ETag revalidation saves bandwidth, not the provider lookups that compute the tag.

**Culture surface.** With an empty `SupportedCultures`, the endpoint answers for every culture the runtime recognizes. Declare the served set for a fixed product language list. The culture is normalized through `CultureInfo`, and `404` covers both an unrecognized culture and one outside the supported set.

**JavaScript safety, not HTML sanitization.** Templates are serialized with the System.Text.Json JavaScript encoder. Quotes, backslashes, control characters, HTML-sensitive characters (`<`, `>`, `&`, `'`, `+`), U+2028/U+2029, and non-ASCII characters are escaped, so the same bytes are safe as a JSON body, an inline `<script>` object literal, or a JavaScript string context. This is not HTML sanitization: a trusted template written into the DOM with `innerHTML` is still markup. Decide at the client boundary whether to use `textContent` or a reviewed HTML sink.

**Security.** The allow-list is the publication boundary: treat it as public API surface, not a convenience list of every known key. Payloads are culture-level; never include per-user content or personal data behind a shared cache. See the [security chapter](../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Integrations.Blazor](../Jeninnet.Localization.Integrations.Blazor/README.md)
- [Jeninnet.Localization.Integrations.RequestLocalization](../Jeninnet.Localization.Integrations.RequestLocalization/README.md)
- [Jeninnet.Localization.Abstractions](../../src/Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [JavaScript and client delivery](../../docs/en/02-guides/10-javascript-and-client.md)
- [ASP.NET Core and MVC](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Security](../../docs/en/04-operations/02-security.md)
- [Performance](../../docs/en/04-operations/03-performance.md)

## License

MIT — see [LICENSE](../../LICENSE).
