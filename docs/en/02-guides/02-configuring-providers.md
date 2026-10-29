# Configuring Providers

Provider configuration looks like ordinary options configuration, but there is one important
difference: most provider options are **construction snapshots**, not `IOptions<T>` services. The
registration call captures what it needs, and the provider never observes later mutations of the
options object.

## Two configuration styles

### Direct options (most providers and the store)

`AddMemoryLocalization`, `AddJsonLocalization`, `AddXmlLocalization`, `AddResXLocalization`,
`AddSatelliteLocalization`, `AddPoLocalization`, `AddPseudoLocalization`, the four database message
providers, and `AddEntityFrameworkLocalizationStore` take a typed options instance or a
configuration delegate. The extension creates the provider immediately and registers it as a
singleton.

```csharp
services.AddJsonLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

The delegate runs during registration. Validation fails at startup rather than at the first
lookup, except for ResX and satellite resource presence, which `ResourceManager` can only discover
when a lookup occurs.

### Options pipeline (translation providers)

`AddAzureTranslationLocalization()` and `AddGoogleTranslationLocalization()` have a no-argument
overload that registers the provider through `IOptions<T>`:

```csharp
services.AddAzureTranslationLocalization();
services.Configure<AzureTranslationOptions>(configuration.GetSection("AzureTranslator"));
```

The provider validates the resolved options when it is constructed, so invalid configuration
throws `InvalidOperationException` at provider resolution. The delegate overload validates
immediately instead:

```csharp
services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.Region = configuration["AzureTranslator:Region"];
});
```

## What is captured, and when

A provider captures the values it needs when it is constructed. Memory copies the dictionary; JSON,
XML, and PO (Portable Object) capture the path, watch flag, debounce, and format; ResX and satellite capture the base
name and assembly; database providers capture the connection string and table name;
pseudo-localization copies its templates; translation providers resolve their options at
construction. Reload re-acquires the source, never re-reads options.

**Pitfall:** `services.Configure<JsonLocalizationOptions>(...)` does nothing for
`AddJsonLocalization`. The JSON provider is not resolved through `IOptions<T>`.

## Validation at registration

| Provider | Validated at registration | Deferred |
|---|---|---|
| Memory | options, `Messages`, dictionary non-null | key contents |
| JSON | non-empty `FilePath`, file exists, positive debounce when watching | JSON syntax and value types |
| XML | non-empty `FilePath`, file exists | XML syntax |
| PO | non-empty `FilePath`, file exists | catalog syntax, duplicate keys, `Plural-Forms` |
| ResX | non-empty `BaseName`, non-null `ResourceAssembly` | manifest resource presence |
| Satellite | non-empty `BaseName`, non-null `ResourceAssembly` | compiled resource presence |
| Pseudo | templates, non-empty pseudo culture, expansion 0-100 | key contents |
| Database | non-empty connection string and table name; identifier shape | connectivity and table existence |
| Azure | `ApiKey`, endpoint URI, cache TTL (Time To Live), timeout, retry, breaker, rate limit | remote call failures |
| Google | `ApiKey`, endpoint URI | remote call failures |
| EF store | non-empty connection string or present named connection string | database schema |

`DatabaseLocalizationOptions.TableName` accepts alphanumeric characters and underscores, up to
three dot-separated segments, each starting with a letter or underscore and containing 1-128
characters. `dbo.LocalizationMessages` is valid; spaces and punctuation are rejected when the
property is set.

JSON, XML, and PO load lazily but validate that the file exists during registration. A file
generated later in startup will fail registration. ResX and satellite defer a missing resource to
the first lookup, which throws `MissingManifestResourceException`.

## Reload behavior that exists

Only the file providers have built-in reload behavior, and it is opt-in.

| Provider | `WatchForChanges` | `ReloadDebounce` | `RefreshAsync` | Acquisition events |
|---|---|---|---|---|
| JSON | yes, default `false` | 500 ms default; positive when watching | yes | `TranslationAcquired`, `AcquisitionFailed` |
| XML | yes, default `false` | 500 ms default; positive when watching | yes | same |
| PO | yes, default `false` | 500 ms default; positive when watching | yes | same |
| Memory, ResX, satellite, database | no | no | no | no |
| Azure translation | no | no | yes, cache invalidation | yes |
| Google translation | no | no | no | no |

A watched change swaps the whole template table or catalog atomically. A failed reload keeps
serving the last-good table and raises `AcquisitionFailed`; a success raises
`TranslationAcquired`. Watching never throws onto the lookup path. `RefreshAsync` reloads
immediately and **throws** on failure, so it suits an explicit command or health check, not
fire-and-forget background use.

The file providers are registered as `IInnerMessageProvider`. To call `RefreshAsync` or subscribe
to events, resolve the concrete provider or keep the instance you registered. The canonical
contract does not expose those capabilities.

**Pitfall:** a non-positive `ReloadDebounce` is only rejected when `WatchForChanges` is enabled.
When watching is off, the value is accepted and unused.

## Per-family examples

Memory is one flat map and ignores culture. Multi-culture memory requires a
`CultureAwareMessageProvider`; there is no multi-culture overload.

```csharp
services.AddMemoryLocalization(new Dictionary<string, string> {
    ["App.Title"] = "Bookstore",
    ["Greeting"] = "Hello, {name}!"
});
```

JSON validates file existence at registration and supports watching:

```csharp
services.AddJsonLocalization(options => {
    options.FilePath = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");
    options.WatchForChanges = true;
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

The convention overload expects `{baseDirectory}/{subdirectory}/{culture}.json`; `XML` has the same
option shape, with `XmlLocalizationOptions` and `.xml` files.

ResX and satellite use a base name and assembly. Set the assembly explicitly; the default is the
provider assembly, not the application assembly. Both disable parent probing.

```csharp
services.AddResXLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
services.AddSatelliteLocalization("MyApp.Resources.ValidationMessages", typeof(Program).Assembly);
```

PO uses `FilePath`, `Format`, `WatchForChanges`, and `ReloadDebounce`. The multi-culture overload
expects `{culture}.po` and does not probe for `.mo`; MO (Machine Object) deployments use explicit paths or a custom
`IPoFileLocationProvider`.

Pseudo-localization uses `SourceTemplates`, `PseudoCulture`, and `ExpansionPercent` (0-100) and
serves only its synthetic culture. Database providers share `ConnectionString` and `TableName`
(columns `MessageKey`, `Culture`, `Template`):

```csharp
services.AddSqlServerLocalization(options => {
    options.ConnectionString = configuration.GetConnectionString("Localization")!;
    options.TableName = "dbo.LocalizationMessages";
});
```

The EF Core resource-set store is not a message provider; schema management is consumer-owned:

```csharp
services.AddEntityFrameworkLocalizationStore(configuration, connectionStringName: "LocalizationDb");
```

Azure uses `ApiKey`, `Region`, `CachingEnabled`, `CacheExpiration`, and resilience settings; Google
uses `ApiKey` and `Endpoint`. See [Machine translation](12-machine-translation.md).

```csharp
services.AddAzureTranslationLocalization(options => {
    options.ApiKey = configuration["AzureTranslator:ApiKey"]!;
    options.CachingEnabled = true;
});
```

## Configuring the pipeline and formatter

`AddLocalization` accepts `LocalizationOptions`, for example
`services.AddLocalization(options => options.DefaultCulture = "en")`. `DefaultCulture` is the final fallback step; empty or null disables it. The configuring overload
validates the name and throws `CultureNotFoundException` for an unknown culture. It also throws
`InvalidOperationException` if `LocalizationOptions` is already registered, so use either
`AddLocalization()` or `AddLocalization(configure)`, not both. `MissingKeySink` is the opt-in
diagnostic hook, reported only after the full chain misses.

`AddLocalization` uses `TryAddSingleton` for `IMessageFormatter`. Register a custom formatter
before calling it, or replace the descriptor afterwards:

```csharp
services.AddSingleton<IMessageFormatter>(new IcuMessageFormatter());
services.AddLocalization();
```

## Worked example: development watch, production snapshot

```csharp
var path = Path.Combine(AppContext.BaseDirectory, "Localization", "en.json");

builder.Services.AddJsonLocalization(options => {
    options.FilePath = path;
    options.WatchForChanges = builder.Environment.IsDevelopment();
    options.ReloadDebounce = TimeSpan.FromMilliseconds(250);
});
```

Both environments produce the same provider and lookup behavior; only reload differs. The path
comes from `AppContext.BaseDirectory`, so development and deployment resolve the same layout when
the content copy rule is correct.

**Pitfall:** assuming reload is atomic across cultures. Each watched file reloads independently; a
multi-file deployment can briefly serve mixed versions. Deploy related files together.

**Pitfall:** enabling watching on a read-only container filesystem; watching is for mutable content.

## Where to go next

- [Choosing a provider](01-choosing-a-provider.md) for the scenario-level decision.
- [Cultures and fallback](03-cultures-and-fallback.md) for `DefaultCulture` and request cultures.
- [Provider reference](../03-reference/03-provider-reference.md) for the complete option matrix.
