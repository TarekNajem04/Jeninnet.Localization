//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message Provider that can be told to populate its local resources (such as an internal cache)
/// for a culture in advance. Optional capability; corresponds to <see cref="ProviderFeature.Preload"/>.
/// </summary>
public interface IPreloadableMessageProvider {
    /// <summary>
    /// Preloads message templates for the given <paramref name="keys"/> and <paramref name="culture"/>
    /// so that subsequent single-key lookups can be served locally.
    /// </summary>
    /// <param name="keys">The collection of message keys to preload. Must not be null.</param>
    /// <param name="culture">The culture to preload for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    ValueTask PreloadAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}
