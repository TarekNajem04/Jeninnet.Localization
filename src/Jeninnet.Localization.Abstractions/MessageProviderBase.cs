//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#pragma warning disable S1694 // Abstract class without inheritable behavior is intentional — provides a common base type for all message providers
namespace Jeninnet.Localization.Abstractions;

/// <inheritdoc cref="IMessageProvider" />
public abstract class MessageProviderBase : IMessageProvider {
    /// <inheritdoc />
    public abstract ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default);
}
