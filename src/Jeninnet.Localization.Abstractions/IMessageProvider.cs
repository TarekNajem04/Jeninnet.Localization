//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Provides a mechanism to retrieve localized message templates.
/// </summary>
public interface IMessageProvider {
    /// <summary>
    /// Gets the message template for the specified key and culture.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">
    /// An optional token to cancel the asynchronous operation. The default value preserves the
    /// pre-cancellation behavior. Cancellation is observed after argument validation and must
    /// propagate to the provider's cancellable I/O; a cancelled lookup throws
    /// <see cref="OperationCanceledException"/> and is never converted into a missing-resource result.
    /// </param>
    /// <returns>The message template or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="culture"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default);
}
