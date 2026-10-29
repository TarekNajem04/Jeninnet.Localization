//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Identifies the expected domain conflict that prevented a resource-administration
/// mutation from applying. Conflicts are reported in
/// <see cref="ResourceAdminResult"/> — they are never thrown — so editors, the XLIFF (XML Localization Interchange File Format)
/// importer, and the translation batcher share one deterministic reporting shape.
/// </summary>
public enum ResourceAdminConflictKind {
    /// <summary>
    /// No conflict; the mutation applied (or would apply in dry-run mode).
    /// </summary>
    None = 0,

    /// <summary>
    /// The entry already exists (create path).
    /// </summary>
    AlreadyExists = 1,

    /// <summary>
    /// The entry, key, or resource set does not exist.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// A rename target already exists and the rename was refused atomically.
    /// </summary>
    TargetExists = 3,
}
