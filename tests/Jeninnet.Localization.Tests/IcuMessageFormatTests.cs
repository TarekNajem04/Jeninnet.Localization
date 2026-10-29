//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the icu message format tests.
/// </summary>
[TestClass]
public class IcuMessageFormatTests {
    private static readonly CultureInfo _english = new("en");

    /// <summary>
    /// Creates formatter.
    /// </summary>
    /// <returns>The formatter result.</returns>
    private static IcuMessageFormatter CreateFormatter() => new();

    /// <summary>
    /// Performs the parameters operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The parameters result.</returns>
    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] entries) =>
        entries.ToDictionary(static entry => entry.Name, static entry => entry.Value);

    /// <summary>
    /// Verifies that format selects cardinal branches.
    /// </summary>
    [TestMethod]
    public void Format_SelectsCardinalBranches() {
        const string template = "You have {count, plural, one {# book} other {# books}}.";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("count", 0)), _english).Should().Be("You have 0 books.");
        formatter.Format(template, Parameters(("count", 1)), _english).Should().Be("You have 1 book.");
        formatter.Format(template, Parameters(("count", 5)), _english).Should().Be("You have 5 books.");
    }

    /// <summary>
    /// Verifies that format honors explicit values before categories.
    /// </summary>
    [TestMethod]
    public void Format_HonorsExplicitValuesBeforeCategories() {
        const string template = "{count, plural, =0 {no books} =42 {a universal amount} one {one book} other {# books}}";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("count", 0)), _english).Should().Be("no books");
        formatter.Format(template, Parameters(("count", 42)), _english).Should().Be("a universal amount");
        formatter.Format(template, Parameters(("count", 1)), _english).Should().Be("one book");
        formatter.Format(template, Parameters(("count", 7)), _english).Should().Be("7 books");
    }

    /// <summary>
    /// Verifies that format applies offset to pound substitution.
    /// </summary>
    [TestMethod]
    public void Format_AppliesOffsetToPoundSubstitution() {
        const string template = "{n, plural, offset:1 =0 {nobody} =1 {just you} one {you and # other} other {you and # others}}";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("n", 0)), _english).Should().Be("nobody");
        formatter.Format(template, Parameters(("n", 1)), _english).Should().Be("just you");
        formatter.Format(template, Parameters(("n", 2)), _english).Should().Be("you and 1 others");
        formatter.Format(template, Parameters(("n", 5)), _english).Should().Be("you and 4 others");
    }

    /// <summary>
    /// Verifies that format selects by string value.
    /// </summary>
    [TestMethod]
    public void Format_SelectsByStringValue() {
        const string template = "{gender, select, male {He} female {She} other {They}} liked this.";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("gender", "male")), _english).Should().Be("He liked this.");
        formatter.Format(template, Parameters(("gender", "female")), _english).Should().Be("She liked this.");
        formatter.Format(template, Parameters(("gender", "unknown")), _english).Should().Be("They liked this.");
    }

    /// <summary>
    /// Verifies that format selects ordinal branches.
    /// </summary>
    [TestMethod]
    public void Format_SelectsOrdinalBranches() {
        const string template = "You are {position, selectordinal, one {#st} two {#nd} few {#rd} other {#th}} in line.";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("position", 1)), _english).Should().Be("You are 1st in line.");
        formatter.Format(template, Parameters(("position", 2)), _english).Should().Be("You are 2nd in line.");
        formatter.Format(template, Parameters(("position", 3)), _english).Should().Be("You are 3rd in line.");
        formatter.Format(template, Parameters(("position", 4)), _english).Should().Be("You are 4th in line.");
        formatter.Format(template, Parameters(("position", 11)), _english).Should().Be("You are 11th in line.");
        formatter.Format(template, Parameters(("position", 21)), _english).Should().Be("You are 21st in line.");
    }

    /// <summary>
    /// Verifies that format supports nested selection.
    /// </summary>
    [TestMethod]
    public void Format_SupportsNestedSelection() {
        const string template = "{gender, select, male {He invited {n, plural, one {one guest} other {# guests}}.} other {They invited {n, plural, one {one guest} other {# guests}}.}}";
        var formatter = CreateFormatter();

        formatter.Format(template, Parameters(("gender", "male"), ("n", 1)), _english)
            .Should().Be("He invited one guest.");
        formatter.Format(template, Parameters(("gender", "male"), ("n", 3)), _english)
            .Should().Be("He invited 3 guests.");
        formatter.Format(template, Parameters(("gender", "female"), ("n", 3)), _english)
            .Should().Be("They invited 3 guests.");
    }

    /// <summary>
    /// Verifies that format selects arabic six forms.
    /// </summary>
    [TestMethod]
    public void Format_SelectsArabicSixForms() {
        const string template = "{n, plural, zero {Z} one {O} two {T} few {F} many {M} other {X}}";
        var formatter = CreateFormatter();
        var arabic = new CultureInfo("ar");

        formatter.Format(template, Parameters(("n", 0)), arabic).Should().Be("Z");
        formatter.Format(template, Parameters(("n", 1)), arabic).Should().Be("O");
        formatter.Format(template, Parameters(("n", 2)), arabic).Should().Be("T");
        formatter.Format(template, Parameters(("n", 3)), arabic).Should().Be("F");
        formatter.Format(template, Parameters(("n", 11)), arabic).Should().Be("M");
        formatter.Format(template, Parameters(("n", 100)), arabic).Should().Be("X");
    }

    /// <summary>
    /// Verifies that format selects russian forms.
    /// </summary>
    [TestMethod]
    public void Format_SelectsRussianForms() {
        const string template = "{n, plural, one {O} few {F} many {M} other {X}}";
        var formatter = CreateFormatter();
        var russian = new CultureInfo("ru");

        formatter.Format(template, Parameters(("n", 1)), russian).Should().Be("O");
        formatter.Format(template, Parameters(("n", 2)), russian).Should().Be("F");
        formatter.Format(template, Parameters(("n", 5)), russian).Should().Be("M");
        formatter.Format(template, Parameters(("n", 11)), russian).Should().Be("M");
        formatter.Format(template, Parameters(("n", 21)), russian).Should().Be("O");
        formatter.Format(template, Parameters(("n", 111)), russian).Should().Be("M");
    }

    /// <summary>
    /// Verifies that format selects french zero and one as singular.
    /// </summary>
    [TestMethod]
    public void Format_SelectsFrenchZeroAndOneAsSingular() {
        const string template = "{n, plural, one {O} other {X}}";
        var formatter = CreateFormatter();
        var french = new CultureInfo("fr");

        formatter.Format(template, Parameters(("n", 0)), french).Should().Be("O");
        formatter.Format(template, Parameters(("n", 1)), french).Should().Be("O");
        formatter.Format(template, Parameters(("n", 2)), french).Should().Be("X");
    }

    /// <summary>
    /// Verifies that format selects czech few.
    /// </summary>
    [TestMethod]
    public void Format_SelectsCzechFew() {
        const string template = "{n, plural, one {O} few {F} other {X}}";
        var formatter = CreateFormatter();
        var czech = new CultureInfo("cs");

        formatter.Format(template, Parameters(("n", 1)), czech).Should().Be("O");
        formatter.Format(template, Parameters(("n", 2)), czech).Should().Be("F");
        formatter.Format(template, Parameters(("n", 4)), czech).Should().Be("F");
        formatter.Format(template, Parameters(("n", 5)), czech).Should().Be("X");
    }

    /// <summary>
    /// Verifies that format formats numbers dates and times.
    /// </summary>
    [TestMethod]
    public void Format_FormatsNumbersDatesAndTimes() {
        var formatter = CreateFormatter();
        var date = new DateTime(2026, 1, 5, 14, 30, 0, DateTimeKind.Unspecified);

        formatter.Format("{n, number}", Parameters(("n", 42)), _english).Should().Be("42");
        formatter.Format("{n, number, integer}", Parameters(("n", 42)), _english).Should().Be("42");
        formatter.Format("{d, date}", Parameters(("d", date)), _english)
            .Should().Be(date.ToString("d", _english));
        formatter.Format("{d, date, long}", Parameters(("d", date)), _english)
            .Should().Be(date.ToString("D", _english));
        formatter.Format("{t, time, short}", Parameters(("t", date)), _english)
            .Should().Be(date.ToString("t", _english));
    }

    /// <summary>
    /// Verifies that format processes apostrophe quoting in icu templates.
    /// </summary>
    [TestMethod]
    public void Format_ProcessesApostropheQuotingInIcuTemplates() {
        var formatter = CreateFormatter();

        formatter.Format("{n, plural, other {# items}} don''t", Parameters(("n", 5)), _english)
            .Should().Be("5 items don't");
        formatter.Format("'{n, plural, other {#}}' and {n, plural, other {# real}}", Parameters(("n", 2)), _english)
            .Should().Be("{n, plural, other {#}} and 2 real");
    }

    /// <summary>
    /// Verifies that format leaves plain templates to the default path.
    /// </summary>
    [TestMethod]
    public void Format_LeavesPlainTemplatesToTheDefaultPath() {
        var formatter = CreateFormatter();

        formatter.Format("Hello {Name}, you have {Count} items.", Parameters(("Name", "Lina"), ("Count", 3)), _english)
            .Should().Be("Hello Lina, you have 3 items.");
        formatter.Format("Use {var} for variables.", Parameters(("unused", 0)), _english)
            .Should().Be("Use {var} for variables.");
        formatter.Format("Value {Unknown} remains.", Parameters(("Other", "x")), _english)
            .Should().Be("Value {Unknown} remains.");
    }

    /// <summary>
    /// Verifies that format throws for malformed templates.
    /// </summary>
    [TestMethod]
    public void Format_ThrowsForMalformedTemplates() {
        var formatter = CreateFormatter();
        var parameters = Parameters(("n", 1));

        Action missingOther = () => formatter.Format("{n, plural, one {x}}", parameters, _english);
        missingOther.Should().Throw<FormatException>();

        Action unbalanced = () => formatter.Format("{n, plural, one {x} other {y}", parameters, _english);
        unbalanced.Should().Throw<FormatException>();

        Action strayClose = () => formatter.Format("broken } {n, plural, one {x} other {y}}", parameters, _english);
        strayClose.Should().Throw<FormatException>();

        Action unknownType = () => formatter.Format("ok {m, plural, one {a} other {b}} bad {n, frobnicate, x}", parameters, _english);
        unknownType.Should().Throw<FormatException>();

        Action duplicate = () => formatter.Format("{n, plural, one {a} one {b} other {c}}", parameters, _english);
        duplicate.Should().Throw<FormatException>();

        Action unsupportedStyle = () => formatter.Format("{n, number, skeleton}", parameters, _english);
        unsupportedStyle.Should().Throw<FormatException>();
    }

    /// <summary>
    /// Verifies that format throws for missing or mistyped selector arguments.
    /// </summary>
    [TestMethod]
    public void Format_ThrowsForMissingOrMistypedSelectorArguments() {
        var formatter = CreateFormatter();

        Action missingSelector = () => formatter.Format("{n, plural, one {a} other {b}}", Parameters(), _english);
        missingSelector.Should().Throw<FormatException>();

        Action textSelector = () => formatter.Format("{n, plural, one {a} other {b}}", Parameters(("n", "five")), _english);
        textSelector.Should().Throw<FormatException>();

        Action missingSelect = () => formatter.Format("{g, select, male {a} other {b}}", Parameters(), _english);
        missingSelect.Should().Throw<FormatException>();

        Action wrongDateType = () => formatter.Format("{d, date}", Parameters(("d", 5)), _english);
        wrongDateType.Should().Throw<FormatException>();
    }

    /// <summary>
    /// Verifies that format uses current UICulture for the culture unaware overload.
    /// </summary>
    [TestMethod]
    public void Format_UsesCurrentUICultureForTheCultureUnawareOverload() {
        var formatter = CreateFormatter();

#pragma warning disable CA1304 // Intentional: this test targets the culture-unaware overload itself.
        formatter.Format("{g, select, male {He} other {They}}", Parameters(("g", "male")))
#pragma warning restore CA1304
            .Should().Be("He");
    }

    /// <summary>
    /// Verifies that format validates arguments.
    /// </summary>
    [TestMethod]
    public void Format_ValidatesArguments() {
        var formatter = CreateFormatter();
        var parameters = Parameters();

        Action nullTemplate = () => formatter.Format(null!, parameters, _english);
        nullTemplate.Should().Throw<ArgumentNullException>();

        Action nullParameters = () => formatter.Format("x", null!, _english);
        nullParameters.Should().Throw<ArgumentNullException>();

        Action nullCulture = () => formatter.Format("x", parameters, null!);
        nullCulture.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async formats icu templates in the requested culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_FormatsIcuTemplatesInTheRequestedCultureAsync() {
        const string template = "{n, plural, zero {Z} one {O} two {T} few {F} many {M} other {X}}";
        var provider = new RecordingProvider(new Dictionary<string, string> { ["ar"] = template });
        var localizer = new DefaultMessageLocalizer(provider, new IcuMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });

        var result = await localizer.LocalizeAsync("key", Parameters(("n", 3)), new CultureInfo("ar"), CancellationToken.None);

        result.Should().Be("F");
    }

    /// <summary>
    /// Verifies that localize async keeps default behavior without the icu formatter.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_KeepsDefaultBehaviorWithoutTheIcuFormatterAsync() {
        const string template = "{n, plural, one {# book} other {# books}}";
        var provider = new RecordingProvider(new Dictionary<string, string> { ["en"] = template });
        var localizer = new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });

        var result = await localizer.LocalizeAsync("key", Parameters(("n", 1)), new CultureInfo("en"), CancellationToken.None);

        result.Should().Be(template);
    }

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class RecordingProvider(Dictionary<string, string> templates) : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            _ = cancellationToken;
            return ValueTask.FromResult(templates.TryGetValue(culture.Name, out var template) ? template : null);
        }
    }
}
