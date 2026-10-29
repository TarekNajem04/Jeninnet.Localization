//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// A point-in-time dry-run estimate for a translation batch: how many keys are already free
/// (provider cache or translation-memory hits) and how many paid translation calls the real
/// run would make. Produced by <see cref="TranslationBatchJob.EstimateAsync"/> without any
/// HTTP request, cache write, or event.
/// </summary>
/// <remarks>
/// The estimate simulates the real run's chunk windows exactly (distinct keys in input order
/// per culture, windows of <c>MaxChunkSize</c>): a window containing at least one miss counts
/// one billable call. It assumes a quiescent provider and fault-free transport — cache TTL (Time To Live)
/// expiry, concurrent writers, retries, and breaker activity between estimate and run change
/// the real count. Idempotent re-runs of a completed backfill estimate (and make) zero calls.
/// </remarks>
public sealed class TranslationBatchEstimate {
    /// <summary>
    /// Initializes a new instance of the TranslationBatchEstimate class.
    /// </summary>
    /// <param name="totalKeys">The total keys.</param>
    /// <param name="freeKeys">The free keys.</param>
    /// <param name="billableKeys">The billable keys.</param>
    /// <param name="billableCalls">The billable calls.</param>
    internal TranslationBatchEstimate(int totalKeys, int freeKeys, int billableKeys, int billableCalls) {
        TotalKeys = totalKeys;
        FreeKeys = freeKeys;
        BillableKeys = billableKeys;
        BillableCalls = billableCalls;
    }

    /// <summary>
    /// Gets the number of distinct requested keys across all requested cultures.
    /// </summary>
    public int TotalKeys { get; }

    /// <summary>
    /// Gets the number of keys already served locally (cache or translation-memory hits):
    /// free at estimate time.
    /// </summary>
    public int FreeKeys { get; }

    /// <summary>
    /// Gets the number of keys requiring translation.
    /// </summary>
    public int BillableKeys { get; }

    /// <summary>
    /// Gets the estimated number of paid HTTP translation calls (one per chunk window that
    /// contains at least one billable key).
    /// </summary>
    public int BillableCalls { get; }
}
