# Jeninnet.Localization.Core

`Jeninnet.Localization.Core` is the behavior layer of the framework: the message pipeline, the fallback-owning localizer, the formatters, the exactly-one provider selector, the store-backed ASP.NET Core `IStringLocalizer` bridge, store decorators, the resource synchronizer, and DI (Dependency Injection) registration.

Core sits above [`Jeninnet.Localization.Abstractions`](../Jeninnet.Localization.Abstractions/README.md) and never depends on a provider, integration, translation, or storage implementation. Read [Core API](../../docs/en/03-reference/01-core-api.md) and [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md) for the second domain.

## Installation

```powershell
dotnet add package Jeninnet.Localization.Core
```

Add exactly one provider package next; Core alone has no messages to serve. The target frameworks are .NET 8, .NET 9, and .NET 10.

## Registration

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;

// Registers the pipeline and the store-backed IStringLocalizerFactory.
services.AddLocalization(options => options.DefaultCulture = "en");

// Register exactly one message source (provider packages expose their own Add* methods).
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["Greeting"] = "Hello, {name}!"
});
```

The parameterless `AddLocalization()` registers a default `LocalizationOptions` with `TryAddSingleton`, so a pre-registered instance wins. The configuring overload throws `InvalidOperationException` when `LocalizationOptions` is already registered and validates a non-empty `DefaultCulture` with `CultureInfo.GetCultureInfo`.

| Service | Implementation | Lifetime |
|----|----|----|
| `IMessageLocalizer` | `DefaultMessageLocalizer` | Singleton (`TryAddSingleton`) |
| `IMessageFormatter` | `DefaultMessageFormatter` | Singleton (`TryAddSingleton`) |
| `IMessageProvider` | `LocalizationProviderSelector` | Singleton (`TryAddSingleton`) |
| `IStringLocalizerFactory` | `CustomStringLocalizerFactory` | Singleton (`Replace`) |
| `IStringLocalizer<>` | Framework `StringLocalizer<>` | Transient (`TryAddTransient`) |

Core does not register `ILocalizationStore`. The ASP.NET Core bridge is resolved only after a store is registered. `AddLocalization` collides by name with the `Microsoft.Extensions.Localization` extension; import the Jeninnet namespace explicitly.

## Configuration

`LocalizationOptions` is the only pipeline options type:

| Property | Type | Default | Meaning |
|----|----|----|----|
| `DefaultCulture` | `string` | `"en"` | Last fallback step. Empty disables it. Captured at localizer construction. |
| `MissingKeySink` | `IMissingKeySink?` | `null` | Opt-in miss observer, called only after the full chain misses. |

Formatter selection is registration-based. `AddLocalization` uses `TryAddSingleton`, so register your formatter first:

| Formatter | Purpose |
|----|----|
| `DefaultMessageFormatter` | Named `{key}` placeholders. Missing parameters stay literal; `null` becomes empty; there is no format-string handling; conversion uses ambient `CurrentCulture`. |
| `IcuMessageFormatter` | Opt-in ICU (International Components for Unicode) MessageFormat (`plural`, `select`, `selectordinal`, `number`, `date`, `time`, nesting). Templates without ICU constructs pass through the inner formatter. Malformed patterns throw `FormatException`. |
| `HtmlSafeMessageFormatter` | Encodes string arguments with `HtmlEncoder.Default`; trusted template markup passes through. Numbers, booleans, and date/time values pass through by identity so ICU selection still works. |
| `MarkdownMessageFormatter` | Renders a documented CommonMark subset before substituting values; arguments are never parsed as Markdown or HTML. Raw HTML is inert unless `allowRawHtml: true`. |

```csharp
services.AddSingleton<IMessageFormatter>(
    new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter())));
