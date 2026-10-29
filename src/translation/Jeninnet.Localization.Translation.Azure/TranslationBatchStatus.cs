//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// The per-key outcome recorded in a translation batch ledger
/// (see <see cref="TranslationBatchEntry"/>).
/// </summary>
public enum TranslationBatchStatus {
    /// <summary>
    /// The key was translated by the machine-translation service during this run. A
    /// <see langword="null"/> template means the service resolved the key as missing.
    /// </summary>
    Translated = 0,

    /// <summary>
    /// The key required no paid call: it was served from the provider cache or the
    /// translation memory.
    /// </summary>
    Cached = 1,

    /// <summary>
    /// The key was not resolved: its chunk acquisition failed. The reason is recorded on
    /// the entry; chunk failures never abort the batch.
    /// </summary>
    Failed = 2
}
