//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

internal static class LocalizationStoreReads {
    public static async Task<LocalizedStringEntry> GetStringAsync(
        Func<string, string, CancellationToken, Task<ILocalizationResourceSet?>> getResourceSetAsync,
        string resourceSet,
        string culture,
        string name,
        CancellationToken cancellationToken
    ) {
        var cachedResourceSet = await getResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

        if(cachedResourceSet is null) {
            return LocalizedStringEntry.Missing(resourceSet, culture, name);
        }

        return await cachedResourceSet.GetStringAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(
        Func<string, string, CancellationToken, Task<ILocalizationResourceSet?>> getResourceSetAsync,
        string resourceSet,
        string culture,
        CancellationToken cancellationToken
    ) {
        var cachedResourceSet = await getResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);

        if(cachedResourceSet is null) {
            return [];
        }

        return await cachedResourceSet.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
    }
}
