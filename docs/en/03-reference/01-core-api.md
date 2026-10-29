# Core API

This chapter is the contract reference for the message pipeline: the provider interface, the localizer
that owns fallback, the formatter that renders a hit, the selector that enforces the exactly-one rule,
and registration. Signatures are public surface; the invariants beside them are behavior a consumer
may rely on.

Generated member pages: <xref:Jeninnet.Localization.Abstractions.IMessageLocalizer>,
<xref:Jeninnet.Localization.Abstractions.IInnerMessageProvider>, and
<xref:Jeninnet.Localization.Abstractions.IMessageFormatter>.

## Pipeline types

```csharp
namespace Jeninnet.Localization.Abstractions;

public interface IMessageProvider {
    ValueTask<string?> GetMessageTemplateAsync(
        string key,
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}

public interface IInnerMessageProvider : IMessageProvider;
```

- `key` and `culture` are required; null throws `ArgumentNullException`.
- The provider is exact-match only. It never walks parents, applies a default culture, or consults
  another provider.
- A miss is `null`. Provider exceptions, including cancellation, propagate; a failed read is never a
  miss.
- The token is checked before work and forwarded to cancellable I/O.

Optional capabilities are separate contracts. `IProviderCapabilities.Supports(feature)` must agree
with the interfaces an instance implements. Features are `None`, `MultiKeys`, `EventsListening`,
`Preload`, `Refresh`, and `Caching`.

| Contract | Purpose |
|---|---|
| `IMultiKeyMessageProvider` | `GetMessageTemplatesAsync(keys, culture, token)` returns a dictionary. |
| `IPreloadableMessageProvider` | `PreloadAsync(keys, culture, token)` warms provider-owned state. |
| `IRefreshableMessageProvider` | `RefreshAsync(token)` invalidates local state; a cancelled token prevents invalidation. |
| `IProviderEventSource` | `TranslationAcquired` / `AcquisitionFailed` for external acquisition. |
| `IPluralFormMessageProvider` | Count-aware plural lookup, optionally with context. |
| `IWasmCompatibleMessageProvider` | Marker: resolution performs no file, database, or network I/O. |

## Localizer

```csharp
public interface IMessageLocalizer {
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters,
        CultureInfo culture, CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count,
        IReadOnlyDictionary<string, object?> parameters, CultureInfo culture,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count,
        IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeWithContextAsync(string key, string context,
        IReadOnlyDictionary<string, object?> parameters, CultureInfo culture,
        CancellationToken cancellationToken = default);
    ValueTask<string?> LocalizeWithContextAsync(string key, string context,
        IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
}
```

The overload without a culture uses `CultureInfo.CurrentUICulture`; it is not a different algorithm.
`LocalizePluralAsync` treats the sign as magnitude.

```csharp
public DefaultMessageLocalizer(
    IMessageProvider provider,
    IMessageFormatter formatter,
    LocalizationOptions? options);
```

- The default culture is captured at construction. `null`, or an empty `DefaultCulture`, disables the
  final default-culture step.
- The chain is requested culture, parents, then the default culture once. Invariant is never queried.
- A miss returns `null` and does not call the formatter.
- Formatter and provider exceptions propagate. Cancellation is never converted to `null`.
- `LocalizePluralAsync` first asks an `IPluralFormMessageProvider`. If that yields nothing, the CLDR (Common Locale Data Repository)
  category selects the key: `One` selects the singular, every other category the plural. If the
  preferred key misses, the other key is tried.
- `LocalizeWithContextAsync` builds `context + '\u0004' + key`; an empty context behaves like the
  plain key.

The localizer is a singleton. It holds the provider, formatter, options snapshot, and sink, so treat
the captured culture configuration as startup configuration.

## Formatters

```csharp
public interface IMessageFormatter {
    string Format(string template, IReadOnlyDictionary<string, object?> parameters);
}
public interface ICultureAwareMessageFormatter : IMessageFormatter {
    string Format(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture);
}
```

`DefaultMessageFormatter` substitutes named `{...}` placeholders:

- A missing parameter stays literal; a null value becomes empty.
- Conversion is `value?.ToString()`; there is no format-string handling.
- `{0}` is key `0`; `{name:N2}` is key `name:N2`, so it will not match `name`.
- It is culture-unaware; conversions use ambient `CurrentCulture`.

