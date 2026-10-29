//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Culture-aware CLDR (Common Locale Data Repository) plural-category selection.
/// </summary>
/// <remarks>
/// <para>
/// Hand-ported, test-vector-pinned subset of the Unicode CLDR plural rules
/// (CLDR 48 supplemental data, "Language Plural Rules", Unicode 17;
/// compact-notation operands are out of scope — see below). Pure category selection: no formatting, no I/O.
/// Rule tables are cached static lookups invoked via static delegates with ordinal
/// comparisons (no per-call table allocation); per call allocates one 40-byte
/// <see cref="decimal.GetBits(decimal)"/> array when deriving CLDR operands.
/// Unknown languages deterministically resolve to
/// <see cref="PluralCategory.Other"/>; negative values resolve by magnitude.
/// </para>
/// <para>Covered cardinal languages (TwoLetterISOLanguageName key):</para>
/// <para>
/// <c>en</c>, <c>de</c>, <c>nl</c>, <c>sv</c>, <c>fi</c>, <c>sw</c> (Germanic);
/// <c>da</c> (Danish <c>t</c>-operand); <c>es</c>; <c>it</c>, <c>ca</c>, <c>gl</c>;
/// <c>fr</c>, <c>pt</c>; <c>fa</c>, <c>am</c>, <c>bn</c>, <c>hi</c>, <c>vi</c>,
/// <c>gu</c>; <c>el</c>, <c>eu</c>, <c>tr</c>, <c>hu</c>, <c>bg</c>, <c>nb</c>,
/// <c>nn</c>, <c>no</c>, <c>sq</c>; <c>ar</c>; <c>ru</c>, <c>uk</c>; <c>be</c>;
/// <c>cs</c>, <c>sk</c>; <c>pl</c>; <c>lt</c>; <c>sl</c>; <c>hr</c>, <c>sr</c>;
/// <c>ro</c>; <c>he</c>; <c>mt</c>; <c>ga</c>; <c>cy</c>; <c>gd</c>;
/// <c>ja</c>, <c>zh</c>, <c>ko</c>, <c>th</c>, <c>id</c>, <c>ms</c> (single-form).
/// Ordinals: <c>en</c> only; every other covered language resolves ordinals to
/// <c>other</c>. Known limitations (documented, deferred): region variants that
/// CLDR distinguishes (<c>pt-PT</c> follows Catalan rules, unreachable through
/// the two-letter key); compact-notation operands (<c>c</c>/<c>e</c>) — plain
/// decimals only, except exact-million <c>many</c> branches which are honored;
/// locales outside the table resolve to <c>other</c>.
/// </para>
/// </remarks>
public static class PluralRules {
    private static readonly decimal[] _powersOf10 = [
        1m, 10m, 100m, 1000m, 10000m, 100000m, 1000000m, 10000000m, 100000000m,
        1000000000m, 10000000000m, 100000000000m, 1000000000000m, 10000000000000m,
        100000000000000m, 1000000000000000m, 10000000000000000m, 100000000000000000m,
        1000000000000000000m, 10000000000000000000m, 100000000000000000000m,
        1000000000000000000000m, 10000000000000000000000m, 100000000000000000000000m,
        1000000000000000000000000m, 10000000000000000000000000m, 100000000000000000000000000m,
        1000000000000000000000000000m, 10000000000000000000000000000m
    ];

    private static readonly Dictionary<string, string> _languageAliases = new(StringComparer.Ordinal) {
        ["ars"] = "ar",
        ["iw"] = "he",
        ["mo"] = "ro"
    };

    /// <summary>
    /// The CLDR <c>one</c> rule of the integer-one languages (<c>i = 1 and v = 0</c>).
    /// </summary>
    /// <param name="o">The plural operands.</param>
    /// <returns>The plural category.</returns>
    private static PluralCategory IntegerOneCardinal(PluralOperands o) =>
        o.I == 1 && o.V == 0 ? PluralCategory.One : PluralCategory.Other;

