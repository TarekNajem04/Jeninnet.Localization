//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Memory;

/// <summary>
/// Provides access to localized messages from in-memory dictionary.
/// </summary>
/// <remarks>
/// Declares <see cref="IWasmCompatibleMessageProvider"/>: the message
/// table is a copied construction snapshot and template resolution performs no
/// I/O, so the provider is safe for browser WebAssembly compositions.
/// </remarks>
public class MemoryMessageProvider : MessageProviderBase, IInnerMessageProvider, IWasmCompatibleMessageProvider {
    private readonly Dictionary<string, string> _messages;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryMessageProvider"/> class.
    /// </summary>
    /// <param name="options">The in-memory localization options containing the message dictionary.</param>
    public MemoryMessageProvider(MemoryLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Messages);

        //  lifecycle invariant: immutable construction snapshot. The provider copies the
        // message table so later mutation of the caller-owned options object cannot alter behavior.
        _messages = new Dictionary<string, string>(options.Messages, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_messages.TryGetValue(key, out var message) ? message : null);
    }
}
