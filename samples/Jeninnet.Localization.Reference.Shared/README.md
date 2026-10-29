# Jeninnet.Localization.Reference.Shared

Shared foundation for the Jeninnet Localization reference examples. This non-packable class library
holds the provider-neutral domain, the localization keys contract, the canonical content tables, an
example-only immutable store, and the composition entry point exercised by every reference host.

---

## Purpose

Every reference host (API, MVC, Blazor, WinForms, WPF) references this single project. The
project guarantees that a Provider switch is a **configuration change only** — no application code
changes when the host moves from JSON to XML to Memory.

---

## Layout

```
Jeninnet.Localization.Reference.Shared/
    Domain/
        Book.cs, OrderItem.cs, Order.cs, OrderLimits.cs,
        CreateOrderRequest.cs, OrderPlacementResult.cs
    Application/
        BookCatalog.cs, OrderService.cs
    Localization/
        ReferenceKeys.cs
        ReferenceCultures.cs
        ReferenceProviderNames.cs
        LocalizationContent.cs
        ReferenceResourceSetStore.cs
        ReferenceSqliteDatabase.cs
        ReferenceServerVendor.cs
        ReferenceServerDatabase.cs
        ReferenceLocalization.cs
    GlobalUsings.cs

samples/Content/Localization/
    en.json, ar.json, ar-SY.json
    en.xml,  ar.xml,  ar-SY.xml
    ResX/
        ReferenceContent.resx neutral + en/ar/ar-SY satellites
```

The JSON files are the canonical source of truth, embedded in the Shared assembly as embedded
resources. XML files are generated views; consistency is guarded by tests.

---

## Domain layer

A minimal, realistic BookStore scenario — four titles, order placement, and deterministic
behavior (no database, no I/O, no external state):

| Type | Role |
|---|---|
| `Book` | Catalog entry (id, title key, description key, author, price). |
| `OrderItem` | A line in an order (references a book, carries quantity and line total). |
| `Order` | One order (items list, computed total). |
| `OrderLimits` | Quantity bounds (`MinQuantity = 1`, `MaxQuantity = 99`). |
| `CreateOrderRequest` | Validated input model; carries `[LocalizedRequired]` and `[LocalizedRange]` attributes keyed to `ReferenceKeys`. |
| `OrderPlacementResult` | Outcome of `PlaceOrder` (success carrying an `Order`, or a deterministic error key). |
| `BookCatalog` | Static in-memory catalog (four books). |
| `OrderService` | Creates orders from validated requests; returns deterministic error keys. |

---

## Localization keys

`ReferenceKeys` is a single `static class` of `const string` fields. Every public member
corresponds to exactly one key present in every committed JSON file. Keys are consumed directly
by the `IStringLocalizer` bridge (`IStringLocalizer<T>`) through strongly-typed code and by
the DataAnnotations validators on `CreateOrderRequest`.

The three teaching keys:

| Key | Present in | Demonstrates |
|---|---|---|
| `Demo.Showcase` | `en` only | Default-culture fallback |
| `Demo.RegionalNote` | `en`, `ar` | Parent-culture fallback (absent from `ar-SY`) |
| `Demo.MissingEverywhere` | none | Deterministic missing-key behavior (`[MISSING:name]`) |

---

## Culture definitions

`ReferenceCultures` defines the supported set and the default culture:

```csharp
DefaultCulture     = "en"
SupportedCultures  = ["en", "ar", "ar-SY"]
```

All culture-specific behavior is driven from this single source. Hosts do not hard-code culture
lists.

---

## Content strategy

`LocalizationContent` is the single source of truth:

- loads embedded JSON at static-constructor time (fail-fast);
- validates that every **required** key (every `ReferenceKeys` constant except the three demo
  keys) is present in every supported culture;
- exposes `GetTemplates(culture)` → `ReadOnlyDictionary<string, string>`;
- exposes `TryGetString(culture, key, out value)` with `[NotNullWhen(true)]` (used by the
  store to keep nulls out of the hot path).

Committed JSON and XML files under `Content/Localization/` are format views of the same content.
JSON is canonical; XML is generated and verified by tests. Hosts copy these files to output
via project references or `<Content CopyToOutputDirectory="PreserveNewest" />`.

---

## ReferenceResourceSetStore

An example-only, immutable `ILocalizationStore` that satisfies the `IStringLocalizer` bridge:

- backed by `LocalizationContent` (no database, no file I/O at lookup time);
- resource-set name is always `DefaultResourceSet` (`"Jeninnet.Localization.Reference"`);
- lookups re-probe the content on every call (no store-level cache);
- CRUD (Create, Read, Update, Delete) is unsupported (`NotSupportedException`) — mutation stays outside the reference
  appliance, mirroring the library's `EmptyLocalizationResourceSet` pattern;