    private static readonly Dictionary<string, Func<PluralOperands, PluralCategory>> _cardinalRules = new(StringComparer.Ordinal) {
        ["en"] = IntegerOneCardinal,
        ["de"] = IntegerOneCardinal,
        ["nl"] = IntegerOneCardinal,
        ["sv"] = IntegerOneCardinal,
        ["fi"] = IntegerOneCardinal,
        ["sw"] = IntegerOneCardinal,
        ["da"] = DanishCardinal,
        ["es"] = SpanishCardinal,
        ["it"] = ItalianCardinal,
        ["ca"] = ItalianCardinal,
        ["gl"] = ItalianCardinal,
        ["fr"] = FrenchCardinal,
        ["pt"] = FrenchCardinal,
        ["fa"] = FaCardinal,
        ["am"] = FaCardinal,
        ["bn"] = FaCardinal,
        ["hi"] = FaCardinal,
        ["vi"] = FaCardinal,
        ["gu"] = FaCardinal,
        ["el"] = NEqualsOneCardinal,
        ["eu"] = NEqualsOneCardinal,
        ["tr"] = NEqualsOneCardinal,
        ["hu"] = NEqualsOneCardinal,
        ["bg"] = NEqualsOneCardinal,
        ["nb"] = NEqualsOneCardinal,
        ["nn"] = NEqualsOneCardinal,
        ["no"] = NEqualsOneCardinal,
        ["sq"] = NEqualsOneCardinal,
        ["ar"] = ArabicCardinal,
        ["ru"] = RussianCardinal,
        ["uk"] = RussianCardinal,
        ["be"] = BelarusCardinal,
        ["cs"] = CzechCardinal,
        ["sk"] = CzechCardinal,
        ["pl"] = PolishCardinal,
        ["lt"] = LithuanianCardinal,
        ["sl"] = SlovenianCardinal,
        ["hr"] = SerboCroatianCardinal,
        ["sr"] = SerboCroatianCardinal,
        ["ro"] = RomanianCardinal,
        ["he"] = HebrewCardinal,
        ["mt"] = MalteseCardinal,
        ["ga"] = IrishCardinal,
        ["cy"] = WelshCardinal,
        ["gd"] = GaelicCardinal,
        ["ja"] = static _ => PluralCategory.Other,
        ["zh"] = static _ => PluralCategory.Other,
        ["ko"] = static _ => PluralCategory.Other,
        ["th"] = static _ => PluralCategory.Other,
        ["id"] = static _ => PluralCategory.Other,
        ["ms"] = static _ => PluralCategory.Other
    };

    private static readonly Dictionary<string, Func<PluralOperands, PluralCategory>> _ordinalRules = new(StringComparer.Ordinal) {
        ["en"] = EnglishOrdinal
    };

    /// <summary>
    /// Selects the cardinal plural category for <paramref name="value"/> in <paramref name="culture"/>.
    /// </summary>
    /// <param name="culture">The culture whose rules apply (matched by two-letter language code).</param>
    /// <param name="value">The numeric value; the sign is ignored (magnitude only).</param>
    /// <returns>The plural category; <see cref="PluralCategory.Other"/> for uncovered languages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    public static PluralCategory GetCardinalCategory(CultureInfo culture, decimal value) {
        ArgumentNullException.ThrowIfNull(culture);

        var operands = CreateOperands(value);
        if(_cardinalRules.TryGetValue(NormalizeLanguage(culture), out var rule)) {
            return rule(operands);
        }

