# Jeninnet.Localization.Translation.Google

Google Cloud Translation provider for `Jeninnet.Localization`. The provider targets the legacy Google Translate v2 REST `translate` method, treats the message key as the source text, and returns the translated text for the requested culture. It implements only the mandatory canonical single-key lookup and reports every optional capability as unsupported.

This is a minimal provider by design. Every lookup is a paid network call: there is no batching, no provider cache, no preload, no refresh, and no acquisition event. Use it for low-volume dynamic content, not as a high-traffic catalog source. See [Machine translation](../../../docs/en/02-guides/12-machine-translation.md) for when a catalog provider or the Azure translation provider is the better fit.

## Installation

```bash
dotnet add package Jeninnet.Localization.Translation.Google
```

## Registration

The delegate overload validates the configured options immediately:

```csharp
using Jeninnet.Localization.Translation.Google.Extensions;

services.AddGoogleTranslationLocalization(options => {
    options.ApiKey = configuration["GoogleTranslate:ApiKey"]!;
});
```

The no-argument overload leaves options to the standard Options pipeline; the provider validates the resolved options at construction:

```csharp
services.AddGoogleTranslationLocalization();
services.Configure<GoogleTranslationOptions>(options => {
    options.ApiKey = configuration["GoogleTranslate:ApiKey"]!;
    options.Endpoint = "https://translation.googleapis.com/language/translate/v2";
});
```

A blank `ApiKey` or an invalid endpoint fails before the first translation call. The provider is registered as a singleton `IInnerMessageProvider` through `IHttpClientFactory`. Core's exactly-one provider selector delegates to it when it is the single registered provider.

## Configuration

`GoogleTranslationOptions` has two settings:

| Option | Default | Meaning |
|----|----|----|
| `ApiKey` | required | Google Cloud API key with translation access. |
| `Endpoint` | `https://translation.googleapis.com/language/translate/v2` | Base v2 endpoint; must be an absolute `http`/`https` URI. |

Credentials are ordinary options, not built-in environment-variable names. Bind them from the host's configuration; environment variables follow the standard `__` nesting, for example `GoogleTranslate__ApiKey`. Use User Secrets locally and a platform secret store in production.

## Usage

The provider has no batch API. Translate a scanned key set one key at a time and stage the results for review:

```csharp
using Jeninnet.Localization.Translation.Google;

var provider = services.GetRequiredService<IInnerMessageProvider>();
var culture = CultureInfo.GetCultureInfo("ar");

foreach (var sourceText in scannedKeys) {
    var draft = await provider.GetMessageTemplateAsync(sourceText, culture, cancellationToken);

    if (draft is not null) {
        staged[sourceText] = draft;
    }
}
```

Review the staged text before applying it. Check placeholders against the source, have a native reviewer edit the drafts, and then apply approved values through the governed store or the normal catalog release process. A successful response without a translation resolves to `null`; a non-success HTTP response throws `HttpRequestException`.

For request-time lookups, inject `IMessageLocalizer` and let the canonical pipeline call the provider:

```csharp
var text = await localizer.LocalizeAsync("Checkout.Pay", parameters, culture, cancellationToken);
```

## Behavior and limits

**Lookup semantics.** The key is the source text. The target language is `CultureInfo.Name`; the source language is not sent, so the service auto-detects it. The request uses the v2 `translate` method with `format=text`. An invariant-culture lookup returns `null` immediately without an HTTP request. The caller's cancellation token is passed to `SendAsync` and response deserialization, so cancellation surfaces as `OperationCanceledException`, never as a missing message. A successful response without a translation resolves to `null`.

**Capabilities.** `Supports` returns `false` for every `ProviderFeature`. There is no multi-key lookup, no cache, no preload, no refresh lifecycle, and no acquisition event. This honest negative report is the capability model's conformance check: a provider must not claim a capability it does not implement. Each lookup performs one HTTP request.

**Failure behavior.** A non-success status throws `HttpRequestException` with the status and reason phrase. There is no retry, circuit breaker, or rate limiter in this provider. Concurrent lookups are not coalesced. A transport failure or service outage propagates to the caller; the canonical pipeline does not convert a provider failure into a miss.

**Security.** The API key is sent as the documented `key` query parameter. Query strings are commonly captured by proxies, gateways, and request logging, so do not log full URLs or enable request logging that records them. Pin the endpoint to the expected host, require HTTPS, restrict outbound network access, and never put personal data or secrets into message keys: the key text is transmitted to the remote service and may be retained by it. See the [security chapter](../../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Translation.Azure](../Jeninnet.Localization.Translation.Azure/README.md)
- [Jeninnet.Localization.Integrations.Diagnostics](../../../integrations/Jeninnet.Localization.Integrations.Diagnostics/README.md)
- [Jeninnet.Localization.Abstractions](../../Jeninnet.Localization.Abstractions/README.md)

## Documentation

- [Machine translation](../../../docs/en/02-guides/12-machine-translation.md)
- [Configuring providers](../../../docs/en/02-guides/02-configuring-providers.md)
- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Security](../../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../../LICENSE).
