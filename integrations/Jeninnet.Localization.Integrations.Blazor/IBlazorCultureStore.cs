//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Persists the user's culture choice for a Blazor host.
/// </summary>
/// <remarks>
/// The host wires the storage: register <see cref="BlazorCookieCultureStore"/>,
/// <see cref="BlazorLocalStorageCultureStore"/>, or a host-owned implementation. Without a
/// registration the culture service keeps the choice in process memory only (a no-op store
/// is used), so persistence is never a hidden requirement.
/// </remarks>
public interface IBlazorCultureStore {
    /// <summary>
    /// Reads the persisted culture name.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The persisted culture name, or <see langword="null"/> when nothing is stored.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the culture name.
    /// </summary>
    /// <param name="culture">The culture name to persist.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default);
}
