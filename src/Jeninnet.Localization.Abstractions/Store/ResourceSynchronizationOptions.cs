//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Per-call options for a resource synchronization: the default
/// culture, the target cultures, dry-run preview mode, the explicit update gate, the
/// explicit target-culture placeholder gate, the explicit rename map, and the actor
/// recorded on every governed mutation.
/// </summary>
public sealed class ResourceSynchronizationOptions {
    /// <summary>
    /// Gets the default culture whose entries are created from the declared templates
    /// and (when <see cref="AllowUpdates"/> is set) kept in sync. Required.
    /// </summary>
    public string DefaultCulture { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target cultures considered for every declaration, in addition to the
    /// default culture. Existing target-culture entries are never modified; missing
    /// ones are skipped unless <see cref="CreateMissingTargetCultures"/> is set.
    /// The default culture is never duplicated.
    /// </summary>
    public IReadOnlyList<string> TargetCultures { get; init; } = [];

    /// <summary>
    /// Gets whether the synchronization only computes its report without changing
    /// anything. Dry-run previews mutate nothing and raise no audit entries (the
    /// governed store's own guarantee).
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets whether differing default-culture values (and declared notes) may be
    /// updated through the governed path. Defaults to <see langword="false"/>: adds
    /// only. Non-default culture values are never updated.
    /// </summary>
    public bool AllowUpdates { get; init; }

    /// <summary>
    /// Gets whether missing target-culture entries are created as empty
    /// (untranslated) placeholders. Defaults to <see langword="false"/>: a missing
    /// target-culture entry keeps resolving through the fallback chain (an empty
    /// stored value would suppress it).
    /// </summary>
    public bool CreateMissingTargetCultures { get; init; }

    /// <summary>
    /// Gets the explicit rename map (old key to new key) applied within every
    /// synchronized resource set where the new key is declared, the old key is no
    /// longer declared, and the old key exists. Renames are never inferred.
    /// </summary>
    public IReadOnlyDictionary<string, string> Renames { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the caller-supplied actor identity recorded on every governed mutation
    /// (empty when anonymous).
    /// </summary>
    public string Actor { get; init; } = string.Empty;
}
