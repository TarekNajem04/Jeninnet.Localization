//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Abstractions;

/// <summary>
/// The per-key outcome of a catalog interchange import (the shared conflict policy,
///  /). Every interchange format reports through this single
/// vocabulary so a key's status means the same thing everywhere.
/// </summary>
public enum CatalogKeyStatus {
    /// <summary>
    /// The target already matches the catalog template (or both are missing); nothing applied.
    /// </summary>
    Unchanged = 0,
    /// <summary>
    /// The target differs from the catalog template and was applied (or would be, in dry-run mode).
    /// </summary>
    Updated = 1,
    /// <summary>
    /// The unit or row was ignored: empty or missing target, or <c>translate="no"</c>. Nothing applied;
    /// existing templates are never cleared by an empty target.
    /// </summary>
    Skipped = 2,
    /// <summary>
    /// The key is unknown to the catalogs. Keys are governed;
    /// interchange never invents keys. Reported, never applied.
    /// </summary>
    UnknownKey = 3,
    /// <summary>
    /// The unit source (or source row) differs from the current source-catalog template: the
    /// translator worked from a stale source. Reported, never applied.
    /// </summary>
    StaleSource = 4,
    /// <summary>
    /// The key cannot be represented in the target catalog format (for example a plural form
    /// beyond the pair slots, or a contextual key against a flat XML catalog). Reported,
    /// never applied; no data is silently dropped.
    /// </summary>
    UnsupportedKey = 5,
}
