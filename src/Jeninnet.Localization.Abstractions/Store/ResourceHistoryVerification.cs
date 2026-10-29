//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The outcome of a resource-history tamper check
/// (<see cref="IResourceHistoryStore.VerifyHistoryAsync"/>): whether the persisted
/// trail verifies against its hash chain, how many records were verified before a
/// failure, and — when tampering is detected — the sequence of the first record
/// whose chain linkage or recomputed hash does not match.
/// </summary>
public sealed class ResourceHistoryVerification {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceHistoryVerification"/> class.
    /// </summary>
    /// <param name="isValid">Whether the whole trail verifies against its hash chain.</param>
    /// <param name="verifiedRecords">The number of records that verified before the first failure (the total when valid).</param>
    /// <param name="firstInvalidSequence">The sequence of the first invalid record, or <see langword="null"/> when valid.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="verifiedRecords"/> is negative, when a valid result carries a first-invalid sequence, or when an invalid result carries none.</exception>
    public ResourceHistoryVerification(bool isValid, int verifiedRecords, long? firstInvalidSequence) {
        ArgumentOutOfRangeException.ThrowIfNegative(verifiedRecords);

        if(isValid && firstInvalidSequence is not null) {
            throw new ArgumentOutOfRangeException(nameof(firstInvalidSequence), "A valid verification result carries no first-invalid sequence.");
        }

        if(!isValid && firstInvalidSequence is null) {
            throw new ArgumentOutOfRangeException(nameof(firstInvalidSequence), "An invalid verification result must identify the first invalid record.");
        }

        IsValid = isValid;
        VerifiedRecords = verifiedRecords;
        FirstInvalidSequence = firstInvalidSequence;
    }

    /// <summary>
    /// Gets whether the whole trail verifies against its hash chain.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the number of records that verified before the first failure
    /// (the total record count when <see cref="IsValid"/> is <see langword="true"/>).
    /// </summary>
    public int VerifiedRecords { get; }

    /// <summary>
    /// Gets the sequence of the first record whose chain linkage or recomputed hash
    /// does not match, or <see langword="null"/> when the trail is valid.
    /// </summary>
    public long? FirstInvalidSequence { get; }
}
