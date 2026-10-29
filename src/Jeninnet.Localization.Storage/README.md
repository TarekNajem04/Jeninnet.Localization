# Jeninnet.Localization.Storage

`Jeninnet.Localization.Storage` defines the canonical message data models used by file providers and tooling: one message key maps to one template, and one culture owns a collection of those entries. The assembly has no dependencies beyond the BCL (Base Class Library) and contains no lookup, fallback, formatting, or persistence logic.

It sits below the providers that parse catalogs. JSON, XML, and PO (Portable Object) providers reference these models directly. The ADO.NET database providers and the EF Core store do not reference this package; the database providers map the documented table shape themselves, and the store uses `LocalizedStringEntry`. See [Storage API](../../docs/en/03-reference/02-storage-api.md) and [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Storage
```

The package normally arrives transitively through the JSON, XML, or PO provider. Reference it directly when you implement a provider, importer, exporter, or tool that needs the canonical models.

## Registration

There is nothing to register: the package exposes data types, not services or DI (Dependency Injection) extensions. Register the pipeline and a provider instead.

```csharp
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Json.Extensions;

services.AddLocalization();
services.AddJsonLocalization(options => options.FilePath = "Localization/en.json");
```

## Configuration

The package has no options type. The contracts are the model itself:

| Type | Members | Role |
|----|----|----|
| `LocalizedMessageEntry` | `MessageKey` (`string`, required), `Template` (`string`, required) | One key/template pair for one culture, without persistence identity. |
| `LocalizedMessageEntity` | `Id` (`long`), `MessageKey`, `Culture`, `Template` | Persistence entity for the canonical `LocalizationMessages` table; `Id` is the auto-generated key and `(MessageKey, Culture)` is the documented unique pair. |
| `LocalizationResource` | `Entries` (`IReadOnlyCollection<LocalizedMessageEntry>`, required) | A complete set of entries for exactly one culture. |

All members are `init`-only after construction, which makes an instance safe to share between readers. `LocalizedMessageEntity` documents the database shape but no in-box package maps it with an ORM (Object-Relational Mapping); the ADO.NET providers issue their own parameterized SQL, and the EF store has a separate model.

## Usage

Build a culture's entries, project them into the dictionary shape used by the memory provider, and format through the canonical pipeline:

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Memory.Extensions;
using Jeninnet.Localization.Storage;
using Microsoft.Extensions.DependencyInjection;

var resource = new LocalizationResource {
    Entries = [
        new LocalizedMessageEntry { MessageKey = "Greeting", Template = "Hello, {name}!" },
        new LocalizedMessageEntry { MessageKey = "Cart.Items", Template = "You have {count} items." }
    ]
};

var templates = resource.Entries.ToDictionary(
    static entry => entry.MessageKey,
    static entry => entry.Template,
    StringComparer.Ordinal);

var services = new ServiceCollection();
services.AddLocalization();
services.AddMemoryLocalization(templates);

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var message = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(message); // Hello, Ada!
```

A persistence row uses the full entity shape:

```csharp
var entity = new LocalizedMessageEntity {
    MessageKey = "Greeting",
    Culture = "en",
    Template = "Hello, {name}!"
};
```

## Behavior and limits

- **Data only.** The package performs no lookup, culture fallback, formatting, caching, or I/O. Those behaviors belong to [`Jeninnet.Localization.Core`](../Jeninnet.Localization.Core/README.md) and the providers.
- **One culture per resource.** `LocalizedMessageEntry` carries no culture; culture lives on `LocalizedMessageEntity` or in the identity of a `LocalizationResource` instance. Culture selection and fallback are never implicit here.
- **Required members.** `MessageKey`, `Template`, `Culture`, and `Entries` are `required`; the C# compiler enforces them at construction. The models do not validate key shape, culture codes, or uniqueness at runtime.
- **Not a store model.** These types are distinct from the resource-set contracts (`LocalizedStringEntry`, `ILocalizationStore`) in Abstractions. A `LocalizationResource` is a catalog snapshot, not a mutable store set.
- **Database boundary.** The canonical table has `Id`, `MessageKey`, `Culture`, and `Template` with a unique `(MessageKey, Culture)` pair. The database provider packages do not depend on this assembly; use `LocalizedMessageEntity` only in your own persistence or tooling code.
- **Thread-safety.** Instances are immutable after construction and can be shared between threads.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Relationship |
|----|----|
| [`Jeninnet.Localization.Abstractions`](../Jeninnet.Localization.Abstractions/README.md) | Pipeline and store contracts. |
| [`Jeninnet.Localization.Core`](../Jeninnet.Localization.Core/README.md) | Localizer, formatters, and registration. |
| [`Jeninnet.Localization.Providers.Json`](../providers/Jeninnet.Localization.Providers.Json/README.md) | Parses JSON catalogs into these models. |
| [`Jeninnet.Localization.Providers.Xml`](../providers/Jeninnet.Localization.Providers.Xml/README.md) | Parses XML catalogs into these models. |
| [`Jeninnet.Localization.Providers.Po`](../providers/Jeninnet.Localization.Providers.Po/README.md) | Parses gettext catalogs into these models. |

## Documentation

- [Storage API](../../docs/en/03-reference/02-storage-api.md)
- [Resource sets and the store](../../docs/en/02-guides/06-resource-sets-and-store.md)
- [Package map](../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
