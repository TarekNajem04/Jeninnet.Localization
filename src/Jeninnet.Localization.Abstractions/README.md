# Jeninnet.Localization.Abstractions

`Jeninnet.Localization.Abstractions` is the contract-only assembly of the framework. It defines the message pipeline (`IMessageProvider`, `IMessageLocalizer`, formatters), culture and plural primitives, diagnostics names, and the resource-set store contracts. It contains no lookup, fallback, formatting, or storage implementation and references only the BCL (Base Class Library).

The package sits below every other runtime package: `Jeninnet.Localization.Core` implements the pipeline against these contracts, providers implement `IInnerMessageProvider`, and stores implement `ILocalizationStore`. See [Core API](../../docs/en/03-reference/01-core-api.md), [Storage API](../../docs/en/03-reference/02-storage-api.md), and the [mental model](../../docs/en/01-getting-started/04-mental-model.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Abstractions
```

Applications normally receive the assembly transitively through `Jeninnet.Localization.Core` or a provider package. Reference it directly when you implement a provider, formatter, store, or diagnostic integration.

## Registration

This package has no `IServiceCollection` extension methods. The pipeline is registered by `Jeninnet.Localization.Core`; provider packages add their own registrations.

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["Greeting"] = "Hello, {name}!"
});
```

Core binds the registered `IInnerMessageProvider` services to exactly one `IMessageProvider` through `LocalizationProviderSelector`. A custom source implements `IInnerMessageProvider` (typically by deriving from `MessageProviderBase`) and is registered as `services.AddSingleton<IInnerMessageProvider, MyProvider>();`.

## Configuration

`LocalizationOptions` is the pipeline options type. It is defined here and consumed by Core's `AddLocalization(Action<LocalizationOptions>)` overload.

| Property | Type | Default | Meaning |
|----|----|----|----|
| `DefaultCulture` | `string` | `"en"` | Final fallback culture after the requested culture and its parents. Empty disables the default-culture step. The value is captured when `DefaultMessageLocalizer` is constructed. |
| `MissingKeySink` | `IMissingKeySink?` | `null` | Opt-in miss observer, consulted only after the complete fallback chain misses. `null` disables collection. |

Store-side options defined in this package:

| Type | Properties | Defaults |
|----|----|----|
| `ResourceAdminOptions` | `DryRun` (`bool`), `Actor` (`string`) | `false`, `""` |
| `ResourceSynchronizationOptions` | `DefaultCulture`, `TargetCultures`, `DryRun`, `AllowUpdates`, `CreateMissingTargetCultures`, `Renames`, `Actor` | `""`, empty list, `false`, `false`, `false`, empty map, `""` |

## Usage

Resolve `IMessageLocalizer` and pass the key, parameters, and culture. The localizer resolves the template through the provider, walks fallback, formats the result, and returns `null` for a complete miss.

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

var message = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(message ?? "(missing)"); // Hello, Ada!
```

Plural and context lookups use the same pipeline:

```csharp
var items = await localizer.LocalizePluralAsync(
    "Cart.One", "Cart.Other", 3,
    new Dictionary<string, object?> { ["count"] = 3 },
    new CultureInfo("en"));

var open = await localizer.LocalizeWithContextAsync(
    "Open", "menu",
    new Dictionary<string, object?>(),
    new CultureInfo("en"));
```

## Behavior and limits

- **Contracts only.** The package does not walk cultures, format templates, read files, query databases, or cache results.
- **Exactly one provider.** Core's `LocalizationProviderSelector` delegates to one `IInnerMessageProvider`. Zero providers makes every lookup a miss; more than one throws `InvalidOperationException` when the selector is constructed. Providers are never chained.
- **Providers are exact-match.** A provider is asked for one key and one culture and returns `null` on a miss. It never applies a parent culture, a default culture, or another provider.
- **Fallback ownership.** `DefaultMessageLocalizer` owns the only fallback algorithm: requested culture, its parents (invariant is never queried), then the default culture once. An empty default culture removes the last step.
- **Plural selection.** `LocalizePluralAsync` first consults `IPluralFormMessageProvider` when available. Otherwise the CLDR (Common Locale Data Repository) cardinal category selects the singular key for `One` and the plural key for every other category; if the preferred key misses, the other key is tried, and a complete miss returns `null`. The count is not injected into the parameters.
- **Context namespacing.** `LocalizeWithContextAsync` sends `context + '\u0004' + key` to the provider. An empty context behaves exactly like `LocalizeAsync`.
- **Store lookup is separate.** `ILocalizationStore` resolves an exact `(resourceSet, culture)` pair with no fallback. `GetStringAsync` returns `LocalizedStringEntry.Missing(...)` (`Value = "[MISSING:key]"`, `ResourceNotFound = true`) instead of `null`.
- **Cancellation and failures.** Cancellation is observed after argument validation, propagates through fallback and provider I/O, and is never converted into a miss. Provider and formatter exceptions also propagate.
- **Thread-safety.** The contracts do not prescribe an implementation. The default localizer is a singleton and captures its culture and sink configuration at construction; implementations must be safe for concurrent lookups.
- **Diagnostics.** `LocalizationDiagnostics` exposes an `ActivitySource` and a `Meter` named `Jeninnet.Localization`. Template text is never tagged.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Relationship |
|----|----|
| [`Jeninnet.Localization.Core`](../Jeninnet.Localization.Core/README.md) | Implements the pipeline against these contracts and provides DI (Dependency Injection) registration. |
| [`Jeninnet.Localization.Storage`](../Jeninnet.Localization.Storage/README.md) | Canonical message data models used by file providers and tooling. |
| [`Jeninnet.Localization.Providers.Memory`](../providers/Jeninnet.Localization.Providers.Memory/README.md) | Smallest `IInnerMessageProvider` implementation. |
| [`Jeninnet.Localization.Providers.Database.EntityFrameworkCore`](../providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | Store implementation for `ILocalizationStore`, `IResourceAdminStore`, and `IResourceHistoryStore`. |

## Documentation

- [Core API](../../docs/en/03-reference/01-core-api.md)
- [Storage API](../../docs/en/03-reference/02-storage-api.md)
- [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Package map](../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
