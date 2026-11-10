//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Placeholder-parity unit tests: top-level identifier extraction and mismatch
/// reporting for default and ICU placeholders.
/// </summary>
[TestClass]
public class SourceTemplatePlaceholderParityTests {
    /// <summary>
    /// Verifies that extract finds default placeholders.
    /// </summary>
    [TestMethod]
    public void Extract_FindsDefaultPlaceholders() =>
        TranslationPlaceholderParity.ExtractPlaceholders("Hello {name}, you have {count} items.")
            .Should().BeEquivalentTo("name", "count");

    /// <summary>
    /// Verifies that extract finds icu argument names.
    /// </summary>
    [TestMethod]
    public void Extract_FindsIcuArgumentNames() =>
        TranslationPlaceholderParity.ExtractPlaceholders("{count, plural, one{# file} other{# files}} by {user}")
            .Should().BeEquivalentTo("count", "user");

    /// <summary>
    /// Verifies that extract ignores unbalanced braces and empties.
    /// </summary>
    [TestMethod]
    public void Extract_IgnoresUnbalancedBracesAndEmpties() =>
        TranslationPlaceholderParity.ExtractPlaceholders("open { brace and {} done")
            .Should().BeEmpty();

    /// <summary>
    /// Verifies that validate accepts matching templates.
    /// </summary>
    [TestMethod]
    public void Validate_AcceptsMatchingTemplates() {
        TranslationPlaceholderParity.Validate("Hello {name}", "Hallo {name}");
        TranslationPlaceholderParity.Validate("{a} and {b}", "{b} et {a}");
        TranslationPlaceholderParity.Validate(
            "{count, plural, one{# file} other{# files}}",
            "{count, plural, one{# fichier} other{# fichiers}}");
        TranslationPlaceholderParity.Validate("Hello {name}", null);
    }

    /// <summary>
    /// Verifies that validate reports missing placeholders.
    /// </summary>
    [TestMethod]
    public void Validate_ReportsMissingPlaceholders() {
        Action act = () => TranslationPlaceholderParity.Validate("Hello {name}, {count} items", "Hallo");

        var exception = act.Should().Throw<SourceTemplateParityException>().Exception;
        exception.Message.Should().Contain("name");
        exception.Message.Should().Contain("count");
    }

    /// <summary>
    /// Verifies that validate reports extra placeholders.
    /// </summary>
    [TestMethod]
    public void Validate_ReportsExtraPlaceholders() {
        Action act = () => TranslationPlaceholderParity.Validate("Hello", "Hallo {name}");

        var exception = act.Should().Throw<SourceTemplateParityException>().Exception;
        exception.Message.Should().Contain("name");
    }

    /// <summary>
    /// Verifies that validate null source throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Validate_NullSource_ThrowsArgumentNullException() {
        Action act = () => TranslationPlaceholderParity.Validate(null!, "x");

        act.Should().Throw<ArgumentNullException>();
    }
}
