//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Localization;

/// <summary>
/// Provides a strongly typed implementation of <see cref="IStringLocalizer{T}"/>
/// using an underlying <see cref="ILocalizationResourceSet"/> retrieved from an <see cref="ILocalizationStore"/>.
/// </summary>
/// <remarks>
/// The name of type <typeparamref name="T"/> is used as the resource set name.
/// The current UI culture is captured at construction, while the resource set is re-probed for
/// each operation so a temporary store miss does not permanently bind the localizer to an empty set.
/// </remarks>
/// <typeparam name="T">
/// The type for which the localizer is targeted.
/// Its name will be used as the resource set name.
/// </typeparam>
public class CustomStringLocalizer<T> : CustomStringLocalizer, IStringLocalizer<T> {
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomStringLocalizer{T}"/> class.
    /// </summary>
    /// <param name="localizationStore">An instance of <see cref="ILocalizationStore"/> used for retrieving dynamic localization resources.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="localizationStore"/> is <see langword="null"/>.</exception>
    public CustomStringLocalizer(ILocalizationStore localizationStore) {
        ArgumentNullException.ThrowIfNull(localizationStore);

        // Use the type name (T) as the resource set name.
        var resourceSetName = typeof(T).Name;
        // Use the current UI culture.
        var culture = CultureInfo.CurrentUICulture.Name;

        // Keep the resource-set identity dynamic so a temporary store miss does not permanently
        // bind this framework-facing localizer to an empty sentinel.
        LocalizationResourceSet = new StoreBackedLocalizationResourceSet(localizationStore, resourceSetName, culture);
    }
}

/// <summary>
/// Adapts the asynchronous store contract to the resource-set contract consumed by
/// <see cref="CustomStringLocalizer"/> while re-probing the store for every operation.
/// </summary>
/// <param name="localizationStore">The store re-probed for every operation.</param>
/// <param name="resourceSet">The resource-set name resolved through the store.</param>
/// <param name="culture">The captured culture name resolved through the store.</param>
internal sealed class StoreBackedLocalizationResourceSet(
    ILocalizationStore localizationStore,
    string resourceSet,
    string culture
) : ILocalizationResourceSet {
    private readonly ILocalizationStore _localizationStore = localizationStore ?? throw new ArgumentNullException(nameof(localizationStore));

    /// <summary>
    /// Gets the resource set.
    /// </summary>
    public string ResourceSet { get; } = resourceSet ?? throw new ArgumentNullException(nameof(resourceSet));

    /// <summary>
    /// Gets the culture.
    /// </summary>
    public string Culture { get; } = culture ?? throw new ArgumentNullException(nameof(culture));

    /// <summary>
    /// Gets culture info.
    /// </summary>
    /// <returns>The culture info result.</returns>
    public CultureInfo GetCultureInfo() => new(Culture);

    /// <summary>
    /// Gets string.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is string.</returns>
    public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) =>
        _localizationStore.GetStringAsync(ResourceSet, Culture, name, cancellationToken);

    /// <summary>
    /// Gets all strings.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is all strings.</returns>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) =>
        _localizationStore.GetAllStringsAsync(ResourceSet, Culture, cancellationToken);

    /// <summary>
    /// Attempts to get string.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) =>
        _localizationStore.TryGetString(ResourceSet, Culture, name, out entry);

    /// <summary>
    /// Performs the add operation.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().AddAsync(entry, cancellationToken);

    /// <summary>
    /// Performs the add operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().AddAsync(entries, cancellationToken);

    /// <summary>
    /// Performs the update operation.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().UpdateAsync(entry, cancellationToken);

    /// <summary>
    /// Performs the update operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().UpdateAsync(entries, cancellationToken);

    /// <summary>
    /// Performs the delete operation.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().DeleteAsync(entry, cancellationToken);

    /// <summary>
    /// Performs the delete operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
        ResolveResourceSet().DeleteAsync(entries, cancellationToken);

    /// <summary>
    /// Resolves resource set.
    /// </summary>
    /// <returns>The resource set result.</returns>
    private ILocalizationResourceSet ResolveResourceSet() =>
        _localizationStore.TryGetResourceSet(ResourceSet, Culture, out var foundResourceSet)
            ? foundResourceSet
            : new EmptyLocalizationResourceSet(ResourceSet, Culture);
}
