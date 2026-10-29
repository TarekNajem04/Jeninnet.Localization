//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// The completed ledger of a translation batch run: one entry per distinct requested
/// key per culture, in input order, plus outcome counts. Constructed by
/// <see cref="TranslationBatchJob"/> only. A cancelled run does not return normally —
/// it throws <see cref="TranslationBatchCancelledException"/> carrying the partial result.
/// </summary>
public sealed class TranslationBatchResult {
    /// <summary>
    /// Initializes a new instance of the TranslationBatchResult class.
    /// </summary>
    /// <param name="entries">The entries.</param>
    internal TranslationBatchResult(IReadOnlyList<TranslationBatchEntry> entries) => Entries = entries;

    /// <summary>
    /// Gets the ledger entries in input order (keys in first-seen order per culture,
    /// cultures in requested order).
    /// </summary>
    public IReadOnlyList<TranslationBatchEntry> Entries { get; }

    /// <summary>
    /// Gets the number of entries translated by the service during this run.
    /// </summary>
    public int TranslatedCount => Entries.Count(static entry => entry.Status == TranslationBatchStatus.Translated);

    /// <summary>
    /// Gets the number of entries served from the provider cache or translation memory.
    /// </summary>
    public int CachedCount => Entries.Count(static entry => entry.Status == TranslationBatchStatus.Cached);

    /// <summary>
    /// Gets the number of entries whose chunk acquisition failed.
    /// </summary>
    public int FailedCount => Entries.Count(static entry => entry.Status == TranslationBatchStatus.Failed);
}
