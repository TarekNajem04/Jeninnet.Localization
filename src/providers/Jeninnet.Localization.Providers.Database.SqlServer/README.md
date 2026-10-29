# Jeninnet.Localization.Providers.Database.SqlServer

`Jeninnet.Localization.Providers.Database.SqlServer` reads message templates from Microsoft SQL Server through `Microsoft.Data.SqlClient`. `SqlServerMessageProvider` implements `IInnerMessageProvider` on top of the shared database base class and performs a single exact key/culture lookup.

The package is one of the four ADO.NET message providers. It is not a resource-set store: it feeds `IMessageLocalizer` and does not implement `ILocalizationStore`. It uses the canonical `LocalizationMessages` table, not the EF store's `LocalizedStrings` schema. See the [provider reference](../../../docs/en/03-reference/03-provider-reference.md) and [Deployment](../../../docs/en/04-operations/01-deployment.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Providers.Database.SqlServer
```

Add [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) for the localizer, fallback, and formatting pipeline.

## Registration

All three overloads register `SqlServerMessageProvider` as a singleton `IInnerMessageProvider`.

```csharp
using Jeninnet.Localization.Providers.Database.SqlServer.Extensions;

// Connection string with the default table name.
services.AddSqlServerLocalization(
    "Server=.;Database=Localization;Trusted_Connection=True;TrustServerCertificate=True;");

// Connection string plus a schema-qualified table.
services.AddSqlServerLocalization(
    "Server=.;Database=Localization;Trusted_Connection=True;TrustServerCertificate=True;",
    tableName: "dbo.LocalizationMessages");

// Configure delegate (an options-object overload also exists).
services.AddSqlServerLocalization(options => {
    options.ConnectionString = "Server=.;Database=Localization;Trusted_Connection=True;TrustServerCertificate=True;";
    options.TableName = "dbo.LocalizationMessages";
});
```

Registration validates that `ConnectionString` and `TableName` are non-empty but deliberately does not open a connection. A null services collection, options object, or configure delegate throws `ArgumentNullException`; an empty connection string throws `ArgumentException`.

## Configuration

`SqlServerLocalizationOptions` derives from `DatabaseLocalizationOptions` and adds no properties.

| Property | Type | Default | Notes |
|----|----|----|----|
| `ConnectionString` | `string` | `""` | Required. Passed to `Microsoft.Data.SqlClient.SqlConnection`. |
| `TableName` | `string` | `"LocalizationMessages"` | Valid SQL identifier, up to three dot-separated segments. A schema-qualified `dbo.LocalizationMessages` is common. |

Values are snapshotted at provider construction; changing the options object afterward has no effect. Identifier segments are quoted with square brackets, so `dbo.LocalizationMessages` becomes `[dbo].[LocalizationMessages]`.

## Usage

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.SqlServer.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddSqlServerLocalization(
    "Server=.;Database=Localization;Trusted_Connection=True;TrustServerCertificate=True;");

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var message = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(message ?? "(missing)"); // Hello, Ada!
```

With this row present, the provider is queried for `Greeting` and `en`, returns `Hello, {name}!`, and the localizer formats it:

```text
MessageKey | Culture | Template
-----------+---------+-----------------
Greeting   | en      | Hello, {name}!
```

## Behavior and limits

- **Exactly one provider.** The registration adds one `IInnerMessageProvider`. Registering this provider alongside another source makes `LocalizationProviderSelector` throw `InvalidOperationException` when `IMessageProvider` is resolved.
- **Exact lookup.** The query filters on the exact `MessageKey` and `Culture` values (`Culture.Name`, such as `en` or `en-US`). There is no parent walk or default-culture step in the provider; Core owns fallback. A missing key or culture returns `null`.
- **Collation.** Key and culture comparison follows the SQL Server column collation. Use a case-sensitive collation when keys must match ordinally.
- **Parameterized and quoted.** Key and culture are ADO.NET parameters; the table name is escaped per segment. Keys may contain quotes or SQL punctuation without changing the query.
- **Read-only and stateless.** The provider only runs a scalar `SELECT`. It does not create or migrate the table, seed data, cache results, or reload. Every lookup opens a connection.
- **Failures propagate.** `SqlException` for connectivity, login, TLS, timeout, or a missing table is surfaced to the caller, never converted into a missing message.
- **Cancellation.** A cancelled token throws `OperationCanceledException`; it is forwarded to the open and execute operations.
- **Thread-safety.** Configuration is immutable after construction and each lookup owns its connection and command, so the singleton registration is safe.
- **Schema.** Expected columns are `Id`, `MessageKey` (`NVARCHAR`), `Culture` (`NVARCHAR`), and `Template` (`NVARCHAR`), with a unique `(MessageKey, Culture)` pair. Schema provisioning is consumer-owned; the repository's guarded rename script is documented in [Deployment](../../../docs/en/04-operations/01-deployment.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Relationship |
|----|----|
| [`Database.Abstractions`](../Jeninnet.Localization.Providers.Database.Abstractions/README.md) | Base provider and options contract. |
| [`Database.SQLite`](../Jeninnet.Localization.Providers.Database.SQLite/README.md) | File-backed sibling provider. |
| [`Database.MySql`](../Jeninnet.Localization.Providers.Database.MySql/README.md) | MySQL sibling provider. |
| [`Database.PostgreSQL`](../Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | PostgreSQL sibling provider. |
| [`Database.EntityFrameworkCore`](../Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | Resource-set store with a separate schema; not a message provider. |
| [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) | Pipeline and fallback owner. |

## Documentation

- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Deployment](../../../docs/en/04-operations/01-deployment.md)
- [Troubleshooting](../../../docs/en/04-operations/04-troubleshooting.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../../LICENSE).
