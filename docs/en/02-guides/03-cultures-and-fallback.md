# Cultures and Fallback

Culture handling is where localization models most often diverge from developer expectations. This
chapter covers culture naming, parent chains, the default culture, request-culture selection, and
the tests that keep fallback behavior honest.

## Culture names and parents

A .NET culture name is a BCP-47-style identifier: `en`, `en-US`, `de-CH`, `sr-Latn-RS`,
`zh-Hans-CN`. `CultureInfo` exposes the name and a `Parent`:

- `new CultureInfo("de-CH").Parent.Name` is `"de"`.
- `new CultureInfo("fr-CA").Parent.Name` is `"fr"`.
- `new CultureInfo("sr-Latn-RS").Parent.Name` is `"sr-Latn"`, whose parent is `"sr"`.
- `new CultureInfo("ar").Parent` is the invariant culture.
- `CultureInfo.InvariantCulture.Name` is the empty string.

The parent chain is the basis of the localizer's fallback. It is not a search for "the closest
catalog"; it is a deterministic walk up the culture hierarchy.

### Neutral and specific cultures

A neutral culture such as `de` represents a language without a region. A specific culture such as
`de-CH` represents a language in a region. Applications often store translations at the neutral
level and let specific requests fall back to it. That works because `de-CH`'s parent is `de`.

It also means a catalog can appear complete when it is not. If a user requests `de-CH` and only `de`
exists, every key resolves. If the product needs Swiss-specific wording, the absence of `de-CH` is
invisible at runtime. Treat the neutral catalog as a valid fallback, not as proof that the specific
locale is covered.

For file providers, the file name is the culture key. `de.json` serves the neutral culture;
`de-CH.json` serves the specific one. The multi-culture conventions use the file stem as the
culture code, so `de-CH.json` is not a variant of `de.json` from the provider's point of view. The
localizer connects them through the parent chain.

### The invariant culture

The invariant culture is not a language and must never be a catalog. The localizer excludes it from
the parent walk. If the requested culture is invariant, the parent loop is skipped entirely and the
default culture, if configured, is the only candidate.

The rule exists for a reason: many cultures have the invariant culture as their parent, and a
lookup for invariant would otherwise silently match a culture-neutral bucket that nobody authored
on purpose. Excluding it makes the fallback chain end at a real default culture instead of at an
accidental one.

## The single fallback algorithm

`DefaultMessageLocalizer` implements exactly one algorithm:

```text
requested culture
  -> parent
  -> parent of parent
  -> ... until the parent chain reaches invariant (exclusive)
  -> configured default culture, once
  -> null
```

Providers are exact-match. They never walk parents, never apply a default culture, and never call
another provider. This separation is what makes fallback testable: you can substitute a provider
that records every requested culture and assert the exact sequence.

The default culture is optional. In DI (Dependency Injection), `AddLocalization()` registers `LocalizationOptions` with
`DefaultCulture = "en"`. The configuring overload can set another value. Setting an empty string
disables the final default-culture step, so a lookup resolves only through the requested culture
and its parents.

The default culture is captured when the localizer is constructed. It is not re-read from options
on every lookup. In a normal DI composition the localizer is a singleton, so treat the default
culture as startup configuration.

## Provider-level culture dispatch

Some registrations wrap one provider per culture in `CultureAwareMessageProvider`. This is what the
multi-culture JSON, XML, and PO (Portable Object) registration overloads do. The dispatcher's behavior is narrower
than the localizer's fallback:

1. If the requested culture's full name is registered, use that provider.
2. Otherwise, if the two-letter ISO language name is registered, use that provider.
3. Otherwise, return `null`.

It does not walk further parents, and it passes the original requested culture through to the
selected provider. The localizer still owns the full parent walk. The dispatcher is a convenience
for the common `de`/`de-CH` layout, not a second fallback engine.

This creates a subtle but useful behavior: for a request in `de-CH` with providers registered for
`de` and `en`, the dispatcher can resolve the `de` provider during the localizer's first step only
if the localizer asks for `de`. In practice the localizer asks for `de-CH` first, the dispatcher
misses, then the localizer asks for `de` and the dispatcher hits. The visible provider-query count
still follows the localizer's chain.

ResX and satellite providers do not use the dispatcher. `ResXMessageProvider` calls
`ResourceManager.GetResourceSet` with parent probing disabled, and `SatelliteMessageProvider` uses
`tryParents: false`. Both resolve the exact requested culture and leave fallback to the localizer.

## Request culture selection

In ASP.NET Core, the framework's `RequestLocalizationMiddleware` selects a request culture from
providers such as query string, cookie, and `Accept-Language`. The
`Jeninnet.Localization.Integrations.RequestLocalization` package configures that middleware from
the localization store and narrows results to cultures the application can actually serve.

`AddLocalizationRequestCultures` registers a configuration with:

- **Served cultures** — explicit `LocalizationRequestCultureOptions.SupportedCultures` when set;
  otherwise `ILocalizationStore.GetSupportedCulturesAsync()`. The resolved default culture is always
  part of the served set.
- **Default request culture** — explicit `options.DefaultCulture`, else the registered
  `LocalizationOptions.DefaultCulture`, else `"en"`.
