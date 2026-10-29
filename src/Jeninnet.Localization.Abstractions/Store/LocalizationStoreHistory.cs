//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Per-store capability advertisement for persisted resource history.
/// Tooling enters through <see cref="RequireHistory"/> so a store without history
/// rejects with the typed <see cref="StoreHistoryUnsupportedException"/> — never
/// silently.
/// </summary>
public static class LocalizationStoreHistory {
    /// <summary>
    /// Returns whether <paramref name="store"/> currently supports persisted history
    /// and versioned reads: it implements <see cref="IResourceHistoryStore"/> and
    /// honestly reports <see cref="IResourceHistoryStore.SupportsHistory"/>.
    /// </summary>
    /// <param name="store">The store to probe.</param>
    /// <returns><see langword="true"/> when the store supports history; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public static bool SupportsHistory(this ILocalizationStore store) {
        ArgumentNullException.ThrowIfNull(store);
        return store is IResourceHistoryStore history && history.SupportsHistory;
    }

    /// <summary>
    /// Returns the history capability for <paramref name="store"/>, or throws the
    /// typed <see cref="StoreHistoryUnsupportedException"/> when the store has no
    /// history support.
    /// </summary>
    /// <param name="store">The store to read history from.</param>
    /// <returns>The store's <see cref="IResourceHistoryStore"/> capability.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="StoreHistoryUnsupportedException">Thrown when the store does not support history.</exception>
    public static IResourceHistoryStore RequireHistory(this ILocalizationStore store) {
        ArgumentNullException.ThrowIfNull(store);

        if(store is IResourceHistoryStore history && history.SupportsHistory) {
            return history;
        }

        throw new StoreHistoryUnsupportedException(
            $"Store '{store.GetType().FullName}' does not support resource history and rejects history reads.");
    }
}
