//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The per-entry outcome of a resource synchronization. Mirrors the
/// honest-zero pattern of <see cref="ResourceAdminOperation"/>: the explicit zero
/// member carries no outcome and must never silently represent a real one.
/// </summary>
public enum ResourceSynchronizationStatus {
    /// <summary>
    /// No outcome. The explicit zero/default member.
    /// </summary>
    None = 0,

    /// <summary>
    /// The entry was created (or would be, in dry-run mode).
    /// </summary>
    Created = 1,

    /// <summary>
    /// The existing default-culture entry (or its declared note) was updated (or would
    /// be, in dry-run mode). Non-default culture values are never updated.
    /// </summary>
    Updated = 2,

    /// <summary>
    /// The store already matches the declaration; nothing was touched.
    /// </summary>
    Unchanged = 3,

    /// <summary>
    /// The declaration was deliberately not applied (for example a differing value with
    /// updates not allowed, or a target culture left to fallback). The detail explains why.
    /// </summary>
    Skipped = 4,

    /// <summary>
    /// The governed store refused the mutation and reported a conflict; nothing applied.
    /// </summary>
    Conflict = 5,

    /// <summary>
    /// An explicit mapped rename applied (or would be, in dry-run mode). Renames are
    /// key-global (cross-culture atomic), so their entries carry an empty culture.
    /// </summary>
    Renamed = 6,
}
