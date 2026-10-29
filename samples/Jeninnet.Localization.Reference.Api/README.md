# Jeninnet.Localization.Reference.Api

The ASP.NET Core Web API host of the Jeninnet.Localization reference application. It exposes the shared BookStore catalog and order scenario over HTTP and demonstrates the canonical localization pipeline — `IMessageLocalizer`, the `IStringLocalizer<T>` bridge, request culture selection, and exactly-one Provider composition — without any external infrastructure by default.

The project is a **sample**, not a package: `IsPackable=false`, never published, and referenced only by the repository solution and its host-composition tests. The shared domain, keys, and content live in [`Jeninnet.Localization.Reference.Shared`](../Jeninnet.Localization.Reference.Shared/README.md).

## What this host demonstrates

- **Localized JSON responses** for catalog headings, book titles, descriptions, and order results.
- **Two consumer APIs side by side**: `IStringLocalizer<BooksController>` for catalog text and `IMessageLocalizer` for formatted order messages.
- **Request culture selection** through `RequestLocalization` with `?culture=` and a cookie, for the supported cultures `en`, `ar`, and `ar-SY`.
- **Exactly one Provider** registered through the shared `AddReferenceLocalizationProvider` helper; switching Providers is a configuration change, never an application-code change.
- **Localized validation and business errors** returned in the response body rather than as raw exception text.

## Prerequisites

- .NET 10 SDK.
- No database, network service, or credential for the default `Json` Provider.
- SQL Server, PostgreSQL, or MySQL only if you select one of the infrastructure-gated Providers.
- Azure Translator or Google Cloud Translation credentials only if you select a translation Provider.

## Run

From the repository root:

```bash
dotnet restore Jeninnet.Localization.slnx
dotnet build Jeninnet.Localization.slnx -c Release

ASPNETCORE_URLS=http://localhost:5060 dotnet run \
    --project samples/Jeninnet.Localization.Reference.Api
```

PowerShell equivalent for the environment variable:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5060"
dotnet run --project samples/Jeninnet.Localization.Reference.Api
```

The launch profile in `Properties/launchSettings.json` binds `https://localhost:2926` and `http://localhost:2930`; the commands above pin port 5060 explicitly so they match the reference runbook in [`samples/README.md`](../README.md).

Smoke-check the running host:

```bash
curl "http://localhost:5060/api/books/catalog?culture=ar"
curl "http://localhost:5060/api/books/catalog/book-001?culture=ar"
curl -X POST "http://localhost:5060/api/books/orders?culture=ar" \
     -H "Content-Type: application/json" \
     -d '{"BookId":"book-001","Quantity":2}'
```

## Endpoints

| Method | Route | Purpose | Success | Failure |
|----|----|----|----|----|
| `GET` | `/api/books/catalog` | Full catalog with localized heading, titles, and descriptions | `200` JSON | — |
| `GET` | `/api/books/catalog/{id}` | One book with localized title and description | `200` JSON | `404` for an unknown id |
| `POST` | `/api/books/orders` | Places an order and returns a localized confirmation or error | `200` JSON | `400` validation problem or `400` with `{ Error, Message }` |

The order request model is `CreateOrderRequest` (`BookId`, `Quantity`) from the shared project. Invalid `ModelState` is returned through `ValidationProblem`; a business failure returns the stable error key plus its localized message.

## Configuration

The host reads the `ReferenceLocalization` configuration section and binds it to `ReferenceLocalizationOptions`. `appsettings.json` selects the default Provider:

```json
{
  "ReferenceLocalization": {
    "Provider": "Json"
  }
}
```

Override the selection per run with an environment variable:

```bash
ASPNETCORE_URLS=http://localhost:5060 \
ReferenceLocalization__Provider=SQLite \
dotnet run --project samples/Jeninnet.Localization.Reference.Api
```

| Provider value | Class | Prerequisites |
|----|----|----|
| `Memory` | Build-safe | None |
| `Json` (default) | Build-safe | None |
| `Xml` | Build-safe | None |
| `ResX` | Build-safe | None |
| `SQLite` | Build-safe | Writes a local database under the host output directory |
| `SqlServer` | Infrastructure-gated | `ReferenceLocalization:SqlServer:ConnectionString` |
| `PostgreSql` | Infrastructure-gated | `ReferenceLocalization:PostgreSql:ConnectionString` |
| `MySql` | Infrastructure-gated | `ReferenceLocalization:MySql:ConnectionString` |
| `AzureTranslation` | Credential-gated | `ReferenceLocalization:AzureTranslation:ApiKey` |
| `GoogleTranslation` | Credential-gated | `ReferenceLocalization:GoogleTranslation:ApiKey` |

The host declares a `UserSecretsId`, so local development can keep gated values outside the repository:

```bash
dotnet user-secrets set "ReferenceLocalization:Provider" "SqlServer" \
    --project samples/Jeninnet.Localization.Reference.Api
dotnet user-secrets set "ReferenceLocalization:SqlServer:ConnectionString" \
    "Server=localhost;Integrated Security=True;TrustServerCertificate=True;" \
    --project samples/Jeninnet.Localization.Reference.Api
```

The complete Provider matrix, database initialization rules, and credential-gated workflow are documented in [`samples/README.md`](../README.md) sections 5–8 and 25–28.

## Culture selection

`Program.cs` configures request localization with:

- default culture `en`;
- supported cultures and UI cultures `en`, `ar`, `ar-SY`;
- `QueryStringRequestCultureProvider` followed by `CookieRequestCultureProvider`.

```text
http://localhost:5060/api/books/catalog?culture=ar
http://localhost:5060/api/books/catalog/book-001?culture=ar-SY
```

Core owns fallback: exact culture, then parent cultures, then the configured default culture. A request for an unsupported culture such as `fr` falls back to `en`. Providers remain exact-match; they never walk parents on their own.

## Composition

```text
Program.cs
    -> AddReferenceLocalizationProvider(options => configuration.Bind(options))
    -> AddControllers()
    -> UseRequestLocalization(...)
    -> MapControllers()
```

`ReferenceApiComposition.AddReferenceApi` exposes the same registration as a reusable composition entry point for tests and hosts. It registers the reference localization stack and MVC controllers; the book endpoints inject `IStringLocalizer<BooksController>` and `IMessageLocalizer` from that container.

## Testing

The API composition is covered by [`Jeninnet.Localization.Reference.Hosts.Tests`](../Jeninnet.Localization.Reference.Hosts.Tests/README.md):

- exactly one inner Provider for every build-safe Provider;
- the expected Provider implementation is resolved after selection;
- the canonical pipeline services and `IStringLocalizer<T>` bridge are available;
- culture-aware bridge lookups match the canonical content.

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Tests \
    --configuration Release
```

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Choosing a provider](../../docs/en/02-guides/01-choosing-a-provider.md)
- [Configuring providers](../../docs/en/02-guides/02-configuring-providers.md)
- [Cultures and fallback](../../docs/en/02-guides/03-cultures-and-fallback.md)
- [ASP.NET Core and MVC guide](../../docs/en/02-guides/08-aspnet-core-and-mvc.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
