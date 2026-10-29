# Jeninnet.Localization Reference Application — Architecture & Matrix

**Status:** Reference application documentation.
**Owner:** Examples architecture.
**Authority:** the public [Architecture](../docs/en/03-reference/00-architecture.md) chapter. This
document records the implementation matrix for the Reference Examples system; it is a
usage/teaching guide, not a second architecture specification.

> **Implementation status:** the Shared Reference Application (host value 02)
> is implemented — `Jeninnet.Localization.Reference.Shared/`, the domain, the keys contract, content files, the example store, and the
> selection helper. The three web hosts are implemented as well (host values 03–05): the ASP.NET Core
> API, ASP.NET Core MVC, and Blazor Server hosts (`Jeninnet.Localization.Reference.Api`, `.Mvc`,
> `.BlazorServer`), each with a focused host-composition
> test project. The three desktop hosts are implemented as part of the authoritative main solution
> WinForms, WPF, and Blazor Hybrid (`Jeninnet.Localization.Reference.WinForms`,
> `.Wpf`, `.BlazorHybrid`), each with `--smoke` headless
> verification and shared host-composition tests under `Jeninnet.Localization.Reference.Hosts.Desktop.Tests/`. The desktop hosts target
> `net10.0-windows`; the main solution builds them cross-platform (sample-only `EnableWindowsTargeting`),
> but running them requires the Windows runtime. RESX is implemented as a
> `ResourceManager`-backed representation of the same canonical content and is selectable on every host;
> SQLite is implemented as a file-backed database demonstration of the production SQLite
> provider over the canonical `LocalizationMessages` schema, seeded from the shared canonical content by
> a sample-owned deterministic initializer. Server database coverage is implemented
> for SQL Server, PostgreSQL, and MySQL through the same `AddReferenceLocalizationProvider(...)`
> boundary: the shared helper creates and seeds the canonical `LocalizationMessages` schema inside a
> dedicated `JeninnetLocalizationReference` database and reuses the production server providers.
> Server providers are infrastructure-gated — they are never the default and never require committed
> credentials; the sample uses the .NET Secret Manager for local development. The web hosts additionally
> demonstrate SQL Server end to end. Translation provider coverage is implemented:
> Azure Translation and Google Translation are real, selectable providers through the same
> `AddReferenceLocalizationProvider(...)` boundary using their actual production contracts. Both
> require an API key (classified `API-KEY`), so they are **credential-gated**: keys are supplied
> through .NET User Secrets with clearly synthetic `REPLACE_WITH_...` placeholders until an owner
> provides real keys, and neither is represented as runtime-verified. The hosts demonstrate the
> Memory, JSON, XML, RESX, SQLite, (infrastructure-gated) server, and (credential-gated) translation
> providers through the shared selection helper. Culture references below follow the implemented
> shared content (`en`, `ar`, `ar-SY` with `DefaultCulture = "en"`), which supersedes the earlier
> `en`/`fr`/`fr-CA` recommendation.

---

## Host integration samples

Outside the BookStore reference matrix, three focused host samples demonstrate the platform
integrations with the same shared English/Arabic content:

- `Jeninnet.Localization.Reference.Avalonia` — file-backed culture persistence and RTL direction.
- `Jeninnet.Localization.Reference.Maui` — MAUI preferences persistence and RTL direction.
- `Jeninnet.Localization.Reference.WinUI` — Windows application-data persistence and RTL XAML.

They share `Jeninnet.Localization.HostSamples.Shared`, and each README documents its build/run
command. The Avalonia sample is in the main solution; the MAUI and WinUI samples are Windows-targeted
and live in `Jeninnet.Localization.Windows.slnx` (see
[README-SOLUTIONS.md](../README-SOLUTIONS.md) for the complete solution and project inventory).

---

## Getting started

**What this is:** one BookStore reference application (`Jeninnet.Localization.Reference`)
demonstrated through six hosts and the full Provider matrix. It teaches the `Jeninnet.Localization`
library through the canonical public APIs — never as a collection of unrelated demos.

**The developer journey:** find the Reference Application → choose a host (§4) → choose exactly one
Provider (§5–§8) → understand Provider prerequisites (§5, §27) → configure culture (§9) → consume
localization (§10) → run the example (§24) → know what to expect (§28) → know which capabilities are
gated (§5, §27).

**Prerequisites:**

- .NET 10 SDK (the repository builds with `TreatWarningsAsErrors=true` and targets `net10.0`).
- To **run** the desktop hosts you need Windows with the matching runtime; they build on any platform.
- To **run** the infrastructure-gated server examples you need a reachable SQL Server / PostgreSQL /
  MySQL instance and connection credentials you supply locally.
- To **run** the credential-gated translation examples you need real Azure/Google API keys.

**Quick path (web host, local Provider, no infrastructure):**

```bash
# from the repository root
dotnet restore Jeninnet.Localization.slnx
dotnet build  Jeninnet.Localization.slnx -c Release

cd samples
ASPNETCORE_URLS=http://localhost:5060 dotnet run --project Jeninnet.Localization.Reference.Api
# then GET http://localhost:5060/api/books/catalog?culture=ar
```

The API host starts with the default **JSON** Provider and serves the catalog/order endpoints in the
requested culture (`en`, `ar`, `ar-SY`). All run commands further down assume
`samples/` as the working directory.

**Document map — question to answer, by section:**

| You want to know... | Section |
|---|---|
| What is this Reference Application? | §1 Purpose, §2 Decision, §3 Shared application |
| Where do I start? / How do I run it? | Getting started (above), §24 Run commands |
| Which host should I open, and why? | §4 Host matrix, §26 Host guidance |
| Which Provider should I choose? | §5 Provider matrix, §6 Host × Provider |
| Which Providers need infrastructure or credentials? | §5 Provider matrix, §6 Host × Provider, §27 Prerequisites matrix |
| How do I select exactly one Provider? | §7 Exactly-one Provider, §8 Selection model |
| How do I run the local build-safe Providers? | §25 Local build-safe Providers |
| How are server connection strings configured? | §24 Server database provider configuration |
| How are translation credentials configured? | §14 Translation scenarios, §24 Translation provider configuration |
| How is culture selected and changed? | §9 Culture demonstration |
| What should I expect to see? | §28 Expected behavior |
| Where is the relevant code? | §30 Source navigation |
| Which examples are build-safe vs runtime-gated? | §19 Build and test strategy, §27 Prerequisites |
| What is deliberately deferred? | §5 Provider matrix, §22 Future sequence |
| What does each host / Provider demonstrate? | §4 Host matrix, §5 Provider matrix, §26 Host guidance |
| Where do I go next after understanding the example? | §31 Where to go next |

---

## 1. Purpose

Deliver **one coherent reference application**, reusable across multiple application hosts, that
teaches and proves the `Jeninnet.Localization` library through the canonical public APIs. The
examples system demonstrates:

- the same application behavior across ASP.NET Core API, ASP.NET Core MVC, Blazor (Server),
  Blazor Hybrid, WinForms, and WPF/XAML hosts;
- the mature Provider ecosystem (Memory, JSON, XML, RESX, SQLite, SQL Server, PostgreSQL, MySQL,
  Azure Translation, Google Translation) as **configuration/composition choices**, never as
  simultaneous fallback Providers;
- the separation between application behavior, host/framework, localization consumer API,
  Provider selection/configuration, and Provider implementation.

## 2. Reference application decision

**Domain — a small BookStore catalog and order form.**

The domain is the smallest realistic scenario that exercises every required localization behavior:

| Required behavior | Demonstrated by |
|---|---|
| Localized UI | Product list and product detail labels (`Products.*`) |
| Localized messages | Formatted results such as `Messages.OrderPlaced` (`Order #{id} — {count} item(s), total {total}`) |
| Localized validation | Checkout form (`Quantity`, `Email`) with localized validation attributes |
| Culture switching | Host language picker (web middleware / desktop `CurrentUICulture` switch) |
| Formatted messages | `IMessageLocalizer.LocalizeAsync(key, parameters, culture)` with template parameters |
| Missing-key behavior | A documented key deliberately absent from resources → `[MISSING:key]` / `ResourceNotFound` |
| `IStringLocalizer*` | `IStringLocalizer<T>` for component/controller strings through the existing Core bridge; `IViewLocalizer` for Razor views (view-scoped → shared resolution) |
| `IMessageLocalizer` | Canonical message lookups in services, controllers, and desktop UI code |
| Provider registration | Exactly-one capability-named registration per host (`AddXLocalization(...)`) |
| Provider-specific configuration | Per-Provider data files, connection strings, and (gated) credentials |
| Resource-set/store scenarios | The `IStringLocalizer` bridge over an example-only in-memory `ILocalizationStore`, plus the infrastructure-gated EF store scenario |

**Why this domain is sufficient:** orders and catalog entries are culture-independent in structure;
only labels, messages, and validation text are localized. No real business complexity (payments,
authentication, persistence of orders) is required to prove localization behavior, so none is
added. The domain is stable across all six hosts.

