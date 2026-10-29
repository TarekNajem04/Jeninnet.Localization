# Database schema artifacts

This folder holds the reviewed SQL artifacts for the database message providers. The providers are
read-only: they never create tables, run migrations, or seed rows at application startup. Schema
provisioning is a deployment step owned by the consumer.

The canonical table is:

```text
LocalizationMessages
    Id          BIGINT / INTEGER, auto-generated primary key
    MessageKey  string, not null
    Culture     string, not null
    Template    string, not null
    PRIMARY KEY (Id)
    UNIQUE (MessageKey, Culture)
```

`MessageKey` and `Culture` are matched by the provider as exact values. The column collation decides
case sensitivity, so the creation scripts choose case-sensitive collations where the vendor default
is not.

## Fresh database: create the table

Pick the script for your vendor. All four scripts are idempotent: an existing
`LocalizationMessages` table is left untouched.

| Vendor | Script | Notes |
|---|---|---|
| SQL Server | [`schema/0001_create_localization_messages.sqlserver.sql`](schema/0001_create_localization_messages.sqlserver.sql) | `dbo` schema, `NVARCHAR`, case-sensitive collation |
| PostgreSQL | [`schema/0001_create_localization_messages.postgresql.sql`](schema/0001_create_localization_messages.postgresql.sql) | `public` schema, `VARCHAR` keys, `TEXT` template |
| MySQL | [`schema/0001_create_localization_messages.mysql.sql`](schema/0001_create_localization_messages.mysql.sql) | `utf8mb4` with `utf8mb4_bin` key collation |
| SQLite | [`schema/0001_create_localization_messages.sqlite.sql`](schema/0001_create_localization_messages.sqlite.sql) | `INTEGER PRIMARY KEY AUTOINCREMENT`, `BINARY` text collation |

Run the table script, then seed the catalog. Seeding is content-specific and is documented in the
[deployment guide](../en/04-operations/01-deployment.md). The scripts do not create the database itself
(MySQL) or a non-default schema (SQL Server, PostgreSQL); use your normal database provisioning for
that and adjust the schema name in the script if needed.

```powershell
# SQL Server
sqlcmd -S localhost -d MyDatabase -E -i docs/database/schema/0001_create_localization_messages.sqlserver.sql
```

```bash
# PostgreSQL
psql "host=localhost dbname=mydb user=postgres" \
    -f docs/database/schema/0001_create_localization_messages.postgresql.sql

# MySQL (database selected before the script runs)
mysql --host=localhost --user=root --database=mydb \
    < docs/database/schema/0001_create_localization_messages.mysql.sql

# SQLite
sqlite3 localization.db < docs/database/schema/0001_create_localization_messages.sqlite.sql
```

## Existing pre-canonical database: rename migration

If a database still has the legacy `ValidationMessages` table (`Key`, `Culture`, `MessageTemplate`),
the create scripts are not the right tool: they would leave the old names in place. Use the guarded
rename migration instead:

- forward: [`0001_localization_messages.sql`](0001_localization_messages.sql)
- rollback: [`0001_localization_messages.rollback.sql`](0001_localization_messages.rollback.sql)

The forward script renames the table and columns while preserving rows and identities. It is
**SQL Server only** (T-SQL `sp_rename` and metadata guards). Other vendors need an equivalent
vendor-specific rename, which this repository does not ship; create the canonical table fresh and
copy the rows when the legacy schema is not in use.

The migration is a breaking change for an old application process that still queries
`ValidationMessages`/`Key`/`MessageTemplate`. Apply it as a reviewed deployment step, with a backup or
restore point, and deploy the matching application version in the same window. The deployment guide
describes the order and rollback expectations in the
[Database schema section](../en/04-operations/01-deployment.md).

## Relationship to the EF Core store

The EF Core store owns a different schema (`LocalizedStrings`, `AuditRecords`, `StringVersions`,
`CatalogState`, `StringNotes`). These scripts do **not** create or migrate that schema. Consumers who
use both the `IMessageProvider` table and the EF store must provision both; see the
[storage API](../en/03-reference/02-storage-api.md) and the
[EF Core provider README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.EntityFrameworkCore/README.md).

## Safety

- Run scripts as a reviewed database change, not from application startup.
- Take a backup or restore point before applying a rename migration.
- The create scripts contain no `DROP TABLE` and no destructive statements.
- A rollback for a freshly created table is a manual `DROP TABLE`; use it only when the data is
  expendable.
- Keep the application's `DatabaseLocalizationOptions.TableName` aligned with the table you create.
  The default is `LocalizationMessages`; schema-qualified names such as `dbo.LocalizationMessages`,
  `public.LocalizationMessages`, or `main.LocalizationMessages` are supported.

## Related documentation

- [Deployment](../en/04-operations/01-deployment.md)
- [Provider reference](../en/03-reference/03-provider-reference.md)
- [Storage API](../en/03-reference/02-storage-api.md)
- [Database providers abstraction](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/README.md)
- [Repository README](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/README.md)

## License

MIT — see [`LICENSE`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
