//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core;

/// <summary>
/// A default implementation of <see cref="IMessageFormatter"/>.
/// </summary>
public partial class DefaultMessageFormatter : IMessageFormatter {
    private static readonly Regex _parameterRegex = ExtractParameterPattern();

    /// <inheritdoc/>
    public string Format(string template, IReadOnlyDictionary<string, object?> parameters) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        return _parameterRegex.Replace(template, match => {
            var key = match.Groups["key"].Value;
            if(parameters.TryGetValue(key, out var value)) {
                return value?.ToString() ?? string.Empty;
            }

            return match.Value; // Keep the placeholder if not found
        });
    }

    /// <summary>
    /// Extracts parameter pattern.
    /// </summary>
    /// <returns>The parameter pattern result.</returns>
    [GeneratedRegex(@"\{(?<key>[^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex ExtractParameterPattern();
}