**Non-goals:** no real checkout, no order persistence, no user accounts, no cart engine, no real
payment, no admin/catalog CRUD (Create, Read, Update, Delete), no MAUI/Avalonia/Xamarin/Console/Worker hosts, no large shared
business framework, no new library abstraction.

## 3. Shared application layer

One shared project (`Jeninnet.Localization.Reference.Shared`, non-packable) holds provider-neutral
application code:

- domain models and the catalog/order scenario services;
- the localization keys contract (well-known key constants, e.g. `Products.LinenShirt`,
  `Cart.Empty`, `Messages.OrderPlaced`, `Checkout.QuantityLabel`);
- shared sample data (the same message tables used by Memory and mirrored in JSON/XML/RESX/SQLite
  files so a Provider switch is code-identical);
- validation models used by the MVC form and reused by other hosts;
- culture definitions (`SupportedCultures = ["en", "ar", "ar-SY"]`, `DefaultCulture = "en"`, and the
  documented `ar-SY` parent-fallback chain shared with `ar`);
- an **example-only** in-memory `ILocalizationStore` (`ReferenceResourceSetStore`) that implements
  the canonical Concept B contract so the `IStringLocalizer` bridge and DataAnnotations/MVC metadata
  demos run without SQL Server;
- an **example-only** Provider-selection helper (section 8).

**The shared layer must not** contain host-specific UI code, XAML, Razor, controllers, or
Provider-specific persistence logic. Provider-specific configuration stays in host configuration and
the Provider-specific data files.

## 4. Host matrix

| Host | Status | Framework | Shared layer | Localization entry point | Culture mechanism | Primary Provider(s) | Runtime prerequisites | Build/test |
|---|---|---|---|---|---|---|---|---|
| ASP.NET Core API | IMPLEMENTED | `net10.0` (`Microsoft.NET.Sdk.Web`) | Yes | `IMessageLocalizer` in endpoints/controllers; localized API messages | `RequestLocalization` middleware + query/cookie switch | JSON (PRIMARY), Memory, XML, RESX, SQLite | None | In main solution, normal CI |
| ASP.NET Core MVC | IMPLEMENTED | `net10.0` (`Microsoft.NET.Sdk.Web`) | Yes | Display metadata localization, `IViewLocalizer` view localization, `IStringLocalizer<T>`, `AddMvcLocalizedValidation()`, `IMessageLocalizer` | `RequestLocalization` middleware + language-switcher back to a cookie/query value | JSON (PRIMARY), Memory, XML, RESX, SQLite | None | In main solution, normal CI |
| Blazor (Server) | IMPLEMENTED | `net10.0` (`Microsoft.NET.Sdk.Web`, components) | Yes | `IMessageLocalizer` and `IStringLocalizer<T>` injected into components | `RequestLocalization` middleware + culture-selection component (cookie) | JSON (PRIMARY), XML, Memory, RESX, SQLite | None | In main solution, normal CI |
| Blazor WebAssembly | DEFERRED | `net10.0` (WASM (WebAssembly)) | Planned | Components | Client-side; no server middleware | JSON/XML shipped with the app | WASM publishing constraints on resource files | Deferred (see note) |
| Blazor Hybrid | IMPLEMENTED | `net10.0-windows` (WinForms shell + Blazor WebView) | Yes | Components in a desktop shell; local/offline Provider | App-set `CurrentUICulture`; in-app culture selector | JSON (PRIMARY), Memory, XML, RESX, SQLite | Windows; WebView2 runtime | Main solution; Windows runtime to run |
| WinForms | IMPLEMENTED | `net10.0-windows` | Yes | `IMessageLocalizer` from UI code; culture ComboBox re-render | Application-set `CurrentUICulture` | JSON (PRIMARY), Memory, XML, RESX, SQLite | Windows | Main solution; Windows runtime to run |
| WPF/XAML | IMPLEMENTED | `net10.0-windows` (MVVM) | Yes | `IMessageLocalizer` through view models/bindings; culture ComboBox | Application-set `CurrentUICulture` | JSON (PRIMARY), Memory, XML, RESX, SQLite | Windows | Main solution; Windows runtime to run |

Notes:

- The prompt's "Blazor" host is realized as **Blazor Server** because it shares the same
  `RequestLocalization` culture model as MVC/API (a single middleware pattern) and stays fully
  build-safe in the repo's Ubuntu CI. **Blazor WebAssembly is DEFERRED explicitly**: it moves resource
  packaging to the client (per-culture files must ship with the published app), removes server-side
  culture middleware, and would add a second Blazor host without new teaching value for the current
  repository.
- **Blazor Hybrid** is specified as a WinForms shell hosting `Blazor WebView`
  (`Microsoft.AspNetCore.Components.WebView.WindowsForms`) — the Blazor component model inside a
  native desktop window. MAUI is not added (non-goal). The blueprint assigns an offline-friendly/local
  Provider (SQLite primary; JSON fallback) to this host, per the frozen guidance; SQLite is implemented
  and selectable through the shared selection helper (verified through the desktop
  `--smoke` path), while the host's implemented default remains JSON.
- All web hosts are `net10.0` and run on the CI platform (Ubuntu) without modification. The desktop
  hosts are part of the main solution and build on Ubuntu via the sample-only `EnableWindowsTargeting`
  setting, but remain **Windows runtime** applications that are executed on Windows (§19).

## 5. Provider matrix

| Provider | Family | Status | Demonstration goal | Configuration | External prerequisites | Default-build eligible | Runtime verification |
|---|---|---|---|---|---|---|---|
| Memory | Static/in-process | PRIMARY (shared mini-demo + deterministic baseline) | Deterministic no-I/O provider; shows Core fallback with a flat table | `AddMemoryLocalization(messages)` from shared seed data | None | Yes | `dotnet run` host default |
| JSON | File | PRIMARY (web/file demo) | Per-culture resource files + `CultureAwareMessageProvider` dispatch | `AddJsonLocalizationProviders(base, cultures)`; `Content/Localization/{culture}.json` | None | Yes | `dotnet run` host default |
| XML | File | SECONDARY | Same keys expressed as XML; XML provider conventions | `AddXmlLocalizationProviders(base, cultures)`; `Content/Localization/{culture}.xml` | None | Yes | `dotnet run` host default |
| RESX | ResourceManager | PRIMARY (desktop .NET resource integration) | Classic .NET resource integration through the Provider model | `AddResXLocalization(baseName, assembly)` with embedded `.resx` | None | Yes | Provider switch (`ReferenceLocalization__Provider=ResX`) verified |
| SQLite | Relational | PRIMARY (hybrid + local/offline) | Local embedded persistence; canonical `LocalizationMessages` schema | `AddSqliteLocalization(connectionString, tableName)`; sample-owned deterministic seed initializer; file-backed database under the host output `Data/` directory | None | Yes | Provider switch verified on all six hosts (HTTP + `--smoke`) |
| SQL Server | Relational | INFRASTRUCTURE-GATED | Server message Provider over the canonical `LocalizationMessages` schema | `ReferenceLocalization:SqlServer:ConnectionString` (user-secrets/environment); sample-owned `ReferenceServerDatabase` initializer | Running SQL Server; credentials via user-secrets/environment | No (gated) | Verified (gated) on the API host + Shared parity tests |
| PostgreSQL | Relational | INFRASTRUCTURE-GATED | Vendor server Provider over the canonical `LocalizationMessages` schema | `ReferenceLocalization:PostgreSql:ConnectionString` (user-secrets/environment); same initializer | Running PostgreSQL; credentials via user-secrets/environment | No (gated) | Implementation + gated tests; runtime SKIP (no credentials at verification time) |
| MySQL | Relational | INFRASTRUCTURE-GATED | Vendor server Provider over the canonical `LocalizationMessages` schema | `ReferenceLocalization:MySql:ConnectionString` (user-secrets/environment); same initializer | Running MySQL; credentials via user-secrets/environment | No (gated) | Implementation + gated tests; runtime SKIP (no credentials at verification time) |
| Azure Translation | Translation | CREDENTIAL-GATED | Translation Provider participating in the canonical model with the real production provider; Azure participates in the capability model (multi-key, preload, refresh, events) | `ReferenceLocalization:Provider` = `AzureTranslation`; `ReferenceLocalization:AzureTranslation:ApiKey` (User Secrets, required); optional `Region`, `Endpoint`, `FromLanguage` | Azure Translator subscription (API key); live service | No (gated) | Configuration + deterministic behavior verified; external runtime SKIP (placeholder key only) |
| Google Translation | Translation | CREDENTIAL-GATED | Translation Provider participating in the canonical model with the real production provider; honest no-capability report | `ReferenceLocalization:Provider` = `GoogleTranslation`; `ReferenceLocalization:GoogleTranslation:ApiKey` (User Secrets, required); optional `Endpoint` | Google Cloud Translation (API key); live service | No (gated) | Configuration + deterministic behavior verified; external runtime SKIP (placeholder key only) |

Rules honored for every row: a translation Provider is an ordinary `IMessageProvider`, never a
second engine; no Provider is represented as verified without runtime verification; infrastructure
and credential-gated Provider rows never default-run and never require credentials in the
repository — translation keys are supplied through .NET User Secrets only.

