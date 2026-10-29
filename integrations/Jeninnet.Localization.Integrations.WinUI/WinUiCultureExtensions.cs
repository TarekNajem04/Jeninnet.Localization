//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.WinUI;

/// <summary>
/// WinUI 3-specific culture helpers.
/// </summary>
public static class WinUiCultureExtensions {
    /// <summary>
    /// Maps the active culture direction to a WinUI <see cref="FlowDirection"/>.
    /// </summary>
    /// <param name="service">The host culture service.</param>
    /// <returns><see cref="FlowDirection.RightToLeft"/> for right-to-left cultures; otherwise <see cref="FlowDirection.LeftToRight"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
    public static FlowDirection ToFlowDirection(this HostCultureService service) {
        ArgumentNullException.ThrowIfNull(service);
        return service.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
