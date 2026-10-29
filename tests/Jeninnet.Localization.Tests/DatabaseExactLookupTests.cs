//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Tests;

/// <summary>
/// Contains the database exact lookup tests.
/// </summary>
[TestClass]
public partial class DatabaseExactLookupTests {
    private static readonly string _solutionRoot = FindSolutionRoot();

    /// <summary>
    /// Verifies that provider queries by key and exact culture.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="fileName">The file name.</param>
    [TestMethod]
    [DataRow("SqlServer", "SqlServerMessageProvider.cs")]
    [DataRow("MySql", "MySqlMessageProvider.cs")]
    [DataRow("PostgreSql", "PostgreSqlMessageProvider.cs")]
    [DataRow("SQLite", "SqliteMessageProvider.cs")]
    public void Provider_QueriesByKeyAndExactCulture(string providerName, string fileName) {
        _ = fileName;
        var baseClassPath = Path.Combine(
            _solutionRoot, "src", "providers",
            "Jeninnet.Localization.Providers.Database.Abstractions",
            "DatabaseMessageProviderBase.cs"
        );

        File.Exists(baseClassPath).Should().BeTrue($"Source file {baseClassPath} should exist");
        var source = File.ReadAllText(baseClassPath);

        // Must query by both key and exact culture
        var hasKeyParam = KeyParamPattern().IsMatch(source);
        var hasCultureParam = CultureParamPattern().IsMatch(source);

        hasKeyParam.Should().BeTrue($"{providerName} provider must filter by Key parameter");
        hasCultureParam.Should().BeTrue($"{providerName} provider must filter by exact Culture parameter");
    }

    /// <summary>
    /// Verifies that provider uses parameterized queries.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="fileName">The file name.</param>
    [TestMethod]
    [DataRow("SqlServer", "SqlServerMessageProvider.cs")]
    [DataRow("MySql", "MySqlMessageProvider.cs")]
    [DataRow("PostgreSql", "PostgreSqlMessageProvider.cs")]
    [DataRow("SQLite", "SqliteMessageProvider.cs")]
    public void Provider_UsesParameterizedQueries(string providerName, string fileName) {
        _ = fileName;
        var baseClassPath = Path.Combine(
            _solutionRoot, "src", "providers",
            "Jeninnet.Localization.Providers.Database.Abstractions",
            "DatabaseMessageProviderBase.cs"
        );

        File.Exists(baseClassPath).Should().BeTrue();
        var source = File.ReadAllText(baseClassPath);

        // Must use AddWithValue or similar parameterized API (not string concatenation for values)
        var hasParameterizedQuery = ParametersPattern().IsMatch(source);
        hasParameterizedQuery.Should().BeTrue(
            $"{providerName} provider must use parameterized queries (AddWithValue)");
    }

    /// <summary>
    /// Finds solution root.
    /// </summary>
    /// <returns>The solution root text.</returns>
    private static string FindSolutionRoot() {
        var dir = AppContext.BaseDirectory;
        var root = new DirectoryInfo(dir);
        while(root is not null) {
            if(File.Exists(Path.Combine(root.FullName, "Jeninnet.Localization.slnx")) ||
               File.Exists(Path.Combine(root.FullName, "Jeninnet.Localization.sln"))) {
                return root.FullName;
            }

            root = root.Parent;
        }

        throw new InvalidOperationException("Could not find solution root");
    }

    /// <summary>
    /// Performs the key param pattern operation.
    /// </summary>
    /// <returns>The key param pattern result.</returns>
    [GeneratedRegex(@"\@MessageKey\b", RegexOptions.Compiled)]
    private static partial Regex KeyParamPattern();

    /// <summary>
    /// Performs the culture param pattern operation.
    /// </summary>
    /// <returns>The culture param pattern result.</returns>
    [GeneratedRegex(@"\@Culture\b", RegexOptions.Compiled)]
    private static partial Regex CultureParamPattern();

    /// <summary>
    /// Performs the parameters pattern operation.
    /// </summary>
    /// <returns>The parameters pattern result.</returns>
    [GeneratedRegex(@"Parameters\.(AddWithValue|Add)", RegexOptions.Compiled)]
    private static partial Regex ParametersPattern();
}
