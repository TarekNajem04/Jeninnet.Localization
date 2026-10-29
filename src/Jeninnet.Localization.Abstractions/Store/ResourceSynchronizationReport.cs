//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The outcome of one resource synchronization: the per-entry
/// outcomes in deterministic order plus per-status counts. A dry-run report marks
/// <see cref="DryRun"/> and describes what would change; applying runs report what
/// changed. Idempotency contract: a second run mutates nothing and reports every
/// entry <see cref="ResourceSynchronizationStatus.Unchanged"/>.
/// </summary>
public sealed class ResourceSynchronizationReport {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSynchronizationReport"/> class.
    /// </summary>
    /// <param name="dryRun">Whether this is a dry-run preview.</param>
    /// <param name="defaultCulture">The default culture the synchronization targeted.</param>
    /// <param name="entries">The per-entry outcomes in deterministic order.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public ResourceSynchronizationReport(bool dryRun, string defaultCulture, IReadOnlyList<ResourceSynchronizationEntry> entries) {
        ArgumentNullException.ThrowIfNull(defaultCulture);
        ArgumentNullException.ThrowIfNull(entries);

        DryRun = dryRun;
        DefaultCulture = defaultCulture;
        Entries = entries;
    }

    /// <summary>
    /// Gets whether this report is a dry-run preview that changed nothing.
    /// </summary>
    public bool DryRun { get; }

    /// <summary>
    /// Gets the default culture the synchronization targeted.
    /// </summary>
    public string DefaultCulture { get; }

    /// <summary>
    /// Gets the per-entry outcomes in deterministic (resource set, key, culture, status) order.
    /// </summary>
    public IReadOnlyList<ResourceSynchronizationEntry> Entries { get; }

    /// <summary>
    /// Gets the total number of entries.
    /// </summary>
    public int Total => Entries.Count;

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Created"/> entries.
    /// </summary>
    public int Created => Count(ResourceSynchronizationStatus.Created);

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Updated"/> entries.
    /// </summary>
    public int Updated => Count(ResourceSynchronizationStatus.Updated);

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Unchanged"/> entries.
    /// </summary>
    public int Unchanged => Count(ResourceSynchronizationStatus.Unchanged);

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Skipped"/> entries.
    /// </summary>
    public int Skipped => Count(ResourceSynchronizationStatus.Skipped);

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Conflict"/> entries.
    /// </summary>
    public int Conflicts => Count(ResourceSynchronizationStatus.Conflict);

    /// <summary>
    /// Gets the number of <see cref="ResourceSynchronizationStatus.Renamed"/> entries.
    /// </summary>
    public int Renamed => Count(ResourceSynchronizationStatus.Renamed);

    /// <summary>
    /// Performs the count operation.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The count value.</returns>
    private int Count(ResourceSynchronizationStatus status) {
        var count = 0;
        foreach(var entry in Entries) {
            if(entry.Status == status) {
                count++;
            }
        }

        return count;
    }
}
