//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the database sql construction tests.
/// </summary>
[TestClass]
public class DatabaseSqlConstructionTests {
    /// <summary>
    /// Verifies that the MySQL provider correctly escapes SQL identifiers using backticks.
    /// </summary>
    [TestMethod]
    public void MySqlProvider_EscapesIdentifiersCorrectly() {
        var provider = new MySqlMessageProvider(new MySqlLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "Messages" });
        var escaped = InvokeEscapeIdentifier(provider, "TableName");

        escaped.Should().Be("`TableName`");
    }

    /// <summary>
    /// Verifies that the PostgreSQL provider correctly escapes SQL identifiers using double quotes.
    /// </summary>
    [TestMethod]
    public void PostgreSqlProvider_EscapesIdentifiersCorrectly() {
        var provider = new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions { ConnectionString = "Host=localhost;Database=db;", TableName = "Messages" });
        var escaped = InvokeEscapeIdentifier(provider, "TableName");

        escaped.Should().Be("\"TableName\"");
    }

    /// <summary>
    /// Verifies that the SQLite provider correctly escapes SQL identifiers using double quotes.
    /// </summary>
    [TestMethod]
    public void SqliteProvider_EscapesIdentifiersCorrectly() {
        var provider = new SqliteMessageProvider(new SqliteLocalizationOptions { ConnectionString = "Data Source=test.db;", TableName = "Messages" });
        var escaped = InvokeEscapeIdentifier(provider, "TableName");

        escaped.Should().Be("\"TableName\"");
    }

    /// <summary>
    /// Verifies that the Microsoft SQL Server provider correctly escapes SQL identifiers using square brackets.
    /// </summary>
    [TestMethod]
    public void MsSqlProvider_EscapesIdentifiersCorrectly() {
        var provider = new SqlServerMessageProvider(new SqlServerLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "Messages" });
        var escaped = InvokeEscapeIdentifier(provider, "TableName");

        escaped.Should().Be("[TableName]");
    }

    /// <summary>
    /// Verifies that each provider accepts a schema-qualified (dotted) table name without the
    /// base constructor rejecting it (consistency with DatabaseLocalizationOptions validation).
    /// </summary>
    [TestMethod]
    public void Provider_AcceptsSchemaQualifiedTableName() {
        // If any constructor rejects a schema-qualified name, the instantiation throws before the
        // assertion is reached and the test fails.
        GC.KeepAlive(new SqlServerMessageProvider(new SqlServerLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "dbo.Messages" }));
        GC.KeepAlive(new MySqlMessageProvider(new MySqlLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "dbo.Messages" }));
        GC.KeepAlive(new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions { ConnectionString = "Host=localhost;Database=db;", TableName = "public.Messages" }));
        GC.KeepAlive(new SqliteMessageProvider(new SqliteLocalizationOptions { ConnectionString = "Data Source=test.db;", TableName = "main.Messages" }));

        GC.KeepAlive(0);

        true.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that schema-qualified identifiers are escaped per segment rather than as a single
    /// identifier (e.g. [dbo].[Messages], not [dbo.Messages]).
    /// </summary>
    [TestMethod]
    public void Provider_EscapesSchemaQualifiedIdentifierPerSegment() {
        var msSql = new SqlServerMessageProvider(new SqlServerLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "Messages" });
        var mySql = new MySqlMessageProvider(new MySqlLocalizationOptions { ConnectionString = "Server=localhost;Database=db;", TableName = "Messages" });
        var postgreSql = new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions { ConnectionString = "Host=localhost;Database=db;", TableName = "Messages" });
        var sqlite = new SqliteMessageProvider(new SqliteLocalizationOptions { ConnectionString = "Data Source=test.db;", TableName = "Messages" });

        InvokeEscapeQualifiedIdentifier(msSql, "dbo.Messages").Should().Be("[dbo].[Messages]");
        InvokeEscapeQualifiedIdentifier(mySql, "dbo.Messages").Should().Be("`dbo`.`Messages`");
        InvokeEscapeQualifiedIdentifier(postgreSql, "public.Messages").Should().Be("\"public\".\"Messages\"");
        InvokeEscapeQualifiedIdentifier(sqlite, "main.Messages").Should().Be("\"main\".\"Messages\"");
    }

    /// <summary>
    /// Invokes the provider's non-public EscapeIdentifier method for testing purposes.
    /// </summary>
    /// <param name="provider">The database provider instance.</param>
    /// <param name="identifier">The SQL identifier to escape.</param>
    /// <returns>The escaped SQL identifier.</returns>
    private static string InvokeEscapeIdentifier(object provider, string identifier) {
        var method = provider.GetType().GetMethod("EscapeIdentifier", BindingFlags.NonPublic | BindingFlags.Instance);
        return (string)method!.Invoke(provider, [identifier])!;
    }

    /// <summary>
    /// Invokes the base type's non-public EscapeQualifiedIdentifier method via the provider instance.
    /// </summary>
    /// <param name="provider">The database provider instance.</param>
    /// <param name="identifier">The (possibly dotted) SQL identifier to escape.</param>
    /// <returns>The per-segment escaped SQL identifier.</returns>
    private static string InvokeEscapeQualifiedIdentifier(object provider, string identifier) {
        var method = provider.GetType().BaseType!.GetMethod("EscapeQualifiedIdentifier", BindingFlags.NonPublic | BindingFlags.Instance);
        return (string)method!.Invoke(provider, [identifier])!;
    }
}
