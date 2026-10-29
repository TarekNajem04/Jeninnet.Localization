//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Defines a message formatter that formats templates in an explicit culture.
/// </summary>
/// <remarks>
/// Culture-aware formatters (for example ICU (International Components for Unicode) MessageFormat templates, whose plural
/// rules and number/date rendering depend on the culture) implement this interface
/// so the localizer can hand them the requested culture. The default localizer
/// prefers this method when the registered formatter implements it and falls
/// back to <see cref="IMessageFormatter.Format(string, IReadOnlyDictionary{string, object?})"/>
/// otherwise, so existing formatters keep working unchanged.
/// </remarks>
public interface ICultureAwareMessageFormatter : IMessageFormatter {
    /// <summary>
    /// Formats a message template with parameters in the specified culture.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="culture">The culture used for culture-sensitive rendering (plural selection, number/date formats).</param>
    /// <returns>The formatted message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/>, <paramref name="parameters"/>, or <paramref name="culture"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when an opted-in template is malformed and cannot be formatted deterministically.</exception>
    string Format(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture);
}