## 6. Host × Provider matrix

States: `PRIMARY` (demonstrated), `SECONDARY` (swappable variant), `NOT APPROPRIATE` (not a
sensible reference for that host), `INFRASTRUCTURE-GATED` (requires live external infrastructure),
`CREDENTIAL-GATED` (requires a local API key supplied through .NET User Secrets),
`DEFERRED` (realized later or by another host, technically usable).

| Host | Memory | JSON | XML | RESX | SQLite | SQL Server | PostgreSQL | MySQL | Azure | Google |
|---|---|---|---|---|---|---|---|---|---|---|
| API | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | INFRASTRUCTURE-GATED | INFRASTRUCTURE-GATED | INFRASTRUCTURE-GATED | CREDENTIAL-GATED | CREDENTIAL-GATED |
| MVC | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | INFRASTRUCTURE-GATED | INFRASTRUCTURE-GATED | INFRASTRUCTURE-GATED | CREDENTIAL-GATED | CREDENTIAL-GATED |
| Blazor (Server) | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | DEFERRED | DEFERRED | DEFERRED | CREDENTIAL-GATED | CREDENTIAL-GATED |
| Blazor WebAssembly | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED | DEFERRED |
| Blazor Hybrid | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | DEFERRED | DEFERRED | DEFERRED | NOT APPROPRIATE | NOT APPROPRIATE |
| WinForms | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE |
| WPF/XAML | SECONDARY | PRIMARY | SECONDARY | SECONDARY | SECONDARY | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE | NOT APPROPRIATE |

Justifications: web hosts expose the file Providers and, for API/MVC and Blazor Server, the
credential-gated translation and server rows; desktop hosts stay local/offline (Memory/JSON/XML/RESX/
SQLite) — network translation is not a desktop reference and desktop hosts expose no secret path for
API keys; RESX and SQLite are implemented as verified swappable
variants (`SECONDARY`) on every host alongside JSON's `PRIMARY` default; Blazor WebAssembly is entirely
deferred because it imposes client-side resource packaging constraints that none of the
currently-required host demos need.

## 7. Exactly-one Provider discipline

The examples must reflect the frozen cardinality contract:

- Core resolves **exactly one** Provider; zero Providers is valid (lookup returns `null`); more than
  one is invalid.
- There is **no Provider chaining, no first-provider-wins, no priority, no aggregation**.

Therefore the examples show Provider selection as composition/configuration, e.g.:

```text
ReferenceLocalization:
  Provider: Json
```

or

```text
ReferenceLocalization:
  Provider: SQLite
```

with **identical application code**. They must never demonstrate `JSON + XML + RESX` as simultaneous
registered fallback Providers. When a multi-culture JSON/XML registration wraps per-culture files in
`CultureAwareMessageProvider`, the docs will state explicitly that this is **one registered Provider
whose dispatcher selects per-culture files** — Provider-owned internal behavior, not chaining.

## 8. Provider-selection and configuration model

**Mechanism:** host configuration (`appsettings.json`) selects the Provider family; each host runs
the standard .NET configuration layering so `ReferenceLocalization__Provider` (environment variable)
or command-line overrides work unchanged. Launch profiles may declare a per-host default.

The Shared layer provides an **example-only** helper (internal to sample code, never shipped):

```text
AddReferenceLocalizationProvider(services, options)
```

which reads `ReferenceLocalizationOptions` (Provider name; `Provider` defaults to `Json`) and
performs exactly **one** canonical capability-named registration. Build-safe `Provider` values:
`Memory`, `Json`, `Xml`, `ResX`, `SQLite`. Infrastructure-gated values: `SqlServer`, `PostgreSql`,
`MySql` — recognized, but they demand a connection string and throw `InvalidOperationException`
naming the missing configuration key when none is supplied. Credential-gated translation values:
`AzureTranslation`, `GoogleTranslation` — recognized, but they demand an API key and throw
`InvalidOperationException` naming the missing configuration key
(`ReferenceLocalization:AzureTranslation:ApiKey` / `ReferenceLocalization:GoogleTranslation:ApiKey`)
when none is supplied; an unknown value throws a configuration error. The helper never registers two
Providers (a duplicate/second Provider descriptor is a configuration error, not a fallback).

The helper takes an options object rather than `IConfiguration` so the Shared project adds no new
package dependency; hosts that want configuration-driven selection read
`ReferenceLocalization:Provider` in their own composition root and construct the options accordingly.

**Secrets and machine values (never committed):**

- repository files contain only safe placeholders (empty strings or documented dummy values);
- real values come from the .NET Secret Manager (`dotnet user-secrets`):
  the web hosts declare a `UserSecretsId` and load
  `ReferenceLocalization:{Provider}:ConnectionString` (server providers) or
  `ReferenceLocalization:{Provider}:ApiKey` (translation providers) from the secret store in
  `Development` (see section 25 for the exact commands);
- for CI / non-interactive runs the same values are supplied through the repository's established
  `ReferenceLocalization__{Provider}__ConnectionString` environment-variable convention, which the
  standard configuration layering already binds;
- no private endpoints, API keys, passwords, or developer-specific paths enter the repository.

**Switching Providers** = change `ReferenceLocalization:Provider` (and, for file/DB Providers, point to
the same keys in the matching resource shape) and restart the host. Application code does not change.

## 9. Culture demonstration model

One consistent scenario across hosts. The implemented shared content uses `en` (default culture),
`ar`, and `ar-SY`, with the teaching chain exercised through an `ar-SY` request:

```text
requested culture (ar-SY)
      ↓   exact match (ar-SY set present but partial → per-key miss)
parent culture (ar) → hit if present
      ↓
DefaultCulture (en) → hit if present AND parent cultures missed
      ↓
missing → null / [MISSING:key]
```

Three deliberately taught outcomes (implemented keys):

1. a key present **only in `ar`**, requested as `ar-SY` → parent-culture hit (`Demo.RegionalNote`);
2. a key present **only in `en`**, requested as `ar-SY` → default-culture hit (`Demo.Showcase`);
3. a key in **no culture** → deterministic miss (`[MISSING:key]`, `Demo.MissingEverywhere`).

The examples respect the frozen rules: invariant culture is never queried by Core fallback,
Providers are exact-match, `CultureAwareMessageProvider` is a dispatcher, `DefaultCulture` is
captured at localizer construction, and the bridge is not a second fallback engine.

Host culture selection (RequestLocalization middleware, desktop `CurrentUICulture` switching) is
**application host code**, explicitly outside the library's responsibility — the docs will state
this boundary.

## 10. Microsoft.Extensions.Localization bridge demonstration

The examples use the existing bridge only:

```text
Application → IStringLocalizer* → CustomStringLocalizerFactory
    → StoreBackedStringLocalizer → Jeninnet.Localization
```

Demand to register an `ILocalizationStore` before resolving `IStringLocalizer<T>`. Because the only
production store is the infrastructure-gated EF Core store, the Shared layer ships an **example-only**
`ReferenceResourceSetStore : ILocalizationStore` (immutable per-culture dictionaries; per-lookup
store probe preserved from). This makes the bridge, DataAnnotations, and MVC metadata demos
runnable with zero infrastructure while keeping the real EF store for the infrastructure-gated
scenario.

The bridge is demonstrated with: strongly-typed localizers (`IStringLocalizer<T>`), base-name
identity, missing-key behavior, formatted values, current-culture behavior, and — only where it
teaches (e.g. a read-only API/keys endpoint) — enumeration. The bridge implementation is never
duplicated in the samples.

## 11. MVC / DataAnnotations demonstration

MVC demo scope (host-specific values from the integrations):

- `DisplayAttribute.Name`, `DisplayAttribute.Description`, `DisplayAttribute.Prompt` localized for
  the checkout form;
- localized validation attributes (`LocalizedRequiredAttribute` and the family) on a checkout model;
- MVC validation-metadata localization via `AddMvcLocalizedValidation()` +
  `registered IStringLocalizerFactory` (Core bridge);
- `IViewLocalizer` for view strings (view-path-scoped key resolution: view scope →
  shared → canonical culture fallback through the same pipeline,) plus
  `IStringLocalizer<T>` for controller/metadata strings; current-culture and
  missing-key behavior.

Respecting the architecture: `Integrations.DataAnnotations` needs no Jeninnet-Core dependency and no
registration API (declarative + DI-resolved `IStringLocalizer<>`); `Integrations.Mvc.Validation` is
the single allowed integration→Core edge; `DisplayAttribute.ShortName` has **no destination in .NET 10
MVC metadata** and must never be demonstrated as supported.

## 12. Blazor and Blazor Hybrid demonstration

**Blazor (Server)** registers localization in `Program.cs` (standard composition), selects culture
through `RequestLocalization` middleware plus a culture-selection component, and injects
`IMessageLocalizer` / `IStringLocalizer<T>` into components. The culture glue comes from the
`Jeninnet.Localization.Integrations.Blazor` package: `AddBlazorServerLocalization` registers
the circuit-scoped culture service and handler (seeded per circuit from the request culture, no
ambient-culture cross-talk), the culture selector persists the choice through the cookie store, and
the hand-rolled JavaScript file is gone. Primary Provider: JSON per-culture files. No infrastructure
required.

