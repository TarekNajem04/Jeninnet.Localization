//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The sample-owned server-database demonstration used by the SQL Server, PostgreSQL, and MySQL
/// reference providers. The type owns only a deterministic sample database and the canonical-content
/// seed; the shipping database providers read the table exclusively through the production contract.
/// </summary>
/// <remarks>
/// Ownership is explicit: the initializer only ever creates and seeds the deterministic
/// <see cref="DATABASE_NAME"/> database, and refuses to operate when the resolved connection does not
/// point at it. The configured connection string (which carries the local credentials) is supplied by
/// .NET User Secrets and is never stored in, or reported from, this type.
/// </remarks>
public static class ReferenceServerDatabase {
    /// <summary>
    /// The deterministic, sample-owned database name shared by every reference host. The initializer
    /// creates this database when it is absent and refuses to touch any other database.
    /// </summary>
    public const string DATABASE_NAME = "JeninnetLocalizationReference";

    /// <summary>
    /// The canonical table name read by the production database providers.
    /// </summary>
    public const string TABLE_NAME = "LocalizationMessages";

    private const string UNKNOWN_VENDOR_MESSAGE = "Unknown reference server vendor.";

    /// <summary>
    /// Creates (when absent) and seeds the sample database so it mirrors the canonical reference
    /// content exactly, then returns the connection string consumed by the production provider.
    /// </summary>
    /// <remarks>
    /// Deterministic and idempotent: the sample database and its table are created once, and every
    /// initialization rewrites the rows from <see cref="LocalizationContent"/> inside a single
    /// transaction, so repeated starts never duplicate rows and stale rows cannot silently alter the
    /// sample's behavior. Any database named by the supplied connection string is overridden with
    /// <see cref="DATABASE_NAME"/> before use.
    /// </remarks>
    /// <param name="vendor">The server vendor to initialize.</param>
    /// <param name="connectionString">The local connection string (server and credentials) supplied by User Secrets or an environment variable.</param>
    /// <returns>The sample-owned connection string consumed by the production provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no connection string is configured, or when the resolved connection does not point at the sample-owned database.</exception>
    public static string EnsureInitialized(ReferenceServerVendor vendor, string connectionString) {
        var sampleConnectionString = BuildConnectionString(vendor, connectionString, DATABASE_NAME);
        EnsureDatabaseExists(vendor, connectionString);

        using var connection = CreateConnection(vendor, sampleConnectionString);
        connection.Open();
        EnsureSampleOwnership(vendor, connection);

        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, SchemaCommand(vendor));
        Execute(connection, transaction, DeleteCommand(vendor));
        SeedCanonicalContent(vendor, connection, transaction);
        transaction.Commit();

