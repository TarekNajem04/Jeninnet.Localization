//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Proves that the sample SQLite database mirrors the canonical reference content exactly, that the
/// real production SQLite provider reads it with exact-culture semantics (no provider-owned parent
/// fallback), and that the sample-owned initialization is deterministic and resettable.
/// </summary>
[TestClass]
public sealed class SqliteContentParityTests {
    /// <summary>
    /// Verifies that sqlite provider resolves every canonical key exactly per culture.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_ResolvesEveryCanonicalKeyExactlyPerCultureAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var provider = CreateProvider(ReferenceSqliteDatabase.EnsureInitialized(temp.Path));

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            var culture = new CultureInfo(cultureCode);
            foreach(var key in CanonicalKeys()) {
                var expected = LocalizationContent.TryGetString(cultureCode, key, out var value) ? value : null;
                var actual = await provider.GetMessageTemplateAsync(key, culture, TestContext.CancellationToken);

                Assert.AreEqual(expected, actual, $"Culture '{cultureCode}', key '{key}'.");
            }
        }
    }

    /// <summary>
    /// Verifies that sqlite provider preserves the intentional partial culture semantics.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_PreservesTheIntentionalPartialCultureSemanticsAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var provider = CreateProvider(ReferenceSqliteDatabase.EnsureInitialized(temp.Path));
        var defaultCulture = new CultureInfo(ReferenceCultures.DEFAULT_CULTURE);
        var arabic = new CultureInfo("ar");
        var regional = new CultureInfo("ar-SY");

        Assert.IsNotNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, arabic, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_REGIONAL_NOTE}' must exist in 'ar'.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, regional, TestContext.CancellationToken),
            $"The 'ar-SY' seed intentionally omits '{ReferenceKeys.DEMO_REGIONAL_NOTE}'; the provider must miss exactly so the pipeline can parent-fall back to 'ar'.");

        Assert.IsNotNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, defaultCulture, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' must exist in the default culture.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, arabic, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' exists only in the default culture.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, regional, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' exists only in the default culture.");

        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_MISSING_EVERYWHERE, arabic, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_MISSING_EVERYWHERE}' is present in no culture.");
    }

    /// <summary>
    /// Verifies that sqlite provider does not fall back to parent culture.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_DoesNotFallBackToParentCultureAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var provider = CreateProvider(ReferenceSqliteDatabase.EnsureInitialized(temp.Path));

        var result = await provider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, new CultureInfo("ar-IQ"), TestContext.CancellationToken);

        Assert.IsNull(result, "The SQLite provider resolves only the exact requested culture; parent fallback belongs to the canonical pipeline.");
    }

    /// <summary>
    /// Verifies that sqlite provider returns regional values without alteration.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_ReturnsRegionalValuesWithoutAlterationAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var provider = CreateProvider(ReferenceSqliteDatabase.EnsureInitialized(temp.Path));

        var regionalDescription = await provider.GetMessageTemplateAsync(
            ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION,
            new CultureInfo("ar-SY"), TestContext.CancellationToken);

        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION],
            regionalDescription,
            "The stored template must be returned verbatim — never pre-formatted or altered.");
    }

    /// <summary>
    /// Verifies that sqlite provider sql significant content is returned verbatim.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_SqlSignificantContent_IsReturnedVerbatimAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var connectionString = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);
        const string sqlSignificantKey = "greet'ing\"; --";
        const string sqlSignificantTemplate = "Hello 'World'; DROP TABLE LocalizationMessages; --";

        await using(var connection = new SqliteConnection(connectionString)) {
            await connection.OpenAsync(TestContext.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LocalizationMessages (MessageKey, Culture, Template)
                VALUES (@MessageKey, @Culture, @Template)
                """;
            command.Parameters.AddWithValue("@MessageKey", sqlSignificantKey);
            command.Parameters.AddWithValue("@Culture", "en");
            command.Parameters.AddWithValue("@Template", sqlSignificantTemplate);
            await command.ExecuteNonQueryAsync(TestContext.CancellationToken);
        }

        var provider = CreateProvider(connectionString);
        var actual = await provider.GetMessageTemplateAsync(sqlSignificantKey, new CultureInfo("en"), TestContext.CancellationToken);

        Assert.AreEqual(sqlSignificantTemplate, actual, "The production provider must parameterize keys and return values verbatim.");
    }

    /// <summary>
    /// Verifies that deterministic initialization starts clean and seeds exactly the canonical row set.
    /// </summary>
    [TestMethod]
    public void DeterministicInitialization_StartsCleanAndSeedsExactlyTheCanonicalRowSet() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var databasePath = ReferenceSqliteDatabase.GetDatabasePath(temp.Path);
        Assert.IsFalse(File.Exists(databasePath), "A clean database run must begin with no pre-existing sample database.");

        var connectionString = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);

        Assert.IsTrue(File.Exists(databasePath), "Startup initialization must create the sample database file.");

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LocalizationMessages";
        var rowCount = (long)command.ExecuteScalar()!;

        var expectedRowCount = ReferenceCultures.SupportedCultures.Sum(static culture => LocalizationContent.GetTemplates(culture).Count);
        Assert.AreEqual(expectedRowCount, rowCount, "The seed must contain exactly the canonical per-culture rows.");
    }

    /// <summary>
    /// Verifies that repeated initialization is idempotent and refreshes to canonical content.
    /// </summary>
    [TestMethod]
    public void RepeatedInitialization_IsIdempotentAndRefreshesToCanonicalContent() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var connectionString = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);

        using(var connection = new SqliteConnection(connectionString)) {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LocalizationMessages (MessageKey, Culture, Template)
                VALUES ('stale.key', 'en', 'stale')
                """;
            command.ExecuteNonQuery();
        }

        _ = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);

        using var after = new SqliteConnection(connectionString);
        after.Open();
        using var staleCheck = after.CreateCommand();
        staleCheck.CommandText = "SELECT COUNT(*) FROM LocalizationMessages WHERE MessageKey = 'stale.key'";
        Assert.AreEqual(0L, (long)staleCheck.ExecuteScalar()!, "Re-initialization must refresh the rows so stale content cannot silently alter the sample's behavior.");
    }

    /// <summary>
    /// Verifies that reset deletes the database and reinitialization recreates equivalent content.
    /// </summary>
    [TestMethod]
    public async Task Reset_DeletesTheDatabase_AndReinitializationRecreatesEquivalentContentAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        _ = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);

        ReferenceSqliteDatabase.Reset(temp.Path);

        Assert.IsFalse(File.Exists(ReferenceSqliteDatabase.GetDatabasePath(temp.Path)), "Reset must delete the database file.");

        var recreated = ReferenceSqliteDatabase.EnsureInitialized(temp.Path);
        var provider = CreateProvider(recreated);

        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await provider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, new CultureInfo("ar"), TestContext.CancellationToken));
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The provider result.</returns>
    private static SqliteMessageProvider CreateProvider(string connectionString) =>
        new(new SqliteLocalizationOptions {
            ConnectionString = connectionString,
            TableName = ReferenceSqliteDatabase.TABLE_NAME
        });

    /// <summary>
    /// Determines whether the value canonical keys.
    /// </summary>
    /// <returns>The canonical keys sequence.</returns>
    private static IEnumerable<string> CanonicalKeys() =>
        LocalizationContent.All.Values
            .SelectMany(static templates => templates.Keys)
            .Distinct(StringComparer.Ordinal)
            .Append(ReferenceKeys.DEMO_MISSING_EVERYWHERE);

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
