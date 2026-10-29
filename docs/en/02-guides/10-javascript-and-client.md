# JavaScript and Client Delivery

Server-rendered pages can resolve messages while the page is being built. Browser code cannot. A
single-page application, a WebAssembly shell, or a desktop WebView needs the message templates as
data. The JavaScript integration serves exactly that: an explicit allow-list of templates for one
culture, as a flat JSON object.

The integration is deliberately small. It does not ship a JavaScript client library, a formatting
engine, or a culture switcher. It defines the payload contract and the HTTP semantics; the client
owns rendering and the choice of culture.

## Two delivery modes, one payload

`JavaScriptResourcePayloadBuilder` produces a `JavaScriptResourcePayload` for one culture:

- `Culture` — the normalized culture name.
- `Json` — a flat `{ "key": "template" }` object in allow-list order.
- `Utf8Json` — the exact UTF-8 (Unicode Transformation Format, 8-bit) bytes of that JSON document.
- `ETag` — a quoted strong entity tag computed as the lowercase SHA-256 (Secure Hash Algorithm, 256-bit) hex digest of those bytes.

The same builder feeds two delivery modes:

- **Endpoint** — `MapJavaScriptLocalization()` maps `GET` on the configured route pattern (default
  `/localization/v1/{culture}`) and serves the payload with its ETag and `Cache-Control`.
- **Static export** — `ExportToDirectoryAsync` writes `{culture}.json` files whose bytes are identical
  to the endpoint response bodies. `ExportAsync` returns the payloads in memory.

One serialization feeds both. A build-time export and a runtime response cannot disagree about the
JSON shape, ordering, or escaping.

## Registration and endpoint mapping

```csharp
using Jeninnet.Localization.Integrations.JavaScript.Extensions;

services.AddJavaScriptLocalization(options => {
    options.SupportedCultures.Add("en");
    options.SupportedCultures.Add("ar");
    options.AllowedKeys.Add("App.Title");
    options.AllowedKeys.Add("Catalog.Heading");
    options.CacheControlHeader = "public, max-age=0, must-revalidate";
});

var app = builder.Build();
app.MapJavaScriptLocalization();
```

The registration validates the options immediately. The route pattern must contain `{culture}`; the
allow-list must be non-empty, non-blank, and duplicate-free; every supported culture name must be
recognized. Registering the composition twice throws.

The endpoint is mapped as a `RequestDelegate`, not a minimal-API parameter-bound delegate. That
avoids reflection-based binding and keeps the endpoint usable in trimmed and Native AOT (Ahead-of-Time) hosts. The
initial `IMessageProvider` pipeline (Core `AddLocalization()` plus exactly one provider) must be
registered before the builder is resolved.

## The payload

`JavaScriptResourcePayloadBuilder.BuildAsync(culture)` asks the provider for every allow-listed key
for the **exact** culture. Keys the provider does not serve for that culture are omitted; they are
not filled from a parent culture. This is the same exact-match rule as the rest of the provider
layer. Culture fallback is owned by `DefaultMessageLocalizer` and is not reimplemented here.

```text
GET /localization/v1/ar
ETag: "3ab6…"
Cache-Control: public, max-age=0, must-revalidate

{ "App.Title": "مكتبة …", "Catalog.Heading": "الفهرس" }
```

The allow-list is the security boundary. Only listed keys are ever requested from the provider, so a
whole-store dump is impossible. The list is ordered: the JSON object preserves declaration order,
which makes payloads deterministic and easy to diff.

The serializer uses the System.Text.Json JavaScript encoder. Quotes, backslashes, control
characters, HTML-sensitive characters (`<`, `>`, `&`, `'`, `+`), U+2028/U+2029, and non-ASCII
characters are escaped. The same bytes are therefore safe as a JSON body, an inline `<script>` object
literal, or a JavaScript string context. `JSON.parse` round-trips to the original template text.

This is JavaScript-context safety, not HTML sanitization. If the client writes a template into the
DOM with `innerHTML`, markup in a trusted template is still markup. Store values are data; decide at
the client boundary whether to use `textContent` or a reviewed HTML sink. See
[Security](../04-operations/02-security.md).

## Culture switching and caching

Culture switching is a client decision. A typical flow stores the selected culture, requests the
matching payload, and re-renders:

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

