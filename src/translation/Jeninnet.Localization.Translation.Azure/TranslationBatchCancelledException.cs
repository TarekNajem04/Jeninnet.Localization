//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// The exception thrown when a translation batch run is cancelled: the job stops promptly
/// (no further chunk is started; the in-flight chunk observes the token) and the ledger
/// completed so far is preserved on <see cref="PartialResult"/>. It derives from
/// <see cref="OperationCanceledException"/>, so existing cancellation handling observes it
/// as a cancellation. The job performs no writes, so cancellation never leaves orphaned
/// writes behind.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TranslationBatchCancelledException"/> class.
/// </remarks>
/// <param name="message">The diagnostic message.</param>
/// <param name="partialResult">The ledger completed before cancellation.</param>
/// <param name="cancellationToken">The token that requested cancellation.</param>
public sealed class TranslationBatchCancelledException(
    string? message,
    TranslationBatchResult partialResult,
    CancellationToken cancellationToken) : OperationCanceledException(message, cancellationToken) {

    /// <summary>
    /// Gets the ledger completed before cancellation, in input order.
    /// </summary>
    public TranslationBatchResult PartialResult { get; } = partialResult ?? throw new ArgumentNullException(nameof(partialResult));
}
