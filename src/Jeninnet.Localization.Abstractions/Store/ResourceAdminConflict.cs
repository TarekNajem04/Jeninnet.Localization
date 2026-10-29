//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// One reported domain conflict that prevented a resource-administration mutation
/// from applying. Produced by the administering store.
/// </summary>
public sealed class ResourceAdminConflict {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAdminConflict"/> class.
    /// </summary>
    /// <param name="kind">The conflict kind.</param>
    /// <param name="resourceSet">The resource-set name the mutation targeted.</param>
    /// <param name="culture">The culture the mutation targeted, or <see langword="null"/> when the mutation spans cultures.</param>
    /// <param name="key">The key the mutation targeted, or <see langword="null"/> when the mutation targets a whole resource set.</param>
    /// <param name="detail">A human-readable detail.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceSet"/> or <paramref name="detail"/> is <see langword="null"/>.</exception>
    public ResourceAdminConflict(
        ResourceAdminConflictKind kind,
        string resourceSet,
        string? culture,
        string? key,
        string detail) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(detail);

        Kind = kind;
        ResourceSet = resourceSet;
        Culture = culture;
        Key = key;
        Detail = detail;
    }

    /// <summary>
    /// Gets the conflict kind.
    /// </summary>
    public ResourceAdminConflictKind Kind { get; }

    /// <summary>
    /// Gets the resource-set name the mutation targeted.
    /// </summary>
    public string ResourceSet { get; }

    /// <summary>
    /// Gets the culture the mutation targeted, or <see langword="null"/> when the
    /// mutation spans cultures (key and resource-set renames).
    /// </summary>
    public string? Culture { get; }

    /// <summary>
    /// Gets the key the mutation targeted, or <see langword="null"/> when the
    /// mutation targets a whole resource set.
    /// </summary>
    public string? Key { get; }

    /// <summary>
    /// Gets a human-readable detail (for example which target key already exists).
    /// </summary>
    public string Detail { get; }
}
