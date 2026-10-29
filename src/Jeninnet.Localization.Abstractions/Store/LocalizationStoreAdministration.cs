//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Per-store capability advertisement for the governed resource-administration path
///. Editors, the XLIFF (XML Localization Interchange File Format) importer, and the translation batcher enter through
/// <see cref="RequireAdmin"/> so a read-only store rejects with the typed
/// <see cref="StoreReadOnlyException"/> — never silently.
/// </summary>
public static class LocalizationStoreAdministration {
    /// <summary>
    /// Returns whether <paramref name="store"/> currently supports governed
    /// administration: it implements <see cref="IResourceAdminStore"/> and honestly
    /// reports <see cref="IResourceAdminStore.SupportsAdministration"/>.
    /// </summary>
    /// <param name="store">The store to probe.</param>
    /// <returns><see langword="true"/> when the store can administer resources; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public static bool SupportsAdministration(this ILocalizationStore store) {
        ArgumentNullException.ThrowIfNull(store);
        return store is IResourceAdminStore admin && admin.SupportsAdministration;
    }

    /// <summary>
    /// Returns the administration capability for <paramref name="store"/>, or throws
    /// the typed <see cref="StoreReadOnlyException"/> when the store is read-only.
    /// </summary>
    /// <param name="store">The store to administer.</param>
    /// <returns>The store's <see cref="IResourceAdminStore"/> capability.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    public static IResourceAdminStore RequireAdmin(this ILocalizationStore store) {
        ArgumentNullException.ThrowIfNull(store);

        if(store is IResourceAdminStore admin && admin.SupportsAdministration) {
            return admin;
        }

        throw new StoreReadOnlyException(
            $"Store '{store.GetType().FullName}' does not support resource administration and rejects the governed write path.");
    }
}