**Blazor WebAssembly**: explicitly deferred (section 4 rationale). The planning record classifies it
rather than silently excluding it. The integration package delivers the WASM *semantics* (static
culture scope plus the `IWasmCompatibleMessageProvider` guard that rejects file/DB providers); true
browser-WASM consumption of the framework-bound package remains deferred with the host.

**Blazor Hybrid** (WinForms shell + Blazor WebView) registers localization in the shell's
composition root, selects culture through the Jeninnet static culture service (Blazor integration
package, local-storage persistence) and an in-app selector, and injects localizers into components.
The blueprint assigns **SQLite as the local/offline Provider**
(JSON as Secondary); SQLite is implemented and selectable on this host through the
shared selection helper — the sample-owned deterministic initializer seeds the canonical
`LocalizationMessages` schema and the desktop `--smoke` path is verified with
`ReferenceLocalization__Provider=SQLite` — while the host's implemented default remains JSON.
Requires Windows and the WebView2 runtime.

## 13. WinForms and WPF/XAML demonstration

The desktop hosts prove the library is not inherently ASP.NET Core.

- **WinForms:** a composition root sets up the app `ServiceProvider`; UI code calls
  `IMessageLocalizer`; a culture ComboBox sets `CurrentUICulture` and re-renders. The blueprint
  assigns RESX as the primary desktop .NET resource-integration Provider; it is implemented
  and the hosts demonstrate Memory/JSON/XML/RESX/SQLite through the shared selection
  helper.
- **WPF/XAML:** minimal MVVM consumption through `IMessageLocalizer` (view models/bindings, culture
  ComboBox); explicit documentation distinguishes Jeninnet.Localization from WPF's own
  resource/localization mechanism (the library does not replace every native XAML feature);
  a clean consumption path with no invented XAML markup extension. A custom markup extension, if
  ever genuinely needed, must be a separate future change, not this plan.

## 14. Translation Provider reference scenarios

Azure and Google are ordinary Providers (section 5). The demo shows:

```text
IMessageProvider → Translation Provider → external translation service
```

The production contracts are reused verbatim. The Provider's source template is the message key
itself; the value is the key translated into the requested target culture. Culture mapping is
verbatim: both providers send `CultureInfo.Name` as the target (`to=ar-SY` / `target=ar-SY`), so a
regional culture such as `ar-SY` is requested directly — the providers own no parent-culture mapping
and Core remains the only fallback owner. The recommended reference scenario translates a canonical
BookStore phrase (e.g. the canonical `Catalog.Headings.Catalog` key, "Catalog") into a supported target culture.

Both providers require an API key, so the reference scenario is **credential-gated** and never part
of the default build: keys live in .NET User Secrets behind synthetic `REPLACE_WITH_...`
placeholders. Azure's reference additionally exercises the capability model the library already
establishes (multi-key, preload, refresh, events) through the canonical interfaces; Google honestly
reports no advanced capabilities. Only reproducible behavior is demonstrated: registration,
exactly-one-provider composition, required-key validation, default-selection rules, and
network-free deterministic behavior (invariant-culture `null` resolution and pre-cancellation fault)
are covered by focused reference tests. Nothing requires translation services for the default
build; real end-to-end translation is expected only after an owner replaces the placeholders with
real keys, and it is **not** reported as runtime-verified here. Translation Providers are never
presented as a second framework-level localization engine.

## 15. Proposed repository layout

```text
samples/
    Jeninnet.Localization.Docs/                             (documentation snippet verification)
    Jeninnet.Localization.Reference.Shared/                 (shared domain, keys, content, store, composition)
    Jeninnet.Localization.Reference.Api/                    (net10.0)
    Jeninnet.Localization.Reference.Mvc/                    (net10.0)
    Jeninnet.Localization.Reference.BlazorServer/           (net10.0)
    Jeninnet.Localization.Reference.WinForms/               (net10.0-windows)
    Jeninnet.Localization.Reference.Wpf/                    (net10.0-windows)
    Jeninnet.Localization.Reference.BlazorHybrid/           (net10.0-windows)
    Jeninnet.Localization.Reference.Shared.Tests/           (shared behavior tests)
    Jeninnet.Localization.Reference.Hosts.Tests/            (web-host composition tests)
    Jeninnet.Localization.Reference.Hosts.Desktop.Tests/    (host-composition tests, Windows runtime)
    Content/Localization/                                   (per-culture en.json/ar.json/ar-SY.json + matching .xml)
        ResX/                                               (ReferenceContent.resx neutral + en/ar/ar-SY satellites)
    README.md
```

Solution membership: every reference-application project is represented **exactly once** in the
authoritative **`Jeninnet.Localization.slnx`** under `/samples/Reference Application/`: `Shared/`,
`Hosts/Web/` (Api, Mvc, BlazorServer), `Hosts/Desktop/` (WinForms, Wpf, BlazorHybrid), and
`Tests/Shared`, `Tests/Web Hosts`, `Tests/Desktop Hosts`. The temporary
`Jeninnet.Localization.Reference.Desktop.slnx` was removed — the main solution is the single entry
point for the whole repository.

Cross-platform build: the four desktop projects (`WinForms`, `Wpf`, `BlazorHybrid`,
`Hosts.Desktop.Tests`) declare the sample-only `EnableWindowsTargeting` setting so the main solution
restores and builds on Ubuntu CI (warnings-as-errors) without weakening it. Running a desktop host is
a **Windows runtime** activity — `Hosts.Desktop.Tests` and the `--smoke` verification execute on
Windows, where the WindowsDesktop runtime exists; Ubuntu CI builds the desktop projects but never
launches them.

RESX resource identity: the `.resx` files live in `Content/Localization/ResX/` and are embedded in the
Shared assembly under the base name
`Jeninnet.Localization.Reference.Shared.Content.Localization.ResX.ReferenceContent` — a neutral
`ReferenceContent.resx` plus explicit `en`, `ar`, and `ar-SY` satellite resource sets (the repository
declares `NeutralLanguage`, so an explicit `en` satellite is exercised for exact-culture `en` lookups).
The provider is configured with that base name and the Shared assembly, so the identity never depends
on the working directory or machine paths.

SQLite runtime database: no `.db` artifact is committed. Each host generates
`Data/reference-localization.db` under its own output directory at startup (schema + canonical seed),
so the database is deterministic, disposable, and never container- or machine-specific; see section 25
for location, initialization, and reset details.

Rationale: shared code is shared (one Shared project referenced by all hosts); hosts are separated
(host frameworks differ materially); Providers are not duplicated (data files, not projects — no
one-project-per-Provider); adding a future host/Provider is an incremental addition that does not
rerun the whole demo.

## 16. Example taxonomy

| Category | Designation | Purpose |
|---|---|---|
| Reference Application | `REF-SHARED` | Shared domain, keys, data, culture definitions, sample store, selection helper |
| Host | `HOST-{API,MVC,BLAZOR,WINFORMS,WPF,BLAZOR-HYBRID}` | Proves host integration and culture mechanics |
| Provider | `PROVIDER-{Memory,Json,Xml,ResX,Sqlite,...,Azure,Google}` | Proves Provider integration through a configuration switch |
| Integration Scenario | `INTEG-{MVC-METADATA,DATAANNOTATIONS,BRIDGE-STRINGLOCALIZER}` | Proves framework-specific behavior (DataAnnotations, MVC metadata, bridge) |
| Infrastructure Scenario | `INFRA-{SqlServer,PostgreSql,MySql}` | Proves server DB Provider behavior, environment-gated |
| Translation Scenario | `TRANSLATION-{Azure,Google}` | Proves translation Provider behavior, environment-gated |

Every example has exactly one purpose; examples whose only distinction is cosmetic are not created.

## 17. Reuse rules

Shared across hosts: domain models, application services, localization keys, sample data,
provider-independent behavior, common configuration conventions, and documentation conventions.
Host-specific code remains host-specific; Provider-specific configuration remains Provider-specific.
Hosts are not forced into an artificial common abstraction when their framework behavior differs.

## 18. Documentation strategy

The examples system makes this journey obvious:

```text
I need localization → choose host → choose Provider → register Provider → configure culture
    → consume localization → run the example
```

Documentation is deliberately consolidated to avoid duplication:

- this README is the single entry point — it covers the journey, the host matrix (§4), Provider
  selection (§5–§8), prerequisites (§27), expected behavior (§28), troubleshooting (§29), and the
  verified run commands (§24);
- `Jeninnet.Localization.Reference.Shared/README.md` describes the shared project (domain, keys, content, store, composition helper);
- each library package documents itself through its own README next to the project (see §30);
- infrastructure/translation scenarios carry explicit environment-gated instruction blocks (§24);
- the documentation teaches usage and does not duplicate the frozen architecture; the repository-root
  `README.md` links to the examples.

## 19. Build and test strategy

