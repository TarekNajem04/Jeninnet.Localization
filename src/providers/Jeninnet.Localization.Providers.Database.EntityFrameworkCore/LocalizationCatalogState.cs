//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// Single-row version counter for the persisted catalog. The row
/// (<see cref="Id"/> is always 1) is created lazily by the first governed
/// mutation; increments use an optimistic conditional update so concurrent
/// writers cannot hand two batches the same version.
/// </summary>
public sealed class LocalizationCatalogState {
    /// <summary>
    /// Gets or sets the singleton row key (always 1).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the last assigned catalog version (0 before the first governed mutation).
    /// </summary>
    public long Version { get; set; }
}
