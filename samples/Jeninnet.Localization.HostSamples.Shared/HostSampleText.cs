//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.HostSamples.Shared;

/// <summary>
/// Resolves sample messages through the Jeninnet localization pipeline.
/// </summary>
public sealed class HostSampleText {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly IMessageLocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostSampleText"/> class.
    /// </summary>
    /// <param name="localizer">The message localizer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localizer"/> is null.</exception>
    public HostSampleText(IMessageLocalizer localizer) {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
    }

    /// <summary>
    /// Resolves a message for the current UI culture.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <returns>The localized message, or the key when no template exists.</returns>
    public async ValueTask<string> GetAsync(string key) {
        var value = await _localizer.LocalizeAsync(key, _noParameters, CultureInfo.CurrentUICulture).ConfigureAwait(false);
        return value ?? key;
    }
}
