//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Per-call options for a governed resource-administration mutation: dry-run preview
/// mode and the caller-supplied actor identity recorded on every audit entry.
/// </summary>
public sealed class ResourceAdminOptions {
    /// <summary>
    /// Gets whether the mutation only computes its <see cref="ResourceAdminResult"/>
    /// without changing anything. Dry-run previews raise no audit entries.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets the caller-supplied actor identity recorded on every
    /// <see cref="ResourceAdminAuditEventArgs"/> (empty when anonymous).
    /// </summary>
    public string Actor { get; init; } = string.Empty;
}