services.AddLocalization();
```

Store caching decorators are wired explicitly; there is no `Add...` extension. `CachedLocalizationStoreDecorator` uses `IMemoryCache`; `DistributedLocalizationStoreDecorator` uses `IDistributedCache` and `DistributedStoreCacheOptions`:

| Property | Type | Default | Constraint |
|----|----|----|----|
| `Expiration` | `TimeSpan` | 30 minutes | Must be positive. |
| `KeyPrefix` | `string` | `"Jeninnet.Localization"` | Must be non-empty and must not end with `:`. |

## Usage

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["Greeting"] = "Hello, {name}!",
    ["Cart.One"] = "You have {count} item.",
    ["Cart.Other"] = "You have {count} items."
});

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var greeting = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

var items = await localizer.LocalizePluralAsync(
    "Cart.One", "Cart.Other", 2,
    new Dictionary<string, object?> { ["count"] = 2 },
    new CultureInfo("en"));

Console.WriteLine(greeting); // Hello, Ada!
Console.WriteLine(items);    // You have 2 items.
```

Code-first synchronization is available through `ResourceSynchronizer.SynchronizeAsync` and `ResourceSynchronizationOptions`; `LocalizedResourceDiscovery` is the reflection-based, `RequiresUnreferencedCode` declaration finder.

## Behavior and limits

- **Fallback ownership.** `DefaultMessageLocalizer` queries the requested culture, then its parent cultures, never the invariant culture, then the configured default culture once. A default culture that equals the requested culture can therefore be queried twice on a miss. An empty default culture removes the final step.
- **Exactly one provider.** Zero `IInnerMessageProvider` registrations make every lookup return `null`; more than one throws `InvalidOperationException` during provider-selector construction. Chaining, ordering, and provider-level fallback do not exist.
- **Exact-match providers.** A provider resolves one key for one culture. Core performs the only parent and default-culture walk.
- **Misses versus failures.** A complete miss returns `null` and never calls the formatter. Provider and formatter exceptions, including cancellation, propagate and are never converted into a miss. Cancellation is observed after argument validation and before each fallback step.
- **Plural and context.** `LocalizePluralAsync` prefers `IPluralFormMessageProvider`; otherwise the CLDR (Common Locale Data Repository) cardinal category selects the singular key for `One`, every other category the plural key, and the other key is tried if the preferred one misses. `LocalizeWithContextAsync` uses `context + '\u0004' + key`; an empty context behaves like the plain key.
- **Singleton configuration.** The localizer and selector are singletons. `DefaultCulture` and `MissingKeySink` are captured when the localizer is constructed; mutate options only before resolution.
- **Store bridge.** `CustomStringLocalizerFactory.Create(Type)` uses `Type.Name` as the resource set; `Create(baseName, location)` uses `baseName` and treats `location` as contract validation only. The localizer captures `CurrentUICulture` at creation, re-probes that exact resource set on every lookup, and returns `[MISSING:key]` with `ResourceNotFound = true` for a miss. `GetAllStrings(true)` enumerates only the captured culture; the bridge does not walk parents. There is no root `IStringLocalizer` registration.
- **Synchronizer.** Adds only by default; differing default-culture values are skipped unless `AllowUpdates` is set. Non-default cultures are never updated; missing targets are skipped or created empty only with `CreateMissingTargetCultures`. Renames are explicit, conflicts are reported per key, dry runs mutate and audit nothing, and a second run is idempotent. There is no delete.
- **Thread-safety.** The pipeline is safe to resolve as a singleton. Cache decorators are per-process; distributed caching coordinates through `IDistributedCache`, not shared memory.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Role |
|----|----|
| [`Jeninnet.Localization.Abstractions`](../Jeninnet.Localization.Abstractions/README.md) | Contracts, options, store interfaces, diagnostics names. |
| [`Jeninnet.Localization.Storage`](../Jeninnet.Localization.Storage/README.md) | Canonical message models used by file providers and tooling. |
| [`Jeninnet.Localization.Providers.Json`](../providers/Jeninnet.Localization.Providers.Json/README.md) | A typical single- or multi-culture message source. |
| [`Jeninnet.Localization.Providers.Database.EntityFrameworkCore`](../providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | A store implementation for the `IStringLocalizer` bridge and governed catalogs. |

## Documentation

- [Core API](../../docs/en/03-reference/01-core-api.md)
- [Storage API](../../docs/en/03-reference/02-storage-api.md)
- [Formatting and plurals](../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)
- [Package map](../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
