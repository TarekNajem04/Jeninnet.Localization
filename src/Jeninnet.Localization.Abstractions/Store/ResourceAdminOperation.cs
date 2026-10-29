//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Identifies a governed resource-administration mutation.
/// The explicit zero member <see cref="None"/> must never silently represent a real
/// operation: a default-constructed value carries no mutation semantics.
/// </summary>
public enum ResourceAdminOperation {
    /// <summary>
    /// No operation. The explicit zero/default member.
    /// </summary>
    None = 0,

    /// <summary>
    /// Creates a single localized string entry; conflicts when the key already exists.
    /// </summary>
    CreateEntry = 1,

    /// <summary>
    /// Updates the template of an existing entry; conflicts when the key is missing.
    /// </summary>
    UpdateEntry = 2,

    /// <summary>
    /// Deletes a single entry; conflicts when the key is missing.
    /// </summary>
    DeleteEntry = 3,

    /// <summary>
    /// Renames a key across every culture of a resource set atomically.
    /// </summary>
    RenameKey = 4,

    /// <summary>
    /// Deletes every entry of one resource-set/culture pair.
    /// </summary>
    DeleteResourceSet = 5,

    /// <summary>
    /// Renames a resource set across every culture atomically.
    /// </summary>
    RenameResourceSet = 6,

    /// <summary>
    /// Creates or updates the translator note of a key; the key must exist
    /// in at least one culture.
    /// </summary>
    SetNote = 7,

    /// <summary>
    /// Deletes the translator note of a key; conflicts when no note exists.
    /// </summary>
    DeleteNote = 8,
}
