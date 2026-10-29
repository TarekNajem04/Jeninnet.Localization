//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the localization content tests.
/// </summary>
[TestClass]
public sealed class LocalizationContentTests {
    /// <summary>
    /// Verifies that culture codes match supported cultures in registration order.
    /// </summary>
    [TestMethod]
    public void CultureCodes_MatchSupportedCulturesInRegistrationOrder() {
        var codes = LocalizationContent.CultureCodes.ToArray();

        Assert.HasCount(3, codes);
        Assert.AreSequenceEqual([.. ReferenceCultures.SupportedCultures], codes);
    }

    /// <summary>
    /// Verifies that every culture contains all required keys.
    /// </summary>
    [TestMethod]
    public void EveryCultureContainsAllRequiredKeys() {
        foreach(var culture in ReferenceCultures.SupportedCultures) {
            var templates = LocalizationContent.GetTemplates(culture);
            Assert.IsNotEmpty(templates, $"Culture '{culture}' resolved to empty content.");
        }
    }

    /// <summary>
    /// Verifies that missing everywhere is absent from every culture.
    /// </summary>
    [TestMethod]
    public void MissingEverywhere_IsAbsentFromEveryCulture() {
        foreach(var culture in ReferenceCultures.SupportedCultures) {
            var found = LocalizationContent.TryGetString(culture, ReferenceKeys.DEMO_MISSING_EVERYWHERE, out _);
            Assert.IsFalse(found, $"Culture '{culture}' must not define {ReferenceKeys.DEMO_MISSING_EVERYWHERE}.");
        }
    }

    /// <summary>
    /// Verifies that showcase exists only in the default culture.
    /// </summary>
    [TestMethod]
    public void Showcase_ExistsOnlyInTheDefaultCulture() {
        Assert.IsTrue(LocalizationContent.TryGetString("en", ReferenceKeys.DEMO_SHOWCASE, out _));
        Assert.IsFalse(LocalizationContent.TryGetString("ar", ReferenceKeys.DEMO_SHOWCASE, out _));
        Assert.IsFalse(LocalizationContent.TryGetString("ar-SY", ReferenceKeys.DEMO_SHOWCASE, out _));
    }

    /// <summary>
    /// Verifies that regional note exists in default and parent but not regional culture.
    /// </summary>
    [TestMethod]
    public void RegionalNote_ExistsInDefaultAndParentButNotRegionalCulture() {
        Assert.IsTrue(LocalizationContent.TryGetString("en", ReferenceKeys.DEMO_REGIONAL_NOTE, out _));
        Assert.IsTrue(LocalizationContent.TryGetString("ar", ReferenceKeys.DEMO_REGIONAL_NOTE, out _));
        Assert.IsFalse(LocalizationContent.TryGetString("ar-SY", ReferenceKeys.DEMO_REGIONAL_NOTE, out _));
    }

    /// <summary>
    /// Verifies that regional culture differs from parent for one value.
    /// </summary>
    [TestMethod]
    public void RegionalCulture_DiffersFromParentForOneValue() {
        var regional = LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION];
        var standard = LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION];

        Assert.AreNotEqual(standard, regional);
    }

    /// <summary>
    /// Verifies that get templates throws for unsupported culture.
    /// </summary>
    [TestMethod]
    public void GetTemplates_ThrowsForUnsupportedCulture() => _ = Assert.ThrowsExactly<KeyNotFoundException>(() => LocalizationContent.GetTemplates("fr"));
}