`IcuMessageFormatter` is an opt-in `ICultureAwareMessageFormatter`: registration is the opt-in.
Templates without an ICU (International Components for Unicode) construct pass through the inner formatter. Malformed patterns and type
mismatches throw `FormatException`; a missing simple argument keeps the literal placeholder.

`HtmlSafeMessageFormatter` encodes argument values but trusts template markup; its static
`EncodeArgument(object?)` is the MVC bridge's shared primitive. `MarkdownMessageFormatter` renders a
CommonMark subset before its inner formatter. Both output encoded HTML and must be emitted raw.

There is exactly one formatter per application. Register it before `AddLocalization()` or replace the
descriptor afterwards.

## Provider selection

```csharp
public sealed class LocalizationProviderSelector : IMessageProvider {
    public LocalizationProviderSelector(IEnumerable<IInnerMessageProvider> providers);
    public ValueTask<string?> GetMessageTemplateAsync(...);
}
```

- Zero providers: every lookup returns `null`.
- One provider: the selector delegates.
- More than one: construction throws `InvalidOperationException` naming the resolved types.

Chaining, ordering, and provider-level fallback do not exist.

`CultureAwareMessageProvider(IReadOnlyDictionary<string, IMessageProvider> providers)` dispatches to
the full culture name, then the two-letter ISO language name, passes the original `CultureInfo`, and
returns `null` otherwise. Invariant returns `null`; it is itself plural-aware.

## Options and registration

```csharp
namespace Jeninnet.Localization.Abstractions.Options;

public class LocalizationOptions {
    public string DefaultCulture { get; set; } = "en";
    public IMissingKeySink? MissingKeySink { get; set; }
}
```

```csharp
namespace Jeninnet.Localization.Core.Extensions;

public static IServiceCollection AddLocalization(this IServiceCollection services);
public static IServiceCollection AddLocalization(this IServiceCollection services,
    Action<LocalizationOptions> configure);
```

The parameterless overload registers default options with `TryAddSingleton`. The configuring overload
throws if options are already registered, validates the culture, and registers the configured
instance. Both register:

- `IMessageFormatter` / `IMessageLocalizer` / `IMessageProvider`, each `TryAddSingleton`.
- `IStringLocalizerFactory` → `CustomStringLocalizerFactory` (`Replace`).
- `IStringLocalizer<>` → the framework `StringLocalizer<>` (`TryAddTransient`).

They do not register an `ILocalizationStore` or a non-generic `IStringLocalizer`.
`AddLocalization` collides by name with the Microsoft.Extensions.Localization extension.

## Missing keys

```csharp
public interface IMissingKeySink {
    void ReportMiss(string key, string culture, string? callSite);
}
```

Core ships `MissingKeyCollector(maxEntries = 1000)` (aggregates by key/culture with a cap),
`LoggerMissingKeySink(ILogger)` (structured warning, event id `1001`), and
`CompositeMissingKeySink(params IMissingKeySink[])` (fans out in order). The localizer contains sink
exceptions; a failing diagnostic sink never breaks a lookup.

## Plurals and cultures

```csharp
public enum PluralCategory { Zero, One, Two, Few, Many, Other }

public static class PluralRules {
    public static PluralCategory GetCardinalCategory(CultureInfo culture, decimal value);
    public static PluralCategory GetOrdinalCategory(CultureInfo culture, decimal value);
}
```

The rules are a hand-ported CLDR subset. Matching uses `TwoLetterISOLanguageName`; unknown languages
resolve to `Other`; negative values use magnitude. Ordinals cover English only. `Other` means "none
of the other categories", not "plural".

## Worked example: one lookup

A provider serves only `de` entries and the default culture is `en`. A `de-CH` request queries
`de-CH`, misses, then queries `de` and resolves. A `fr` request queries `fr`, then `en`; if both miss,
the result is `null` and the formatter is never called. No other component moves between cultures.

## Pitfalls

**Pitfall:** registering two providers. The selector throws at construction; there is no chain.

**Pitfall:** expecting the formatter to run on a miss. A miss is `null` and formatting is skipped.

**Pitfall:** expecting the `IStringLocalizer` bridge to walk parents or use named placeholders. It
does neither; see [Storage API](02-storage-api.md).

## Invariants at a glance

- Exactly one provider, or none; providers are exact-match.
- The localizer is the only fallback owner.
- A miss is `null`; an error is an exception; cancellation is never a miss.
- Formatting runs only after a hit; the store bridge has different semantics.

## Where to go next

- [Storage API](02-storage-api.md) for the store and bridge contracts.
