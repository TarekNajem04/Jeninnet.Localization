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
/// </summary>
public enum ProviderFeature {
    /// <summary>
    /// No capability. The explicit zero/default member: <see langword="default"/>(<see cref="ProviderFeature"/>)
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
    EventsListening = 2,

    /// <summary>
    /// The Provider can be told to populate its local resources in advance.
    /// </summary>
    Preload = 3,

    /// <summary>
    /// The Provider can invalidate and re-acquire its local resources.
    /// </summary>
    Refresh = 4,

    /// <summary>
    /// The Provider maintains an internal cache of resolved messages.
    /// </summary>
    Caching = 5
}
