//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core;

/// <summary>
/// Enforces the exactly-one-Provider architectural rule: Core accepts a single resolved
/// localized message Provider. Zero Providers preserve the existing Core policy (lookups
/// return <see langword="null"/>); more than one Provider is an invalid configuration and
/// throws at resolution/construction time.
/// </summary>
/// <remarks>
/// The resolved provider instances are the authoritative runtime provider set; provider
/// cardinality is never determined by DI (Dependency Injection) service-descriptor scanning. Provider chaining,
/// composition, priority, and fallback are not part of the architecture (the exactly-one-provider rule).
/// </remarks>
public sealed class LocalizationProviderSelector : IMessageProvider {
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<IInnerMessageProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationProviderSelector"/> class.
    /// </summary>
    /// <param name="providers">The resolved inner message providers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="providers"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when more than one provider is resolved.</exception>
    public LocalizationProviderSelector(IEnumerable<IInnerMessageProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToList().AsReadOnly();

        if(_providers.Count > 1) {
            var types = string.Join(", ", _providers.Select(static provider => provider.GetType().FullName));
            throw new InvalidOperationException(
                $"Exactly one Localization Provider must be registered, but {_providers.Count} Providers were resolved: {types}. Provider chaining/composition is forbidden (the exactly-one-provider rule).");
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        if(_providers.Count == 0) {
            return null;
        }

        return await _providers[0].GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
    }
}
