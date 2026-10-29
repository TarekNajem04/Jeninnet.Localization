//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the culture info extensions tests.
/// </summary>
[TestClass]
public class CultureInfoExtensionsTests {
    /// <summary>
    /// Verifies that culture exists null culture name throws argument null exception.
    /// </summary>
    [TestMethod]
    public void CultureExists_NullCultureName_ThrowsArgumentNullException() {
        var act = () => CultureInfoExtensions.CultureExists(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that culture exists matches culture names ordinal ignore case casing.
    /// </summary>
    [TestMethod]
    public void CultureExists_MatchesCultureNamesOrdinalIgnoreCaseCasing() {
        CultureInfoExtensions.CultureExists("en-US").Should().BeTrue();
        CultureInfoExtensions.CultureExists("EN").Should().BeTrue();
    }

    /// <summary>
    /// Verifies that culture exists unknown culture name returns false.
    /// </summary>
    [TestMethod]
    public void CultureExists_UnknownCultureName_ReturnsFalse() => CultureInfoExtensions.CultureExists("definitely-not-a-culture").Should().BeFalse();
}
