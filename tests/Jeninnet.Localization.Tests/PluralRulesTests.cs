//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// CLDR-derived test vectors for <see cref="PluralRules"/>.
/// Vectors pinned to CLDR (Common Locale Data Repository) 48 ("Language Plural Rules", Unicode 17).
/// Values travel as strings so exact decimal scale is preserved
/// (<c>1.0</c> and <c>1</c> select different categories in several languages).
/// </summary>
[TestClass]
public class PluralRulesTests {
    /// <summary>
    /// Verifies that get cardinal category resolves expected category.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="valueText">The value text.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow("en", "1", PluralCategory.One)]
    [DataRow("en", "0", PluralCategory.Other)]
    [DataRow("en", "2", PluralCategory.Other)]
    [DataRow("en", "1.0", PluralCategory.Other)]
    [DataRow("en", "1.5", PluralCategory.Other)]
    [DataRow("en", "-1", PluralCategory.One)]
    [DataRow("en-US", "1", PluralCategory.One)]
    [DataRow("de", "1", PluralCategory.One)]
    [DataRow("de", "0", PluralCategory.Other)]
    [DataRow("de", "1.0", PluralCategory.Other)]
    [DataRow("nl", "1", PluralCategory.One)]
    [DataRow("nl", "2", PluralCategory.Other)]
    [DataRow("sv", "1", PluralCategory.One)]
    [DataRow("sv", "0", PluralCategory.Other)]
    [DataRow("fi", "1", PluralCategory.One)]
    [DataRow("fi", "2", PluralCategory.Other)]
    [DataRow("sw", "1", PluralCategory.One)]
    [DataRow("sw", "0", PluralCategory.Other)]
    [DataRow("da", "1", PluralCategory.One)]
    [DataRow("da", "0.5", PluralCategory.One)]
    [DataRow("da", "1.5", PluralCategory.One)]
    [DataRow("da", "0", PluralCategory.Other)]
    [DataRow("da", "2", PluralCategory.Other)]
    [DataRow("da", "2.5", PluralCategory.Other)]
    [DataRow("es", "1", PluralCategory.One)]
    [DataRow("es", "1.0", PluralCategory.One)]
    [DataRow("es", "0", PluralCategory.Other)]
    [DataRow("es", "2", PluralCategory.Other)]
    [DataRow("es", "1000000", PluralCategory.Many)]
    [DataRow("it", "1", PluralCategory.One)]
    [DataRow("it", "1.0", PluralCategory.Other)]
    [DataRow("it", "0", PluralCategory.Other)]
    [DataRow("it", "1000000", PluralCategory.Many)]
    [DataRow("ca", "1", PluralCategory.One)]
    [DataRow("ca", "2", PluralCategory.Other)]
    [DataRow("gl", "1", PluralCategory.One)]
    [DataRow("gl", "0", PluralCategory.Other)]
    [DataRow("pt", "0", PluralCategory.One)]
    [DataRow("pt", "1", PluralCategory.One)]
    [DataRow("pt", "2", PluralCategory.Other)]
    [DataRow("pt", "1000000", PluralCategory.Many)]
    [DataRow("pt-BR", "0", PluralCategory.One)]
    [DataRow("fr", "0", PluralCategory.One)]
    [DataRow("fr", "1", PluralCategory.One)]
    [DataRow("fr", "0.5", PluralCategory.One)]
    [DataRow("fr", "1.5", PluralCategory.One)]
    [DataRow("fr", "2", PluralCategory.Other)]
    [DataRow("fr", "1000000", PluralCategory.Many)]
    [DataRow("fa", "0", PluralCategory.One)]
    [DataRow("fa", "0.5", PluralCategory.One)]
    [DataRow("fa", "1", PluralCategory.One)]
    [DataRow("fa", "1.5", PluralCategory.Other)]
    [DataRow("fa", "2", PluralCategory.Other)]
    [DataRow("am", "0", PluralCategory.One)]
    [DataRow("am", "1", PluralCategory.One)]
    [DataRow("am", "2", PluralCategory.Other)]
    [DataRow("bn", "0", PluralCategory.One)]
    [DataRow("bn", "1", PluralCategory.One)]
    [DataRow("bn", "2", PluralCategory.Other)]
    [DataRow("hi", "1", PluralCategory.One)]
    [DataRow("hi", "2", PluralCategory.Other)]
    [DataRow("vi", "0", PluralCategory.One)]
    [DataRow("vi", "1", PluralCategory.One)]
    [DataRow("vi", "2", PluralCategory.Other)]
    [DataRow("gu", "1", PluralCategory.One)]
    [DataRow("gu", "0", PluralCategory.One)]
    [DataRow("gu", "2", PluralCategory.Other)]
    [DataRow("el", "1", PluralCategory.One)]
    [DataRow("el", "1.0", PluralCategory.One)]
    [DataRow("el", "0", PluralCategory.Other)]
    [DataRow("el", "2", PluralCategory.Other)]
    [DataRow("eu", "1", PluralCategory.One)]
    [DataRow("eu", "0", PluralCategory.Other)]
    [DataRow("tr", "1", PluralCategory.One)]
    [DataRow("tr", "1.0", PluralCategory.One)]
    [DataRow("tr", "2", PluralCategory.Other)]
    [DataRow("hu", "1", PluralCategory.One)]
    [DataRow("hu", "0", PluralCategory.Other)]
    [DataRow("bg", "1", PluralCategory.One)]
    [DataRow("bg", "2", PluralCategory.Other)]
    [DataRow("nb", "1", PluralCategory.One)]
    [DataRow("nb", "2", PluralCategory.Other)]
    [DataRow("nn", "1", PluralCategory.One)]
    [DataRow("no", "1", PluralCategory.One)]
    [DataRow("no", "0", PluralCategory.Other)]
    [DataRow("sq", "1", PluralCategory.One)]
    [DataRow("sq", "2", PluralCategory.Other)]
    [DataRow("ar", "0", PluralCategory.Zero)]
    [DataRow("ar", "1", PluralCategory.One)]
    [DataRow("ar", "2", PluralCategory.Two)]
    [DataRow("ar", "3", PluralCategory.Few)]
    [DataRow("ar", "10", PluralCategory.Few)]
    [DataRow("ar", "11", PluralCategory.Many)]
    [DataRow("ar", "99", PluralCategory.Many)]
    [DataRow("ar", "100", PluralCategory.Other)]
    [DataRow("ar", "102", PluralCategory.Other)]
    [DataRow("ar", "103", PluralCategory.Few)]
    [DataRow("ar", "111", PluralCategory.Many)]
    [DataRow("ar", "2.5", PluralCategory.Other)]
    [DataRow("ar-SY", "2", PluralCategory.Two)]
    [DataRow("ru", "1", PluralCategory.One)]
    [DataRow("ru", "21", PluralCategory.One)]
    [DataRow("ru", "101", PluralCategory.One)]
    [DataRow("ru", "2", PluralCategory.Few)]
    [DataRow("ru", "4", PluralCategory.Few)]
    [DataRow("ru", "22", PluralCategory.Few)]
    [DataRow("ru", "5", PluralCategory.Many)]
    [DataRow("ru", "0", PluralCategory.Many)]
    [DataRow("ru", "11", PluralCategory.Many)]
    [DataRow("ru", "14", PluralCategory.Many)]
    [DataRow("ru", "25", PluralCategory.Many)]
    [DataRow("ru", "111", PluralCategory.Many)]
    [DataRow("ru", "1.5", PluralCategory.Other)]
    [DataRow("uk", "1", PluralCategory.One)]
    [DataRow("uk", "3", PluralCategory.Few)]
    [DataRow("uk", "5", PluralCategory.Many)]
    [DataRow("uk", "11", PluralCategory.Many)]
    [DataRow("uk", "14", PluralCategory.Many)]
    [DataRow("be", "1", PluralCategory.One)]
    [DataRow("be", "1.0", PluralCategory.One)]
    [DataRow("be", "21.0", PluralCategory.One)]
    [DataRow("be", "2", PluralCategory.Few)]
    [DataRow("be", "22", PluralCategory.Few)]
    [DataRow("be", "5", PluralCategory.Many)]
    [DataRow("be", "0", PluralCategory.Many)]
    [DataRow("be", "1.5", PluralCategory.Other)]
    [DataRow("be", "2.5", PluralCategory.Other)]
    [DataRow("cs", "1", PluralCategory.One)]
    [DataRow("cs", "2", PluralCategory.Few)]
    [DataRow("cs", "4", PluralCategory.Few)]
    [DataRow("cs", "5", PluralCategory.Other)]
    [DataRow("cs", "0", PluralCategory.Other)]
    [DataRow("cs", "1.5", PluralCategory.Many)]
    [DataRow("sk", "1", PluralCategory.One)]
    [DataRow("sk", "3", PluralCategory.Few)]
    [DataRow("sk", "5", PluralCategory.Other)]
    [DataRow("pl", "1", PluralCategory.One)]
    [DataRow("pl", "2", PluralCategory.Few)]
    [DataRow("pl", "4", PluralCategory.Few)]
    [DataRow("pl", "22", PluralCategory.Few)]
    [DataRow("pl", "12", PluralCategory.Many)]
    [DataRow("pl", "5", PluralCategory.Many)]
    [DataRow("pl", "0", PluralCategory.Many)]
    [DataRow("pl", "1.5", PluralCategory.Other)]
    [DataRow("lt", "1", PluralCategory.One)]
    [DataRow("lt", "21", PluralCategory.One)]
    [DataRow("lt", "11", PluralCategory.Other)]
    [DataRow("lt", "2", PluralCategory.Few)]
    [DataRow("lt", "9", PluralCategory.Few)]
    [DataRow("lt", "10", PluralCategory.Other)]
    [DataRow("lt", "1.5", PluralCategory.Many)]
    [DataRow("sl", "1", PluralCategory.One)]
    [DataRow("sl", "101", PluralCategory.One)]
    [DataRow("sl", "2", PluralCategory.Two)]
    [DataRow("sl", "102", PluralCategory.Two)]
    [DataRow("sl", "3", PluralCategory.Few)]
    [DataRow("sl", "104", PluralCategory.Few)]
    [DataRow("sl", "0.5", PluralCategory.Few)]
    [DataRow("sl", "5", PluralCategory.Other)]
    [DataRow("sl", "0", PluralCategory.Other)]
    [DataRow("hr", "1", PluralCategory.One)]
    [DataRow("hr", "21", PluralCategory.One)]
    [DataRow("hr", "1.1", PluralCategory.One)]
    [DataRow("hr", "2", PluralCategory.Few)]
    [DataRow("hr", "22", PluralCategory.Few)]
    [DataRow("hr", "1.2", PluralCategory.Few)]
    [DataRow("hr", "5", PluralCategory.Other)]
    [DataRow("hr", "0", PluralCategory.Other)]
    [DataRow("hr", "12", PluralCategory.Other)]
    [DataRow("sr", "1", PluralCategory.One)]
    [DataRow("sr", "3", PluralCategory.Few)]
    [DataRow("sr", "5", PluralCategory.Other)]
    [DataRow("sr", "0", PluralCategory.Other)]
    [DataRow("sr", "11", PluralCategory.Other)]
    [DataRow("sr", "1.1", PluralCategory.One)]
    [DataRow("sr", "2.2", PluralCategory.Few)]
    [DataRow("ro", "1", PluralCategory.One)]
    [DataRow("ro", "1.0", PluralCategory.Few)]
    [DataRow("ro", "0", PluralCategory.Few)]
    [DataRow("ro", "19", PluralCategory.Few)]
    [DataRow("ro", "20", PluralCategory.Other)]
    [DataRow("ro", "101", PluralCategory.Few)]
    [DataRow("he", "1", PluralCategory.One)]
    [DataRow("he", "0.5", PluralCategory.One)]
    [DataRow("he", "2", PluralCategory.Two)]
    [DataRow("he", "0", PluralCategory.Other)]
    [DataRow("he", "3", PluralCategory.Other)]
    [DataRow("mt", "1", PluralCategory.One)]
    [DataRow("mt", "2", PluralCategory.Two)]
    [DataRow("mt", "0", PluralCategory.Few)]
    [DataRow("mt", "3", PluralCategory.Few)]
    [DataRow("mt", "10", PluralCategory.Few)]
    [DataRow("mt", "11", PluralCategory.Many)]
    [DataRow("mt", "19", PluralCategory.Many)]
    [DataRow("mt", "20", PluralCategory.Other)]
    [DataRow("mt", "1.5", PluralCategory.Other)]
    [DataRow("ga", "1", PluralCategory.One)]
    [DataRow("ga", "2", PluralCategory.Two)]
    [DataRow("ga", "3", PluralCategory.Few)]
    [DataRow("ga", "6", PluralCategory.Few)]
    [DataRow("ga", "7", PluralCategory.Many)]
    [DataRow("ga", "10", PluralCategory.Many)]
    [DataRow("ga", "0", PluralCategory.Other)]
    [DataRow("ga", "11", PluralCategory.Other)]
    [DataRow("cy", "0", PluralCategory.Zero)]
    [DataRow("cy", "1", PluralCategory.One)]
    [DataRow("cy", "2", PluralCategory.Two)]
    [DataRow("cy", "3", PluralCategory.Few)]
    [DataRow("cy", "6", PluralCategory.Many)]
    [DataRow("cy", "4", PluralCategory.Other)]
    [DataRow("cy", "5", PluralCategory.Other)]
    [DataRow("gd", "1", PluralCategory.One)]
    [DataRow("gd", "11", PluralCategory.One)]
    [DataRow("gd", "2", PluralCategory.Two)]
    [DataRow("gd", "12", PluralCategory.Two)]
    [DataRow("gd", "3", PluralCategory.Few)]
    [DataRow("gd", "19", PluralCategory.Few)]
    [DataRow("gd", "0", PluralCategory.Other)]
    [DataRow("gd", "20", PluralCategory.Other)]
    [DataRow("ja", "0", PluralCategory.Other)]
    [DataRow("ja", "1", PluralCategory.Other)]
    [DataRow("ja", "2", PluralCategory.Other)]
    [DataRow("zh", "1", PluralCategory.Other)]
    [DataRow("zh-Hant", "5", PluralCategory.Other)]
    [DataRow("ko", "1", PluralCategory.Other)]
    [DataRow("ko", "5", PluralCategory.Other)]
    [DataRow("th", "0", PluralCategory.Other)]
    [DataRow("th", "1", PluralCategory.Other)]
    [DataRow("id", "1", PluralCategory.Other)]
    [DataRow("ms", "1", PluralCategory.Other)]
    [DataRow("ms", "2", PluralCategory.Other)]
    public void GetCardinalCategory_ResolvesExpectedCategory(string cultureName, string valueText, PluralCategory expected) {
        var culture = new CultureInfo(cultureName);
        var value = decimal.Parse(valueText, CultureInfo.InvariantCulture);

        PluralRules.GetCardinalCategory(culture, value).Should().Be(expected);
    }