- **Provider order** — route value, query string, cookie, `Accept-Language`. The route provider is
  `RouteValueRequestCultureProvider` with a configurable `RouteCultureKey` (default `"culture"`).

`ServedCultureRequestCultureProvider` narrows any provider result to the served set: exact culture
name first, then the framework-configured bounded parent fallback, otherwise the default request
culture. The bridge never mutates the ambient culture; the framework middleware applies the
resolved culture per request as usual.

The failure behavior is deliberate:

| Condition | Behavior |
|---|---|
| Store reachable and reports zero cultures | startup failure with an actionable exception |
| Store unreachable, or no store registered | served set becomes the default culture only, with a warning when logging is available |
| A configured culture name is unrecognized | startup failure naming the value |
| The default culture is not in the served set | the default is added to the set |

The second row is a common production surprise: if no store is registered and no explicit cultures
are configured, the application can only serve the default culture. Requesting `ar` will not select
`ar` just because a provider contains Arabic files. The request-culture bridge reads the store, not
the message provider.

For Blazor, culture management has two scopes. Blazor Server registers a scoped
`BlazorCultureService` per circuit, seeded from the request culture and never mutating the ambient
culture. Static-scope hosts (WebAssembly, Hybrid) register a singleton service that applies the
process ambient culture and raises `CultureChanged`. See [Blazor](09-blazor.md).

## Worked example: tracing the chain

The following test-style example uses a recording provider. The provider itself is deliberately
simple: it stores entries keyed by `culture:key` and records every culture it is asked for.

```csharp
sealed class RecordingProvider(IReadOnlyDictionary<string, string> messages) : IMessageProvider {
    public List<string> QueriedCultures { get; } = [];

    public ValueTask<string?> GetMessageTemplateAsync(
        string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        QueriedCultures.Add(culture.Name);
        return ValueTask.FromResult(
            messages.TryGetValue($"{culture.Name}:{key}", out var value) ? value : null);
    }
}

var provider = new RecordingProvider(new Dictionary<string, string> { ["de:Greeting"] = "Hallo" });
var localizer = new DefaultMessageLocalizer(
    provider,
    new DefaultMessageFormatter(),
    new LocalizationOptions { DefaultCulture = "en" });

var result = await localizer.LocalizeAsync(
    "Greeting", new Dictionary<string, object?>(), new CultureInfo("de-CH"));
// result is "Hallo"; QueriedCultures is ["de-CH", "de"].
```

| Scenario | Expected queries | Result |
|---|---|---|
| `de-CH`, key present in `de` | `de-CH`, `de` | German template |
| `de`, key only in default `en` | `de`, `en` | English template |
| `fr-CA`, key present in `fr` | `fr-CA`, `fr` | French template; default not queried |
| `fr-CA`, key present nowhere | `fr-CA`, `fr`, `en` | `null` |
| Invariant requested, key in `en` | `en` | English template |
| `en` missing, default `en` | `en`, `en` | `null` |

The last row documents the duplicated default-culture query when the default equals the requested
culture. It is expected and visible in fallback-step metrics.

## Tests that catch culture regressions

At minimum, test four things:

1. **The exact chain.** Use a recording provider and assert the ordered culture names. Do not assert
   only the returned string; a wrong chain can return the right string for the wrong reason.
2. **Invariant exclusion.** Assert that no query carries the empty culture name.
3. **Default-culture reachability.** Cover a requested culture with no entries and a default culture
   with entries.
4. **Request selection.** For ASP.NET Core, test the request bridge with explicit supported
   cultures, an unknown culture, and a missing store.

If you use `CultureInfo.CurrentUICulture` in tests, restore it in a `finally` block. Ambient
culture leaks between tests are a common source of order-dependent failures.

## Pitfalls

**Pitfall:** using `CurrentCulture` where `CurrentUICulture` is intended. The former controls
formatting; the latter selects UI resources. The convenience overload uses `CurrentUICulture`; in
background code the two can differ.

**Pitfall:** assuming `pt-BR` and `pt-PT` have distinct plural rules in the plural engine. The CLDR (Common Locale Data Repository)
subset matches by two-letter language, so `pt-PT` uses the `pt` rules even though CLDR distinguishes
the region. The limitation is documented; if region-specific plural behavior is a hard requirement,
select keys or templates at a higher level.

**Pitfall:** creating a file named `de-CH.json` and expecting it to serve all German requests. It
serves `de-CH` only. The localizer's parent walk goes from a specific request up to neutral, not
down from neutral to specific. A `de` request does not read `de-CH.json`.

**Pitfall:** relying on `Accept-Language` alone without the request-culture bridge. The middleware
needs configured supported cultures; the bridge reads them from the store. See
[ASP.NET Core and MVC](08-aspnet-core-and-mvc.md).

## Where to go next

- [Formatting and plurals](04-formatting-and-plurals.md) for the culture-aware formatting stage.
- [The mental model](../01-getting-started/04-mental-model.md) for the provider-selection and
  fallback rules in context.
- [Testing your localization](15-testing-your-localization.md) for a broader test strategy.
