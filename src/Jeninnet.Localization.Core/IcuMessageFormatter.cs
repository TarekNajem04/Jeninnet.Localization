//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core;

/// <summary>
/// An <see cref="ICultureAwareMessageFormatter"/> that formats ICU (International Components for Unicode) MessageFormat
/// templates.
/// </summary>
/// <remarks>
/// <para>
/// Registration is the opt-in: register this formatter in place of
/// <see cref="DefaultMessageFormatter"/> (the DI (Dependency Injection) setup uses <c>TryAdd</c>, so a
/// pre-registered formatter wins) and any <c>{arg, type, ...}</c> shape engages
/// the ICU engine. Every template without such a shape (plain <c>{0}</c>/
/// <c>{key}</c> content) takes the exact default path through the inner
/// formatter, so existing content is byte-identical. Unknown argument types
/// also engage the engine and throw descriptively instead of passing through
/// silently.
/// </para>
/// <para>
/// Supported subset: <c>plural</c> (explicit <c>=number</c>, <c>offset:N</c>,
/// cardinal categories), <c>selectordinal</c>, <c>select</c>, <c>number</c>
/// (default/integer/currency/percent), <c>date</c>/<c>time</c> (default/short/medium/long/full),
/// nested messages, <c>#</c> substitution, and ICU apostrophe quoting
/// (<c>''</c> is a literal apostrophe; <c>'{...}'</c> is literal text). The
/// documented <c>::</c> skeleton subset: <c>percent</c>,
/// <c>currency/XXX</c> (three-letter ISO code, symbol resolved from BCL (Base Class Library)
/// <see cref="RegionInfo"/> data), fixed precision <c>.00</c>-style digits, and
/// numeric date (<c>y</c>/<c>M</c>/<c>d</c>) / time (<c>H</c>/<c>h</c>/<c>m</c>/<c>s</c>/<c>a</c>/<c>z</c>/<c>S</c>)
/// skeletons mapped to <see cref="NumberFormatInfo"/>/<see cref="DateTimeFormatInfo"/>
/// facts (locale date order and separators derived from the culture's short-date
/// pattern). Excluded and rejected with precise diagnostics: compact notation
/// (<c>K</c>/<c>KK</c> — no CLDR (Common Locale Data Repository) compact data in the BCL), variable precision
/// (<c>#</c>), text months, weekday, and mixed date+time skeletons. Malformed
/// templates, unknown argument types, missing selector arguments, type
/// mismatches, a missing <c>other</c> branch, and duplicate selectors throw <see cref="FormatException"/>
/// with the pattern offset. Missing <em>simple</em> arguments keep the pipeline's
/// lenient contract (the placeholder is left as-is).
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="IcuMessageFormatter"/> class.
/// </remarks>
/// <param name="inner">The formatter used for templates without ICU constructs. Defaults to <see cref="DefaultMessageFormatter"/> when null.</param>
public sealed partial class IcuMessageFormatter(IMessageFormatter? inner = null) : ICultureAwareMessageFormatter {
    private readonly IMessageFormatter _inner = inner ?? new DefaultMessageFormatter();

    /// <inheritdoc/>
    public string Format(string template, IReadOnlyDictionary<string, object?> parameters) =>
        Format(template, parameters, CultureInfo.CurrentUICulture);

    /// <inheritdoc/>
    public string Format(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(culture);

        if(!DetectIcuConstruct().IsMatch(template)) {
            return _inner.Format(template, parameters);
        }

        var (nodes, hasComplexArgument) = new IcuMessageParser(template).Parse();
        if(!hasComplexArgument) {
            return _inner.Format(template, parameters);
        }

        var builder = new StringBuilder();
        FormatNodes(builder, nodes, parameters, culture, pluralValue: null, pluralOffset: 0);
        return builder.ToString();
    }

    /// <summary>
    /// Formats nodes.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="nodes">The nodes.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="pluralValue">The plural value.</param>
    /// <param name="pluralOffset">The plural offset.</param>
    private static void FormatNodes(StringBuilder builder, IReadOnlyList<IIcuNode> nodes, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, decimal? pluralValue, decimal pluralOffset) {
        foreach(var node in nodes) {
            switch(node) {
                case IcuTextNode text:
                    builder.Append(text.Text);
                    break;
                case IcuPoundNode:
                    builder.Append(((pluralValue ?? 0) - pluralOffset).ToString("#,##0.###", culture));
                    break;
                case IcuArgumentNode argument:
                    FormatArgument(builder, argument, parameters, culture, pluralValue, pluralOffset);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported ICU node '{node.GetType().Name}'.");
            }
        }
    }

