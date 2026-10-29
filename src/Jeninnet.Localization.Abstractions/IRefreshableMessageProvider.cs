//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// A message Provider that can invalidate and re-acquire its local resources (such as an internal
/// cache). Optional capability; corresponds to <see cref="ProviderFeature.Refresh"/>.
/// </summary>
public interface IRefreshableMessageProvider {
    /// <summary>
    /// Invalidates any internally cached or otherwise locally held resources, so that subsequent
    /// lookups are re-acquired from the underlying source. In-flight external acquisitions are not
    /// canceled; stale completions are discarded by the Provider.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the refresh operation itself; an already-canceled
    /// token prevents the invalidation and surfaces <see cref="OperationCanceledException"/>.</param>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