        return sampleConnectionString;
    }

    /// <summary>
    /// Drops the sample-owned table so the next startup recreates identical canonical content.
    /// </summary>
    /// <remarks>
    /// Idempotent and safe: it only ever drops <see cref="TABLE_NAME"/> inside the sample-owned
    /// <see cref="DATABASE_NAME"/> database and never removes a database or any other object.
    /// </remarks>
    /// <param name="vendor">The server vendor to reset.</param>
    /// <param name="connectionString">The local connection string (server and credentials) supplied by User Secrets or an environment variable.</param>
    /// <exception cref="InvalidOperationException">Thrown when no connection string is configured, or when the resolved connection does not point at the sample-owned database.</exception>
    public static void Reset(ReferenceServerVendor vendor, string connectionString) {
        var sampleConnectionString = BuildConnectionString(vendor, connectionString, DATABASE_NAME);

        using var connection = CreateConnection(vendor, sampleConnectionString);
        connection.Open();
        EnsureSampleOwnership(vendor, connection);

        Execute(connection, transaction: null, DropTableCommand(vendor));
    }

    /// <summary>
    /// Ensures database exists.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connectionString">The connection string.</param>
    private static void EnsureDatabaseExists(ReferenceServerVendor vendor, string connectionString) {
        var adminConnectionString = BuildConnectionString(vendor, connectionString, AdminDatabaseName(vendor));
        using var connection = CreateConnection(vendor, adminConnectionString);
        connection.Open();

        if(vendor == ReferenceServerVendor.PostgreSql) {
            using var exists = connection.CreateCommand();
            exists.CommandText = PostgreSqlDatabaseExistsCommand();
            if(exists.ExecuteScalar() is not null) {
                return;
            }
        }

        Execute(connection, transaction: null, CreateDatabaseCommand(vendor));
    }

    /// <summary>
    /// Ensures sample ownership.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connection">The connection.</param>
    /// <exception cref="InvalidOperationException">Thrown when the connection does not point at the sample-owned database.</exception>
    private static void EnsureSampleOwnership(ReferenceServerVendor vendor, DbConnection connection) {
        if(!string.Equals(connection.Database, DATABASE_NAME, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                $"The reference {Describe(vendor)} demonstration only initializes its own '{DATABASE_NAME}' database. " +
                "Verify the configured server and credentials; the connection string itself is never displayed.");
        }
    }

    /// <summary>
    /// Performs the seed canonical content operation.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    private static void SeedCanonicalContent(ReferenceServerVendor vendor, DbConnection connection, DbTransaction transaction) {
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = InsertCommand(vendor);

        var keyParameter = insert.CreateParameter();
        keyParameter.ParameterName = "@MessageKey";
        insert.Parameters.Add(keyParameter);
        var cultureParameter = insert.CreateParameter();
        cultureParameter.ParameterName = "@Culture";
        insert.Parameters.Add(cultureParameter);
        var templateParameter = insert.CreateParameter();
        templateParameter.ParameterName = "@Template";
        insert.Parameters.Add(templateParameter);

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            foreach(var (messageKey, value) in LocalizationContent.GetTemplates(cultureCode)) {
                keyParameter.Value = messageKey;
                cultureParameter.Value = cultureCode;
                templateParameter.Value = value;
                insert.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Performs the execute operation.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="commandText">The command text.</param>
    private static void Execute(DbConnection connection, DbTransaction? transaction, string commandText) {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Builds connection string.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="database">The database.</param>
    /// <returns>The connection string text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is invalid.</exception>
    private static string BuildConnectionString(ReferenceServerVendor vendor, string connectionString, string database) {
        if(string.IsNullOrWhiteSpace(connectionString)) {
            throw new InvalidOperationException(
                $"The reference {Describe(vendor)} provider requires a connection string. Configure " +
                $"'{ConfigurationKey(vendor)}' through .NET User Secrets or the matching environment variable.");
        }

        var builder = CreateConnectionStringBuilder(vendor, connectionString);
        if(!string.IsNullOrEmpty(database)) {
            builder["Database"] = database;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Creates a vendor-specific connection-string builder for the supplied connection string.
    /// </summary>
    /// <param name="vendor">The reference server vendor that selects the builder type.</param>
    /// <param name="connectionString">The configured connection string to parse.</param>
    /// <returns>The vendor-specific builder used to replace the database name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static DbConnectionStringBuilder CreateConnectionStringBuilder(ReferenceServerVendor vendor, string connectionString) => vendor switch {
        ReferenceServerVendor.SqlServer => new SqlConnectionStringBuilder(connectionString),
        ReferenceServerVendor.PostgreSql => new NpgsqlConnectionStringBuilder(connectionString),
        ReferenceServerVendor.MySql => new MySqlConnectionStringBuilder(connectionString),
        _ => UnsupportedVendor<DbConnectionStringBuilder>(vendor),
    };

    /// <summary>
    /// Creates the vendor-specific connection for the supplied connection string.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <param name="connectionString">The initialized sample-database connection string.</param>
    /// <returns>The openable vendor-specific connection.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static DbConnection CreateConnection(ReferenceServerVendor vendor, string connectionString) => vendor switch {
        ReferenceServerVendor.SqlServer => new SqlConnection(connectionString),
        ReferenceServerVendor.PostgreSql => new NpgsqlConnection(connectionString),
        ReferenceServerVendor.MySql => new MySqlConnection(connectionString),
        _ => UnsupportedVendor<DbConnection>(vendor),
    };

    /// <summary>
    /// Gets the administrative database name used to create the sample database for the vendor.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The administrative database name, or an empty string when the vendor does not use one.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string AdminDatabaseName(ReferenceServerVendor vendor) =>
        VendorText(vendor, "master", "postgres", string.Empty);

    /// <summary>
    /// Gets the human-readable display name of the vendor.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The vendor display name used in diagnostics.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string Describe(ReferenceServerVendor vendor) =>
        VendorText(vendor, "SQL Server", "PostgreSQL", "MySQL");

    /// <summary>
    /// Gets the configuration key that supplies the connection string for the vendor.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The configuration key read from user secrets or the matching environment variable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string ConfigurationKey(ReferenceServerVendor vendor) =>
        VendorText(
            vendor,
            "ReferenceLocalization:SqlServer:ConnectionString",
            "ReferenceLocalization:PostgreSql:ConnectionString",
            "ReferenceLocalization:MySql:ConnectionString");

    /// <summary>
    /// Builds the vendor-specific SQL statement that creates the reference database when it is absent.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The SQL statement applied against the vendor's administrative database.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string CreateDatabaseCommand(ReferenceServerVendor vendor) =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        VendorText(
            vendor,
            $"IF DB_ID(N'{DATABASE_NAME}') IS NULL CREATE DATABASE [{DATABASE_NAME}];",
            $"CREATE DATABASE \"{DATABASE_NAME}\";",
            $"CREATE DATABASE IF NOT EXISTS `{DATABASE_NAME}`;");
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Performs the postgre sql database exists command operation.
    /// </summary>
    /// <returns>The postgre sql database exists command text.</returns>
    private static string PostgreSqlDatabaseExistsCommand() =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        $"SELECT 1 FROM pg_database WHERE datname = '{DATABASE_NAME}';";
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Builds the vendor-specific SQL statement that creates the localization table when it is absent.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The idempotent table-creation statement for the canonical schema.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string SchemaCommand(ReferenceServerVendor vendor) =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        VendorText(
            vendor,
            $"""
                IF OBJECT_ID(N'[{TABLE_NAME}]', N'U') IS NULL
                CREATE TABLE [{TABLE_NAME}] (
                    [MessageKey] NVARCHAR(200) NOT NULL,
                    [Culture] NVARCHAR(35) NOT NULL,
                    [Template] NVARCHAR(MAX) NOT NULL,
                    CONSTRAINT [PK_{TABLE_NAME}] PRIMARY KEY ([MessageKey], [Culture])
                );
                """,
            $"""
                CREATE TABLE IF NOT EXISTS "{TABLE_NAME}" (
                    "MessageKey" VARCHAR(200) NOT NULL,
                    "Culture" VARCHAR(35) NOT NULL,
                    "Template" TEXT NOT NULL,
                    PRIMARY KEY ("MessageKey", "Culture")
                );
                """,
            $"""
                CREATE TABLE IF NOT EXISTS `{TABLE_NAME}` (
                    `MessageKey` VARCHAR(200) NOT NULL,
                    `Culture` VARCHAR(35) NOT NULL,
                    `Template` TEXT NOT NULL,
                    PRIMARY KEY (`MessageKey`, `Culture`)
                );
                """);
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Builds the vendor-specific SQL statement that deletes every seeded localization row.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The DELETE statement for the canonical table.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string DeleteCommand(ReferenceServerVendor vendor) =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        VendorText(
            vendor,
            $"DELETE FROM [{TABLE_NAME}];",
            $"DELETE FROM \"{TABLE_NAME}\";",
            $"DELETE FROM `{TABLE_NAME}`;");
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Builds the vendor-specific parameterized INSERT statement for one localized message.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The INSERT statement using the canonical parameter names.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string InsertCommand(ReferenceServerVendor vendor) =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        VendorText(
            vendor,
            $"INSERT INTO [{TABLE_NAME}] ([MessageKey], [Culture], [Template]) VALUES (@MessageKey, @Culture, @Template);",
            $"INSERT INTO \"{TABLE_NAME}\" (\"MessageKey\", \"Culture\", \"Template\") VALUES (@MessageKey, @Culture, @Template);",
            $"INSERT INTO `{TABLE_NAME}` (`MessageKey`, `Culture`, `Template`) VALUES (@MessageKey, @Culture, @Template);");
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Builds the vendor-specific SQL statement that drops the localization table when it exists.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The idempotent table-drop statement.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string DropTableCommand(ReferenceServerVendor vendor) =>
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        VendorText(
            vendor,
            $"DROP TABLE IF EXISTS [{TABLE_NAME}];",
            $"DROP TABLE IF EXISTS \"{TABLE_NAME}\";",
            $"DROP TABLE IF EXISTS `{TABLE_NAME}`;");
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

    /// <summary>
    /// Selects the vendor-specific text value, failing fast for an unknown vendor.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <param name="sqlServer">The value used for SQL Server.</param>
    /// <param name="postgreSql">The value used for PostgreSQL.</param>
    /// <param name="mySql">The value used for MySQL.</param>
    /// <returns>The value selected for <paramref name="vendor"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vendor is unknown.</exception>
    private static string VendorText(
        ReferenceServerVendor vendor,
        string sqlServer,
        string postgreSql,
        string mySql) => vendor switch {
            ReferenceServerVendor.SqlServer => sqlServer,
            ReferenceServerVendor.PostgreSql => postgreSql,
            ReferenceServerVendor.MySql => mySql,
            _ => UnsupportedVendor<string>(vendor),
        };

    /// <summary>
    /// Fails fast for a vendor value outside the supported enum set.
    /// </summary>
    /// <typeparam name="T">The value type required by the calling switch expression.</typeparam>
    /// <param name="vendor">The unsupported vendor.</param>
    /// <returns>Never returns; the method always throws.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Always thrown for <paramref name="vendor"/>.</exception>
    private static T UnsupportedVendor<T>(ReferenceServerVendor vendor) =>
        throw new ArgumentOutOfRangeException(nameof(vendor), vendor, UNKNOWN_VENDOR_MESSAGE);
}
