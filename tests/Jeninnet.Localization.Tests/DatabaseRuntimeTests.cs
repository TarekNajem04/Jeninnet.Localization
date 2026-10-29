//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Real-database runtime verification for the canonical database message providers (R29).
/// Each test requires a connection string supplied through an environment variable; when the
/// variable is unset the test is inconclusive so CI without database services stays green.
/// MySQL/PostgreSQL tests create an isolated database; the SQL Server test runs its isolated
/// schema inside the provided test database (a SQL-authentication login with only dbcreator
/// cannot open a database owned by another login). All tests seed rows directly as test setup;
/// the production provider remains read-only and performs the real lookup SQL.
/// </summary>
[TestClass]
[DoNotParallelize]
public partial class DatabaseRuntimeTests {
    private const string MYSQL_CONNECTION_ENV = "JENINNET_TEST_MYSQL_CONNECTION";
    private const string POSTGRESQL_CONNECTION_ENV = "JENINNET_TEST_POSTGRESQL_CONNECTION";
    private const string SQLSERVER_CONNECTION_ENV = "JENINNET_TEST_SQLSERVER_CONNECTION";
    private const string TABLE_NAME = "LocalizationMessages";

    /// <summary>
    /// Verifies that my sql provider runtime lookup against real my sql.
    /// </summary>
    [TestMethod]
    public async Task MySqlProvider_RuntimeLookup_AgainstRealMySqlAsync() {
        var baseConnection = RequireEnvironment(MYSQL_CONNECTION_ENV);
        const string database = "jeninnet_localization_runtime_tests";

        // Isolated database + canonical schema, created and seeded directly as test setup.
        await using(var setup = new MySqlConnection(baseConnection)) {
            if(await TryOpenAsync(setup) is { } openError) {
                Assert.Inconclusive($"the database engine runtime environment unavailable ({SanitizeDiagnostic(openError)})");
            }

            await ExecuteAsync(setup, $"CREATE DATABASE IF NOT EXISTS `{database}`", TestContext.CancellationToken);
            await ExecuteAsync(setup, $"""
                CREATE TABLE IF NOT EXISTS `{database}`.`{TABLE_NAME}` (
                    `MessageKey` VARCHAR(255) NOT NULL,
                    `Culture` VARCHAR(64) NOT NULL,
                    `Template` VARCHAR(4000) NOT NULL,
                    PRIMARY KEY (`MessageKey`, `Culture`)
                )
                """, TestContext.CancellationToken);
            await ExecuteAsync(setup, $"DELETE FROM `{database}`.`{TABLE_NAME}`", TestContext.CancellationToken);
            await SeedAsync(setup, database, "greeting", "en", "Hello", null, TestContext.CancellationToken);
            await SeedAsync(setup, database, "greeting", "nl", "Hallo", null, TestContext.CancellationToken);
            await SeedAsync(setup, database, "greet'ing\"; --", "en", "Param safe", null, TestContext.CancellationToken);
        }

        var connectionString = WithDatabase(baseConnection, database);
        var provider = new MySqlMessageProvider(new MySqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = TABLE_NAME,
        });

        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello", "exact lookup, seeded culture");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("nl"), CancellationToken.None)).Should().Be("Hallo", "culture separation");
        (await provider.GetMessageTemplateAsync("nonexistent", new CultureInfo("en"), CancellationToken.None)).Should().BeNull("missing key");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("de"), CancellationToken.None)).Should().BeNull("missing culture");
        (await provider.GetMessageTemplateAsync("greet'ing\"; --", new CultureInfo("en"), CancellationToken.None)).Should().Be("Param safe", "key supplied as a parameter");

        var qualified = new MySqlMessageProvider(new MySqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = $"{database}.{TABLE_NAME}",
        });
        (await qualified.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello", "schema-qualified table with backtick quoting");
    }

    /// <summary>
    /// Verifies that postgre sql provider runtime lookup against real postgre sql.
    /// </summary>
    [TestMethod]
    public async Task PostgreSqlProvider_RuntimeLookup_AgainstRealPostgreSqlAsync() {
        var baseConnection = RequireEnvironment(POSTGRESQL_CONNECTION_ENV);
        const string database = "jeninnet_localization_runtime_tests";

        // Isolated database + canonical schema, created and seeded directly as test setup.
        await using(var setup = new NpgsqlConnection(baseConnection)) {
            if(await TryOpenAsync(setup) is { } openError) {
                Assert.Inconclusive($"the database engine runtime environment unavailable ({SanitizeDiagnostic(openError)})");
            }

            await ExecuteAsync(setup, $"SELECT 1 FROM pg_database WHERE datname = '{database}'", TestContext.CancellationToken);
            var databaseExists = false;
            await using(var command = setup.CreateCommand()) {
                command.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{database}'";
                databaseExists = await command.ExecuteScalarAsync(TestContext.CancellationToken) is not null;
            }

            if(!databaseExists) {
                await ExecuteAsync(setup, $"CREATE DATABASE \"{database}\"", TestContext.CancellationToken);
            }
        }

        var connectionString = WithDatabase(baseConnection, database);
        await using(var setup = new NpgsqlConnection(connectionString)) {
            if(await TryOpenAsync(setup) is { } openError) {
                Assert.Inconclusive($"the database engine runtime environment unavailable ({SanitizeDiagnostic(openError)})");
            }

            await ExecuteAsync(setup, $"""
                CREATE TABLE IF NOT EXISTS public."{TABLE_NAME}" (
                    "MessageKey" VARCHAR(255) NOT NULL,
                    "Culture" VARCHAR(64) NOT NULL,
                    "Template" TEXT NOT NULL,
                    PRIMARY KEY ("MessageKey", "Culture")
                )
                """, TestContext.CancellationToken);
            await ExecuteAsync(setup, $"DELETE FROM public.\"{TABLE_NAME}\"", TestContext.CancellationToken);
            await SeedAsync(setup, "public", "greeting", "en", "Hello", "\"", TestContext.CancellationToken);
            await SeedAsync(setup, "public", "greeting", "nl", "Hallo", "\"", TestContext.CancellationToken);
            await SeedAsync(setup, "public", "greet'ing\"; --", "en", "Param safe", "\"", TestContext.CancellationToken);
        }

        var provider = new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = $"public.{TABLE_NAME}",
        });

        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello", "exact lookup, seeded culture");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("nl"), CancellationToken.None)).Should().Be("Hallo", "culture separation");
        (await provider.GetMessageTemplateAsync("nonexistent", new CultureInfo("en"), CancellationToken.None)).Should().BeNull("missing key");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("de"), CancellationToken.None)).Should().BeNull("missing culture");
        (await provider.GetMessageTemplateAsync("greet'ing\"; --", new CultureInfo("en"), CancellationToken.None)).Should().Be("Param safe", "key supplied as a parameter");

        var qualified = new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = $"{database}.public.{TABLE_NAME}",
        });
        (await qualified.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello", "database-qualified table with double-quote quoting");
    }

    /// <summary>
    /// Verifies that sql server provider runtime lookup against real sql server.
    /// </summary>
    [TestMethod]
    public async Task SqlServerProvider_RuntimeLookup_AgainstRealSqlServerAsync() {
        var connectionString = RequireEnvironment(SQLSERVER_CONNECTION_ENV);

        // Canonical schema created idempotently inside the provided test database and seeded
        // directly as test setup; a dedicated database is intentionally not required because a
        // SQL-authentication login granted only dbcreator cannot open a database owned by another
        // login (the R29 tests must run under the same minimal permissions as the production user).
        await using(var setup = new SqlConnection(connectionString)) {
            if(await TryOpenAsync(setup) is { } openError) {
                Assert.Inconclusive($"the database engine runtime environment unavailable ({SanitizeDiagnostic(openError)})");
            }

            await ExecuteAsync(setup, $"""
                IF OBJECT_ID(N'dbo.{TABLE_NAME}', N'U') IS NULL
                CREATE TABLE dbo.{TABLE_NAME} (
                    MessageKey NVARCHAR(255) NOT NULL,
                    Culture NVARCHAR(64) NOT NULL,
                    Template NVARCHAR(4000) NOT NULL,
                    CONSTRAINT PK_{TABLE_NAME} PRIMARY KEY (MessageKey, Culture)
                )
                """, TestContext.CancellationToken);
            await ExecuteAsync(setup, $"DELETE FROM dbo.{TABLE_NAME}", TestContext.CancellationToken);
            await SeedAsync(setup, "dbo", "greeting", "en", "Hello", null, TestContext.CancellationToken);
            await SeedAsync(setup, "dbo", "greeting", "nl", "Hallo", null, TestContext.CancellationToken);
            await SeedAsync(setup, "dbo", "greet'ing\"; --", "en", "Param safe", null, TestContext.CancellationToken);
        }

        var provider = new SqlServerMessageProvider(new SqlServerLocalizationOptions {
            ConnectionString = connectionString,
            TableName = $"dbo.{TABLE_NAME}",
        });

        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello", "exact lookup, seeded culture");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("nl"), CancellationToken.None)).Should().Be("Hallo", "culture separation");
        (await provider.GetMessageTemplateAsync("nonexistent", new CultureInfo("en"), CancellationToken.None)).Should().BeNull("missing key");
        (await provider.GetMessageTemplateAsync("greeting", new CultureInfo("de"), CancellationToken.None)).Should().BeNull("missing culture");
        (await provider.GetMessageTemplateAsync("greet'ing\"; --", new CultureInfo("en"), CancellationToken.None)).Should().Be("Param safe", "key supplied as a parameter");
    }

    /// <summary>
    /// Attempts to open.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>A task whose result is open.</returns>
    private async Task<Exception?> TryOpenAsync(DbConnection connection) {
        try {
            await connection.OpenAsync(TestContext.CancellationToken);
            return null;
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception exception) {
            return exception;
        }
    }

    /// <summary>
    /// Performs the sanitize diagnostic operation.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The sanitize diagnostic text.</returns>
    private static string SanitizeDiagnostic(Exception exception) {
        var message = exception.Message;
        var passwordMarker = message.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
        if(passwordMarker >= 0) {
            var end = message.IndexOf(';', passwordMarker);
            message = (end >= 0 ? message.Remove(passwordMarker, end - passwordMarker + 1) : message[..passwordMarker]) + "<redacted>";
        }

        return message.Length > 160 ? message[..160] + "..." : message;
    }

    /// <summary>
    /// Performs the require environment operation.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The require environment text.</returns>
    private static string RequireEnvironment(string name) {
        var value = Environment.GetEnvironmentVariable(name);
        if(string.IsNullOrWhiteSpace(value)) {
            Assert.Inconclusive($"set {name} to a connection string to run the real-engine runtime test");
        }

        return value;
    }

    /// <summary>
    /// Returns a copy with database.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="database">The database.</param>
    /// <returns>The with database text.</returns>
    private static string WithDatabase(string connectionString, string database) =>
        DatabaseKeyPattern().IsMatch(connectionString)
            ? DatabaseKeyPattern().Replace(connectionString, $"Database={database}", 1)
            : connectionString.TrimEnd(';') + $";Database={database}";

    /// <summary>
    /// Performs the execute operation.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="commandText">The command text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private static async Task ExecuteAsync(DbConnection connection, string commandText, CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts one localized row into the vendor's table using vendor-specific identifier quoting.
    /// </summary>
    /// <param name="connection">The open connection to the sample database.</param>
    /// <param name="schema">The schema that qualifies the table, or an empty string when not applicable.</param>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="template">The message template.</param>
    /// <param name="identifierQuote">The identifier-quote character for the vendor, or <see langword="null"/> when quoting is not used.</param>
    /// <param name="cancellationToken">A token to cancel the insert.</param>
    /// <returns>A task that completes when the row has been inserted.</returns>
    private static async Task SeedAsync(
        DbConnection connection,
        string schema,
        string key,
        string culture,
        string template,
        string? identifierQuote,
        CancellationToken cancellationToken
    ) {
        await using var command = connection.CreateCommand();
        var tableReference = identifierQuote is null
            ? $"{schema}.{TABLE_NAME}"
            : $"{identifierQuote}{schema}{identifierQuote}.{identifierQuote}{TABLE_NAME}{identifierQuote}";
        var quoted = identifierQuote is null
            ? (name: "MessageKey", culture: "Culture", template: "Template")
            : (name: identifierQuote + "MessageKey" + identifierQuote, culture: identifierQuote + "Culture" + identifierQuote, template: identifierQuote + "Template" + identifierQuote);
        command.CommandText = $"INSERT INTO {tableReference} ({quoted.name}, {quoted.culture}, {quoted.template}) VALUES (@MessageKey, @Culture, @Template)";
        var keyParameter = command.CreateParameter();
        keyParameter.ParameterName = "@MessageKey";
        keyParameter.Value = key;
        command.Parameters.Add(keyParameter);
        var cultureParameter = command.CreateParameter();
        cultureParameter.ParameterName = "@Culture";
        cultureParameter.Value = culture;
        command.Parameters.Add(cultureParameter);
        var templateParameter = command.CreateParameter();
        templateParameter.ParameterName = "@Template";
        templateParameter.Value = template;
        command.Parameters.Add(templateParameter);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs the database key pattern operation.
    /// </summary>
    /// <returns>The database key pattern result.</returns>
    [GeneratedRegex(@"Database=[^;]*", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseKeyPattern();

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