- **Default build-safe examples**: Memory, JSON, XML, RESX, SQLite only — no servers, no services,
  no secrets, no machine-specific paths.
- **Environment-gated examples**: SQL Server, PostgreSQL, MySQL (infrastructure-gated) and Azure/Google
  translation (credential-gated). Never default-runnable; never represented as verified without a
  live run at the time of verification. The server-provider parity tests in `Shared.Tests` are gated
  by the
  `JENINNET_TEST_SQLSERVER_CONNECTION`, `JENINNET_TEST_POSTGRESQL_CONNECTION`, and
  `JENINNET_TEST_MYSQL_CONNECTION` environment variables and report `Inconclusive` (skipped, never
  failing) when a variable is absent, so the default build stays green without infrastructure.
  Translation coverage adds network-free focused reference tests (registration, validation,
  exactly-one composition, deterministic provider behavior) that run in the default build without any
  credential or outbound network.
- The main solution contains the complete reference application. Ubuntu CI restores and builds the
  whole solution (warnings-as-errors) including the desktop projects via the sample-only
  `EnableWindowsTargeting` setting, runs the Ubuntu-runnable test projects (library, Shared, web-host
  tests), and verifies formatting. The desktop `Hosts.Desktop.Tests` and the desktop `--smoke`
  verification run on Windows, where the WindowsDesktop runtime exists.
- **Documentation snippets:** `Jeninnet.Localization.Docs` compiles selected examples and executes
  the quick-start path in CI. It uses only local, hermetic inputs; compile-only registration snippets
  never run.
- No timing-sensitive tests and no benchmark suites are added anywhere in the examples.

## 20. Package consumption vs project references

The repository is local, unreleased, and uses project references throughout. Reference hosts use
**project references** to the library projects (including the sample exemption for packability).
Package-consumption examples are classified as a later bounded change unless they become
essential; nothing here introduces package publication workflows.

## 21. Dependency and API discipline

- Example projects may reference library projects; the reverse never happens (no production project
  may reference a sample; no sample is copied into production assemblies).
- The SQLite reference reuses the production `Jeninnet.Localization.Providers.Database.SQLite` project
  through a normal project reference from `Shared`; the sample adds no second database library and no
  EF Core dependency (the production provider's `Microsoft.Data.Sqlite` dependency flows transitively).
- No production package is added solely for an example; sample-only framework packages (e.g. the
  Blazor WebView package) are declared via central package management without touching the
  production graph.
- No new production public API is introduced. No `IExampleLocalizationProvider`, `IProviderSelector`,
  or `IHostLocalization` facade exists (or will exist). Example-only helpers are internal to the
  samples.
- Example projects are non-packable (`IsPackable=false`). Identity follows sample practice: project
  names are sample-scoped deviations from the project==assembly==namespace==package invariant
  (documented in each sample README). Where public XML documentation would be required, example
  projects apply the constitution's sample exemption consistently (same mechanism the repository
  already uses for sample/test exemption from CS1591); public library-type surfaces used by examples
  remain fully documented.

## 22. Implementation sequence

Each milestone is independently buildable and verifiable; none is created automatically:

| Capability | Scope |
|---|---|
| Shared Reference Application | **IMPLEMENTED** — `Shared` project, domain, keys, sample data, culture definitions, sample store, selection helper, resource files |
| API Host | **IMPLEMENTED** — minimal API/controllers with `IMessageLocalizer`, culture middleware, Provider switching |
| MVC Host | **IMPLEMENTED** — display metadata, localized validation, `IViewLocalizer` view localization, `IStringLocalizer<T>`, `AddMvcLocalizedValidation()` |
| Blazor (Server) Host | **IMPLEMENTED** — components, culture selector, Provider switching |
| File Provider Coverage | **IMPLEMENTED** — the same keys are represented as the Memory seed, per-culture JSON/XML files, and committed RESX (neutral + `en`/`ar`/`ar-SY` satellites), with parity and compiled-identity tests |
| SQLite Reference | **IMPLEMENTED** — sample-owned deterministic initializer over the canonical `LocalizationMessages` schema reusing the production SQLite provider, with parity/initialization/reset tests and verified provider switching on all six hosts |
| WinForms Host | **IMPLEMENTED** — desktop consumption, culture switch, Provider switching (Memory/JSON/XML/RESX/SQLite) |
| WPF/XAML Host | **IMPLEMENTED** — MVVM consumption, culture switch, Provider switching (Memory/JSON/XML/RESX/SQLite) |
| Blazor Hybrid Host | **IMPLEMENTED** — WinForms + Blazor WebView shell, Provider switching (Memory/JSON/XML/RESX/SQLite) |
| Server Database References | **IMPLEMENTED** — SQL Server/PostgreSQL/MySQL environment-gated scenarios through the shared selection helper (canonical `LocalizationMessages` schema in a dedicated `JeninnetLocalizationReference` database, production providers reused, .NET Secret Manager workflow); SQL Server verified, PostgreSQL/MySQL runtime SKIP (no credentials) |
| Translation Provider References | **IMPLEMENTED** — Azure/Google translation scenarios through the shared selection helper using the real production providers; API-KEY classified; keys via .NET User Secrets with synthetic placeholders (configuration/deterministic behavior verified; external runtime SKIP until real keys) |
| Example Documentation & DX | **IMPLEMENTED** — getting-started journey, host guidance, prerequisites matrix, expected behavior, troubleshooting, source navigation, stale-claim corrections, repository-root navigation |
| Final Example System Audit | Coherence, isolation, discoverability, fidelity, no secrets |

## 23. Product quality criteria

Coherence (one application model); meaningful reuse (shared logic); isolation (host/Provider
specifics stay isolated); discoverability (dev journey obvious); truthfulness (only supported
behavior is demonstrated; limitations identified); architecture fidelity (canonical APIs, frozen
dependency graph); Provider fidelity (each Provider per its real semantics); framework fidelity
(web/Blazor/desktop/XAML hosts do not pretend to share runtime models); operational safety (no
secrets, no external-system mutations, no machine-specific assumptions); maintainability (adding a
host or Provider does not duplicate the application).

## 24. Run commands

Unless stated otherwise, every command in this section runs from
`samples/` (the directory that contains the `Jeninnet.Localization.Reference.*` projects and
`Content/`). Build the whole solution once from the repository root before first run:

```bash
dotnet build Jeninnet.Localization.slnx -c Release
```

All hosts default to the JSON provider. Override via the `ReferenceLocalization__Provider`
environment variable (`Json` | `Memory` | `Xml` | `ResX` | `SQLite`). The web hosts switch culture via the `?culture=`
query string (`en`, `ar`, `ar-SY`); the desktop hosts switch `CurrentUICulture` in-app. Web hosts bind
Kestrel's default address unless `ASPNETCORE_URLS` is set; the examples below use explicit ports.
Desktop hosts run either interactively (GUI) or headless with `--smoke`, which prints the localized
content and order message for every supported culture and returns a process exit code.

**API host** (example port 5060):

```bash
ASPNETCORE_URLS=http://localhost:5060 dotnet run --project Jeninnet.Localization.Reference.Api
# GET  http://localhost:5060/api/books/catalog?culture=ar
# GET  http://localhost:5060/api/books/catalog/book-001?culture=ar
# POST http://localhost:5060/api/books/orders?culture=ar
# ASPNETCORE_URLS=http://localhost:5060 ReferenceLocalization__Provider=Xml dotnet run --project Jeninnet.Localization.Reference.Api
# ASPNETCORE_URLS=http://localhost:5060 ReferenceLocalization__Provider=ResX dotnet run --project Jeninnet.Localization.Reference.Api
# ASPNETCORE_URLS=http://localhost:5060 ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.Api
```

**MVC host** (example port 5062):

```bash
ASPNETCORE_URLS=http://localhost:5062 dotnet run --project Jeninnet.Localization.Reference.Mvc
# http://localhost:5062/catalog?culture=ar             (catalog — localized headings, titles, descriptions)
# http://localhost:5062/catalog/book-001?culture=ar    (product detail — localized description, price)
# http://localhost:5062/orders/create?culture=ar       (order form — localized Display metadata + validation)
# ASPNETCORE_URLS=http://localhost:5062 ReferenceLocalization__Provider=Xml dotnet run --project Jeninnet.Localization.Reference.Mvc
# ASPNETCORE_URLS=http://localhost:5062 ReferenceLocalization__Provider=ResX dotnet run --project Jeninnet.Localization.Reference.Mvc
# ASPNETCORE_URLS=http://localhost:5062 ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.Mvc
```

**Blazor Server host** (example port 5064):

```bash
ASPNETCORE_URLS=http://localhost:5064 dotnet run --project Jeninnet.Localization.Reference.BlazorServer
# http://localhost:5064/?culture=ar          (SSR: localized catalog headings, titles)
# http://localhost:5064/orders?culture=ar    (SSR: localized order form labels, options, button)
# ASPNETCORE_URLS=http://localhost:5064 ReferenceLocalization__Provider=Xml dotnet run --project Jeninnet.Localization.Reference.BlazorServer
# ASPNETCORE_URLS=http://localhost:5064 ReferenceLocalization__Provider=ResX dotnet run --project Jeninnet.Localization.Reference.BlazorServer
# ASPNETCORE_URLS=http://localhost:5064 ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.BlazorServer
```

