//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core;

/// <summary>
/// The ICU (International Components for Unicode) <c>::</c> skeleton mapping of <see cref="IcuMessageFormatter"/>
///: a documented, BCL-only subset mapped deterministically to
/// <see cref="NumberFormatInfo"/>/<see cref="DateTimeFormatInfo"/> facts.
/// Everything outside the subset throws <see cref="FormatException"/> with a
/// precise diagnostic — never a silent fallback.
/// </summary>
public sealed partial class IcuMessageFormatter {
    private static readonly ConcurrentDictionary<string, string> _currencySymbols = new(StringComparer.Ordinal);

    /// <summary>
    /// Formats number skeleton.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="skeleton">The ICU skeleton.</param>
    /// <param name="culture">The culture.</param>
    private static void FormatNumberSkeleton(StringBuilder builder, string name, object? value, string skeleton, CultureInfo culture) {
        ParseNumberSkeleton(name, skeleton, out var stem, out var precision);

        if(stem is null) {
            builder.Append(((IFormattable)value!).ToString("N" + precision!.Value.ToString(CultureInfo.InvariantCulture), culture) ?? string.Empty);
            return;
        }

        AppendNumberSkeleton(builder, name, value, stem, precision, culture);
    }

    /// <summary>
    /// Splits one number skeleton into its optional stem and precision token.
    /// </summary>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <param name="skeleton">The skeleton text.</param>
    /// <param name="stem">The stem token, or <see langword="null"/> when absent.</param>
    /// <param name="precision">The precision token, or <see langword="null"/> when absent.</param>
    /// <exception cref="FormatException">Thrown when the skeleton is empty or repeats a token kind.</exception>
    private static void ParseNumberSkeleton(string name, string skeleton, out string? stem, out int? precision) {
        stem = null;
        precision = null;
        foreach(var token in skeleton.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if(token.StartsWith('.')) {
                if(precision is not null) {
                    throw new FormatException($"ICU argument '{{{name}}}' has more than one precision token in skeleton '::{skeleton}'.");
                }

                precision = ParsePrecision(token, name);
                continue;
            }

            if(stem is not null) {
                throw new FormatException($"ICU argument '{{{name}}}' has more than one stem in skeleton '::{skeleton}'. Supported: one stem (percent, currency/XXX) and one precision token ('.00').");
            }

            stem = token;
        }

        if(stem is null && precision is null) {
            throw new FormatException($"ICU argument '{{{name}}}' has an empty number skeleton: expected a stem (percent, currency/XXX) or a precision token ('.00').");
        }
    }

    /// <summary>
    /// Applies one number skeleton stem (percent, currency/XXX) to the value.
    /// </summary>
    /// <param name="builder">The output builder.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <param name="value">The argument value.</param>
    /// <param name="stem">The stem token.</param>
    /// <param name="precision">The precision token, or <see langword="null"/> when absent.</param>
    /// <param name="culture">The requested culture.</param>
    /// <exception cref="FormatException">Thrown for compact notation or an unsupported stem.</exception>
    private static void AppendNumberSkeleton(StringBuilder builder, string name, object? value, string stem, int? precision, CultureInfo culture) {
        if(stem is "K" or "KK") {
            throw new FormatException($"ICU argument '{{{name}}}' uses compact notation '{stem}', which is excluded: the BCL carries no CLDR compact patterns, and fabricated suffixes would be locale-incorrect.");
        }

        if(string.Equals(stem, "percent", StringComparison.Ordinal)) {
            builder.Append(((IFormattable)value!).ToString("P" + (precision ?? 0).ToString(CultureInfo.InvariantCulture), culture) ?? string.Empty);
            return;
        }

        if(stem.StartsWith("currency/", StringComparison.Ordinal)) {
            var code = stem["currency/".Length..];
            if(code.Length != 3 || !code.All(char.IsAsciiLetter)) {
                throw new FormatException($"ICU argument '{{{name}}}' has an invalid currency skeleton '{stem}': expected 'currency/XXX' with a three-letter ISO code.");
            }

            code = code.ToUpperInvariant();
            var numberFormat = (NumberFormatInfo)culture.NumberFormat.Clone();
            numberFormat.CurrencySymbol = ResolveCurrencySymbol(code, name);
            var pattern = precision.HasValue ? "C" + precision.Value.ToString(CultureInfo.InvariantCulture) : "C";
            builder.Append(((IFormattable)value!).ToString(pattern, numberFormat) ?? string.Empty);
            return;
        }

        throw new FormatException($"ICU argument '{{{name}}}' has an unsupported number skeleton stem '{stem}'. Supported stems: percent, currency/XXX (with an optional precision token such as '.00').");
    }

