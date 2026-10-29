//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The typed rejection for a resource-history read against a store that has no
/// history support: either the store does not implement
/// <see cref="IResourceHistoryStore"/> or it honestly reports
/// <see cref="IResourceHistoryStore.SupportsHistory"/> as <see langword="false"/>.
/// Stores without history reject with this type — never silently and never with only
/// a framework-level <see cref="NotSupportedException"/> — so operators and tooling
/// can distinguish "no history support" from other failures.
/// </summary>
public class StoreHistoryUnsupportedException : NotSupportedException {
    /// <inheritdoc/>
    public StoreHistoryUnsupportedException(string message) : base(message) { }

    /// <inheritdoc/>
    public StoreHistoryUnsupportedException() { }

    /// <inheritdoc/>
    public StoreHistoryUnsupportedException(string? message, Exception? innerException) : base(message, innerException) { }
}