- GUIDs are stable and deterministic (SHA256-based on `resourceSet + culture + key`).

Registered automatically by the composition helper unless `RegisterReferenceStore = false`.

---

## Composition entry point

`ReferenceLocalization.AddReferenceLocalizationProvider(services, ...)` is the shared
composition helper exercised by every host's `Program.cs` / startup:

```csharp
// Action-based (configure provider via lambda):
services.AddReferenceLocalizationProvider(o => {
    o.Provider = "Json";
    o.ContentRootPath = AppContext.BaseDirectory;
});

// Or options-based (no IConfiguration dependency):
services.AddReferenceLocalizationProvider(new ReferenceLocalizationOptions {
    Provider = "Xml"
});
```

The helper performs exactly one provider registration, then calls
`services.AddLocalization(o => o.DefaultCulture = "en")`, and optionally registers the
`ReferenceResourceSetStore` as the `ILocalizationStore`.

`ReferenceProviderNames` declares the canonical provider values:

| Constant | Provider | Status |
|---|---|---|
| `Memory` | Culture-agnostic static seed | Implemented |
| `Json` | Per-culture JSON files via `CultureAwareMessageProvider` | Implemented |
| `Xml` | Per-culture XML files via `CultureAwareMessageProvider` | Implemented |
| `ResX` | Embedded ResourceManager resource sets | Implemented |
| `SQLite` | File-backed database via the production SQLite provider | Implemented |
| `SqlServer` | Server database via the production SQL Server provider | Infrastructure-gated |
| `PostgreSql` | Server database via the production PostgreSQL provider | Infrastructure-gated |
| `MySql` | Server database via the production MySQL provider | Infrastructure-gated |
| `AzureTranslation`, `GoogleTranslation` | Translation providers | Credential-gated |

An unrecognized value throws `ArgumentException` (naming the supported, infrastructure-gated, and
credential-gated sets); a gated value without its required configuration throws
`InvalidOperationException` naming the missing key (see the reference README §5, §8, §25).

`ReferenceProviderNames.SupportedProviders` is the build-safe set (`Memory`, `Json`, `Xml`, `ResX`,
`SQLite`); `InfrastructureGatedProviders` adds the three server names; `CredentialGatedProviders`
adds the two translation names; `DeferredProviders` is empty and retains the partition shape. The
four sets are disjoint and their union is every known provider name.

`SQLite` additionally consumes `ReferenceLocalizationOptions.SqliteDataDirectory` (defaults to a
`Data/` directory under the application base directory) and seeds the canonical
`LocalizationMessages` table through the sample-owned `ReferenceSqliteDatabase` initializer before
calling the production `AddSqliteLocalization(connectionString, tableName)`.

The three server providers consume `ReferenceLocalizationOptions.{SqlServer,PostgreSql,MySql}.ConnectionString`.
`ReferenceServerDatabase.EnsureInitialized(vendor, connectionString)` creates the
`JeninnetLocalizationReference` database when absent, creates the canonical `LocalizationMessages`
schema inside it, deterministically re-seeds the canonical content, and returns the per-vendor
connection string that is then passed to the production `Add{SqlServer,PostgreSql,MySql}Localization`
registration. A blank connection string throws `InvalidOperationException` naming the expected
configuration key (`ReferenceLocalization:{Vendor}:ConnectionString`), and `Reset(vendor, connectionString)`
drops the sample table. The initializer refuses to operate on any database other than
`JeninnetLocalizationReference`, and never echoes the connection string in an error message.

---

## Important implementation notes for host integrations

1. **Pin culture before resolving `IStringLocalizer<T>`** — `CustomStringLocalizerFactory`
   captures `CultureInfo.CurrentUICulture.Name` at localizer creation. Tests and hosts
   must set culture **before** the service provider resolves the localizer.

2. **JSON provider reads eagerly** — `JsonMessageProvider` reads the file at registration
   time. The file must exist on disk before the provider is registered (not lazily on first
   lookup).

3. **Content must be staged for file-based providers** — hosts that exercise JSON/XML must
   copy `Content/Localization/*` to the output directory. The test harness copies these
   files to a temporary location before registration.

4. **DataAnnotations must receive the same instance** — `Validator.TryValidateObject` requires
   the `ValidationContext.ObjectInstance` to match the instance being validated.

5. **No new package dependencies** — the Shared project intentionally avoids
   `Microsoft.Extensions.Configuration.Abstractions`; host-specific configuration reading
   belongs in the host composition root, not in the shared layer.
