//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Defines a formatter for messages.
/// </summary>
public interface IMessageFormatter {
    /// <summary>
    /// Formats a message template with parameters.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The formatted message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> or <paramref name="parameters"/> is null.</exception>
    string Format(string template, IReadOnlyDictionary<string, object?> parameters);
}
