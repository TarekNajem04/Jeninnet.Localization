//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Options;

/// <summary>
/// Localization options for the canonical localization pipeline: the default-culture fallback
/// configuration consumed by the message localizer (<c>DefaultMessageLocalizer</c>). Provider
/// options types must not derive from this type — fallback ownership belongs to the localizer,
/// never to a provider.
/// </summary>
public class LocalizationOptions {
    /// <summary>
    /// Gets or sets the default culture to use when no other culture matches.
    /// </summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>
    /// Gets or sets the opt-in missing-key sink. The localizer reports a
    /// miss only after the full fallback chain resolved nothing; lookup semantics
    /// are unchanged (misses still return <see langword="null"/>). The default
    /// <see langword="null"/> disables collection with no overhead.
    /// </summary>
    public IMissingKeySink? MissingKeySink { get; set; }
}
