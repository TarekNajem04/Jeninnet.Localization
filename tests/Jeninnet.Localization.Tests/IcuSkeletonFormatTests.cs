//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// ICU (International Components for Unicode) skeleton subset: the named <c>percent</c> style, the
/// documented <c>::</c> number skeletons (percent, currency/XXX, fixed precision),
/// the numeric date / time skeleton mapping (locale order and separators derived
/// from the culture's short-date pattern), pinned vectors across en/de/fr/ar, and
/// the precise diagnostics of every excluded construct. The supported subset is
/// documented in the <c>IcuMessageFormatter</c> XML remarks and the Core README.
/// </summary>
[TestClass]
public sealed class IcuSkeletonFormatTests {
    private static readonly CultureInfo _english = new("en");
    private static readonly CultureInfo _german = new("de");
    private static readonly CultureInfo _french = new("fr");
    private static readonly CultureInfo _arabic = new("ar");

    private static readonly DateTime _moment = new(2026, 3, 4, 14, 30, 15, DateTimeKind.Unspecified);

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

    // ------------------------------------------------------------------ numbers

    /// <summary>
    /// Verifies that format named percent style uses zero fraction digits.
    /// </summary>
    [TestMethod]
    public void Format_NamedPercentStyle_UsesZeroFractionDigits() {
        var formatter = CreateFormatter();
        const decimal ratio = 0.256m;

        formatter.Format("{x, number, percent}", Parameters(("x", ratio)), _english).Should().Be("26%");
        foreach(var culture in new[] { _german, _french, _arabic }) {
            formatter.Format("{x, number, percent}", Parameters(("x", ratio)), culture)
                .Should().Be(ratio.ToString("P0", culture), $"the percent mapping is 'P0' for {culture.Name}");
        }
    }

    /// <summary>
    /// Verifies that format skeleton percent maps to zero fraction digits.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonPercent_MapsToZeroFractionDigits() {
        var formatter = CreateFormatter();
        const decimal ratio = 0.256m;

