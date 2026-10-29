//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Carries the applied culture raised by <see cref="BlazorCultureService.CultureChanged"/>.
/// </summary>
public sealed class BlazorCultureChangedEventArgs : EventArgs {
    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorCultureChangedEventArgs"/> class.
    /// </summary>
    /// <param name="culture">The applied culture name.</param>
    /// <param name="cultureInfo">The applied culture.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultureInfo"/> is null.</exception>
    public BlazorCultureChangedEventArgs(string culture, CultureInfo cultureInfo) {
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentNullException.ThrowIfNull(cultureInfo);

        Culture = culture;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// Gets the applied culture name.
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets the applied culture.
    /// </summary>
    public CultureInfo CultureInfo { get; }
}
