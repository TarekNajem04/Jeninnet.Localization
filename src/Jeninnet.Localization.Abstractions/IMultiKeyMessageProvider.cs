//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message Provider that can resolve multiple message templates for a culture in a single
/// (batched) call. Optional capability; corresponds to <see cref="ProviderFeature.MultiKeys"/>.
/// </summary>
public interface IMultiKeyMessageProvider : IMessageProvider {
    /// <summary>
    /// Resolves message templates for the given <paramref name="keys"/> and <paramref name="culture"/>,
    /// typically in a single batched request.
    /// </summary>
    /// <param name="keys">The collection of message keys to resolve. Must not be null.</param>
    /// <param name="culture">The culture to resolve for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A dictionary mapping each requested key to its resolved template (or <see langword="null"/>
    /// when the template could not be resolved for that key).
    /// </returns>
    ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
        IReadOnlyCollection<string> keys,
        CultureInfo culture,
        CancellationToken cancellationToken = default);
}
