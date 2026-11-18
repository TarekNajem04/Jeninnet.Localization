//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Identifies an optional capability that a message Provider may support.
/// Provider-neutral feature names; see <see cref="IProviderCapabilities"/>.
/// Flag values are powers of two so callers can combine and test feature sets
/// with bitwise operations; <see cref="None"/> stays zero.
/// </summary>
[Flags]
public enum ProviderFeatures {
    /// <summary>
    /// No capability. The explicit zero/default member: <see langword="default"/>(<see cref="ProviderFeatures"/>)
    /// must never silently represent a real capability (see <see cref="IProviderCapabilities"/>).
    /// </summary>
    None = 0,

    /// <summary>
    /// The Provider can resolve several message templates in a single (batched) call.
    /// </summary>
    MultiKeys = 1,

    /// <summary>
    /// The Provider raises acquisition events that consumers may subscribe to.
    /// </summary>
    EventsListening = 1 << 1,

    /// <summary>
    /// The Provider can be told to populate its local resources in advance.
    /// </summary>
    Preload = 1 << 2,

    /// <summary>
    /// The Provider can invalidate and re-acquire its local resources.
    /// </summary>
    Refresh = 1 << 3,

    /// <summary>
    /// The Provider maintains an internal cache of resolved messages.
    /// </summary>
    Caching = 1 << 4,

    /// <summary>
    /// The Provider can translate an explicit source template instead of an opaque
    /// key (see <see cref="ISourceTemplateMessageProvider"/>).
    /// </summary>
    SourceTemplates = 1 << 5,
}
