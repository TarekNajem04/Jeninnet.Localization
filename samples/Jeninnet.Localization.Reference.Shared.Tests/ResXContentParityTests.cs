//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Proves that the committed RESX resource set is a faithful view of the canonical reference
/// content and that the compiled resource identity resolves through the shipping RESX provider.
/// </summary>
[TestClass]
public sealed class ResXContentParityTests {
    private const string NEUTRAL_FILE_NAME = "ReferenceContent.resx";

    /// <summary>
    /// Verifies that committed res XFiles match the embedded canonical content.
    /// </summary>
    [TestMethod]
    public void CommittedResXFiles_MatchTheEmbeddedCanonicalContent() {
        AssertContentEqual(
            ReferenceCultures.DEFAULT_CULTURE,
            LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE),
            ReadResX(GetResXPath(NEUTRAL_FILE_NAME)));

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            AssertContentEqual(
                cultureCode,
                LocalizationContent.GetTemplates(cultureCode),
                ReadResX(GetResXPath($"ReferenceContent.{cultureCode}.resx")));
        }
    }

    /// <summary>
    /// Verifies that committed res XFiles use exactly the supported culture set.
    /// </summary>
    [TestMethod]
    public void CommittedResXFiles_UseExactlyTheSupportedCultureSet() {
        var expectedNames = ReferenceCultures.SupportedCultures
            .Select(static cultureCode => $"ReferenceContent.{cultureCode}.resx")
            .Append(NEUTRAL_FILE_NAME)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var actualNames = Directory.EnumerateFiles(GetResXDirectory(), "*.resx")
            .Select(static path => Path.GetFileName(path))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.AreSequenceEqual(
            expectedNames, actualNames, "The committed RESX file set must mirror the supported reference cultures exactly.");
    }

    /// <summary>
    /// Verifies that res XProvider resolves every canonical key exactly per culture.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_ResolvesEveryCanonicalKeyExactlyPerCultureAsync() {
        var provider = CreateProvider();

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
    /// Verifies that res XProvider preserves the intentional partial culture semantics.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_PreservesTheIntentionalPartialCultureSemanticsAsync() {
        var provider = CreateProvider();
        var defaultCulture = new CultureInfo(ReferenceCultures.DEFAULT_CULTURE);
        var arabic = new CultureInfo("ar");
        var regional = new CultureInfo("ar-SY");

        Assert.IsNotNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, arabic, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_REGIONAL_NOTE}' must exist in 'ar'.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, regional, TestContext.CancellationToken),
            $"The 'ar-SY' resource set intentionally omits '{ReferenceKeys.DEMO_REGIONAL_NOTE}'; parent fallback belongs to the pipeline.");

        Assert.IsNotNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, defaultCulture, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' must exist in the default culture.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, arabic, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' exists only in the default culture.");
        Assert.IsNull(
            await provider.GetMessageTemplateAsync(ReferenceKeys.DEMO_SHOWCASE, regional, TestContext.CancellationToken),
            $"'{ReferenceKeys.DEMO_SHOWCASE}' exists only in the default culture.");
    }

    /// <summary>
    /// Verifies that res XProvider does not fall back to parent culture.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_DoesNotFallBackToParentCultureAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, new CultureInfo("ar-IQ"), TestContext.CancellationToken);

        Assert.IsNull(result, "The RESX provider resolves only the requested resource set; parent fallback belongs to the canonical pipeline.");
    }

    /// <summary>
    /// Verifies that res XResource identity is embedded under the expected manifest names.
    /// </summary>
    [TestMethod]
    public void ResXResourceIdentity_IsEmbeddedUnderTheExpectedManifestNames() {
        var assembly = typeof(ReferenceLocalization).Assembly;

        Assert.Contains(
            ReferenceLocalization.RES_X_RESOURCE_BASE_NAME + ".resources", assembly.GetManifestResourceNames(), "The neutral RESX resource set must be embedded in the Shared assembly under the documented base name.");

        foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
            var satellite = assembly.GetSatelliteAssembly(new CultureInfo(cultureCode));

            Assert.Contains(
                $"{ReferenceLocalization.RES_X_RESOURCE_BASE_NAME}.{cultureCode}.resources", satellite.GetManifestResourceNames(), $"The '{cultureCode}' RESX resource set must be embedded in its culture satellite assembly.");
        }
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <returns>The provider result.</returns>
    private static ResXMessageProvider CreateProvider() =>
        new(new ResXLocalizationOptions {
            BaseName = ReferenceLocalization.RES_X_RESOURCE_BASE_NAME,
            ResourceAssembly = typeof(ReferenceLocalization).Assembly
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
    /// Reads res x.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The res x result.</returns>
    private static Dictionary<string, string> ReadResX(string path) {
        var root = XDocument.Load(path).Root
            ?? throw new InvalidOperationException($"The RESX file '{path}' has no root element.");

        return root.Elements("data").ToDictionary(
            static element => element.Attribute("name")?.Value
                ?? throw new InvalidOperationException("A RESX data element is missing its name attribute."),
            static element => element.Element("value")?.Value ?? string.Empty,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Asserts content equal.
    /// </summary>
    /// <param name="cultureCode">The culture.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    private static void AssertContentEqual(string cultureCode, IReadOnlyDictionary<string, string> expected, Dictionary<string, string> actual) {
        Assert.HasCount(expected.Count, actual, $"Culture '{cultureCode}' RESX content count differs from the embedded canonical content.");
        foreach(var (key, value) in expected) {
            Assert.IsTrue(actual.TryGetValue(key, out var committed), $"Culture '{cultureCode}' RESX content is missing key '{key}'.");
            Assert.AreEqual(value, committed, $"Culture '{cultureCode}' RESX value for '{key}' drifted from the embedded canonical content.");
        }
    }

    /// <summary>
    /// Gets res XDirectory.
    /// </summary>
    /// <returns>The res XDirectory text.</returns>
    private static string GetResXDirectory() =>
        Path.Combine(TestPaths.GetRepositoryRoot(), "samples", "Content", "Localization", "ResX");

    /// <summary>
    /// Gets res XPath.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The res XPath text.</returns>
    private static string GetResXPath(string fileName) {
        var path = Path.Combine(GetResXDirectory(), fileName);
        Assert.IsTrue(File.Exists(path), $"Expected committed RESX file '{path}'.");
        return path;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
