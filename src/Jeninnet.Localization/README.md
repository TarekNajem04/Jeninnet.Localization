# Jeninnet.Localization

The discoverability package for `Jeninnet.Localization`: installing it brings the canonical
localization pipeline — contracts (`Jeninnet.Localization.Abstractions`), the localizer pipeline
(`Jeninnet.Localization.Core`), and the resource-set storage model
(`Jeninnet.Localization.Storage`) — in one reference. It carries no messages itself and it
registers no provider: exactly one message provider is still required, and the choice stays with
the consumer. The one-provider rule is unchanged.

## Installation

```bash
dotnet add package Jeninnet.Localization --version 1.0.0-preview.1
```

Then add exactly one message provider, for example:

```bash
dotnet add package Jeninnet.Localization.Providers.Json --version 1.0.0-preview.1
```

## Registration

```csharp
// Register the base pipeline (localizer, formatter, provider selector).
services.AddLocalization();

// Register exactly one message source, for example:
services.AddJsonLocalization(options => options.FilePath = "Localization/messages.json");
```

## Behavior and limits

- The metapackage adds no behavior of its own: resolution, fallback, formatting, and storage
  semantics are owned by the three referenced packages and documented in their READMEs and in
  the [localization book](https://tareknajem04.github.io/Jeninnet.Localization/).
- Depending on the metapackage instead of the three packages directly changes nothing at
  runtime; the package graph is identical apart from the extra metapackage identity.
- For web, desktop, and mobile hosts, see the bridge and integration packages
  (`Jeninnet.Localization.Integrations.*`).

## Related packages

- [`Jeninnet.Localization.Abstractions`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [`Jeninnet.Localization.Core`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md)
- [`Jeninnet.Localization.Storage`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Storage/README.md)
