//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Localization;

/// <summary>
/// Store-backed <see cref="IStringLocalizer"/> used by <see cref="CustomStringLocalizerFactory"/>.
/// Resolves the resource set through the store on every lookup so that a temporary store miss never
/// permanently binds the localizer to an empty resource set and no localizer-level cache exists
///. Missing entries surface the canonical missing marker; positive
/// caching belongs exclusively to the store decorators and is invalidated by store reload.
/// </summary>
/// <param name="localizationStore">The localization store that provides resource sets.</param>
/// <param name="resourceSet">The resource set name.</param>
/// <param name="culture">The culture identifier used for resource-set resolution.</param>
internal sealed class StoreBackedStringLocalizer(
    ILocalizationStore localizationStore,
    string resourceSet,
    string culture
) : IStringLocalizer {
    /// <inheritdoc/>
    public LocalizedString this[string name] {
        get {
            ArgumentNullException.ThrowIfNull(name);
            return new CustomStringLocalizer(ResolveResourceSet())[name];
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments] {
        get {
            ArgumentNullException.ThrowIfNull(name);
            return new CustomStringLocalizer(ResolveResourceSet())[name, arguments];
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        new CustomStringLocalizer(ResolveResourceSet()).GetAllStrings(includeParentCultures);

    /// <summary>
    /// Resolves the current resource set from the store. A missing set resolves to an empty set for
    /// this lookup only; a later lookup re-probes the store, so data that becomes available (or a
    /// store reload) is observed without re-creating the localizer.
    /// </summary>
    /// <returns>The resource set result.</returns>
    private ILocalizationResourceSet ResolveResourceSet() =>
        localizationStore.TryGetResourceSet(resourceSet, culture, out var foundResourceSet)
            ? foundResourceSet
            : new EmptyLocalizationResourceSet(resourceSet, culture);
}
