//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// One entry of a resource synchronization report: the subject
/// (<see cref="ResourceSet"/> + <see cref="Key"/> + <see cref="Culture"/>), the
/// <see cref="Status"/>, and a human-readable <see cref="Detail"/>. Rename entries are
/// key-global and carry an empty culture.
/// </summary>
public sealed class ResourceSynchronizationEntry {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSynchronizationEntry"/> class.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture code, or empty for key-global entries (renames).</param>
    /// <param name="status">The outcome status (never <see cref="ResourceSynchronizationStatus.None"/>).</param>
    /// <param name="detail">The human-readable detail.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="status"/> is <see cref="ResourceSynchronizationStatus.None"/>.</exception>
    public ResourceSynchronizationEntry(string resourceSet, string key, string culture, ResourceSynchronizationStatus status, string detail) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(detail);

        if(status == ResourceSynchronizationStatus.None) {
            throw new ArgumentException("A report entry must carry a real outcome status.", nameof(status));
        }

        ResourceSet = resourceSet;
        Key = key;
        Culture = culture;
        Status = status;
        Detail = detail;
    }

    /// <summary>
    /// Gets the resource-set name.
    /// </summary>
    public string ResourceSet { get; }

    /// <summary>
    /// Gets the message key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the culture code, or empty for key-global entries (renames).
    /// </summary>
    public string Culture { get; }

    /// <summary>
    /// Gets the outcome status.
    /// </summary>
    public ResourceSynchronizationStatus Status { get; }

    /// <summary>
    /// Gets the human-readable detail (never empty).
    /// </summary>
    public string Detail { get; }
}