        formatter.Format("{x, number, ::percent}", Parameters(("x", ratio)), _english).Should().Be("26%");
        formatter.Format("{x, number, ::percent}", Parameters(("x", ratio)), _arabic)
            .Should().Be(ratio.ToString("P0", _arabic));
    }

    /// <summary>
    /// Verifies that format skeleton percent with precision maps to fixed digits.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonPercentWithPrecision_MapsToFixedDigits() {
        var formatter = CreateFormatter();
        const decimal ratio = 0.256m;

        formatter.Format("{x, number, ::percent .00}", Parameters(("x", ratio)), _english).Should().Be("25.60%");
        formatter.Format("{x, number, ::percent .000}", Parameters(("x", ratio)), _french)
            .Should().Be(ratio.ToString("P3", _french));
    }

    /// <summary>
    /// Verifies that format skeleton decimal precision maps to grouped fixed digits.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonDecimalPrecision_MapsToGroupedFixedDigits() {
        var formatter = CreateFormatter();
        const decimal amount = 1234.567m;

        formatter.Format("{x, number, ::.00}", Parameters(("x", amount)), _english).Should().Be("1,234.57");
        formatter.Format("{x, number, ::.00}", Parameters(("x", amount)), _german)
            .Should().Be(amount.ToString("N2", _german));
    }

    /// <summary>
    /// Verifies that format skeleton currency maps to iso symbol with culture conventions.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonCurrency_MapsToIsoSymbolWithCultureConventions() {
        var formatter = CreateFormatter();
        const decimal amount = 1234.567m;

        formatter.Format("{x, number, ::currency/EUR}", Parameters(("x", amount)), _english).Should().Be("€1,234.57");

        var numberFormat = (NumberFormatInfo)_english.NumberFormat.Clone();
        numberFormat.CurrencySymbol = "€";
        formatter.Format("{x, number, ::currency/eur}", Parameters(("x", amount)), _english)
            .Should().Be(amount.ToString("C", numberFormat), "the code is case-insensitive and maps to the ISO symbol");
        formatter.Format("{x, number, ::currency/EUR .00}", Parameters(("x", amount)), _german)
            .Should().Be(amount.ToString("C2", GermanWithSymbol("€")));
        var jpyFormat = (NumberFormatInfo)_english.NumberFormat.Clone();
        jpyFormat.CurrencySymbol = new RegionInfo("ja-JP").CurrencySymbol;
        formatter.Format("{x, number, ::currency/JPY}", Parameters(("x", 1234m)), _english)
            .Should().Be(1234m.ToString("C", jpyFormat), "the ISO symbol for JPY resolves from BCL region data");
    }

    // -------------------------------------------------------------- date / time

    /// <summary>
    /// Verifies that format skeleton date uses locale order and separator.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonDate_UsesLocaleOrderAndSeparator() {
        var formatter = CreateFormatter();

        formatter.Format("{d, date, ::yMd}", Parameters(("d", _moment)), _english).Should().Be("3/4/2026");
        formatter.Format("{d, date, ::yMd}", Parameters(("d", _moment)), _german).Should().Be("4.3.2026");
        formatter.Format("{d, date, ::yMd}", Parameters(("d", _moment)), _french).Should().Be("4/3/2026");
        formatter.Format("{d, date, ::yyMMdd}", Parameters(("d", _moment)), _german)
            .Should().Be("04.03.26", "counts select widths (two-digit) and the locale order still decides");
        formatter.Format("{d, date, ::yMd}", Parameters(("d", _moment)), _arabic)
            .Should().Be(_moment.ToString("d/M/yyyy", _arabic),
                "the derivation carries the locale order and separator; bidi control marks embedded in some patterns are a documented approximation");

        formatter.Format("{d, date, ::dMy}", Parameters(("d", _moment)), _english)
            .Should().Be("3/4/2026", "skeleton field order is canonical: the locale order decides");
        formatter.Format("{d, date, ::Md}", Parameters(("d", _moment)), _german).Should().Be("4.3");
        formatter.Format("{d, date, ::yM}", Parameters(("d", _moment)), _french).Should().Be("3/2026");
        formatter.Format("{d, date, ::y}", Parameters(("d", _moment)), _english).Should().Be("2026");
        formatter.Format("{d, date, ::yy}", Parameters(("d", _moment)), _english).Should().Be("26");
    }

    /// <summary>
    /// Verifies that format skeleton time maps to locale separators and period placement.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonTime_MapsToLocaleSeparatorsAndPeriodPlacement() {
        var formatter = CreateFormatter();

        formatter.Format("{t, time, ::Hm}", Parameters(("t", _moment)), _english).Should().Be("14:30");
        formatter.Format("{t, time, ::Hms}", Parameters(("t", _moment)), _german).Should().Be("14:30:15");
        formatter.Format("{t, time, ::hm}", Parameters(("t", _moment)), _english).Should().Be("2:30");
        formatter.Format("{t, time, ::hma}", Parameters(("t", _moment)), _english)
            .Should().Be(_moment.ToString("h:mm tt", _english), "the day period is placed per the culture's short time pattern");
        formatter.Format("{t, time, ::HmsS}", Parameters(("t", _moment)), _english)
            .Should().Be(_moment.ToString("HH:mm:ss.f", _english));
        formatter.Format("{t, time, ::Hmz}", Parameters(("t", _moment)), _french)
            .Should().Be(_moment.ToString("HH:mm zzz", _french), "the timezone field maps to the offset form");
    }

    // -------------------------------------------------------------- diagnostics

    /// <summary>
    /// Verifies that format skeleton exclusions throw precise diagnostics.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonExclusions_ThrowPreciseDiagnostics() {
        var formatter = CreateFormatter();
        var cases = new (string Template, string Expected)[]
        {
            ("{x, number, ::K}", "compact"),
            ("{x, number, ::KK}", "compact"),
            ("{x, number, ::.##}", "variable precision"),
            ("{x, number, ::percent .0#}", "variable precision"),
            ("{x, number, ::percent percent}", "more than one stem"),
            ("{x, number, ::group-off}", "unsupported number skeleton stem"),
            ("{x, number, ::currency/ZZZ}", "unknown"),
            ("{x, number, ::currency/EU}", "three-letter ISO code"),
            ("{d, date, ::yMMMd}", "text months"),
            ("{d, date, ::MMMM}", "text months"),
            ("{d, date, ::EEEE}", "unsupported date skeleton field"),
            ("{d, date, ::yyyy}", "unsupported date skeleton count"),
            ("{d, date, ::yMdHm}", "unsupported date skeleton field"),
            ("{t, time, ::HHH}", "unsupported time skeleton count"),
            ("{t, time, ::hH}", "both 'H' and 'h'"),
            ("{t, time, ::SSS}", "without an hour field"),
            ("{t, time, ::zzzz}", "long localized timezone names"),
            ("{t, time, ::QQ}", "unsupported time skeleton field"),
        };

        foreach(var (template, expected) in cases) {
            Action act = () => formatter.Format(template, Parameters(("x", 1234.567m), ("d", _moment), ("t", _moment)), _english);
            act.Should().Throw<FormatException>().WithMessage($"*{expected}*", $"template '{template}'");
        }
    }

    /// <summary>
    /// Verifies that format malformed skeletons fail at parse time with offsets.
    /// </summary>
    [TestMethod]
    public void Format_MalformedSkeletons_FailAtParseTimeWithOffsets() {
        var formatter = CreateFormatter();

        Action empty = () => formatter.Format("{x, number, ::}", Parameters(("x", 1m)), _english);
        Action invalidChar = () => formatter.Format("{x, number, ::percent,}", Parameters(("x", 1m)), _english);

        empty.Should().Throw<FormatException>().WithMessage("*Empty '::' skeleton*");
        invalidChar.Should().Throw<FormatException>().WithMessage("*Invalid skeleton character*");
    }

    /// <summary>
    /// Verifies that format skeleton value type mismatches throw.
    /// </summary>
    [TestMethod]
    public void Format_SkeletonValueTypeMismatches_Throw() {
        var formatter = CreateFormatter();

        Action percentText = () => formatter.Format("{x, number, ::percent}", Parameters(("x", "nope")), _english);
        Action dateNumber = () => formatter.Format("{d, date, ::yMd}", Parameters(("d", 5)), _english);
        Action timeNumber = () => formatter.Format("{t, time, ::Hm}", Parameters(("t", 5)), _english);

        percentText.Should().Throw<FormatException>().WithMessage("*numeric*");
        dateNumber.Should().Throw<FormatException>().WithMessage("*date value*");
        timeNumber.Should().Throw<FormatException>().WithMessage("*time value*");
    }

    /// <summary>
    /// Verifies that format unsupported named styles keep their precise diagnostics.
    /// </summary>
    [TestMethod]
    public void Format_UnsupportedNamedStyles_KeepTheirPreciseDiagnostics() {
        var formatter = CreateFormatter();

        Action number = () => formatter.Format("{x, number, skeleton}", Parameters(("x", 1m)), _english);
        Action date = () => formatter.Format("{d, date, skeleton}", Parameters(("d", _moment)), _english);
        Action time = () => formatter.Format("{t, time, skeleton}", Parameters(("t", _moment)), _english);

        number.Should().Throw<FormatException>().WithMessage("*integer, currency, percent*");
        date.Should().Throw<FormatException>().WithMessage("*short, medium, long, full*");
        time.Should().Throw<FormatException>().WithMessage("*short, medium, long, full*");
    }

    /// <summary>
    /// Performs the german with symbol operation.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The german with symbol result.</returns>
    private static CultureInfo GermanWithSymbol(string symbol) {
        var numberFormat = (NumberFormatInfo)_german.NumberFormat.Clone();
        numberFormat.CurrencySymbol = symbol;
        var culture = (CultureInfo)_german.Clone();
        culture.NumberFormat = numberFormat;
        return culture;
    }
}
