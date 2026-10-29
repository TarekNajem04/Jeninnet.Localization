//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// A persisted translator note: key-level, language-agnostic context attached
/// to one message key of one resource set (hint, usage, tone, screenshot reference).
/// Notes are governed data: they mutate through the  admin path only, move and
/// die with the key lifecycle (rename, last-row deletion, set rename), and never
/// outlive their key (no orphan rows). The unique index on
/// (<see cref="ResourceSet"/>, <see cref="Name"/>) enforces one note per key.
/// </summary>
public sealed class LocalizedStringNote {
    /// <summary>
    /// Gets or sets the persisted identity of the note (preserved across renames).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the resource-set name the note's key belongs to (the new name for
    /// set renames).
    /// </summary>
    public string ResourceSet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message key the note is attached to (the new name for renames).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the note text (empty is a valid stored value).
    /// </summary>
    public string Note { get; set; } = string.Empty;
}