    /// <summary>
    /// Parses precision.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <param name="name">The name.</param>
    /// <returns>The precision value.</returns>
    private static int ParsePrecision(string token, string name) {
        if(token.Contains('#')) {
            throw new FormatException($"ICU argument '{{{name}}}' uses variable precision '{token}', which is not supported: use fixed digits such as '.00'.");
        }

        for(var index = 1; index < token.Length; index++) {
            if(token[index] != '0') {
                throw new FormatException($"ICU argument '{{{name}}}' has an invalid precision token '{token}': expected '.' followed by '0' digits.");
            }
        }

        if(token.Length == 1) {
            throw new FormatException($"ICU argument '{{{name}}}' has an invalid precision token '.': expected at least one '0' digit.");
        }

        return token.Length - 1;
    }

    /// <summary>
    /// Resolves the currency symbol for an ISO 4217 code from the first matching specific culture in deterministic order, caching the result per code.
    /// </summary>
    /// <param name="code">The ISO 4217 currency code.</param>
    /// <param name="name">The ICU argument name used in the diagnostic when the code is unknown.</param>
    /// <returns>The currency symbol associated with <paramref name="code"/>.</returns>
    private static string ResolveCurrencySymbol(string code, string name) =>
        _currencySymbols.GetOrAdd(code, c => {
            foreach(var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                        .OrderBy(static candidate => candidate.Name, StringComparer.Ordinal)) {
                string iso;
                string symbol;
                try {
                    var region = new RegionInfo(culture.Name);
                    iso = region.ISOCurrencySymbol;
                    symbol = region.CurrencySymbol;
                }
                catch(ArgumentException) {
                    continue;
                }

                if(string.Equals(iso, c, StringComparison.Ordinal)) {
                    return symbol;
                }
            }

            throw new FormatException($"ICU argument '{{{name}}}' has an unsupported currency skeleton 'currency/{c}': the ISO currency code is unknown.");
        });

    /// <summary>
    /// Maps date runs.
    /// </summary>
    /// <param name="runs">The parsed field runs.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The BCL date pattern.</returns>
    private static string MapDateRuns(List<(char Letter, int Count)> runs, string name, CultureInfo culture) {
        var counts = CollectDateFieldCounts(runs, name);
        var order = DateFieldOrder(culture);

        var tokens = new List<string>();
        foreach(var letter in order) {
            if(!counts.TryGetValue(letter, out var count)) {
                continue;
            }

            tokens.Add(DateToken(letter, count));
        }

        var hasText = counts.Any(kvp => (kvp.Key == 'M' && kvp.Value >= 3) || kvp.Key == 'E' || kvp.Key == 'G');
        return hasText ? string.Join(" ", tokens) : string.Join(DateSeparator(culture), tokens);
    }