**WinForms host** (Windows):

```bash
dotnet run --project Jeninnet.Localization.Reference.WinForms                 # GUI (culture ComboBox)
dotnet run --project Jeninnet.Localization.Reference.WinForms -- --smoke      # headless verification
# ReferenceLocalization__Provider=Xml dotnet run --project Jeninnet.Localization.Reference.WinForms -- --smoke
# ReferenceLocalization__Provider=ResX dotnet run --project Jeninnet.Localization.Reference.WinForms -- --smoke
# ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.WinForms -- --smoke
```

**WPF host** (Windows):

```bash
dotnet run --project Jeninnet.Localization.Reference.Wpf                           # GUI (culture ComboBox)
dotnet run --project Jeninnet.Localization.Reference.Wpf -- --smoke                # headless verification
# ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.Wpf -- --smoke
```

**Blazor Hybrid host** (Windows):

```bash
dotnet run --project Jeninnet.Localization.Reference.BlazorHybrid         # GUI (WebView2 required)
dotnet run --project Jeninnet.Localization.Reference.BlazorHybrid -- --smoke  # headless verification
# ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.BlazorHybrid -- --smoke
```

**SQLite provider configuration** (any host; no server, no credentials, no network):

- **Location:** `<host output directory>/Data/reference-localization.db`, derived from the application
  base directory (`ReferenceLocalizationOptions.SqliteDataDirectory`), never from the current working
  directory and never a machine-specific absolute path.
- **Registration:** the Shared helper calls `ReferenceSqliteDatabase.EnsureInitialized(...)` and then
  the production `AddSqliteLocalization(connectionString, tableName)` with the canonical
  `LocalizationMessages` table.
- **Initialization:** sample-owned and deterministic. The first start creates the canonical schema
  (`MessageKey`, `Culture`, `Template`; composite primary key) and seeds the canonical `en`/`ar`/`ar-SY`
  rows from the shared content; every later start rewrites the rows, so repeated starts never duplicate
  content and stale rows cannot silently alter behavior.
- **Reset:** stop the host, delete `Data/reference-localization.db` (and any `-wal`/`-shm` files) or
  the whole `Data/` directory, then start again — equivalent content is recreated. The database is
  disposable, local, and never committed.
- **Ownership boundary:** schema hosting/migrations remain a production deployment responsibility; the
  sample only seeds its own demonstration data and never modifies the shipping provider.

**Server database provider configuration** (web hosts; infrastructure-gated — never the default):

- **Scope:** SQL Server, PostgreSQL, and MySQL select through the same `ReferenceLocalization:Provider`
  switch and reuse the production provider projects; the shared `ReferenceServerDatabase` initializer
  creates and seeds the canonical `LocalizationMessages` schema inside a dedicated
  `JeninnetLocalizationReference` database before the provider is registered.
- **Configuration keys:** `ReferenceLocalization:Provider` (`SqlServer` | `PostgreSql` | `MySql`) and
  `ReferenceLocalization:{Vendor}:ConnectionString`. The initializer uses `admin` credentials only to
  create the sample database when it is absent, then refuses to run against any database whose name is
  not `JeninnetLocalizationReference`.
- **Local development (Secret Manager):** the web hosts declare a `UserSecretsId`; store the machine
  values outside the repository and run in `Development` so they are loaded:

  ```bash
  dotnet user-secrets set "ReferenceLocalization:Provider" "SqlServer" \
      --project Jeninnet.Localization.Reference.Api
  dotnet user-secrets set "ReferenceLocalization:SqlServer:ConnectionString" \
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;" \
      --project Jeninnet.Localization.Reference.Api
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5060 \
      dotnet run --project Jeninnet.Localization.Reference.Api
  # GET http://localhost:5060/api/books/catalog?culture=ar  → Arabic strings sourced from SQL Server
  dotnet user-secrets clear --project Jeninnet.Localization.Reference.Api
  ```

- **Non-interactive / CI:** set environment variables instead — e.g.
  `ReferenceLocalization__Provider=PostgreSql` and
  `ReferenceLocalization__PostgreSql__ConnectionString=...`. No secret is ever committed.
- **Tests:** the same connections can drive the gated parity tests by setting
  `JENINNET_TEST_SQLSERVER_CONNECTION` / `JENINNET_TEST_POSTGRESQL_CONNECTION` /
  `JENINNET_TEST_MYSQL_CONNECTION`; without them those tests report `Inconclusive`.
- **Cleanup:** `dotnet user-secrets clear --project <host>` and drop the
  `JeninnetLocalizationReference` database (the parity tests leave the schema, `Reset(...)` drops the
  table).

**Translation provider configuration** (web hosts; credential-gated — never the default):

- **Scope:** Azure Translation and Google Translation select through the same
  `ReferenceLocalization:Provider` switch and reuse the production translation providers and their
  registration extensions verbatim. The default selection (`Json`) is never replaced.
- **Configuration keys:**
  - Azure: `ReferenceLocalization:Provider` = `AzureTranslation`; `ReferenceLocalization:AzureTranslation:ApiKey`
    (required), optional `ReferenceLocalization:AzureTranslation:Region`,
    `ReferenceLocalization:AzureTranslation:Endpoint`,
    `ReferenceLocalization:AzureTranslation:FromLanguage`.
  - Google: `ReferenceLocalization:Provider` = `GoogleTranslation`; `ReferenceLocalization:GoogleTranslation:ApiKey`
    (required), optional `ReferenceLocalization:GoogleTranslation:Endpoint`.
- **Local development (Secret Manager):** the three web hosts declare a `UserSecretsId`; store the
  API keys outside the repository. The placeholder entries below are already present in the local
  secret stores of the Api, Mvc, and BlazorServer hosts and must be replaced with real keys before
  any end-to-end translation run:

  ```bash
  dotnet user-secrets set "ReferenceLocalization:AzureTranslation:ApiKey" "REPLACE_WITH_AZURE_TRANSLATOR_KEY" \
      --project Jeninnet.Localization.Reference.Api
  dotnet user-secrets set "ReferenceLocalization:GoogleTranslation:ApiKey" "REPLACE_WITH_GOOGLE_TRANSLATOR_KEY" \
      --project Jeninnet.Localization.Reference.Api

  # replace the placeholders above with real keys, then:
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5060 \
      dotnet run --project Jeninnet.Localization.Reference.Api
  # GET http://localhost:5060/api/books/catalog?culture=ar  → Arabic strings sourced from the translation service
  #   (provider source template = the message key; target culture = CultureInfo.Name sent verbatim)
  ```

  The identical `ApiKey` keys are stored for the Mvc and BlazorServer hosts under their own
  `UserSecretsId`s. Translating is expected only after the placeholders are replaced; a placeholder
  key reaches authentication only (401/403) and is never presented as runtime verification of a
  translation.
- **Non-interactive / CI:** set environment variables instead — e.g.
  `ReferenceLocalization__Provider=GoogleTranslation` and
  `ReferenceLocalization__GoogleTranslation__ApiKey=...`. No secret is ever committed.
- **Expected reply shape notes:** Azure sends the key as the `Ocp-Apim-Subscription-Key` header
  (region, when set, as `Ocp-Apim-Subscription-Region`); Google sends the key as the `key` query
  parameter on the v2 Basic `translate` endpoint. Both surface HTTP failures as
  `HttpRequestException`.
- **Cleanup:** `dotnet user-secrets clear --project <host>` removes the local keys.

---

## 25. Local build-safe Providers

The five local Providers (Memory, JSON, XML, RESX, SQLite) require **no server, no service, no
credential, and no machine-specific path**. Every host can exercise all five by changing one
configuration value; the content they serve is the same canonical content, so output is
code-identical across Providers (see §7). Selection value → production registration:

| Provider | Selection (`ReferenceLocalization:Provider`) | Production registration (via the shared helper) | Content / data source | Prerequisites |
|---|---|---|---|---|
| Memory | `Memory` | `AddMemoryLocalization(messages)` | embedded shared seed (`LocalizationContent`) | none |
| JSON | `Json` (default) | `AddJsonLocalizationProviders(base, cultures, "Localization")` | `Content/Localization/{en,ar,ar-SY}.json` staged to the host output `Localization/` directory | none |
| XML | `Xml` | `AddXmlLocalizationProviders(base, cultures, "Localization")` | `Content/Localization/{en,ar,ar-SY}.xml` staged to the host output `Localization/` directory | none |
| RESX | `ResX` | `AddResXLocalization(baseName, assembly)` | embedded ResourceManager satellite sets `ReferenceContent{.en,.ar,.ar-SY}.resx` in the Shared assembly | none |
| SQLite | `SQLite` | `AddSqliteLocalization(connectionString, tableName)` | `<host output>/Data/reference-localization.db` (`LocalizationMessages` table) | writes a local file only |

Notes:

