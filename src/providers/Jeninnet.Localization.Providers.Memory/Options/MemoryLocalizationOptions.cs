//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Memory.Options;

/// <summary>
/// Options for Memory localization provider.
/// </summary>
public class MemoryLocalizationOptions {
    /// <summary>
    /// Gets or sets the dictionary of localization messages. The key is the message identifier, and the value is the localized message.
    /// </summary>
    public IReadOnlyDictionary<string, string> Messages { get; set; } = new Dictionary<string, string>();
}