        return PluralCategory.Other;
    }

    /// <summary>
    /// Selects the ordinal plural category for <paramref name="value"/> in <paramref name="culture"/>.
    /// </summary>
    /// <param name="culture">The culture whose rules apply (matched by two-letter language code).</param>
    /// <param name="value">The numeric value; the sign is ignored (magnitude only).</param>
    /// <returns>The plural category; <see cref="PluralCategory.Other"/> for uncovered languages.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    public static PluralCategory GetOrdinalCategory(CultureInfo culture, decimal value) {
        ArgumentNullException.ThrowIfNull(culture);

        var operands = CreateOperands(value);
        if(_ordinalRules.TryGetValue(NormalizeLanguage(culture), out var rule)) {
            return rule(operands);
        }

        return PluralCategory.Other;
    }

    /// <summary>
    /// Normalizes language.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <returns>The normalize language text.</returns>
    private static string NormalizeLanguage(CultureInfo culture) {
        var language = culture.TwoLetterISOLanguageName;
        return _languageAliases.TryGetValue(language, out var aliased) ? aliased : language;
    }

    /// <summary>
    /// Represents plural operands.
    /// </summary>
    /// <param name="N">The n.</param>
    /// <param name="I">The i.</param>
    /// <param name="V">The v.</param>
    /// <param name="F">The f.</param>
    /// <param name="T">The t.</param>
    private readonly record struct PluralOperands(decimal N, decimal I, int V, decimal F, decimal T);

    /// <summary>
    /// Determines whether <paramref name="value"/> has no fractional part.
    /// Integer ranges in CLDR conditions match integral values only (fraction
    /// digits all zeros); singletons compare numerically. This reproduces every
    /// integer/decimal sample in the CLDR data (e.g. Belarusian <c>2.0</c> is
    /// <c>few</c> while <c>2.5</c> is <c>other</c>).
    /// </summary>
    /// <param name="value">The numeric operand to test.</param>
    /// <returns><see langword="true"/> when the value has no fractional part.</returns>
    private static bool IsIntegral(decimal value) => decimal.Truncate(value) == value;

    /// <summary>
    /// Determines whether <paramref name="value"/> is integral and within
    /// [<paramref name="low"/>, <paramref name="high"/>].
    /// </summary>
    /// <param name="value">The numeric operand to test.</param>
    /// <param name="low">The inclusive lower bound.</param>
    /// <param name="high">The inclusive upper bound.</param>
    /// <returns><see langword="true"/> when the value is integral and within the bounds.</returns>
    private static bool InRange(decimal value, decimal low, decimal high) =>
        IsIntegral(value) && value >= low && value <= high;

    /// <summary>
    /// Creates operands.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The operands result.</returns>
    private static PluralOperands CreateOperands(decimal value) {
        var n = decimal.Abs(value);
        var i = decimal.Truncate(n);
        var scale = (decimal.GetBits(n)[3] >> 16) & 0xFF;
        var f = (n - i) * _powersOf10[scale];
        var t = f;
        while(t != 0 && t % 10 == 0) {
            t /= 10;
        }

        return new PluralOperands(n, i, scale, f, t);
    }

    /// <summary>
    /// Performs the danish cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The danish cardinal result.</returns>
    private static PluralCategory DanishCardinal(PluralOperands o) =>
        o.N == 1 || (o.T != 0 && (o.I == 0 || o.I == 1)) ? PluralCategory.One : PluralCategory.Other;

    /// <summary>
    /// Performs the spanish cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The spanish cardinal result.</returns>
    private static PluralCategory SpanishCardinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        return o.I != 0 && o.I % 1000000 == 0 && o.V == 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the italian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The italian cardinal result.</returns>
    private static PluralCategory ItalianCardinal(PluralOperands o) {
        if(o.I == 1 && o.V == 0) {
            return PluralCategory.One;
        }

        return o.I != 0 && o.I % 1000000 == 0 && o.V == 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the french cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The french cardinal result.</returns>
    private static PluralCategory FrenchCardinal(PluralOperands o) {
        if(o.I is 0 or 1) {
            return PluralCategory.One;
        }

        return o.I != 0 && o.I % 1000000 == 0 && o.V == 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the fa cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The fa cardinal result.</returns>
    private static PluralCategory FaCardinal(PluralOperands o) =>
        o.I == 0 || o.N == 1 ? PluralCategory.One : PluralCategory.Other;

    /// <summary>
    /// Performs the NEquals one cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The NEquals one cardinal result.</returns>
    private static PluralCategory NEqualsOneCardinal(PluralOperands o) =>
        o.N == 1 ? PluralCategory.One : PluralCategory.Other;

    /// <summary>
    /// Performs the arabic cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The arabic cardinal result.</returns>
    private static PluralCategory ArabicCardinal(PluralOperands o) {
        if(o.N == 0) {
            return PluralCategory.Zero;
        }

        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        var mod100 = o.N % 100;
        if(InRange(mod100, 3, 10)) {
            return PluralCategory.Few;
        }

        return InRange(mod100, 11, 99) ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the russian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The russian cardinal result.</returns>
    private static PluralCategory RussianCardinal(PluralOperands o) {
        if(o.V != 0) {
            return PluralCategory.Other;
        }

        var mod10 = o.I % 10;
        var mod100 = o.I % 100;
        if(mod10 == 1 && mod100 != 11) {
            return PluralCategory.One;
        }

        if(mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
            return PluralCategory.Few;
        }

        return mod10 == 0 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 11 && mod100 <= 14)
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the belarus cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The belarus cardinal result.</returns>
    private static PluralCategory BelarusCardinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        if(mod10 == 1 && mod100 != 11) {
            return PluralCategory.One;
        }

        if(InRange(mod10, 2, 4) && (mod100 < 12 || mod100 > 14)) {
            return PluralCategory.Few;
        }

        return mod10 == 0 || InRange(mod10, 5, 9) || InRange(mod100, 11, 14)
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the czech cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The czech cardinal result.</returns>
    private static PluralCategory CzechCardinal(PluralOperands o) {
        if(o.I == 1 && o.V == 0) {
            return PluralCategory.One;
        }

        if(o.I >= 2 && o.I <= 4 && o.V == 0) {
            return PluralCategory.Few;
        }

        return o.V != 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the polish cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The polish cardinal result.</returns>
    private static PluralCategory PolishCardinal(PluralOperands o) {
        if(o.I == 1 && o.V == 0) {
            return PluralCategory.One;
        }

        if(o.V != 0) {
            return PluralCategory.Other;
        }

        var mod10 = o.I % 10;
        var mod100 = o.I % 100;
        if(mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
            return PluralCategory.Few;
        }

        return o.I != 1 && (mod10 <= 1 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 12 && mod100 <= 14))
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the lithuanian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The lithuanian cardinal result.</returns>
    private static PluralCategory LithuanianCardinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        if(mod10 == 1 && (mod100 < 11 || mod100 > 19)) {
            return PluralCategory.One;
        }

        if(InRange(mod10, 2, 9) && (mod100 < 11 || mod100 > 19)) {
            return PluralCategory.Few;
        }

        return o.F != 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the slovenian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The slovenian cardinal result.</returns>
    private static PluralCategory SlovenianCardinal(PluralOperands o) {
        if(o.V == 0 && o.I % 100 == 1) {
            return PluralCategory.One;
        }

        if(o.V == 0 && o.I % 100 == 2) {
            return PluralCategory.Two;
        }

        return (o.V == 0 && o.I % 100 >= 3 && o.I % 100 <= 4) || o.V != 0
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the serbo croatian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The serbo croatian cardinal result.</returns>
    private static PluralCategory SerboCroatianCardinal(PluralOperands o) {
        if((o.V == 0 && o.I % 10 == 1 && o.I % 100 != 11) || (o.F % 10 == 1 && o.F % 100 != 11)) {
            return PluralCategory.One;
        }

        if((o.V == 0 && o.I % 10 >= 2 && o.I % 10 <= 4 && (o.I % 100 < 12 || o.I % 100 > 14))
            || (o.F % 10 >= 2 && o.F % 10 <= 4 && (o.F % 100 < 12 || o.F % 100 > 14))) {
            return PluralCategory.Few;
        }

        return PluralCategory.Other;
    }

    /// <summary>
    /// Performs the romanian cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The romanian cardinal result.</returns>
    private static PluralCategory RomanianCardinal(PluralOperands o) {
        if(o.I == 1 && o.V == 0) {
            return PluralCategory.One;
        }

        var mod100 = o.N % 100;
        return o.V != 0 || o.N == 0 || (o.N != 1 && InRange(mod100, 1, 19))
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the hebrew cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The hebrew cardinal result.</returns>
    private static PluralCategory HebrewCardinal(PluralOperands o) {
        if((o.I == 1 && o.V == 0) || (o.I == 0 && o.V != 0)) {
            return PluralCategory.One;
        }

        return o.I == 2 && o.V == 0 ? PluralCategory.Two : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the maltese cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The maltese cardinal result.</returns>
    private static PluralCategory MalteseCardinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        var mod100 = o.N % 100;
        if(o.N == 0 || InRange(mod100, 3, 10)) {
            return PluralCategory.Few;
        }

        return InRange(mod100, 11, 19) ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the irish cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The irish cardinal result.</returns>
    private static PluralCategory IrishCardinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        if(InRange(o.N, 3, 6)) {
            return PluralCategory.Few;
        }

        return InRange(o.N, 7, 10) ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the welsh cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The welsh cardinal result.</returns>
    private static PluralCategory WelshCardinal(PluralOperands o) {
        if(o.N == 0) {
            return PluralCategory.Zero;
        }

        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        if(o.N == 3) {
            return PluralCategory.Few;
        }

        return o.N == 6 ? PluralCategory.Many : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the gaelic cardinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The gaelic cardinal result.</returns>
    private static PluralCategory GaelicCardinal(PluralOperands o) {
        if(o.N is 1 or 11) {
            return PluralCategory.One;
        }

        if(o.N is 2 or 12) {
            return PluralCategory.Two;
        }

        return InRange(o.N, 3, 10) || InRange(o.N, 13, 19)
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    /// <summary>
    /// Performs the english ordinal operation.
    /// </summary>
    /// <param name="o">The o.</param>
    /// <returns>The english ordinal result.</returns>
    private static PluralCategory EnglishOrdinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        if(mod10 == 1 && mod100 != 11) {
            return PluralCategory.One;
        }

        if(mod10 == 2 && mod100 != 12) {
            return PluralCategory.Two;
        }

        return mod10 == 3 && mod100 != 13 ? PluralCategory.Few : PluralCategory.Other;
    }
}
