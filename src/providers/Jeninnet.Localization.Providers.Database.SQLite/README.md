# Jeninnet.Localization.Providers.Database.SQLite

`Jeninnet.Localization.Providers.Database.SQLite` reads message templates from a SQLite database through `Microsoft.Data.Sqlite`. `SqliteMessageProvider` implements `IInnerMessageProvider` on top of the shared database base class and performs a single exact key/culture lookup.

The package is a message provider, not a resource-set store. It feeds `IMessageLocalizer` and does not implement `ILocalizationStore`; it uses the canonical `LocalizationMessages` table and not the EF store's schema. See the [provider reference](../../../docs/en/03-reference/03-provider-reference.md) and [Deployment](../../../docs/en/04-operations/01-deployment.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Providers.Database.SQLite
```

Add [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) for the localizer, fallback, and formatting pipeline.

## Registration

All three overloads register `SqliteMessageProvider` as a singleton `IInnerMessageProvider`.

```csharp
using Jeninnet.Localization.Providers.Database.SQLite.Extensions;

// Connection string with the default table name.
services.AddSqliteLocalization("Data Source=localization.db");

// Connection string plus a schema-qualified table.
services.AddSqliteLocalization("Data Source=localization.db", tableName: "main.LocalizationMessages");

// Configure delegate (an options-object overload also exists).
services.AddSqliteLocalization(options => {
    options.ConnectionString = "Data Source=localization.db";
    options.TableName = "main.LocalizationMessages";
});
```

Registration validates that `ConnectionString` and `TableName` are non-empty but does not open the database. A null services collection, options object, or configure delegate throws `ArgumentNullException`; an empty connection string throws `ArgumentException`.

Resolve the database path from `AppContext.BaseDirectory`, not the current working directory; a relative `Data Source` is resolved by SQLite against the process working directory.

## Configuration

`SqliteLocalizationOptions` derives from `DatabaseLocalizationOptions` and adds no properties.

| Property | Type | Default | Notes |
|----|----|----|----|
| `ConnectionString` | `string` | `""` | Required. A SQLite connection string such as `Data Source=localization.db`, optionally with `Mode=ReadOnly`. |
| `TableName` | `string` | `"LocalizationMessages"` | Valid SQL identifier, up to three dot-separated segments. `main.LocalizationMessages` names the main schema explicitly. |

Values are snapshotted at provider construction; changing the options object afterward has no effect. Identifier segments are quoted with double quotes, so `main.LocalizationMessages` becomes `"main"."LocalizationMessages"`.

## Usage

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.SQLite.Extensions;
using Microsoft.Extensions.DependencyInjection;

var databasePath = Path.Combine(AppContext.BaseDirectory, "localization.db");
var services = new ServiceCollection();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddSqliteLocalization($"Data Source={databasePath}");

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

- **Exactly one provider.** The registration adds one `IInnerMessageProvider`. Adding this provider alongside another source makes `LocalizationProviderSelector` throw `InvalidOperationException` when `IMessageProvider` is resolved.
- **Exact lookup.** The query filters on the exact `MessageKey` and `Culture` values (`Culture.Name`, such as `en` or `en-US`). There is no parent walk or default-culture step in the provider; Core owns fallback. A missing key or culture returns `null`.
- **Collation.** SQLite compares text with the column collation. The default `BINARY` collation is case-sensitive; `COLLATE NOCASE` changes that behavior for the column.
- **Parameterized and quoted.** Key and culture are ADO.NET parameters; the table name is escaped per segment with double quotes. Keys containing quotes or SQL punctuation are safe.
- **Read-only and stateless.** The provider only runs a scalar `SELECT`. It does not create or migrate the table, seed data, cache results, or reload. Every lookup opens the database.
- **Failures propagate.** `SqliteException` for a locked database, missing table, or unwritable path is surfaced to the caller, never converted into a missing message.
- **Cancellation.** A cancelled token throws `OperationCanceledException`; it is forwarded to the open and execute operations.
- **Thread-safety.** Configuration is immutable after construction and each lookup owns its connection and command, so the singleton registration is safe.
- **Deployment state.** The database is a file, not part of the assembly. Put it on a writable, backed-up volume, do not bake a mutable copy into a read-only image, and treat schema migration as a deployment step.
- **Schema.** Expected columns are `Id`, `MessageKey` (`TEXT`), `Culture` (`TEXT`), and `Template` (`TEXT`), with a unique `(MessageKey, Culture)` pair. Schema provisioning is consumer-owned; see [Deployment](../../../docs/en/04-operations/01-deployment.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Relationship |
|----|----|
| [`Database.Abstractions`](../Jeninnet.Localization.Providers.Database.Abstractions/README.md) | Base provider and options contract. |
| [`Database.SqlServer`](../Jeninnet.Localization.Providers.Database.SqlServer/README.md) | SQL Server sibling provider. |
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
