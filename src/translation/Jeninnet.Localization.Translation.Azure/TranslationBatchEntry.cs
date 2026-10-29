//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// One ledger line of a translation batch run: a key, its target culture, the outcome, and
/// either the resolved template or the failure reason. Constructed by
/// <see cref="TranslationBatchJob"/> only.
/// </summary>
public sealed class TranslationBatchEntry {
    /// <summary>
    /// Initializes a new instance of the TranslationBatchEntry class.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="status">The status.</param>
    /// <param name="template">The message template.</param>
    /// <param name="failureReason">The failure reason.</param>
    internal TranslationBatchEntry(
        string key,
        CultureInfo culture,
        TranslationBatchStatus status,
        string? template,
        string? failureReason) {
        Key = key;
        Culture = culture;
        Status = status;
        Template = template;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the translated message key (the source template text).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the target culture the key was resolved for.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets the per-key outcome.
    /// </summary>
    public TranslationBatchStatus Status { get; }

    /// <summary>
    /// Gets the resolved template for <see cref="TranslationBatchStatus.Translated"/> and
    /// <see cref="TranslationBatchStatus.Cached"/> entries (<see langword="null"/> when the
    /// service resolved the key as missing); <see langword="null"/> for
    /// <see cref="TranslationBatchStatus.Failed"/> entries.
    /// </summary>
    public string? Template { get; }

    /// <summary>
    /// Gets the failure reason (<c>ExceptionType: message</c>) for
    /// <see cref="TranslationBatchStatus.Failed"/> entries; <see langword="null"/> otherwise.
    /// </summary>
    public string? FailureReason { get; }
}