    /// <summary>
    /// The locale's date-field order: weekday first, then the short-date pattern's
    /// y/M/d order, then era last (documented approximation).
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The field letters in render order.</returns>
    private static List<char> DateFieldOrder(CultureInfo culture) {
        var order = new List<char> { 'E' };
        // The locale's numeric date order and separator come from the short-date
        // pattern; bidi control marks embedded in some patterns and separators
        // (e.g. ar) are stripped — the formatter's own bidi handling applies
        //.
        order.AddRange(culture.DateTimeFormat.ShortDatePattern.Where(candidate => candidate is 'y' or 'M' or 'd' && !order.Contains(candidate)));
        order.AddRange("yMd".Where(candidate => !order.Contains(candidate)));
        order.Add('G');
        return order;
    }

    /// <summary>
    /// Maps one date field and its run length to the BCL (Base Class Library) date token.
    /// </summary>
    /// <param name="letter">The field letter.</param>
    /// <param name="count">The run length.</param>
    /// <returns>The BCL token.</returns>
    private static string DateToken(char letter, int count) => letter switch {
        'y' => count == 2 ? "yy" : "yyyy",
        'M' => count switch {
            1 => "M",
            2 => "MM",
            3 => "MMM",
            _ => "MMMM",
        },
        'E' => count <= 3 ? "ddd" : "dddd",
        'G' => "g",
        _ => count == 1 ? "d" : "dd",
    };

    /// <summary>
    /// The locale's date separator with bidi control marks stripped.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The separator.</returns>
    private static string DateSeparator(CultureInfo culture) =>
        string.Concat(culture.DateTimeFormat.DateSeparator.Where(static candidate => candidate is not ('\u200e' or '\u200f' or '\u061c')));

    private static bool IsDateSkeletonLetter(char letter) =>
        letter is 'y' or 'u' or 'M' or 'L' or 'd' or 'E' or 'G';

    private static bool IsTimeSkeletonLetter(char letter) =>
        letter is 'H' or 'h' or 'k' or 'K' or 'm' or 's' or 'a' or 'b' or 'B' or 'S'
            or 'z' or 'v' or 'V' or 'O' or 'X' or 'x' or 'Z';

    /// <summary>
    /// Maps a date-skeleton field letter to its count cap and diagnostics.
    /// </summary>
    private static readonly (char Letter, int MaxCount, string Counts)[] DateFieldCaps = [
        ('y', 4, "supported year counts are 1-4."),
        ('M', 4, "supported month counts are 1-4 (1-2 numeric, 3 abbreviated, 4 wide; narrow is excluded)."),
        ('d', 2, "supported day counts are 1-2."),
        ('E', 4, "supported weekday counts are 1-4 (1-3 abbreviated, 4 wide; narrow/short are excluded)."),
        ('G', 5, "supported era counts are 1-5."),
    ];

    /// <summary>
    /// Collects the date-field counts of one skeleton, rejecting unsupported fields,
    /// oversized counts, and conflicting duplicates.
    /// </summary>
    /// <param name="runs">The parsed field runs.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <returns>The field counts keyed by field letter.</returns>
    /// <exception cref="FormatException">Thrown when the skeleton violates the date subset.</exception>
    private static Dictionary<char, int> CollectDateFieldCounts(List<(char Letter, int Count)> runs, string name) {
        var counts = new Dictionary<char, int>();
        foreach(var (rawLetter, count) in runs) {
            var letter = rawLetter switch {
                'u' => 'y',
                'L' => 'M',
                _ => rawLetter,
            };

            var (maxCount, countText) = FindDateFieldCap(letter)
                ?? throw new FormatException($"ICU argument '{{{name}}}' has an unsupported date skeleton field '{new string(rawLetter, count)}'. Supported date fields: y/u, M/L, d, E, G; quarter, week, day-of-year and mixed date/time in a date-only skeleton are excluded (use a mixed skeleton with a DateTime value).");

            if(count > maxCount) {
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported date skeleton count in '{new string(rawLetter, count)}': {countText}");
            }

            if(counts.TryGetValue(letter, out var existing) && existing != count) {
                throw new FormatException($"ICU argument '{{{name}}}' has conflicting counts for date skeleton field '{letter}'.");
            }

            counts[letter] = count;
        }

        return counts;
    }