The HTTP semantics are designed for revalidation rather than long-lived immutable caching:

- `ETag` is a strong quoted content hash. Equal bytes produce equal tags; changed bytes produce a
  different tag.
- A matching `If-None-Match` returns `304 Not Modified` with no body. The comparison is the RFC 9110
  weak comparison: `W/"…"` forms are accepted, and `*` matches.
- `Cache-Control` defaults to `public, max-age=0, must-revalidate`. Setting
  `CacheControlHeader` to null or empty omits the header. Because culture payloads are not
  per-user, a shared cache or CDN can hold them, but the default makes clients revalidate.
- Unrecognized cultures and cultures outside `SupportedCultures` return `404`.

The endpoint builds the payload on each request. It has no provider-level cache of its own. With a
file or memory provider that is cheap; with a database provider, every request performs one provider
lookup per allow-listed key. If that cost matters, put a fronting cache or CDN in place, or use the
static export. ETag revalidation avoids the response body, not the provider queries that compute the
tag.

**Note:** `SupportedCultures` empty serves any recognized culture. That is convenient for a catalog
directory that is discovered at startup, but it also means the endpoint will answer for every culture
the runtime recognizes. Declare the served set when the product has a fixed language list.

## Build-time export

Static hosting, offline shells, and WebAssembly packages often cannot or should not call the runtime
endpoint. Export the same bytes at build time:

```csharp
var builder = new JavaScriptResourcePayloadBuilder(
    provider.GetRequiredService<IMessageProvider>(),
    options);

await builder.ExportToDirectoryAsync("wwwroot/localization", ["en", "ar", "ar-SY"]);
```

Cultures that are unrecognized or not in `SupportedCultures` are skipped. The directory is created
when missing. Each file is named `{culture}.json` and is byte-identical to the endpoint body, so a
client can switch between static and dynamic delivery without changing its parser.

The export uses the same provider graph, which means it observes the provider's current state at
export time. If the provider is a database, export during deployment after the catalog is final, not
during request handling.

## Worked example: a SPA with a reviewed allow-list

A product team has a JSON catalog with four hundred keys, but the public storefront only needs
navigation, cart, and checkout strings. They create an explicit allow-list of sixty keys and register
the endpoint with `SupportedCultures` set to `en`, `ar`, and `de`.

The SPA loads `/localization/v1/en` at startup, stores the payload in memory, and re-fetches on a
language switch. The service worker caches successful responses by URL and sends `If-None-Match` on
revalidation; unchanged payloads return `304`. The checkout page, which runs in a separate WebView
without network access, ships the `en.json` and `de.json` files generated by
`ExportToDirectoryAsync`.

The remaining three hundred forty keys never cross the network. When a new public key is added, the
allow-list is a reviewable code change, and the parity tool reports whether the new key exists in all
three cultures. See [Parity and quality gates](13-parity-and-quality-gates.md).

## Pitfalls

**Pitfall:** expecting parent-culture fallback in the payload. A key present only in `ar` is absent
from `ar-SY`; the client gets `{}` for that key unless the host serves the parent culture or lists
the fallback culture explicitly.

**Pitfall:** dumping a store by adding every known key to the allow-list. The list is a publication
decision, not a convenience list. Treat it as part of the public API surface.

**Pitfall:** assuming the payload is HTML-safe. It is JavaScript-safe. Templates may contain trusted
markup, and the client decides how to render it.

**Pitfall:** putting per-user content behind a shared cache. Payload granularity is culture-level;
there are no per-user payloads. If a message contains personal data, it does not belong in this
endpoint.

**Pitfall:** omitting `SupportedCultures` and assuming the endpoint is closed. With an empty list,
every recognized culture is served.

**Pitfall:** treating the ETag as a free cache for provider work. It saves bandwidth, not the
provider lookups that build the payload.

**Pitfall:** mapping the endpoint before registering `AddJavaScriptLocalization`. `MapJavaScriptLocalization`
throws with an actionable message when the options are missing.

## Where to go next

- [ASP.NET Core and MVC](08-aspnet-core-and-mvc.md) for request culture selection on the server.
- [Blazor](09-blazor.md) for culture management inside Blazor hosts.
- [Security](../04-operations/02-security.md) for content trust and cache boundaries.
