# Jeninnet.Localization.Providers.Composite

Opt-in ordered composition of message providers for Jeninnet.Localization: each key resolves against the inner providers in construction order and the first exact (non-null) template wins. The composite invents nothing — no translation, no fallback chain, no merging.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Providers.Composite --version 1.0.0-preview.3
```

## Registration

```csharp
services.AddCompositeLocalization(
    _ => new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = overlay }),
    sp => new JsonMessageProvider(sp.GetRequiredService<IConfiguration>()["Catalogs:Main"]!));
```

The composite is the single `IInnerMessageProvider`; registering another provider alongside it still throws at the selector boundary.

## Behavior and limits

- **Ordering.** Inners are tried in construction order; the first non-null template wins. Empty strings are templates, not misses.
- **Failures propagate.** An inner throwing is a failure, not a miss: the first exception surfaces through `AcquisitionFailed` and propagates, and remaining inners are skipped.
- **Capability honesty.** `Supports` reports the OR over inners that declare the capability interface; batch, preload, and refresh fan out only to supporting inners (neutral when none does). Plural lookups forward to the first plural-capable inner.
- **Events are composite-owned.** One `TranslationAcquired` per resolved lookup, never per inner call; inner events are not forwarded.
- **Ownership.** The composite owns no watcher and implements no disposal abstraction (like the caching decorators). Inner lifetimes — including file watchers — belong to whoever constructed the inners; dispose a watching file provider directly when the composite is no longer needed.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. The composite performs no serialization and no reflection.

## Related packages

- [Jeninnet.Localization.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Abstractions/README.md)
- [Jeninnet.Localization.Providers.Memory](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Memory/README.md)

## Documentation

- [Localization book](https://tareknajem04.github.io/Jeninnet.Localization/)
- [Provider reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/03-provider-reference.html)

## License

[MIT](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
