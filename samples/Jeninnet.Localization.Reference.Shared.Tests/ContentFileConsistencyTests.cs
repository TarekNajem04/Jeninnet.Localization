//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the content file consistency tests.
/// </summary>
[TestClass]
public sealed class ContentFileConsistencyTests {
    /// <summary>
    /// Verifies that committed json files match the embedded canonical content.
    /// </summary>
    [TestMethod]
    public void CommittedJsonFiles_MatchTheEmbeddedCanonicalContent() {
        var jsonFiles = GetCommittedFiles("*.json");
        var expectedNames = ReferenceCultures.SupportedCultures
            .Select(static culture => culture + ".json")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreSequenceEqual(expectedNames, [.. jsonFiles.Select(static file => file.Name)]);

        foreach(var file in jsonFiles) {
            var culture = Path.GetFileNameWithoutExtension(file.Name);
            var committed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file.FullName))
                ?? throw new InvalidOperationException($"Committed JSON file '{file.Name}' is empty or invalid.");
            AssertContentEqual(culture, LocalizationContent.GetTemplates(culture), committed);
        }
    }

    /// <summary>
    /// Verifies that committed xml files match the embedded canonical content.
    /// </summary>
    [TestMethod]
    public void CommittedXmlFiles_MatchTheEmbeddedCanonicalContent() {
        var xmlFiles = GetCommittedFiles("*.xml");
        var expectedNames = ReferenceCultures.SupportedCultures
            .Select(static culture => culture + ".xml")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreSequenceEqual(expectedNames, [.. xmlFiles.Select(static file => file.Name)]);

        foreach(var file in xmlFiles) {
            var culture = Path.GetFileNameWithoutExtension(file.Name);
            var document = XDocument.Load(file.FullName);
            var committed = document.Root?.Elements()
                .ToDictionary(static element => element.Name.LocalName, static element => element.Value)
                ?? throw new InvalidOperationException($"Committed XML file '{file.Name}' has no root element.");
            AssertContentEqual(culture, LocalizationContent.GetTemplates(culture), committed);
        }
    }

    /// <summary>
    /// Gets committed files.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The committed files sequence.</returns>
    private static FileInfo[] GetCommittedFiles(string pattern) {
        var directory = Path.Combine(TestPaths.GetRepositoryRoot(), "samples", "Content", "Localization");
        return [.. Directory.EnumerateFiles(directory, pattern)
            .Select(static path => new FileInfo(path))
            .OrderBy(static file => file.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Asserts content equal.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    private static void AssertContentEqual(string culture, IReadOnlyDictionary<string, string> expected, Dictionary<string, string> actual) {
        Assert.HasCount(expected.Count, actual, $"Culture '{culture}' committed content count differs from the embedded canonical content.");
        foreach(var (key, value) in expected) {
            Assert.IsTrue(actual.TryGetValue(key, out var committed), $"Culture '{culture}' committed content is missing key '{key}'.");
            Assert.AreEqual(value, committed, $"Culture '{culture}' committed value for '{key}' drifted from the embedded canonical content.");
        }
    }
}
