# Jeninnet.Localization.Providers.Database.Abstractions

`Jeninnet.Localization.Providers.Database.Abstractions` holds the shared base type and options for the ADO.NET database message providers. It is an implementation package, not a message source: the concrete packages (`Database.SQLite`, `Database.SqlServer`, `Database.MySql`, `Database.PostgreSQL`) derive from these types and add vendor-specific connection and identifier rules.

The package sits under the database providers and depends only on [`Jeninnet.Localization.Abstractions`](../../Jeninnet.Localization.Abstractions/README.md). It does not reference `Jeninnet.Localization.Storage`, does not implement a store, and is not registered directly. See the [provider reference](../../../docs/en/03-reference/03-provider-reference.md).

## Installation

```powershell
dotnet add package Jeninnet.Localization.Providers.Database.Abstractions
```

Applications normally receive it transitively through one concrete database provider; reference it directly only when implementing a new ADO.NET provider.

## Registration

This package has no `IServiceCollection` extension methods and no provider class to register. Choose one concrete provider package and call its registration method:

```csharp
services.AddLocalization();
services.AddSqliteLocalization("Data Source=localization.db");
```

A custom provider derives from `DatabaseMessageProviderBase<TOptions>`, supplies a `Func<string, DbConnection>` factory, implements `EscapeIdentifier`, and is registered as an `IInnerMessageProvider`. The concrete packages already do this.

## Configuration

`DatabaseLocalizationOptions` is the abstract options base. Each vendor options type inherits these two properties and adds none.

| Property | Type | Default | Rules |
|----|----|----|----|
| `ConnectionString` | `string` | `""` | Must be non-empty at provider construction; empty throws `ArgumentException` (null throws `ArgumentNullException`). The string is passed verbatim to the vendor connection type. |
| `TableName` | `string` | `"LocalizationMessages"` | Must be a valid SQL identifier. The setter throws `ArgumentNullException` for null and `ArgumentException` for empty or invalid input. |

`TableName` accepts one to three dot-separated segments. Each segment starts with a letter or underscore, continues with letters, digits, or underscores, and contains 1–128 characters. `LocalizationMessages`, `dbo.LocalizationMessages`, and `app.Dictionaries.LocalizationMessages` are valid; `1Messages`, `dbo..Messages`, `Messages; DROP TABLE`, and four-segment names are rejected.

The base constructor snapshots `ConnectionString` and `TableName`. Mutating the caller-owned options object after construction does not change provider behavior.

## Usage

The base type issues this shape of query, with vendor-specific identifier quoting and parameterized values:

```text
SELECT {Template} FROM {table} WHERE {MessageKey} = @MessageKey AND {Culture} = @Culture
```

A complete lookup through the canonical pipeline, using the SQLite provider as the concrete implementation:

```csharp
using System.Globalization;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Core.Extensions;
using Jeninnet.Localization.Providers.Database.SQLite.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalization(options => options.DefaultCulture = "en");
services.AddSqliteLocalization("Data Source=localization.db");

await using var provider = services.BuildServiceProvider();
var localizer = provider.GetRequiredService<IMessageLocalizer>();

var message = await localizer.LocalizeAsync(
    "Greeting",
    new Dictionary<string, object?> { ["name"] = "Ada" },
    new CultureInfo("en"));

Console.WriteLine(message ?? "(missing)"); // Hello, Ada!
```

## Behavior and limits

- **Exact lookup.** The query filters on the exact `MessageKey` and `Culture` values. There is no parent-culture walk, default culture, or provider composition. Culture fallback belongs to Core's `DefaultMessageLocalizer`.
- **Database collation.** Values are passed as parameters, so quoting is safe, but equality follows the database's collation and comparison rules. Configure the column collation if you need case-sensitive or accent-sensitive matching.
- **Read-only provider.** The base type only issues `SELECT`. It does not create tables, migrate schemas, seed rows, cache templates, or reload catalogs.
- **Connection per lookup.** Each lookup creates a connection, opens it, executes one scalar query, and disposes everything. There is no connection pooling at this layer beyond what the vendor ADO.NET provider supplies.
- **Identifier handling.** Table names are split on `.` and each segment is escaped with the vendor's quote characters. Values are never concatenated into SQL.
- **Failures propagate.** Missing keys and cultures return `null`. Connection, login, timeout, missing-table, and syntax failures surface as the vendor exception (`SqlException`, `MySqlException`, `NpgsqlException`, or `SqliteException`); they are never converted into a miss.
- **Cancellation.** A pre-cancelled token throws `OperationCanceledException` before a connection is created. The token is forwarded to `OpenAsync` and `ExecuteScalarAsync`.
- **Thread-safety.** Provider state is immutable after construction, so a singleton is safe. Each call owns its connection and command.
- **Schema.** The canonical table has `Id`, `MessageKey`, `Culture`, and `Template`, with a unique `(MessageKey, Culture)` pair. Deployment and migration are consumer-owned; see [Deployment](../../../docs/en/04-operations/01-deployment.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

| Package | Relationship |
|----|----|
| [`Database.SQLite`](../Jeninnet.Localization.Providers.Database.SQLite/README.md) | SQLite provider built on this base. |
| [`Database.SqlServer`](../Jeninnet.Localization.Providers.Database.SqlServer/README.md) | SQL Server provider built on this base. |
| [`Database.MySql`](../Jeninnet.Localization.Providers.Database.MySql/README.md) | MySQL provider built on this base. |
| [`Database.PostgreSQL`](../Jeninnet.Localization.Providers.Database.PostgreSQL/README.md) | PostgreSQL provider built on this base. |
| [`Database.EntityFrameworkCore`](../Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md) | Separate store package with a different schema; not a message provider. |
| [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) | Pipeline and fallback owner. |

## Documentation

- [Provider reference](../../../docs/en/03-reference/03-provider-reference.md)
- [Deployment](../../../docs/en/04-operations/01-deployment.md)
- [Troubleshooting](../../../docs/en/04-operations/04-troubleshooting.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../../LICENSE).