    /// <summary>
    /// Finds the date field cap.
    /// </summary>
    /// <param name="letter">The normalized field letter.</param>
    /// <returns>The cap, or <see langword="null"/> when the letter is outside the date subset.</returns>
    private static (int MaxCount, string Counts)? FindDateFieldCap(char letter) {
        foreach(var (fieldLetter, fieldMax, fieldCounts) in DateFieldCaps) {
            if(fieldLetter == letter) {
                return (fieldMax, fieldCounts);
            }
        }

        return null;
    }

    /// <summary>
    /// Maps time runs.
    /// </summary>
    /// <param name="runs">The parsed field runs.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The BCL time pattern.</returns>
    private static string MapTimeRuns(List<(char Letter, int Count)> runs, string name, CultureInfo culture) {
        var fields = new TimeSkeletonFields();
        foreach(var (letter, count) in runs) {
            ApplyTimeField(letter, count, name, fields);
        }

        if(fields.Hour is null) {
            throw new FormatException($"ICU argument '{{{name}}}' has a time skeleton without an hour field: supported time skeletons require H, h, k, or K.");
        }

        return AssembleTimePattern(fields, culture);
    }

    /// <summary>
    /// Assembles the BCL time pattern from the collected skeleton fields.
    /// </summary>
    /// <param name="fields">The collected fields.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The BCL time pattern.</returns>
    private static string AssembleTimePattern(TimeSkeletonFields fields, CultureInfo culture) {
        var format = culture.DateTimeFormat;
        var time = new StringBuilder();
        time.Append(HourToken(fields));
        if(fields.HasMinute) {
            time.Append(format.TimeSeparator).Append("mm");
        }

        if(fields.HasSecond) {
            time.Append(format.TimeSeparator).Append("ss");
        }

        if(fields.FractionDigits > 0) {
            time.Append(culture.NumberFormat.NumberDecimalSeparator).Append('f', fields.FractionDigits);
        }

        var pattern = time.ToString();
        if(fields.HasPeriod) {
            pattern = PlacePeriod(format, pattern);
        }

        if(fields.HasOffset) {
            pattern += " zzz";
        }

        return pattern;
    }

    /// <summary>
    /// Maps the collected hour field to its BCL hour token.
    /// </summary>
    /// <param name="fields">The collected fields.</param>
    /// <returns>The hour token.</returns>
    private static string HourToken(TimeSkeletonFields fields) =>
        (fields.Hour, fields.HourCount) switch {
            ('H', 1) => "H",
            ('H', _) => "HH",
            (_, 1) => "h",
            _ => "hh",
        };

    /// <summary>
    /// Places the period token before or after the time pattern according to the culture's
    /// short-time pattern.
    /// </summary>
    /// <param name="format">The culture's date/time format facts.</param>
    /// <param name="pattern">The assembled time pattern.</param>
    /// <returns>The pattern with the period token placed.</returns>
    private static string PlacePeriod(DateTimeFormatInfo format, string pattern) {
        var periodIndex = format.ShortTimePattern.IndexOf('t');
        var hourIndex = format.ShortTimePattern.IndexOfAny(['h', 'H']);
        return periodIndex >= 0 && hourIndex >= 0 && periodIndex < hourIndex
            ? "tt " + pattern
            : pattern + " tt";
    }

