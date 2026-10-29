//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Localization;

/// <summary>
/// Represents a read-only fallback localization resource set that always returns missing entries.
/// This is used when no localized resources are found in the underlying store. Mutation methods
/// throw <see cref="NotSupportedException"/> because this sentinel cannot store resources.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EmptyLocalizationResourceSet"/> class.
/// </remarks>
/// <param name="resourceSet">The resource set name.</param>
/// <param name="culture">The culture identifier.</param>
public class EmptyLocalizationResourceSet(string resourceSet, string culture) : ILocalizationResourceSet {
    /// <inheritdoc/>
    public string ResourceSet { get; } = resourceSet;

    /// <inheritdoc/>
    public string Culture { get; } = culture;

    /// <inheritdoc/>
    public CultureInfo GetCultureInfo() => new(Culture);

    /// <inheritdoc/>
    public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(LocalizedStringEntry.Missing(ResourceSet, Culture, name));

    /// <inheritdoc/>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

    /// <inheritdoc/>
    public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
        entry = null;
        return false;
    }

    #region Unsupported Write Methods
    private const string NOT_SUPPORTED_MESSAGE = "EmptyLocalizationResourceSet does not support CRUD operations.";
    /// <inheritdoc/>
    public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    /// <inheritdoc/>
    public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    /// <inheritdoc/>
    public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    /// <inheritdoc/>
    public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    /// <inheritdoc/>
    public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    /// <inheritdoc/>
    public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

    #endregion Unsupported Write Methods
}
