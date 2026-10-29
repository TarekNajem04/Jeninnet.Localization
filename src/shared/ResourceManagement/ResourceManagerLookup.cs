//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Globalization;
using System.Resources;

#if JENINNET_PROVIDER_SHARED_RESX
namespace Jeninnet.Localization.Providers.ResX;
#elif JENINNET_PROVIDER_SHARED_SATELLITE
namespace Jeninnet.Localization.Providers.Satellite;
#else
#error A provider project must define JENINNET_PROVIDER_SHARED_RESX or JENINNET_PROVIDER_SHARED_SATELLITE.
#endif

internal static class ResourceManagerLookup {
    public static string? GetString(ResourceManager resourceManager, string key, CultureInfo culture) {
        var resourceSet = GetResourceSet(resourceManager, key, culture);
        return resourceSet?.GetString(key, ignoreCase: false);
    }

    public static string? GetStringOrNull(ResourceManager resourceManager, string key, CultureInfo culture) {
        var resourceSet = GetResourceSet(resourceManager, key, culture);

        if(resourceSet is null) {
            return null;
        }

        try {
            return resourceSet.GetString(key, ignoreCase: false);
        }
        catch(InvalidOperationException) {
            return null;
        }
    }

    private static ResourceSet? GetResourceSet(ResourceManager resourceManager, string key, CultureInfo culture) {
        ArgumentNullException.ThrowIfNull(resourceManager);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        var resourceSet = resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);

        if(resourceSet is null) {
            var invariantResourceSet = culture == CultureInfo.InvariantCulture
                ? null
                : resourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);

            if(invariantResourceSet is null) {
                _ = resourceManager.GetString(key, CultureInfo.InvariantCulture);
            }
        }

        return resourceSet;
    }
}
