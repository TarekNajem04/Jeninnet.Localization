//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The audit metadata shared by a resource-administration audit line
/// (<see cref="ResourceAdminAuditEventArgs"/>) and its persisted counterpart
/// (<see cref="ResourceHistoryRecord"/>): when the mutation committed, who performed
/// it, and which operation produced it.
/// </summary>
public sealed class ResourceAdminAuditContext {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceAdminAuditContext"/> class.
    /// </summary>
    /// <param name="timestamp">When the mutation committed (UTC).</param>
    /// <param name="actor">Who performed the mutation (empty when anonymous).</param>
    /// <param name="operation">The operation that produced the audit line.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    public ResourceAdminAuditContext(DateTimeOffset timestamp, string actor, ResourceAdminOperation operation) {
        ArgumentNullException.ThrowIfNull(actor);

        Timestamp = timestamp;
        Actor = actor;
        Operation = operation;
    }

    /// <summary>
    /// Gets when the mutation committed (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets who performed the mutation (the caller-supplied actor; empty when anonymous).
    /// </summary>
    public string Actor { get; }

    /// <summary>
    /// Gets the operation that produced the audit line.
    /// </summary>
    public ResourceAdminOperation Operation { get; }
}
