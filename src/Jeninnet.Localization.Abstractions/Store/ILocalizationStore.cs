//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Service for retrieving and managing localized string entries.
/// </summary>
public interface ILocalizationStore {
    /// <summary>
    /// Retrieves a specific resource set for a given culture.
    /// </summary>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the <see cref="ILocalizationResourceSet"/>
    /// for the specified resource set and culture if available; otherwise, <see langword="null"/>.
    /// </returns>
    Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all resource sets for a specific culture.
    /// </summary>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an enumerable of <see cref="ILocalizationResourceSet"/>
    /// for the specified culture.
    /// </returns>
    Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a resource set.
    /// </summary>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="localizationResourceSet">
    /// When this method returns, contains the <see cref="ILocalizationResourceSet"/> if found; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the resource set was found; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet);

    /// <summary>
    /// Retrieves a localized string for a specific key and culture.
    /// </summary>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the <see cref="LocalizedStringEntry"/>
    /// corresponding to the specified key.
    /// </returns>
    Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all localized string entries in a resource set for a specific culture.
    /// </summary>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary mapping each localized string key
    /// to its corresponding <see cref="LocalizedStringEntry"/>.
    /// </returns>
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all localized string entries for a specific culture across all resource sets.
    /// </summary>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary mapping each localized string key
    /// to its corresponding <see cref="LocalizedStringEntry"/> across all resource sets.
    /// </returns>
    Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a localized string entry.
    /// </summary>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="entry">
    /// When this method returns, contains the <see cref="LocalizedStringEntry"/> for the specified key if found; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the localized string entry was found; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry);

    /// <summary>
    /// Reloads the localization store from its underlying source.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous reload operation.</returns>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of supported culture codes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an enumerable of culture codes (e.g., "en-US", "nl-NL") supported by the localization store.
    /// </returns>
    Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default);
}