    /// <summary>
    /// Formats argument.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="node">The node.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="pluralValue">The plural value.</param>
    /// <param name="pluralOffset">The plural offset.</param>
    private static void FormatArgument(StringBuilder builder, IcuArgumentNode node, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, decimal? pluralValue, decimal pluralOffset) {
        parameters.TryGetValue(node.Name, out var value);
        switch(node.Format) {
            case IcuSimpleFormat:
                builder.Append(value?.ToString() ?? ("{" + node.Name + "}"));
                break;
            case IcuNumberFormat number:
                FormatNumber(builder, node.Name, value, number, culture);
                break;
            case IcuDateFormat date:
                FormatDate(builder, node.Name, value, date, culture);
                break;
            case IcuTimeFormat time:
                FormatTime(builder, node.Name, value, time, culture);
                break;
            case IcuSelectFormat select:
                FormatSelect(builder, node, value, select.Options, new IcuFormatContext(parameters, culture, pluralValue, pluralOffset));
                break;
            case IcuPluralFormat plural:
                FormatPlural(builder, node, value, plural, parameters, culture);
                break;
            default:
                throw new InvalidOperationException($"Unsupported ICU argument format '{node.Format.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Formats number.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="format">The format.</param>
    /// <param name="culture">The culture.</param>
    private static void FormatNumber(StringBuilder builder, string name, object? value, IcuNumberFormat format, CultureInfo culture) {
        if(value is null || !IsNumeric(value)) {
            throw new FormatException($"ICU argument '{{{name}}}' must be a numeric value for 'number' formatting.");
        }

        if(format.Skeleton is not null) {
            FormatNumberSkeleton(builder, name, value, format.Skeleton, culture);
            return;
        }

        var formatString = format.Style switch {
            null => "#,##0.###",
            "integer" => "N0",
            "currency" => "C",
            "percent" => "P0",
            _ => throw new FormatException($"ICU argument '{{{name}}}' has an unsupported number style '{format.Style}'. Supported styles: integer, currency, percent (or a '::' skeleton)."),
        };
        builder.Append(((IFormattable)value).ToString(formatString, culture) ?? string.Empty);
    }

    /// <summary>
    /// Formats date.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="format">The format.</param>
    /// <param name="culture">The culture.</param>
    private static void FormatDate(StringBuilder builder, string name, object? value, IcuDateFormat format, CultureInfo culture) {
        if(value is not (DateTime or DateTimeOffset or DateOnly)) {
            throw new FormatException($"ICU argument '{{{name}}}' must be a date value (DateTime, DateTimeOffset, DateOnly) for 'date' formatting.");
        }

        if(format.Skeleton is not null) {
            var pattern = MapDateSkeleton(format.Skeleton, name, culture);
            builder.Append(((IFormattable)value).ToString(pattern, culture) ?? string.Empty);
            return;
        }

        var formatString = format.Style switch {
            null or "short" => "d",
            "medium" or "long" or "full" => "D",
            _ => throw new FormatException($"ICU argument '{{{name}}}' has an unsupported date style '{format.Style}'. Supported styles: short, medium, long, full (or a '::' skeleton)."),
        };
        builder.Append(((IFormattable)value).ToString(formatString, culture) ?? string.Empty);
    }

    /// <summary>
    /// Formats time.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="format">The format.</param>
    /// <param name="culture">The culture.</param>
    private static void FormatTime(StringBuilder builder, string name, object? value, IcuTimeFormat format, CultureInfo culture) {
        if(value is not (DateTime or DateTimeOffset or TimeOnly)) {
            throw new FormatException($"ICU argument '{{{name}}}' must be a time value (DateTime, DateTimeOffset, TimeOnly) for 'time' formatting.");
        }

        if(format.Skeleton is not null) {
            var pattern = MapTimeSkeleton(format.Skeleton, name, culture);
            builder.Append(((IFormattable)value).ToString(pattern, culture) ?? string.Empty);
            return;
        }

        var formatString = format.Style switch {
            null or "short" => "t",
            "medium" or "long" or "full" => "T",
            _ => throw new FormatException($"ICU argument '{{{name}}}' has an unsupported time style '{format.Style}'. Supported styles: short, medium, long, full (or a '::' skeleton)."),
        };
        builder.Append(((IFormattable)value).ToString(formatString, culture) ?? string.Empty);
    }

    /// <summary>
    /// Formats a <c>select</c> argument by dispatching on the matching option's message.
    /// </summary>
    /// <param name="builder">The output builder.</param>
    /// <param name="node">The select argument node.</param>
    /// <param name="value">The selector value from the argument map.</param>
    /// <param name="options">The select options in declaration order.</param>
    /// <param name="context">The recursive formatting context.</param>
    /// <exception cref="FormatException">Thrown when the argument has an unsupported format.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the argument has no valid option.</exception>
    private static void FormatSelect(StringBuilder builder, IcuArgumentNode node, object? value, IReadOnlyList<IcuOption> options, IcuFormatContext context) {
        var text = value?.ToString() ?? throw new FormatException($"ICU argument '{{{node.Name}}}' has no value for 'select' selection.");
        var match = (options.FirstOrDefault(option => string.Equals(option.Keyword, text, StringComparison.Ordinal))
            ?? options.FirstOrDefault(static option => string.Equals(option.Keyword, "other", StringComparison.Ordinal))) ?? throw new InvalidOperationException($"ICU argument '{{{node.Name}}}' has no 'other' option (parser invariant).");
        FormatNodes(builder, match.Message, context.Parameters, context.Culture, context.PluralValue, context.PluralOffset);
    }

    /// <summary>
    /// Formats plural.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="node">The node.</param>
    /// <param name="value">The value.</param>
    /// <param name="plural">The plural.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture.</param>
    private static void FormatPlural(StringBuilder builder, IcuArgumentNode node, object? value, IcuPluralFormat plural, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture) {
        var number = ToDecimal(value, node.Name);
        var offset = plural.Offset ?? 0;
        var explicitMatch = plural.Options.FirstOrDefault(option => option.ExplicitValue == number);
        if(explicitMatch is not null) {
            FormatNodes(builder, explicitMatch.Message, parameters, culture, number, offset);
            return;
        }

        var category = plural.Ordinal
            ? PluralRules.GetOrdinalCategory(culture, number)
            : PluralRules.GetCardinalCategory(culture, number);
        var keyword = category switch {
            PluralCategory.Zero => "zero",
            PluralCategory.One => "one",
            PluralCategory.Two => "two",
            PluralCategory.Few => "few",
            PluralCategory.Many => "many",
            _ => "other",
        };
        var match = (plural.Options.FirstOrDefault(option => string.Equals(option.Keyword, keyword, StringComparison.Ordinal))
            ?? plural.Options.FirstOrDefault(static option => string.Equals(option.Keyword, "other", StringComparison.Ordinal))) ?? throw new InvalidOperationException($"ICU argument '{{{node.Name}}}' has no 'other' option (parser invariant).");
        FormatNodes(builder, match.Message, parameters, culture, number, offset);
    }

    /// <summary>
    /// Converts to decimal.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="name">The name.</param>
    /// <returns>The to decimal value.</returns>
    private static decimal ToDecimal(object? value, string name) {
        try {
            return value switch {
                sbyte v => v,
                byte v => v,
                short v => v,
                ushort v => v,
                int v => v,
                uint v => v,
                long v => v,
                ulong v => v,
                Half v => (decimal)v,
                float v => Convert.ToDecimal(v),
                double v => Convert.ToDecimal(v),
                decimal v => v,
                _ => throw new FormatException($"ICU argument '{{{name}}}' must be a numeric value for plural selection."),
            };
        }
        catch(OverflowException exception) {
            throw new FormatException($"ICU argument '{{{name}}}' is out of range for plural selection.", exception);
        }
    }

    /// <summary>
    /// Determines whether the value is numeric.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsNumeric(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or Half or float or double or decimal;

    /// <summary>
    /// Performs the detect icu construct operation.
    /// </summary>
    /// <returns>The detect icu construct result.</returns>
    [GeneratedRegex(@"\{[^{}]*,\s*[A-Za-z]+\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DetectIcuConstruct();

    /// <summary>
    /// The recursive formatting context shared by ICU nodes: the argument map, the target culture,
    /// and the enclosing plural value and offset used by <c>#</c> substitution.
    /// </summary>
    /// <param name="Parameters">The argument values by name.</param>
    /// <param name="Culture">The culture used for number, date, and time formatting.</param>
    /// <param name="PluralValue">The enclosing plural value, or <see langword="null"/> when not in a plural branch.</param>
    /// <param name="PluralOffset">The enclosing plural offset subtracted from <c>#</c>.</param>
    private readonly record struct IcuFormatContext(
        IReadOnlyDictionary<string, object?> Parameters,
        CultureInfo Culture,
        decimal? PluralValue,
        decimal PluralOffset);
}