- **Default selection:** `Json` (`ReferenceLocalizationOptions.Provider`). All file Providers share the
  exact same keys; per-culture JSON/XML dispatch is `CultureAwareMessageProvider` — **one registered
  Provider**, not a chain (§7).
- **Content staging is automatic:** the Shared project embeds the JSON files and copies the JSON/XML
  files to each host's output directory, so hosts never need a manual copy step.
- **RESX:** the base name
  `Jeninnet.Localization.Reference.Shared.Content.Localization.ResX.ReferenceContent` and the Shared
  assembly are passed to the provider, so identity never depends on the working directory.
- **SQLite:** the file is created and seeded on first start and idempotently rewritten on every later
  start. To reset: stop the host and delete `<host output>/Data/` (or just
  `reference-localization.db` and any `-wal`/`-shm` sidecars), then start again. The sample never
  commits a `.db` artifact.

Example — run the API host on Memory, then on SQLite (from this directory; §24 sets the full
Provider list):

```bash
ASPNETCORE_URLS=http://localhost:5060 ReferenceLocalization__Provider=Memory dotnet run --project Jeninnet.Localization.Reference.Api
ASPNETCORE_URLS=http://localhost:5060 ReferenceLocalization__Provider=SQLite dotnet run --project Jeninnet.Localization.Reference.Api
```

The same variable works on every web and desktop host (desktop hosts read
`ReferenceLocalization__Provider` directly from the environment, §26).

---

## 26. Host guidance

Each host demonstrates the same application on a different framework, plus host-specific teaching.
The table summarizes what is materially different; §24 has the exact commands; §28 states expected
behavior.

| Host | Project (under `samples/`) | Startup / composition | Provider selection | Culture mechanism | What it demonstrates |
|---|---|---|---|---|---|
| API | `Jeninnet.Localization.Reference.Api` | `Program.cs` → `ReferenceApiComposition.AddReferenceApi` | `Program.cs` binds `ReferenceLocalization` → `AddReferenceLocalizationProvider` | `RequestLocalization` middleware; `?culture=` query string or cookie | Localized JSON responses over HTTP; localized catalog and order messages; missing-book error shape |
| MVC | `Jeninnet.Localization.Reference.Mvc` | `Program.cs` → `ReferenceMvcComposition.AddReferenceMvc` (`AddMvcLocalizedValidation`) | same as API | `RequestLocalization` middleware; `?culture=` query string or cookie | Server-rendered catalog/order pages, localized `DisplayAttribute` metadata, localized validation, `IViewLocalizer` view localization (view → shared fallback), `IStringLocalizer<T>` for the controller |
| Blazor Server | `Jeninnet.Localization.Reference.BlazorServer` | `Program.cs` → `ReferenceBlazorComposition.AddReferenceBlazor` (`AddBlazorServerLocalization`) | same as API | `RequestLocalization` middleware; culture-selection component persists a cookie through the Jeninnet culture service | SSR catalog/order pages plus interactive server components (`AddInteractiveServerRenderMode`); circuit-scoped culture management through the Blazor integration package |
| Blazor Hybrid | `Jeninnet.Localization.Reference.BlazorHybrid` | `Program.cs` → `ReferenceBlazorHybridComposition` (`AddBlazorLocalization` static scope) | environment variable `ReferenceLocalization__Provider` | Jeninnet static culture service (local-storage persistence); in-app culture selector | Blazor components inside a WinForms/WebView2 desktop shell; local/offline Providers; package-driven culture switching |
| WinForms | `Jeninnet.Localization.Reference.WinForms` | `Program.cs` → `ReferenceWinFormsComposition` | environment variable `ReferenceLocalization__Provider` | app-set `CurrentUICulture`; culture ComboBox re-render | `IMessageLocalizer` from classic UI code; culture switching without ASP.NET Core |
| WPF/XAML | `Jeninnet.Localization.Reference.Wpf` | `Program.cs` → `ReferenceWpfComposition` | environment variable `ReferenceLocalization__Provider` | app-set `CurrentUICulture`; culture ComboBox | `IMessageLocalizer` through MVVM view models/bindings; distinct from XAML-native resources |

Host notes:

- **Windows-only at runtime:** the three desktop hosts target `net10.0-windows` and require the
  WindowsDesktop runtime and (for Blazor Hybrid) the WebView2 runtime. They **build** cross-platform
  via the sample `EnableWindowsTargeting` setting but are only executed on Windows.
- **Desktop headless verification:** every desktop host supports `--smoke`, which prints the localized
  headings, titles, demo keys, and order message for all supported cultures and returns a process
  exit code — no GUI session needed.
- **Automation limitation:** Blazor Server's interactive components run over a SignalR circuit in the
  browser; it has no headless CLI. Its server-side composition and routes are covered by the web-host
  tests (`Hosts.Tests`), and the desktop hosts provide the headless `--smoke` paths.
- **Not appropriate here:** server database and translation Providers are not reference scenarios for
  desktop hosts (no secret path / no network-teaching value); the Web host × Provider matrix in §6
  marks the exact cells.

---

## 27. Prerequisites matrix

Statuses: `Build-safe` (builds and runs with no external prerequisite), `Infrastructure-gated`
(needs a reachable server), `Credential-gated` (needs a local API key), `Windows-only` (needs the
Windows runtime to *run*), `Deferred` (not implemented in this sample).

| Example | OS | .NET SDK | External service | Credentials | Database | Build-safe |
|---|---|---|---|---|---|---|
| Shared (`Jeninnet.Localization.Reference.Shared`) | any | 10 | Not required | Not required | Not required | Yes |
| API host + JSON/Memory/XML/RESX/SQLite | any | 10 | Not required | Not required | Not required (SQLite writes a local file) | Yes |
| MVC host + local Providers | any | 10 | Not required | Not required | Not required | Yes |
| Blazor Server host + local Providers | any | 10 | Not required | Not required | Not required | Yes |
| Blazor Hybrid host + local Providers | Windows (runtime) | 10 | WebView2 runtime | Not required | Not required | Build-safe (Windows to run) |
| WinForms host + local Providers | Windows (runtime) | 10 | Not required | Not required | Not required | Build-safe (Windows to run) |
| WPF host + local Providers | Windows (runtime) | 10 | Not required | Not required | Not required | Build-safe (Windows to run) |
| API/MVC host + SQL Server | any | 10 | Running SQL Server | `ReferenceLocalization:SqlServer:ConnectionString` (local) | `JeninnetLocalizationReference` | Infrastructure-gated |
| API/MVC host + PostgreSQL | any | 10 | Running PostgreSQL | `ReferenceLocalization:PostgreSql:ConnectionString` (local) | `JeninnetLocalizationReference` | Infrastructure-gated |
| API/MVC host + MySQL | any | 10 | Running MySQL | `ReferenceLocalization:MySql:ConnectionString` (local) | `JeninnetLocalizationReference` | Infrastructure-gated |
| Web hosts + Azure Translation | any | 10 | Azure Translator | `ReferenceLocalization:AzureTranslation:ApiKey` (local) | Not required | Credential-gated |
| Web hosts + Google Translation | any | 10 | Google Cloud Translation | `ReferenceLocalization:GoogleTranslation:ApiKey` (local) | Not required | Credential-gated |
| Blazor WebAssembly | any | 10 | — | — | — | Deferred (not implemented) |

Credentials are supplied through .NET User Secrets or environment variables; they are never committed
(§14, §24). Test connections use the same secret mechanism or the `JENINNET_TEST_{SQLSERVER,POSTGRESQL,MYSQL}_CONNECTION`
environment variables; without them the corresponding tests are reported `Inconclusive` and the
default build stays green (§19).

---

## 28. Expected behavior

Use stable semantic expectations — the exact payload shape is not a contract, but the localized
content and status codes are stable. All values below come from the canonical content
(`Content/Localization/`) and were verified today on the JSON and server-database switches (API host
+ desktop `--smoke`); the XML, RESX, and SQLite switches are pinned by the same-content parity tests
(§19) and the focused fallback tests.

Canonical content availability (identical across every Provider and host; enforced by
`LocalizationContentTests`):

| Key | `en` | `ar` | `ar-SY` |
|---|---|---|---|
| catalog/heading, application titles, book titles/descriptions, order message | ✔ | ✔ | ✔ |
| `Demo.RegionalNote` | ✔ | ✔ | absent |
| `Demo.Showcase` | ✔ | absent | absent |
| `Demo.MissingEverywhere` | absent | absent | absent |

What a key resolves to depends on the consumer API — this split is part of the teaching:

| Consumer | `ar-SY` + `Demo.RegionalNote` | `ar-SY` + `Demo.Showcase` | `ar` + `Demo.Showcase` | `Demo.MissingEverywhere` |
|---|---|---|---|---|
| `IMessageLocalizer` (canonical chain: exact → parent → default) | `ar` value (parent hit) | `en` value (default hit) | `en` value (default hit) | `null`, surfaced as `[MISSING:key]` |
| `IStringLocalizer<T>` bridge (single exact-culture set) | `[MISSING:Demo.RegionalNote]` | `[MISSING:Demo.Showcase]` | `[MISSING:Demo.Showcase]` | `[MISSING:key]` |
| `Memory` provider (single culture-agnostic set) | `en` value | `en` value | `en` value | `[MISSING:key]` |

