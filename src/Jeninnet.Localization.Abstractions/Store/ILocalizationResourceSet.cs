//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Represents a set of localized strings for a specific culture.
/// </summary>
public interface ILocalizationResourceSet {
    /// <summary>
    /// Gets the name of the resource set.
    /// </summary>
    string ResourceSet { get; }

    /// <summary>
    /// Gets the culture code of the resource set.
    /// </summary>
    string Culture { get; }

    /// <summary>
    /// Returns the culture as a <see cref="CultureInfo"/> object.
    /// </summary>
    /// <returns>
    /// The <see cref="CultureInfo"/> corresponding to the resource set's <see cref="Culture"/>.
    /// </returns>
    CultureInfo GetCultureInfo();

    /// <summary>
    /// Retrieves a localized string by its key.
    /// </summary>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the <see cref="LocalizedStringEntry"/>
    /// for the specified key.
    /// </returns>
    Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all localized string entries in the resource set.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary mapping each localized string key
    /// to its corresponding <see cref="LocalizedStringEntry"/>.
    /// </returns>
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a localized string by its key.
    /// </summary>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="entry">
    /// When this method returns, contains the <see cref="LocalizedStringEntry"/> corresponding to the specified key if found; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the localized string entry was found; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry);

    /// <summary>
    /// Adds a new localized string entry to the resource set.
    /// </summary>
    /// <param name="entry">The localized string entry to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple localized string entries to the resource set.
    /// </summary>
    /// <param name="entries">The localized string entries to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous bulk add operation.</returns>
    Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing localized string entry in the resource set.
    /// </summary>
    /// <param name="entry">The localized string entry to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple localized string entries in the resource set.
    /// </summary>
    /// <param name="entries">The localized string entries to update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous bulk update operation.</returns>
    Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a localized string entry from the resource set.
    /// </summary>
    /// <param name="entry">The localized string entry to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple localized string entries from the resource set.
    /// </summary>
    /// <param name="entries">The localized string entries to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous bulk delete operation.</returns>
    Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default);
}
