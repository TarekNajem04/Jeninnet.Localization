//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Avalonia;

/// <summary>
/// Avalonia-specific culture helpers.
/// </summary>
public static class AvaloniaCultureExtensions {
    /// <summary>
    /// Maps the active culture direction to an Avalonia <see cref="FlowDirection"/>.
    /// </summary>
    /// <param name="service">The host culture service.</param>
    /// <returns><see cref="FlowDirection.RightToLeft"/> for right-to-left cultures; otherwise <see cref="FlowDirection.LeftToRight"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
    public static FlowDirection ToFlowDirection(this HostCultureService service) {
        ArgumentNullException.ThrowIfNull(service);
        return service.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
