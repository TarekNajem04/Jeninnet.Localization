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
    /// Maps date skeleton.
    /// </summary>
    /// <param name="skeleton">The ICU skeleton.</param>
    /// <param name="name">The name.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The map date skeleton text.</returns>
    private static string MapDateSkeleton(string skeleton, string name, CultureInfo culture) {
        var counts = CollectDateFieldCounts(skeleton, name);
        var order = DateFieldOrder(culture);

        var tokens = new List<string>();
        foreach(var letter in order) {
            if(!counts.TryGetValue(letter, out var count)) {
                continue;
            }

            tokens.Add(DateToken(letter, count));
        }

        return string.Join(DateSeparator(culture), tokens);
    }

    /// <summary>
    /// The locale's numeric date-field order: the short-date pattern's order first, then the
    /// remaining fields in the canonical y-M-d order.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The field letters in render order.</returns>
    private static List<char> DateFieldOrder(CultureInfo culture) {
        var order = new List<char>();
        // The locale's numeric date order and separator come from the short-date
        // pattern; bidi control marks embedded in some patterns and separators
        // (e.g. ar) are stripped — the formatter's own bidi handling applies
        //.
        order.AddRange(culture.DateTimeFormat.ShortDatePattern.Where(candidate => candidate is 'y' or 'M' or 'd' && !order.Contains(candidate)));
        order.AddRange("yMd".Where(candidate => !order.Contains(candidate)));
        return order;
    }

    /// <summary>
    /// Maps one numeric date field and its run length to the BCL (Base Class Library) date token.
    /// </summary>
    /// <param name="letter">The field letter.</param>
    /// <param name="count">The run length.</param>
    /// <returns>The BCL token.</returns>
    private static string DateToken(char letter, int count) => letter switch {
        'y' => count == 1 ? "yyyy" : "yy",
        'M' => count == 1 ? "M" : "MM",
        _ => count == 1 ? "d" : "dd",
    };

    /// <summary>
    /// The locale's date separator with bidi control marks stripped.
    /// </summary>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The separator.</returns>
    private static string DateSeparator(CultureInfo culture) =>
        string.Concat(culture.DateTimeFormat.DateSeparator.Where(static candidate => candidate is not ('\u200e' or '\u200f' or '\u061c')));

    /// <summary>
    /// Collects the numeric date-field counts of one skeleton, rejecting unsupported fields,
    /// oversized counts, and conflicting duplicates.
    /// </summary>
    /// <param name="skeleton">The skeleton text.</param>
    /// <param name="name">The ICU argument name (diagnostics).</param>
    /// <returns>The field counts keyed by field letter.</returns>
    /// <exception cref="FormatException">Thrown when the skeleton violates the numeric subset.</exception>
    private static Dictionary<char, int> CollectDateFieldCounts(string skeleton, string name) {
        var counts = new Dictionary<char, int>();
        foreach(var (letter, count) in ParseFieldRuns(skeleton, name, "date")) {
            if(letter is not ('y' or 'M' or 'd')) {
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported date skeleton field '{new string(letter, count)}'. Supported date fields: numeric y, M, d (counts 1-2); text months, weekday, and mixed date/time skeletons are excluded.");
            }

            if(count > 2) {
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported date skeleton count in '{new string(letter, count)}': supported counts are 1-2 (text months are excluded).");
            }

            if(counts.TryGetValue(letter, out var existing) && existing != count) {
                throw new FormatException($"ICU argument '{{{name}}}' has conflicting counts for date skeleton field '{letter}'.");
            }

            counts[letter] = count;
        }

        return counts;
    }

    /// <summary>
    /// Maps time skeleton.
    /// </summary>
    /// <param name="skeleton">The ICU skeleton.</param>
    /// <param name="name">The name.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The map time skeleton text.</returns>
    private static string MapTimeSkeleton(string skeleton, string name, CultureInfo culture) {
        var fields = new TimeSkeletonFields();
        foreach(var (letter, count) in ParseFieldRuns(skeleton, name, "time")) {
            ApplyTimeField(letter, count, name, fields);
        }

        if(fields.Hour is null) {
            throw new FormatException($"ICU argument '{{{name}}}' has a time skeleton without an hour field: supported time skeletons require H or h.");
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
            case 'H' or 'h':
                if(fields.Hour is not null) {
                    throw new FormatException($"ICU argument '{{{name}}}' has both 'H' and 'h' in its time skeleton: the hour field must be unique.");
                }

                RequireShortCount(letter, count, name);
                fields.Hour = letter;
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
            case 'a':
                fields.HasPeriod = true;
                break;
            case 'S':
                if(count > 7) {
                    throw new FormatException($"ICU argument '{{{name}}}' has an unsupported fraction count in '{new string(letter, count)}': supported counts are 1-7.");
                }

                fields.FractionDigits = count;
                break;
            case 'z':
                if(count > 3) {
                    throw new FormatException($"ICU argument '{{{name}}}' has an unsupported timezone skeleton field '{new string(letter, count)}': long localized timezone names are excluded (supported: z, zz, zzz as the offset form).");
                }

                fields.HasOffset = true;
                break;
            default:
                throw new FormatException($"ICU argument '{{{name}}}' has an unsupported time skeleton field '{new string(letter, count)}'. Supported time fields: H, h, m, s, a, z, S.");
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
