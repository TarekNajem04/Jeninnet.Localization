# Jeninnet.Localization.Providers.Caching

Message-provider cache decorators: `CachedMessageProvider` (in-memory TTL) and `DistributedCachedMessageProvider` (`IDistributedCache`-backed TTL) wrap exactly one inner `IInnerMessageProvider` and serve repeat lookups without re-acquiring. No new dependency beyond BCL plus `Microsoft.Extensions.*` abstractions.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Caching --version 1.0.0-preview.1
```

## Registration

```csharp
services.AddCachedMessageProvider(
    static _ => new JsonMessageProvider("Localization/en.json"),
    static options => options.CacheExpiration = TimeSpan.FromMinutes(30));
```

Or wrap an existing instance (for example in tests):

```csharp
services.AddCachedMessageProvider(inner);
```

Distributed caching resolves `IDistributedCache` from the service provider:

```csharp
services.AddDistributedCache(...); // your IDistributedCache registration
services.AddDistributedCachedMessageProvider(
    static _ => new JsonMessageProvider("Localization/en.json"));
```

The decorator is registered as the single `IInnerMessageProvider`; Core's exactly-one selector delegates to it. The inner provider should live for the process lifetime, like any provider singleton.

## Configuration

`MessageProviderCacheOptions` is a construction snapshot, not an `IOptions<T>` service.

| Property | Type | Default | Notes |
|----|----|----|----|
| `CacheExpiration` | `TimeSpan` | 1 hour | TTL per entry. Must be positive. |
| `MaxEntries` | `int` | 4096 | In-memory bound; the entry closest to expiry is evicted first. Must be at least 1. The distributed decorator namespaces entries by generation instead of enumerating them. |

Mutating the options object after registration does not affect the decorator.

## Usage

```csharp
var text = await localizer.LocalizeAsync("Cart.Title", null, CultureInfo.GetCultureInfo("en"));
```

The first lookup acquires through the inner provider and caches the template; repeats serve from cache. A miss on the inner provider caches as a miss result for the TTL window (matching the provider pipeline's missing-resource semantics: a later lookup retries only after expiry or refresh).

## Behavior and limits

- **Capabilities.** Both decorators honestly report every capability (`MultiKeys`, `EventsListening`, `Preload`, `Refresh`, `Caching`): multi-key lookups fan out per key through the cache, joining in-flight acquisitions; when the inner provider implements `IMultiKeyMessageProvider`, one batch call is used. Preload fills the cache. Refresh clears the decorator cache and forwards to the inner provider when it is refreshable.
- **Events.** The decorators own their `TranslationAcquired`/`AcquisitionFailed` events, raised on decorator-level external acquisitions (inner calls) only; cache hits raise no event. Inner-provider events are not forwarded.
- **Identity.** Distributed entries are keyed by decorator instance, cache generation, culture, and a SHA-256 hash of the key, so keys of any shape stay within store key limits and two decorators never share entries.
- **Invalidation.** Refresh clears the in-memory cache and advances a generation (stale pre-refresh completions cannot repopulate it). Distributed entries become unreachable on refresh and expire naturally; no key enumeration is ever performed.
- **Null templates.** A `null` template is cached as a miss result (in-memory) or a single `0x00` marker byte (distributed), staying distinguishable from an absent entry.
- **TTL enforcement.** The in-memory decorator checks expiry on read against an injectable clock (deterministic under test). Distributed TTL is enforced by the store via absolute expiration.
- **No disposal.** Neither decorator owns a watcher or timer, so neither implements `IDisposable`.
- **One provider per composition.** The decorator counts as the single registered provider; registering another source throws `InvalidOperationException` at resolution.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md)
- [Jeninnet.Localization.Storage](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md)
- [Jeninnet.Localization.Providers.Json](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Json/README.md)

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [Choosing a provider](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/01-choosing-a-provider.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
