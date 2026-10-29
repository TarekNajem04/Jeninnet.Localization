//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

#pragma warning disable S3881 // Test class uses simple IDisposable pattern
/// <summary>
/// Contains the database provider contract tests.
/// </summary>
[TestClass]
public class DatabaseProviderContractTests : IDisposable {
#pragma warning restore S3881
    private readonly string _connectionString;
    private const string TABLE_NAME = "LocalizationMessages";
    private readonly SqliteConnection _keeper;

    /// <summary>
    /// Initializes a new instance of the DatabaseProviderContractTests class.
    /// </summary>
    public DatabaseProviderContractTests() {
        _connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(_connectionString);
        _keeper.Open();
        using var cmd = _keeper.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS [{TABLE_NAME}] (
                [MessageKey] TEXT NOT NULL,
                [Culture] TEXT NOT NULL,
                [Template] TEXT NOT NULL,
                PRIMARY KEY ([MessageKey], [Culture])
            )
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Releases the resources used by the current instance.
    /// </summary>
    public void Dispose() {
        _keeper?.Close();
        _keeper?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the test initialize operation.
    /// </summary>
    [TestInitialize]
    public void TestInitialize() {
        using var cmd = _keeper.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{TABLE_NAME}]";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <returns>The provider result.</returns>
    private SqliteMessageProvider CreateProvider(string? tableName = null) {
        var options = new SqliteLocalizationOptions {
            ConnectionString = _connectionString,
            TableName = tableName ?? TABLE_NAME
        };
        return new SqliteMessageProvider(options);
    }

    /// <summary>
    /// Performs the seed data operation.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="template">The message template.</param>
    private void SeedData(string key, string culture, string template) {
        using var cmd = _keeper.CreateCommand();
        cmd.CommandText = $"INSERT INTO [{TABLE_NAME}] ([MessageKey], [Culture], [Template]) VALUES (@MessageKey, @Culture, @Template)";
        cmd.Parameters.AddWithValue("@MessageKey", key);
        cmd.Parameters.AddWithValue("@Culture", culture);
        cmd.Parameters.AddWithValue("@Template", template);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Verifies that exact lookup returns template.
    /// </summary>
    [TestMethod]
    public async Task ExactLookup_ReturnsTemplateAsync() {
        SeedData("greeting", "en", "Hello");

        var result = await CreateProvider().GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that missing key returns null.
    /// </summary>
    [TestMethod]
    public async Task MissingKey_ReturnsNullAsync() {
        SeedData("greeting", "en", "Hello");

        var result = await CreateProvider().GetMessageTemplateAsync("nonexistent", new CultureInfo("en"), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that missing culture returns null.
    /// </summary>
    [TestMethod]
    public async Task MissingCulture_ReturnsNullAsync() {
        SeedData("greeting", "en", "Hello");

        var result = await CreateProvider().GetMessageTemplateAsync("greeting", new CultureInfo("de"), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that no fallback behavior returns null when only different culture exists.
    /// </summary>
    [TestMethod]
    public async Task NoFallbackBehavior_ReturnsNull_WhenOnlyDifferentCultureExistsAsync() {
        SeedData("greeting", "en", "Hello");

        var result = await CreateProvider().GetMessageTemplateAsync("greeting", new CultureInfo("en-US"), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that culture separation each culture returns own template.
    /// </summary>
    [TestMethod]
    public async Task CultureSeparation_EachCultureReturnsOwnTemplateAsync() {
        SeedData("Greeting", "en-US", "Hello");
        SeedData("Greeting", "nl-NL", "Hallo");

        var en = await CreateProvider().GetMessageTemplateAsync("Greeting", new CultureInfo("en-US"), CancellationToken.None);
        var nl = await CreateProvider().GetMessageTemplateAsync("Greeting", new CultureInfo("nl-NL"), CancellationToken.None);

        en.Should().Be("Hello");
        nl.Should().Be("Hallo");
    }

    /// <summary>
    /// Verifies that schema qualified table lookup returns template.
    /// </summary>
    [TestMethod]
    public async Task SchemaQualifiedTable_Lookup_ReturnsTemplateAsync() {
        SeedData("greeting", "en", "Hello");

        var result = await CreateProvider(tableName: "main.LocalizationMessages")
            .GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that parameterization template with sql significant characters is returned verbatim.
    /// </summary>
    [TestMethod]
    public async Task Parameterization_TemplateWithSqlSignificantCharacters_IsReturnedVerbatimAsync() {
        const string template = "Hello 'World'; DROP TABLE LocalizationMessages; --";
        SeedData("greeting", "en", template);

        var result = await CreateProvider().GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None);

        result.Should().Be(template);
    }

    /// <summary>
    /// Verifies that options mutation after construction does not affect provider behavior.
    /// </summary>
    [TestMethod]
    public async Task OptionsMutationAfterConstruction_DoesNotAffectProviderBehaviorAsync() {
        SeedData("greeting", "en", "Hello");

        var options = new SqliteLocalizationOptions {
            ConnectionString = _connectionString,
            TableName = TABLE_NAME
        };
        var provider = new SqliteMessageProvider(options);

        options.ConnectionString = "Data Source=does-not-exist-after-construction.db";
        options.TableName = "MutatedTable";

        var result = await provider.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Hello", "the provider snapshots the connection string and table name at construction");
    }

    /// <summary>
    /// Verifies that parameterization key with sql significant characters matches exactly.
    /// </summary>
    [TestMethod]
    public async Task Parameterization_KeyWithSqlSignificantCharacters_MatchesExactlyAsync() {
        const string key = "greet'ing\"; --";
        SeedData(key, "en", "Hello");

        var result = await CreateProvider().GetMessageTemplateAsync(key, new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that pre cancelled token throws operation canceled exception without querying.
    /// </summary>
    [TestMethod]
    public async Task PreCancelledToken_ThrowsOperationCanceledException_WithoutQueryingAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await CreateProvider().GetMessageTemplateAsync("greeting", new CultureInfo("en"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>("the canonical database lookup observes cancellation before creating a connection, so a cancelled request never becomes a database round-trip or a missing-resource result");
    }

    /// <summary>
    /// Verifies that connection failure propagates exception instead of returning null.
    /// </summary>
    [TestMethod]
    public async Task ConnectionFailure_PropagatesException_InsteadOfReturningNullAsync() {
        // A connection string whose directory does not exist fails deterministically at OpenAsync.
        var provider = new SqliteMessageProvider(new SqliteLocalizationOptions {
            ConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "jeninnet-no-such-" + Guid.NewGuid().ToString("N"), "localization.sqlite")};"
        });

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("greeting", new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<SqliteException>("a database engine failure must propagate to the caller; it must not be conflated with a missing message (null)");
    }
}
