//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Localization;

/// <summary>
/// Provides an implementation of <see cref="IStringLocalizerFactory"/> that creates
/// <see cref="CustomStringLocalizer"/> instances using an underlying <see cref="ILocalizationStore"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CustomStringLocalizerFactory"/> class.
/// </remarks>
/// <param name="localizationStore">The localization store that provides localized resource sets.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="localizationStore"/> is null.</exception>
public class CustomStringLocalizerFactory(ILocalizationStore localizationStore) : IStringLocalizerFactory {
    private readonly ILocalizationStore _localizationStore = localizationStore ?? throw new ArgumentNullException(nameof(localizationStore));

    /// <inheritdoc/>
    public IStringLocalizer Create(Type resourceSource) {
        ArgumentNullException.ThrowIfNull(resourceSource);

        // Use the resource type's Name as the resource set name.
        var resourceSet = resourceSource.Name;
        // Use the current UI culture to select the proper resource set.
        var culture = CultureInfo.CurrentUICulture.Name;
        return CreateStringLocalizer(resourceSet, culture);
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(string baseName, string location) {
        if(string.IsNullOrEmpty(baseName)) {
            throw new ArgumentNullException(nameof(baseName));
        }

        ArgumentNullException.ThrowIfNull(location);

        // Use the base name as the resource set name.
        var culture = CultureInfo.CurrentUICulture.Name;
        return CreateStringLocalizer(baseName, culture);
    }

    /// <summary>
    /// Creates a <see cref="IStringLocalizer"/> for the specified resource set and culture.
    /// </summary>
    /// <param name="resourceSet">The resource set to use.</param>
    /// <param name="culture">The culture identifier (e.g. "en-US").</param>
    /// <returns>A store-backed <see cref="IStringLocalizer"/> instance.</returns>
    /// <remarks>
    ///  lifecycle invariant: the returned localizer resolves the resource set through the
    /// store on every lookup. A temporary miss therefore never permanently binds an empty resource
    /// set, and no localizer-level cache exists; positive caching belongs to the store decorators
    /// and is invalidated by store reload.
    /// </remarks>
    protected IStringLocalizer CreateStringLocalizer(string resourceSet, string culture)
        => new StoreBackedStringLocalizer(_localizationStore, resourceSet, culture);
}
