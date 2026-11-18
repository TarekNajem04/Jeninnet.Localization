//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// Shared capability mapping for the caching decorators: both decorators expose the same
/// multi-key, event, preload, refresh, and caching surface over their inner provider.
/// </summary>
internal static class CachingCapability {
    /// <summary>
    /// Resolves whether the caching surface supports a feature.
    /// </summary>
    /// <param name="feature">The queried feature.</param>
    /// <returns>True when supported.</returns>
    internal static bool Supports(ProviderFeatures feature) => feature switch {
        ProviderFeatures.MultiKeys => true,
        ProviderFeatures.EventsListening => true,
        ProviderFeatures.Preload => true,
        ProviderFeatures.Refresh => true,
        ProviderFeatures.Caching => true,
        _ => false,
    };
}
