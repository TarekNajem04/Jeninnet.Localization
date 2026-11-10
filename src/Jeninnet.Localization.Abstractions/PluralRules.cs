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
/// Hand-ported, test-vector-pinned full coverage of the Unicode CLDR plural rules
/// (CLDR 48 supplemental data, "Language Plural Rules", Unicode 17;
/// compact-notation operands <c>c</c>/<c>e</c> are zero for plain decimals — see below).
/// Pure category selection: no formatting, no I/O.
/// Rule tables are cached static lookups invoked via static delegates with ordinal
/// comparisons (no per-call table allocation); per call allocates one 40-byte
/// <see cref="decimal.GetBits(decimal)"/> array when deriving CLDR operands.
/// Unknown languages deterministically resolve to
/// <see cref="PluralCategory.Other"/>; negative values resolve by magnitude.
/// </para>
/// <para>Covered cardinal languages (keyed by <see cref="CultureInfo.TwoLetterISOLanguageName"/>,
/// plus <c>pt-PT</c> matched by full name): Germanic, Romance, Slavic, Baltic, Celtic,
/// Semitic, Indic, and single-form families — the full CLDR 48 cardinal table, including
/// region handling for <c>pt-PT</c> (Catalan-like) and the <c>in</c>/<c>id</c>, <c>iw</c>/<c>he</c>,
/// <c>mo</c>/<c>ro</c>, <c>ars</c>/<c>ar</c> aliases.
/// Ordinals: full CLDR 48 ordinal table (English, Swedish, Spanish, French-family single-one,
/// Hungarian, Nepali, Afrikaans/Belarusian/Ukrainian/Turkmen few, Kazakh/Italian-family many,
/// Georgian, Albanian, Cornish, Anii, Bulgarian, Konkani/Marathi, Gaelic, Catalan, Macedonian,
/// Azerbaijani, Gujarati/Hindi, Bengali/Assamese, Oriya, Welsh); every other language resolves
/// ordinals to <c>other</c>. Known limitations (documented, deferred): compact-notation operands
/// (<c>c</c>/<c>e</c>) are zero for plain <see cref="decimal"/> inputs — only exact-million
/// <c>many</c> branches are honored; locales outside the CLDR table resolve to <c>other</c>.
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
        ["mo"] = "ro",
        ["in"] = "id"
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
        ["ast"] = IntegerOneCardinal,
        ["et"] = IntegerOneCardinal,
        ["fy"] = IntegerOneCardinal,
        ["ia"] = IntegerOneCardinal,
        ["ie"] = IntegerOneCardinal,
        ["io"] = IntegerOneCardinal,
        ["ji"] = IntegerOneCardinal,
        ["lij"] = IntegerOneCardinal,
        ["sc"] = IntegerOneCardinal,
        ["ur"] = IntegerOneCardinal,
        ["yi"] = IntegerOneCardinal,
        ["da"] = DanishCardinal,
        ["es"] = SpanishCardinal,
        ["it"] = ItalianCardinal,
        ["ca"] = ItalianCardinal,
        ["gl"] = ItalianCardinal,
        ["lld"] = ItalianCardinal,
        ["scn"] = ItalianCardinal,
        ["vec"] = ItalianCardinal,
        ["fr"] = FrenchCardinal,
        ["pt"] = FrenchCardinal,
        ["fa"] = FaCardinal,
        ["am"] = FaCardinal,
        ["bn"] = FaCardinal,
        ["hi"] = FaCardinal,
        ["vi"] = FaCardinal,
        ["gu"] = FaCardinal,
        ["as"] = FaCardinal,
        ["doi"] = FaCardinal,
        ["kn"] = FaCardinal,
        ["kok"] = FaCardinal,
        ["pcm"] = FaCardinal,
        ["tg"] = FaCardinal,
        ["zu"] = FaCardinal,
        ["el"] = NEqualsOneCardinal,
        ["eu"] = NEqualsOneCardinal,
        ["tr"] = NEqualsOneCardinal,
        ["hu"] = NEqualsOneCardinal,
        ["bg"] = NEqualsOneCardinal,
        ["nb"] = NEqualsOneCardinal,
        ["nn"] = NEqualsOneCardinal,
        ["no"] = NEqualsOneCardinal,
        ["sq"] = NEqualsOneCardinal,
        ["af"] = NEqualsOneCardinal,
        ["an"] = NEqualsOneCardinal,
        ["asa"] = NEqualsOneCardinal,
        ["az"] = NEqualsOneCardinal,
        ["bal"] = NEqualsOneCardinal,
        ["bem"] = NEqualsOneCardinal,
        ["bez"] = NEqualsOneCardinal,
        ["brx"] = NEqualsOneCardinal,
        ["ce"] = NEqualsOneCardinal,
        ["cgg"] = NEqualsOneCardinal,
        ["chr"] = NEqualsOneCardinal,
        ["ckb"] = NEqualsOneCardinal,
        ["dv"] = NEqualsOneCardinal,
        ["ee"] = NEqualsOneCardinal,
        ["eo"] = NEqualsOneCardinal,
        ["fo"] = NEqualsOneCardinal,
        ["fur"] = NEqualsOneCardinal,
        ["gsw"] = NEqualsOneCardinal,
        ["ha"] = NEqualsOneCardinal,
        ["haw"] = NEqualsOneCardinal,
        ["jgo"] = NEqualsOneCardinal,
        ["jmc"] = NEqualsOneCardinal,
        ["ka"] = NEqualsOneCardinal,
        ["kaj"] = NEqualsOneCardinal,
        ["kcg"] = NEqualsOneCardinal,
        ["kk"] = NEqualsOneCardinal,
        ["kkj"] = NEqualsOneCardinal,
        ["kl"] = NEqualsOneCardinal,
        ["ks"] = NEqualsOneCardinal,
        ["ksb"] = NEqualsOneCardinal,
        ["ku"] = NEqualsOneCardinal,
        ["ky"] = NEqualsOneCardinal,
        ["lb"] = NEqualsOneCardinal,
        ["lg"] = NEqualsOneCardinal,
        ["mas"] = NEqualsOneCardinal,
        ["mgo"] = NEqualsOneCardinal,
        ["ml"] = NEqualsOneCardinal,
        ["mn"] = NEqualsOneCardinal,
        ["mr"] = NEqualsOneCardinal,
        ["nah"] = NEqualsOneCardinal,
        ["nd"] = NEqualsOneCardinal,
        ["ne"] = NEqualsOneCardinal,
        ["nnh"] = NEqualsOneCardinal,
        ["nr"] = NEqualsOneCardinal,
        ["ny"] = NEqualsOneCardinal,
        ["nyn"] = NEqualsOneCardinal,
        ["om"] = NEqualsOneCardinal,
        ["or"] = NEqualsOneCardinal,
        ["os"] = NEqualsOneCardinal,
        ["pap"] = NEqualsOneCardinal,
        ["ps"] = NEqualsOneCardinal,
        ["rm"] = NEqualsOneCardinal,
        ["rof"] = NEqualsOneCardinal,
        ["rwk"] = NEqualsOneCardinal,
        ["saq"] = NEqualsOneCardinal,
        ["sd"] = NEqualsOneCardinal,
        ["sdh"] = NEqualsOneCardinal,
        ["seh"] = NEqualsOneCardinal,
        ["sn"] = NEqualsOneCardinal,
        ["so"] = NEqualsOneCardinal,
        ["ss"] = NEqualsOneCardinal,
        ["ssy"] = NEqualsOneCardinal,
        ["st"] = NEqualsOneCardinal,
        ["syr"] = NEqualsOneCardinal,
        ["ta"] = NEqualsOneCardinal,
        ["te"] = NEqualsOneCardinal,
        ["teo"] = NEqualsOneCardinal,
        ["tig"] = NEqualsOneCardinal,
        ["tk"] = NEqualsOneCardinal,
        ["tn"] = NEqualsOneCardinal,
        ["ts"] = NEqualsOneCardinal,
        ["ug"] = NEqualsOneCardinal,
        ["uz"] = NEqualsOneCardinal,
        ["ve"] = NEqualsOneCardinal,
        ["vo"] = NEqualsOneCardinal,
        ["vun"] = NEqualsOneCardinal,
        ["wae"] = NEqualsOneCardinal,
        ["xh"] = NEqualsOneCardinal,
        ["xog"] = NEqualsOneCardinal,
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
        ["bs"] = SerboCroatianCardinal,
        ["sh"] = SerboCroatianCardinal,
        ["ro"] = RomanianCardinal,
        ["he"] = HebrewCardinal,
        ["mt"] = MalteseCardinal,
        ["ga"] = IrishCardinal,
        ["cy"] = WelshCardinal,
        ["gd"] = GaelicCardinal,
        ["ff"] = FfHyKabCardinal,
        ["hy"] = FfHyKabCardinal,
        ["kab"] = FfHyKabCardinal,
        ["si"] = SinhalaCardinal,
        ["ak"] = AkBroadCardinal,
        ["bho"] = AkBroadCardinal,
        ["csw"] = AkBroadCardinal,
        ["guw"] = AkBroadCardinal,
        ["ln"] = AkBroadCardinal,
        ["mg"] = AkBroadCardinal,
        ["nso"] = AkBroadCardinal,
        ["pa"] = AkBroadCardinal,
        ["ti"] = AkBroadCardinal,
        ["wa"] = AkBroadCardinal,
        ["tzm"] = TzmCardinal,
        ["is"] = IcelandicCardinal,
        ["mk"] = MacedonianCardinal,
        ["ceb"] = FilipinoCardinal,
        ["fil"] = FilipinoCardinal,
        ["tl"] = FilipinoCardinal,
        ["lv"] = LatvianCardinal,
        ["prg"] = LatvianCardinal,
        ["lag"] = LagCardinal,
        ["blo"] = BloCvKshCardinal,
        ["cv"] = BloCvKshCardinal,
        ["ksh"] = BloCvKshCardinal,
        ["iu"] = IuNaqCardinal,
        ["naq"] = IuNaqCardinal,
        ["sat"] = IuNaqCardinal,
        ["se"] = IuNaqCardinal,
        ["sma"] = IuNaqCardinal,
        ["smi"] = IuNaqCardinal,
        ["smj"] = IuNaqCardinal,
        ["smn"] = IuNaqCardinal,
        ["sms"] = IuNaqCardinal,
        ["shi"] = ShiCardinal,
        ["dsb"] = DsbHsbCardinal,
        ["hsb"] = DsbHsbCardinal,
        ["sgs"] = SgsCardinal,
        ["br"] = BretonCardinal,
        ["gv"] = ManxCardinal,
        ["kw"] = CornishCardinal,
        ["bm"] = static _ => PluralCategory.Other,
        ["bo"] = static _ => PluralCategory.Other,
        ["dz"] = static _ => PluralCategory.Other,
        ["hnj"] = static _ => PluralCategory.Other,
        ["ig"] = static _ => PluralCategory.Other,
        ["ii"] = static _ => PluralCategory.Other,
        ["jbo"] = static _ => PluralCategory.Other,
        ["jv"] = static _ => PluralCategory.Other,
        ["jw"] = static _ => PluralCategory.Other,
        ["kde"] = static _ => PluralCategory.Other,
        ["kea"] = static _ => PluralCategory.Other,
        ["km"] = static _ => PluralCategory.Other,
        ["lkt"] = static _ => PluralCategory.Other,
        ["lo"] = static _ => PluralCategory.Other,
        ["my"] = static _ => PluralCategory.Other,
        ["nqo"] = static _ => PluralCategory.Other,
        ["osa"] = static _ => PluralCategory.Other,
        ["root"] = static _ => PluralCategory.Other,
        ["sah"] = static _ => PluralCategory.Other,
        ["ses"] = static _ => PluralCategory.Other,
        ["sg"] = static _ => PluralCategory.Other,
        ["su"] = static _ => PluralCategory.Other,
        ["to"] = static _ => PluralCategory.Other,
        ["tpi"] = static _ => PluralCategory.Other,
        ["wo"] = static _ => PluralCategory.Other,
        ["yo"] = static _ => PluralCategory.Other,
        ["yue"] = static _ => PluralCategory.Other,
        ["ja"] = static _ => PluralCategory.Other,
        ["zh"] = static _ => PluralCategory.Other,
        ["ko"] = static _ => PluralCategory.Other,
        ["th"] = static _ => PluralCategory.Other,
        ["id"] = static _ => PluralCategory.Other,
        ["ms"] = static _ => PluralCategory.Other
    };

    private static readonly Dictionary<string, Func<PluralOperands, PluralCategory>> _ordinalRules = new(StringComparer.Ordinal) {
        ["en"] = EnglishOrdinal,
        ["sv"] = SwedishOrdinal,
        ["es"] = SpanishOrdinal,
        ["bal"] = SingleOneOrdinal,
        ["fil"] = SingleOneOrdinal,
        ["fr"] = SingleOneOrdinal,
        ["ga"] = SingleOneOrdinal,
        ["hy"] = SingleOneOrdinal,
        ["lo"] = SingleOneOrdinal,
        ["ms"] = SingleOneOrdinal,
        ["ro"] = SingleOneOrdinal,
        ["tl"] = SingleOneOrdinal,
        ["vi"] = SingleOneOrdinal,
        ["hu"] = HungarianOrdinal,
        ["ne"] = NepaliOrdinal,
        ["af"] = AfrikaansOrdinal,
        ["be"] = BelarusOrdinal,
        ["uk"] = UkrainianOrdinal,
        ["tk"] = TurkmenOrdinal,
        ["kk"] = KazakhOrdinal,
        ["it"] = ItalianOrdinalMany,
        ["lld"] = ItalianOrdinalMany,
        ["sc"] = ItalianOrdinalMany,
        ["vec"] = ItalianOrdinalMany,
        ["lij"] = LijScnOrdinal,
        ["scn"] = LijScnOrdinal,
        ["ka"] = GeorgianOrdinal,
        ["sq"] = AlbanianOrdinal,
        ["kw"] = CornishOrdinal,
        ["blo"] = BloOrdinal,
        ["bg"] = BulgarianOrdinal,
        ["kok"] = KokMrOrdinal,
        ["mr"] = KokMrOrdinal,
        ["gd"] = GaelicOrdinal,
        ["ca"] = CatalanOrdinal,
        ["mk"] = MacedonianOrdinal,
        ["az"] = AzerbaijaniOrdinal,
        ["gu"] = GuHiOrdinal,
        ["hi"] = GuHiOrdinal,
        ["as"] = AsBnOrdinal,
        ["bn"] = AsBnOrdinal,
        ["or"] = OriyaOrdinal,
        ["cy"] = WelshOrdinal
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
        if(IsPortuguesePortugal(culture)) {
            return ItalianCardinal(operands);
        }

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

    private static bool IsPortuguesePortugal(CultureInfo culture) =>
        culture.Name.StartsWith("pt-PT", StringComparison.OrdinalIgnoreCase);

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
    /// Performs the ff-hy-kab cardinal operation (one when the integer part is 0 or 1).
    /// </summary>
    private static PluralCategory FfHyKabCardinal(PluralOperands o) =>
        o.I == 0 || o.I == 1 ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory SinhalaCardinal(PluralOperands o) =>
        o.N == 0 || o.N == 1 || (o.I == 0 && o.F == 1) ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory AkBroadCardinal(PluralOperands o) =>
        InRange(o.N, 0, 1) ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory TzmCardinal(PluralOperands o) =>
        InRange(o.N, 0, 1) || InRange(o.N, 11, 99) ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory IcelandicCardinal(PluralOperands o) {
        if(o.T == 0 && o.I % 10 == 1 && o.I % 100 != 11) {
            return PluralCategory.One;
        }

        return o.T % 10 == 1 && o.T % 100 != 11 ? PluralCategory.One : PluralCategory.Other;
    }

    private static PluralCategory MacedonianCardinal(PluralOperands o) {
        if(o.V == 0 && o.I % 10 == 1 && o.I % 100 != 11) {
            return PluralCategory.One;
        }

        return o.F % 10 == 1 && o.F % 100 != 11 ? PluralCategory.One : PluralCategory.Other;
    }

    private static PluralCategory FilipinoCardinal(PluralOperands o) {
        if(o.V == 0 && (o.I == 1 || o.I == 2 || o.I == 3)) {
            return PluralCategory.One;
        }

        if(o.V == 0 && o.I % 10 != 4 && o.I % 10 != 6 && o.I % 10 != 9) {
            return PluralCategory.One;
        }

        return o.V != 0 && o.F % 10 != 4 && o.F % 10 != 6 && o.F % 10 != 9
            ? PluralCategory.One
            : PluralCategory.Other;
    }

    private static PluralCategory LatvianCardinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        if(mod10 == 0 || InRange(mod100, 11, 19) || (o.V == 2 && InRange(o.F % 100, 11, 19))) {
            return PluralCategory.Zero;
        }

        if((mod10 == 1 && mod100 != 11)
            || (o.V == 2 && o.F % 10 == 1 && o.F % 100 != 11)
            || (o.V != 2 && o.F % 10 == 1)) {
            return PluralCategory.One;
        }

        return PluralCategory.Other;
    }

    private static PluralCategory LagCardinal(PluralOperands o) {
        if(o.N == 0) {
            return PluralCategory.Zero;
        }

        return o.I == 0 || o.I == 1 ? PluralCategory.One : PluralCategory.Other;
    }

    private static PluralCategory BloCvKshCardinal(PluralOperands o) {
        if(o.N == 0) {
            return PluralCategory.Zero;
        }

        return o.N == 1 ? PluralCategory.One : PluralCategory.Other;
    }

    private static PluralCategory IuNaqCardinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        return o.N == 2 ? PluralCategory.Two : PluralCategory.Other;
    }

    private static PluralCategory ShiCardinal(PluralOperands o) {
        if(o.I == 0 || o.N == 1) {
            return PluralCategory.One;
        }

        return InRange(o.N, 2, 10) ? PluralCategory.Few : PluralCategory.Other;
    }

    private static PluralCategory DsbHsbCardinal(PluralOperands o) {
        if((o.V == 0 && o.I % 100 == 1) || o.F % 100 == 1) {
            return PluralCategory.One;
        }

        if((o.V == 0 && o.I % 100 == 2) || o.F % 100 == 2) {
            return PluralCategory.Two;
        }

        return (o.V == 0 && InRange(o.I % 100, 3, 4)) || InRange(o.F % 100, 3, 4)
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    private static PluralCategory SgsCardinal(PluralOperands o) {
        if(o.N % 10 == 1 && o.N % 100 != 11) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        if(o.N != 2 && InRange(o.N % 10, 2, 9) && (o.N % 100 < 11 || o.N % 100 > 19)) {
            return PluralCategory.Few;
        }

        return o.F != 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory BretonCardinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        if(mod10 == 1 && mod100 != 11 && mod100 != 71 && mod100 != 91) {
            return PluralCategory.One;
        }

        if(mod10 == 2 && mod100 != 12 && mod100 != 72 && mod100 != 92) {
            return PluralCategory.Two;
        }

        if((InRange(mod10, 3, 4) || mod10 == 9)
            && !InRange(mod100, 10, 19) && !InRange(mod100, 70, 79) && !InRange(mod100, 90, 99)) {
            return PluralCategory.Few;
        }

        return o.N != 0 && o.N % 1000000 == 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory ManxCardinal(PluralOperands o) {
        if(o.V == 0 && o.I % 10 == 1) {
            return PluralCategory.One;
        }

        if(o.V == 0 && o.I % 10 == 2) {
            return PluralCategory.Two;
        }

        if(o.V == 0 && (o.I % 100 == 0 || o.I % 100 == 20 || o.I % 100 == 40 || o.I % 100 == 60 || o.I % 100 == 80)) {
            return PluralCategory.Few;
        }

        return o.V != 0 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory CornishCardinal(PluralOperands o) {
        if(o.N == 0) {
            return PluralCategory.Zero;
        }

        if(o.N == 1) {
            return PluralCategory.One;
        }

        var mod100 = o.N % 100;
        var mod1000 = o.N % 1000;
        var mod100000 = o.N % 100000;
        if(mod100 == 2 || mod100 == 22 || mod100 == 42 || mod100 == 62 || mod100 == 82
            || (mod1000 == 0 && (InRange(mod100000, 1000, 20000) || mod100000 == 40000 || mod100000 == 60000 || mod100000 == 80000))
            || (o.N != 0 && o.N % 1000000 == 100000)) {
            return PluralCategory.Two;
        }

        if(mod100 == 3 || mod100 == 23 || mod100 == 43 || mod100 == 63 || mod100 == 83) {
            return PluralCategory.Few;
        }

        return o.N != 1 && (mod100 == 1 || mod100 == 21 || mod100 == 41 || mod100 == 61 || mod100 == 81)
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    private static PluralCategory SingleOneOrdinal(PluralOperands o) =>
        o.N == 1 ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory SwedishOrdinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        return (mod10 == 1 || mod10 == 2) && mod100 != 11 && mod100 != 12
            ? PluralCategory.One
            : PluralCategory.Other;
    }

    private static PluralCategory SpanishOrdinal(PluralOperands o) {
        var mod10 = o.N % 10;
        return (mod10 == 1 || mod10 == 3) && o.N % 100 != 11
            ? PluralCategory.One
            : PluralCategory.Other;
    }

    private static PluralCategory HungarianOrdinal(PluralOperands o) =>
        o.N == 1 || o.N == 5 ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory NepaliOrdinal(PluralOperands o) =>
        InRange(o.N, 1, 4) ? PluralCategory.One : PluralCategory.Other;

    private static PluralCategory AfrikaansOrdinal(PluralOperands o) =>
        InRange(o.I % 100, 2, 19) ? PluralCategory.Few : PluralCategory.Other;

    private static PluralCategory BelarusOrdinal(PluralOperands o) {
        var mod10 = o.N % 10;
        var mod100 = o.N % 100;
        return (mod10 == 2 || mod10 == 3) && mod100 != 12 && mod100 != 13
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    private static PluralCategory UkrainianOrdinal(PluralOperands o) =>
        o.N % 10 == 3 && o.N % 100 != 13 ? PluralCategory.Few : PluralCategory.Other;

    private static PluralCategory TurkmenOrdinal(PluralOperands o) =>
        o.N % 10 == 6 || o.N % 10 == 9 || o.N == 10 ? PluralCategory.Few : PluralCategory.Other;

    private static PluralCategory KazakhOrdinal(PluralOperands o) =>
        o.N % 10 == 6 || o.N % 10 == 9 || (o.N % 10 == 0 && o.N != 0)
            ? PluralCategory.Many
            : PluralCategory.Other;

    private static PluralCategory ItalianOrdinalMany(PluralOperands o) =>
        o.N == 8 || o.N == 11 || o.N == 80 || o.N == 800 ? PluralCategory.Many : PluralCategory.Other;

    private static PluralCategory LijScnOrdinal(PluralOperands o) =>
        o.N == 8 || o.N == 11 || InRange(o.N, 80, 89) || InRange(o.N, 800, 899)
            ? PluralCategory.Many
            : PluralCategory.Other;

    private static PluralCategory GeorgianOrdinal(PluralOperands o) {
        if(o.I == 1) {
            return PluralCategory.One;
        }

        var mod100 = o.I % 100;
        return o.I == 0 || InRange(mod100, 2, 20) || mod100 == 40 || mod100 == 60 || mod100 == 80
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    private static PluralCategory AlbanianOrdinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        return o.N % 10 == 4 && o.N % 100 != 14 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory CornishOrdinal(PluralOperands o) {
        var mod100 = o.N % 100;
        if(InRange(o.N, 1, 4) || InRange(mod100, 1, 4) || InRange(mod100, 21, 24)
            || InRange(mod100, 41, 44) || InRange(mod100, 61, 64) || InRange(mod100, 81, 84)) {
            return PluralCategory.One;
        }

        return o.N == 5 || mod100 == 5 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory BloOrdinal(PluralOperands o) {
        if(o.I == 0) {
            return PluralCategory.Zero;
        }

        if(o.I == 1) {
            return PluralCategory.One;
        }

        return o.I >= 2 && o.I <= 6 ? PluralCategory.Few : PluralCategory.Other;
    }

    private static PluralCategory BulgarianOrdinal(PluralOperands o) {
        var mod10 = o.I % 10;
        var mod100 = o.I % 100;
        if(mod10 == 1 && mod100 != 11) {
            return PluralCategory.One;
        }

        if(mod10 == 2 && mod100 != 12) {
            return PluralCategory.Two;
        }

        return (mod10 == 3 || mod10 == 4) && mod100 != 13 && mod100 != 14
            ? PluralCategory.Few
            : PluralCategory.Other;
    }

    private static PluralCategory KokMrOrdinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2 || o.N == 3) {
            return PluralCategory.Two;
        }

        return o.N == 4 ? PluralCategory.Few : PluralCategory.Other;
    }

    private static PluralCategory GaelicOrdinal(PluralOperands o) {
        if(o.N == 1 || o.N == 11) {
            return PluralCategory.One;
        }

        if(o.N == 2 || o.N == 12) {
            return PluralCategory.Two;
        }

        return o.N == 3 || o.N == 13 ? PluralCategory.Few : PluralCategory.Other;
    }

    private static PluralCategory CatalanOrdinal(PluralOperands o) {
        if(o.N == 1 || o.N == 3) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        return o.N == 4 ? PluralCategory.Few : PluralCategory.Other;
    }

    private static PluralCategory MacedonianOrdinal(PluralOperands o) {
        var mod10 = o.I % 10;
        var mod100 = o.I % 100;
        if(mod10 == 1 && mod100 != 11) {
            return PluralCategory.One;
        }

        if(mod10 == 2 && mod100 != 12) {
            return PluralCategory.Two;
        }

        return (mod10 == 7 || mod10 == 8) && mod100 != 17 && mod100 != 18
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    private static PluralCategory AzerbaijaniOrdinal(PluralOperands o) {
        var mod10 = o.I % 10;
        var mod100 = o.I % 100;
        var mod1000 = o.I % 1000;
        if(mod10 == 1 || mod10 == 2 || mod10 == 5 || mod10 == 7 || mod10 == 8
            || mod100 == 20 || mod100 == 50 || mod100 == 70 || mod100 == 80) {
            return PluralCategory.One;
        }

        if(mod10 == 3 || mod10 == 4
            || mod1000 == 100 || mod1000 == 200 || mod1000 == 300 || mod1000 == 400
            || mod1000 == 500 || mod1000 == 600 || mod1000 == 700 || mod1000 == 800 || mod1000 == 900) {
            return PluralCategory.Few;
        }

        return o.I == 0 || mod10 == 6 || mod100 == 40 || mod100 == 60 || mod100 == 90
            ? PluralCategory.Many
            : PluralCategory.Other;
    }

    private static PluralCategory GuHiOrdinal(PluralOperands o) {
        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2 || o.N == 3) {
            return PluralCategory.Two;
        }

        if(o.N == 4) {
            return PluralCategory.Few;
        }

        return o.N == 6 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory AsBnOrdinal(PluralOperands o) {
        if(o.N == 1 || o.N == 5 || o.N == 7 || o.N == 8 || o.N == 9 || o.N == 10) {
            return PluralCategory.One;
        }

        if(o.N == 2 || o.N == 3) {
            return PluralCategory.Two;
        }

        if(o.N == 4) {
            return PluralCategory.Few;
        }

        return o.N == 6 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory OriyaOrdinal(PluralOperands o) {
        if(o.N == 1 || o.N == 5 || o.N == 7 || o.N == 8 || o.N == 9) {
            return PluralCategory.One;
        }

        if(o.N == 2 || o.N == 3) {
            return PluralCategory.Two;
        }

        if(o.N == 4) {
            return PluralCategory.Few;
        }

        return o.N == 6 ? PluralCategory.Many : PluralCategory.Other;
    }

    private static PluralCategory WelshOrdinal(PluralOperands o) {
        if(o.N == 0 || o.N == 7 || o.N == 8 || o.N == 9) {
            return PluralCategory.Zero;
        }

        if(o.N == 1) {
            return PluralCategory.One;
        }

        if(o.N == 2) {
            return PluralCategory.Two;
        }

        if(o.N == 3 || o.N == 4) {
            return PluralCategory.Few;
        }

        return o.N == 5 || o.N == 6 ? PluralCategory.Many : PluralCategory.Other;
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