    /// <summary>
    /// Verifies that get ordinal category resolves expected category.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="valueText">The value text.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow("en", "1", PluralCategory.One)]
    [DataRow("en", "2", PluralCategory.Two)]
    [DataRow("en", "3", PluralCategory.Few)]
    [DataRow("en", "4", PluralCategory.Other)]
    [DataRow("en", "11", PluralCategory.Other)]
    [DataRow("en", "12", PluralCategory.Other)]
    [DataRow("en", "13", PluralCategory.Other)]
    [DataRow("en", "21", PluralCategory.One)]
    [DataRow("en", "22", PluralCategory.Two)]
    [DataRow("en", "23", PluralCategory.Few)]
    [DataRow("en", "101", PluralCategory.One)]
    [DataRow("en", "111", PluralCategory.Other)]
    [DataRow("fr", "1", PluralCategory.Other)]
    [DataRow("ar", "2", PluralCategory.Other)]
    public void GetOrdinalCategory_ResolvesExpectedCategory(string cultureName, string valueText, PluralCategory expected) {
        var culture = new CultureInfo(cultureName);
        var value = decimal.Parse(valueText, CultureInfo.InvariantCulture);

        PluralRules.GetOrdinalCategory(culture, value).Should().Be(expected);
    }

    /// <summary>
    /// Verifies that get cardinal category falls back to other for uncovered cultures.
    /// </summary>
    [TestMethod]
    public void GetCardinalCategory_FallsBackToOtherForUncoveredCultures() {
        PluralRules.GetCardinalCategory(CultureInfo.InvariantCulture, 1).Should().Be(PluralCategory.Other);
        PluralRules.GetOrdinalCategory(CultureInfo.InvariantCulture, 1).Should().Be(PluralCategory.Other);
    }

    /// <summary>
    /// Verifies that get cardinal category validates arguments.
    /// </summary>
    [TestMethod]
    public void GetCardinalCategory_ValidatesArguments() {
        Action nullCulture = () => PluralRules.GetCardinalCategory(null!, 1);
        nullCulture.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that get ordinal category validates arguments.
    /// </summary>
    [TestMethod]
    public void GetOrdinalCategory_ValidatesArguments() {
        Action nullCulture = () => PluralRules.GetOrdinalCategory(null!, 1);
        nullCulture.Should().Throw<ArgumentNullException>();
    }
}