- The `IMessageLocalizer` outcomes are pinned by `Shared.Tests` (`JsonProvider_ResolvesThroughTheFullFallbackChain`
  plus the XML/RESX/SQLite equivalents — including the unregistered-culture `fr` → default fallback);
  the bridge outcomes are pinned by `BridgeStringLocalizer_ResolvesFromTheStoreUnderCurrentUiCulture`
  and `ReferenceResourceSetStoreTests`.
- The desktop `--smoke` lines for heading/title/demo/regional/missing resolve through the
  `IStringLocalizer<T>` bridge, so the regional `[MISSING:...]` marks in the table are exactly what
  the headless output prints. Desktop view models resolve the same keys through `IMessageLocalizer`
  and therefore show the fallback values in the GUI instead; §9 describes the canonical chain.

API host (`Jeninnet.Localization.Reference.Api`, port 5060):

- `GET /api/books/catalog?culture=ar` → 200 JSON: `Heading = "الفهرس"`, books with localized Arabic
  titles and descriptions.
- `GET /api/books/catalog/book-001?culture=ar` → 200 JSON for a known id; 404 for an unknown id.
- `POST /api/books/orders?culture=ar` with `{"BookId":"book-001","Quantity":2}` → 200 with a
  localized confirmation message using the order id, quantity, and total.
- `POST /api/books/orders?culture=ar` with an unknown book → 400 `{"Error":"Orders.Validation.BookNotFound","Message":"...تعذّر العثور على الكتاب..."}`.
- `POST /api/books/orders` with invalid quantity → 400 (MVC validation problem shape).

MVC host (`Jeninnet.Localization.Reference.Mvc`, port 5062):

- `GET /catalog?culture=ar` → localized page heading and book titles/descriptions.
- `GET /catalog/book-001?culture=ar` → localized detail (description, price label).
- `GET /orders/create?culture=ar` → order form with localized `DisplayAttribute` metadata, prompts,
  and localized book-selection/quantity validation messages when submitting invalid data.

Blazor Server host (`Jeninnet.Localization.Reference.BlazorServer`, port 5064):

- `GET /?culture=ar` → SSR catalog page with localized headings/titles.
- `GET /orders?culture=ar` → SSR order form with localized labels, options, and button.
- The culture-selection component switches culture via a cookie and reloads the page.

Desktop hosts (`--smoke`):

- Print one block per culture with: heading, application title, `Demo.Showcase`, `Demo.RegionalNote`,
  `Demo.MissingEverywhere`, and the localized order message; process exit code 0. Empty lines separate
  cultures. The JSON default and the SQLite switch have been verified on every desktop host (Host ×
  Provider matrix); the WPF `--smoke` for both switches was re-verified for this document.

Translation configuration gate:

- Selecting `AzureTranslation` or `GoogleTranslation` without a configured API key fails at startup
  with `InvalidOperationException` naming the exact missing key
  (`ReferenceLocalization:AzureTranslation:ApiKey` / `ReferenceLocalization:GoogleTranslation:ApiKey`);
  this was re-verified today by overriding the key to an empty value and observing the startup
  failure. The deterministic behavior (invariant culture → `null` without HTTP; pre-cancelled →
  cancellation) is covered by the focused reference tests. Real E2E translation only runs after the
  placeholder keys in User Secrets are replaced with real ones (§14).

---

## 29. Troubleshooting

| Symptom | Cause | Remedy |
|---|---|---|
| Startup fails with `ArgumentException` listing supported/infrastructure/credential-gated Provider names | Unknown `ReferenceLocalization:Provider` value | Use one of `Memory`/`Json`/`Xml`/`ResX`/`SQLite` for local runs (§8, §25) |
| Startup fails with `InvalidOperationException` naming `ReferenceLocalization:AzureTranslation:ApiKey` or `...:GoogleTranslation:ApiKey` | Translation Provider selected without a key | `dotnet user-secrets set "ReferenceLocalization:AzureTranslation:ApiKey" "..." --project Jeninnet.Localization.Reference.Api` (and the Mvc/Blazor equivalents); see §14, §24 |
| Startup fails naming `ReferenceLocalization:{Vendor}:ConnectionString` | Server Provider selected without a connection string | Provide the value through User Secrets or `ReferenceLocalization__{Vendor}__ConnectionString` (§24) |
| Startup hangs or times out contacting a database | Server unreachable or credentials rejected | Start the server, fix the connection value, or switch back to a local Provider |
| Desktop host fails to start on non-Windows / missing WindowsDesktop runtime | Windows-only runtime requirement | Run desktop hosts on Windows with the Windows Desktop framework (and WebView2 for Blazor Hybrid) (§26) |
| Host still uses an old Provider after changing configuration | Stale User Secrets or cached environment | Re-run `dotnet user-secrets set` / refresh the environment variable, then restart the host; verify by running the host and observing the response, not by echoing the secret |
| `?culture=fr` returns English | `fr` is not in `SupportedCultures` (`["en","ar","ar-SY"]`) | Request a supported culture (§9, §27); unsupported cultures fall back to the default |
| A UI label renders `[MISSING:<key>]` | The key is absent in every culture (by design for `Demo.MissingEverywhere`) or was removed from content | For intentional demonstration keys, this is expected; otherwise add the key to the canonical content and keep JSON/XML/RESX in parity (tests enforce this) |
| `dotnet run --project <Project>` cannot find a project | Wrong working directory | Run from `samples/` (§24) |

Only failures evidenced by the implementation contracts are listed; unrelated environment failures
are outside this sample's scope.

---

## 30. Source navigation

All paths are repository-relative. This is where each concern lives:

| Concern | Path |
|---|---|
| Shared domain (books, orders, catalog) | `samples/Jeninnet.Localization.Reference.Shared/Domain/`, `.../Application/` |
| Localization keys contract | `samples/Jeninnet.Localization.Reference.Shared/Localization/ReferenceKeys.cs` |
| Culture definitions | `samples/Jeninnet.Localization.Reference.Shared/Localization/ReferenceCultures.cs` |
| Canonical content (embed + validation) | `samples/Jeninnet.Localization.Reference.Shared/Localization/LocalizationContent.cs` |
| Provider names + build-safe/gated partition | `samples/Jeninnet.Localization.Reference.Shared/Localization/ReferenceProviderNames.cs` |
| Provider selection & translation/server configuration | `samples/Jeninnet.Localization.Reference.Shared/Localization/ReferenceLocalization.cs`; SQLite `.../ReferenceSqliteDatabase.cs`; server `.../ReferenceServerDatabase.cs`, `.../ReferenceServerVendor.cs` |
| Example-only store (Concept B) | `samples/Jeninnet.Localization.Reference.Shared/Localization/ReferenceResourceSetStore.cs` |
| Content files (JSON/XML/RESX) | `samples/Content/Localization/` |
| Web hosts `Program.cs` + composition | `samples/Jeninnet.Localization.Reference.{Api,Mvc,BlazorServer}/` |
| Desktop hosts `Program.cs` + composition + `--smoke` | `samples/Jeninnet.Localization.Reference.{WinForms,Wpf,BlazorHybrid}/` |
| Reference tests (behavior expectations) | `samples/Jeninnet.Localization.Reference.Shared.Tests/`, `...Hosts.Tests/`, `...Hosts.Desktop.Tests/` |
| Canonical contracts (`IMessageProvider`, options) | `src/Jeninnet.Localization.Abstractions/` |
| Library engine, bridge, DI (Dependency Injection) | `src/Jeninnet.Localization.Core/` |
| Production Providers (each with a README) | `src/providers/`, `src/translation/` |
| Integration READMEs (DataAnnotations, MVC validation) | `integrations/` |

Every path above exists in the current repository working tree; if you open a linked file and it is
missing, the repository layout has moved and the link should be updated (the tests and this document
must stay in sync).

---

## 31. Where to go next

After running the reference application and understanding the journey, the natural next steps are:

1. **The public architecture** — [Architecture](../docs/en/03-reference/00-architecture.md)
   explains *why* the library is designed this way (exactly-one Provider, single-owner culture
   fallback, capability model). This document only teaches usage.
2. **The library itself** — the repository-root `README.md` quick start registers the pipeline and a
   Provider in a few lines of real application code.
3. **Production Provider contracts** — each Provider under `src/providers/` and `src/translation/`
   carries a package README describing its options and registration methods.
4. **Library validation** — `tests/Jeninnet.Localization.Tests` holds the architecture, contract,
   provider, and integration tests that freeze the behavior the reference application demonstrates.
5. **Governance** — [`CONTRIBUTING.md`](../CONTRIBUTING.md) documents the public engineering rules
   this sample follows (no secrets, no machine-specific paths, build-safe defaults, honest runtime
   classification).

If this is your first time with the library, start at the repository root
(`README.md` → *Reference application* → this document's *Getting started*).
