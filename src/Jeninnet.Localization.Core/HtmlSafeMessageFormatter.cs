//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core;

/// <summary>
/// An <see cref="IMessageFormatter"/> decorator that HTML-encodes argument
/// values before substitution.
/// </summary>
/// <remarks>
/// <para>
/// Architecture decision: templates are trusted developer markup and
/// pass through verbatim; every string (or stringified) argument value is
/// encoded with <see cref="HtmlEncoder.Default"/> (BCL (Base Class Library), no new dependencies),
/// so markup in argument values renders inert. Numeric, boolean, and date/time
/// values are substituted verbatim: their rendering is provably markup-free,
/// and preserving their runtime type keeps  selection (plural, number,
/// date, time) working through this formatter. A <see langword="null"/>
/// argument formats as an empty string, matching
/// <see cref="DefaultMessageFormatter"/>. The output is HTML-encoded content
/// and must be emitted raw (for example through the MVC
/// <c>IHtmlLocalizer</c> bridge, which returns it as a framework
/// <c>LocalizedHtmlString</c>); writing it through a normal HTML-encoding sink
/// would double-encode it. Templates themselves are never sanitized.
/// </para>
/// <para>
/// This formatter performs no lookup and no culture fallback; it composes with
/// any inner formatter (including the ICU (International Components for Unicode) formatter — pre-encoded arguments
/// are opaque strings to it).
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="HtmlSafeMessageFormatter"/> class.
/// </remarks>
/// <param name="inner">The formatter performing substitution after encoding. Defaults to <see cref="DefaultMessageFormatter"/> when null.</param>
public sealed class HtmlSafeMessageFormatter(IMessageFormatter? inner = null) : IMessageFormatter {
    private readonly IMessageFormatter _inner = inner ?? new DefaultMessageFormatter();

    /// <summary>
    /// Encodes a single argument value for HTML output.
    /// </summary>
    /// <remarks>
    /// The shared encoding primitive: the MVC
    /// <c>IHtmlLocalizer</c> bridge encodes arguments through this method
    /// before delegating to the message pipeline, so both paths guarantee
    /// identical encoding. Strings (and every other markup-capable value) are
    /// HTML-encoded; numeric, boolean, and date/time values pass through by
    /// identity so  selection keeps working.
    /// </remarks>
    /// <param name="value">The argument value; <see langword="null"/> encodes as an empty string.</param>
    /// <returns>The HTML-encoded value, or <paramref name="value"/> itself for numeric, boolean, and date/time values.</returns>
    public static object? EncodeArgument(object? value) => value switch {
        null => string.Empty,
        string text => HtmlEncoder.Default.Encode(text),
        sbyte or byte or short or ushort or int or uint or long or ulong or Half or float or double or decimal or bool or DateTime or DateTimeOffset or DateOnly or TimeOnly => value,
        _ => HtmlEncoder.Default.Encode(value.ToString() ?? string.Empty),
    };

    /// <inheritdoc/>
    public string Format(string template, IReadOnlyDictionary<string, object?> parameters) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        Dictionary<string, object?>? encoded = null;
        foreach(var pair in parameters) {
            (encoded ??= new Dictionary<string, object?>(parameters.Count))[pair.Key] = EncodeArgument(pair.Value);
        }

        return _inner.Format(template, encoded ?? parameters);
    }
}