    /// <summary>
    /// Records one time-skeleton field run, rejecting unsupported letters, oversized counts,
    /// and a repeated hour field.
    /// </summary>
    /// <param name="letter">The field letter.</param>
    /// <param name="count">The run length.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <param name="fields">The collected fields.</param>
    /// <exception cref="FormatException">Thrown when the field violates the time subset.</exception>
    private static void ApplyTimeField(char letter, int count, string name, TimeSkeletonFields fields) {
        switch(letter) {
            case 'H' or 'h' or 'k' or 'K':
                if(fields.Hour is not null) {
                    throw new FormatException($"ICU argument '{{{name}}}' has more than one hour field in its time skeleton: the hour field (H/h/k/K) must be unique.");
                }

                RequireShortCount(letter, count, name);
                fields.Hour = letter switch {
                    'k' => 'H',
                    'K' => 'h',
                    _ => letter,
                };
                fields.HourCount = count;
                break;
            case 'm':
                RequireShortCount(letter, count, name);
                fields.HasMinute = true;
                break;
            case 's':
                RequireShortCount(letter, count, name);
                fields.HasSecond = true;
                break;
            case 'a' or 'b' or 'B':
                fields.HasPeriod = true;
                break;
            case 'S':
                if(count > 7) {
                    throw new FormatException($"ICU argument '{{{name}}}' has an unsupported fraction count in '{new string(letter, count)}': supported counts are 1-7.");
                }

                fields.FractionDigits = count;
                break;
            case 'z' or 'v' or 'V' or 'O' or 'X' or 'x' or 'Z':
                if(count > 5) {
                    throw new FormatException($"ICU argument '{{{name}}}' has an unsupported timezone skeleton field '{new string(letter, count)}': supported counts are 1-5 (mapped to the numeric offset form; long localized names are approximated as offsets).");
                }

                fields.HasOffset = true;
                break;
            default:
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported time skeleton field '{new string(letter, count)}'. Supported time fields: H/h/k/K, m, s, a/b/B, S, z/v/V/O/X/x/Z (offsets).");
        }
    }

    /// <summary>
    /// Rejects a time-field run longer than the supported two.
    /// </summary>
    /// <param name="letter">The field letter.</param>
    /// <param name="count">The run length.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <exception cref="FormatException">Thrown when the run is longer than two.</exception>
    private static void RequireShortCount(char letter, int count, string name) {
        if(count > 2) {
            throw new FormatException($"ICU argument '{{{name}}}' has an unsupported time skeleton count in '{new string(letter, count)}': supported counts are 1-2.");
        }
    }

    /// <summary>
    /// The mutable time-skeleton facts collected from one skeleton's field runs.
    /// </summary>
    private sealed class TimeSkeletonFields {
        /// <summary>Gets or sets the hour field letter ('H' or 'h'), or <see langword="null"/> when absent.</summary>
        internal char? Hour { get; set; }

        /// <summary>Gets or sets the hour field run length.</summary>
        internal int HourCount { get; set; }

        /// <summary>Gets or sets whether the skeleton carries a minute field.</summary>
        internal bool HasMinute { get; set; }

        /// <summary>Gets or sets whether the skeleton carries a second field.</summary>
        internal bool HasSecond { get; set; }

        /// <summary>Gets or sets whether the skeleton carries a period field.</summary>
        internal bool HasPeriod { get; set; }

        /// <summary>Gets or sets the fraction-digit count.</summary>
        internal int FractionDigits { get; set; }

        /// <summary>Gets or sets whether the skeleton carries a timezone-offset field.</summary>
        internal bool HasOffset { get; set; }
    }

    /// <summary>
    /// Parses field runs.
    /// </summary>
    /// <param name="skeleton">The ICU skeleton.</param>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The field runs sequence.</returns>
    private static List<(char Letter, int Count)> ParseFieldRuns(string skeleton, string name, string kind) {
        var runs = new List<(char Letter, int Count)>();
        var index = 0;
        while(index < skeleton.Length) {
            var current = skeleton[index];
            if(current == ' ') {
                index++;
                continue;
            }

            if(!char.IsAsciiLetter(current)) {
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported {kind} skeleton character '{current}': {kind} skeletons carry field letters only.");
            }

            var start = index;
            while(index < skeleton.Length && skeleton[index] == current) {
                index++;
            }

            runs.Add((current, index - start));
        }

        if(runs.Count == 0) {
            throw new FormatException($"ICU argument '{{{name}}}' has an empty {kind} skeleton.");
        }

        return runs;
    }
}
