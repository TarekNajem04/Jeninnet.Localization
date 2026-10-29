//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Real-engine parity for the server-backed reference providers. Each test requires a connection
/// string supplied through the environment variable established by the repository database runtime
/// tests; when the variable is unset the test is inconclusive so ordinary CI stays green without any
/// server. The sample-owned database is created and seeded by <see cref="ReferenceServerDatabase"/>.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ServerDatabaseParityTests {
    private const string SQL_SERVER_ENVIRONMENT = "JENINNET_TEST_SQLSERVER_CONNECTION";
    private const string POSTGRE_SQL_ENVIRONMENT = "JENINNET_TEST_POSTGRESQL_CONNECTION";
    private const string MY_SQL_ENVIRONMENT = "JENINNET_TEST_MYSQL_CONNECTION";
    private const string UNKNOWN_VENDOR_MESSAGE = "Unknown reference server vendor.";

    /// <summary>
    /// Verifies that sql server provider matches canonical content.
    /// </summary>
    [TestMethod]
    public Task SqlServerProvider_MatchesCanonicalContent() =>
        VerifyVendorParityAsync(ReferenceServerVendor.SqlServer, SQL_SERVER_ENVIRONMENT);

    /// <summary>
    /// Verifies that postgre sql provider matches canonical content.
    /// </summary>
    [TestMethod]
    public Task PostgreSqlProvider_MatchesCanonicalContent() =>
        VerifyVendorParityAsync(ReferenceServerVendor.PostgreSql, POSTGRE_SQL_ENVIRONMENT);

    /// <summary>
    /// Verifies that my sql provider matches canonical content.
    /// </summary>
    [TestMethod]
    public Task MySqlProvider_MatchesCanonicalContent() =>
        VerifyVendorParityAsync(ReferenceServerVendor.MySql, MY_SQL_ENVIRONMENT);

    /// <summary>
    /// Verifies that the vendor's production provider resolves every canonical key exactly per culture.
    /// </summary>
    /// <param name="vendor">The reference server vendor under test.</param>
    /// <param name="environmentVariable">The environment variable that supplies the admin connection string.</param>
    /// <returns>A task that completes when every canonical lookup has been asserted.</returns>
    private static async Task VerifyVendorParityAsync(ReferenceServerVendor vendor, string environmentVariable) {
        var baseConnection = Environment.GetEnvironmentVariable(environmentVariable);
        if(string.IsNullOrWhiteSpace(baseConnection)) {
            Assert.Inconclusive($"set {environmentVariable} to a connection string to run the real {vendor} reference parity test");
        }

        var connectionString = ReferenceServerDatabase.EnsureInitialized(vendor, baseConnection);
        var provider = CreateProvider(vendor, connectionString);

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            var culture = new CultureInfo(cultureCode);
            foreach(var key in CanonicalKeys()) {
                var expected = LocalizationContent.TryGetString(cultureCode, key, out var value) ? value : null;
                var actual = await provider.GetMessageTemplateAsync(key, culture);

                Assert.AreEqual(expected, actual, $"Vendor '{vendor}', culture '{cultureCode}', key '{key}'.");
            }
        }

        var arabic = new CultureInfo("ar");
        var regional = new CultureInfo("ar-SY");

        Assert.IsNotNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, arabic),
            $"Vendor '{vendor}': '{ReferenceKeys.DEMO_REGIONAL_NOTE}' must exist in 'ar'.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, regional),
            $"Vendor '{vendor}': the 'ar-SY' seed intentionally omits '{ReferenceKeys.DEMO_REGIONAL_NOTE}'.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, new CultureInfo("ar-IQ")),
            $"Vendor '{vendor}': the provider resolves only the exact requested culture; parent fallback belongs to Core.");

        // SQL-significant key and value inserted raw, then read verbatim through the production provider.
        const string sqlSignificantKey = "greet'ing\"; --";
        const string sqlSignificantTemplate = "Hello 'World'; DROP TABLE LocalizationMessages; --";
        InsertRaw(vendor, connectionString, sqlSignificantKey, sqlSignificantTemplate);
        Assert.AreEqual(
            sqlSignificantTemplate,
            await provider.GetMessageTemplateAsync(sqlSignificantKey, new CultureInfo("en")),
            $"Vendor '{vendor}': the production provider must parameterize keys and return values verbatim.");

        await VerifyCompositionAsync(vendor, baseConnection);

        ReferenceServerDatabase.Reset(vendor, baseConnection);
        var recreated = ReferenceServerDatabase.EnsureInitialized(vendor, baseConnection);
        var recreatedProvider = CreateProvider(vendor, recreated);
        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await recreatedProvider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, arabic),
            $"Vendor '{vendor}': re-initialization after reset must recreate equivalent canonical content.");
    }

    /// <summary>
    /// Verifies composition.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="baseConnection">The base connection.</param>
    private static async Task VerifyCompositionAsync(ReferenceServerVendor vendor, string baseConnection) {
        var services = new ServiceCollection();
        services.AddReferenceLocalizationProvider(options => {
            options.Provider = ProviderName(vendor);
            ConnectionOptions(options, vendor).ConnectionString = baseConnection;
        });

        await using var provider = services.BuildServiceProvider();
        var innerProviders = provider.GetServices<IInnerMessageProvider>().ToArray();
        Assert.HasCount(1, innerProviders, $"Vendor '{vendor}' composition must register exactly one inner provider.");
        Assert.AreEqual(
            ExpectedProviderType(vendor),
            innerProviders[0].GetType(),
            $"Vendor '{vendor}' composition must register the matching production provider.");

        await AssertFormattedMessageAsync(provider, vendor);
    }

    /// <summary>
    /// Asserts formatted message.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="vendor">The vendor.</param>
    private static async Task AssertFormattedMessageAsync(ServiceProvider services, ReferenceServerVendor vendor) {
        var originalCulture = CultureInfo.CurrentCulture;

        try {
            // The canonical formatter substitutes values under the current culture, so pin it for a
            // deterministic expectation on any developer machine.
            CultureInfo.CurrentCulture = new CultureInfo(ReferenceCultures.DEFAULT_CULTURE);

            var localizer = services.GetRequiredService<IMessageLocalizer>();
            var orderMessage = await localizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                new Dictionary<string, object?> { ["0"] = "ORD-0001", ["1"] = 2, ["2"] = 39.98m },
                new CultureInfo(ReferenceCultures.DEFAULT_CULTURE));

            var expected = string.Format(
                CultureInfo.GetCultureInfo(ReferenceCultures.DEFAULT_CULTURE),
                LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE)[ReferenceKeys.ORDER_PLACED_MESSAGE],
                "ORD-0001",
                2,
                39.98m);
            Assert.AreEqual(expected, orderMessage, $"Vendor '{vendor}' must feed the stored template unchanged into the canonical formatter.");
        }
        finally {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>
    /// Inserts raw.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="key">The message key.</param>
    /// <param name="template">The message template.</param>
    private static void InsertRaw(ReferenceServerVendor vendor, string connectionString, string key, string template) {
        using var connection = CreateConnection(vendor, connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = InsertCommand(vendor);
        command.Parameters.Add(CreateParameter(command, "@MessageKey", key));
        command.Parameters.Add(CreateParameter(command, "@Culture", "en"));
        command.Parameters.Add(CreateParameter(command, "@Template", template));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates parameter.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The parameter result.</returns>
    private static DbParameter CreateParameter(DbCommand command, string name, string value) {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    /// <summary>
    /// Creates connection.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection result.</returns>
    private static DbConnection CreateConnection(ReferenceServerVendor vendor, string connectionString) => vendor switch {
        ReferenceServerVendor.SqlServer => new SqlConnection(connectionString),
        ReferenceServerVendor.PostgreSql => new NpgsqlConnection(connectionString),
        ReferenceServerVendor.MySql => new MySqlConnection(connectionString),
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Creates the production database provider for the selected vendor and reference table.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <param name="connectionString">The initialized sample-database connection string.</param>
    /// <returns>The vendor-specific message provider used by the parity assertions.</returns>
    private static IMessageProvider CreateProvider(ReferenceServerVendor vendor, string connectionString) => vendor switch {
        ReferenceServerVendor.SqlServer => new SqlServerMessageProvider(new SqlServerLocalizationOptions {
            ConnectionString = connectionString,
            TableName = ReferenceServerDatabase.TABLE_NAME
        }),
        ReferenceServerVendor.PostgreSql => new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = ReferenceServerDatabase.TABLE_NAME
        }),
        ReferenceServerVendor.MySql => new MySqlMessageProvider(new MySqlLocalizationOptions {
            ConnectionString = connectionString,
            TableName = ReferenceServerDatabase.TABLE_NAME
        }),
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Performs the expected provider type operation.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <returns>The expected provider type result.</returns>
    private static Type ExpectedProviderType(ReferenceServerVendor vendor) => vendor switch {
        ReferenceServerVendor.SqlServer => typeof(SqlServerMessageProvider),
        ReferenceServerVendor.PostgreSql => typeof(PostgreSqlMessageProvider),
        ReferenceServerVendor.MySql => typeof(MySqlMessageProvider),
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Performs the connection options operation.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <param name="vendor">The vendor.</param>
    /// <returns>The connection options result.</returns>
    private static ReferenceDatabaseOptions ConnectionOptions(ReferenceLocalizationOptions options, ReferenceServerVendor vendor) => vendor switch {
        ReferenceServerVendor.SqlServer => options.SqlServer,
        ReferenceServerVendor.PostgreSql => options.PostgreSql,
        ReferenceServerVendor.MySql => options.MySql,
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Performs the provider name operation.
    /// </summary>
    /// <param name="vendor">The vendor.</param>
    /// <returns>The provider name text.</returns>
    private static string ProviderName(ReferenceServerVendor vendor) => vendor switch {
        ReferenceServerVendor.SqlServer => ReferenceProviderNames.SQL_SERVER,
        ReferenceServerVendor.PostgreSql => ReferenceProviderNames.POSTGRE_SQL,
        ReferenceServerVendor.MySql => ReferenceProviderNames.MY_SQL,
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Builds the vendor-specific parameterized INSERT statement used to seed SQL-significant parity rows.
    /// </summary>
    /// <param name="vendor">The reference server vendor.</param>
    /// <returns>The INSERT statement using the canonical parameter names.</returns>
    private static string InsertCommand(ReferenceServerVendor vendor) => vendor switch {
        ReferenceServerVendor.SqlServer => "INSERT INTO [LocalizationMessages] ([MessageKey], [Culture], [Template]) VALUES (@MessageKey, @Culture, @Template)",
        ReferenceServerVendor.PostgreSql => "INSERT INTO \"LocalizationMessages\" (\"MessageKey\", \"Culture\", \"Template\") VALUES (@MessageKey, @Culture, @Template)",
        ReferenceServerVendor.MySql => "INSERT INTO `LocalizationMessages` (`MessageKey`, `Culture`, `Template`) VALUES (@MessageKey, @Culture, @Template)",
        _ => throw UnsupportedVendor(vendor),
    };

    /// <summary>
    /// Creates the failure raised when a switch expression receives an unsupported vendor.
    /// </summary>
    /// <param name="vendor">The unsupported vendor.</param>
    /// <returns>The exception to throw for <paramref name="vendor"/>.</returns>
    private static ArgumentOutOfRangeException UnsupportedVendor(ReferenceServerVendor vendor) =>
        new(nameof(vendor), vendor, UNKNOWN_VENDOR_MESSAGE);

    /// <summary>
    /// Gets the canonical keys served by the reference content.
    /// </summary>
    /// <returns>The canonical key sequence, including the demo missing key.</returns>
    private static IEnumerable<string> CanonicalKeys() =>
        LocalizationContent.All.Values
            .SelectMany(static templates => templates.Keys)
            .Distinct(StringComparer.Ordinal)
            .Append(ReferenceKeys.DEMO_MISSING_EVERYWHERE);
}
