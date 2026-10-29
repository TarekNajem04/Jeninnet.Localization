//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Cumulative progress of a translation batch run, reported through
/// <see cref="IProgress{T}"/> after every chunk window. Constructed by
/// <see cref="TranslationBatchJob"/> only.
/// </summary>
public sealed class TranslationBatchProgress {
    /// <summary>
    /// Initializes a new instance of the TranslationBatchProgress class.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="processedKeys">The processed keys.</param>
    /// <param name="totalKeys">The total keys.</param>
    /// <param name="translated">The translated.</param>
    /// <param name="cached">The cached.</param>
    /// <param name="failed">The failed.</param>
    internal TranslationBatchProgress(
        CultureInfo culture,
        int processedKeys,
        int totalKeys,
        int translated,
        int cached,
        int failed) {
        Culture = culture;
        ProcessedKeys = processedKeys;
        TotalKeys = totalKeys;
        Translated = translated;
        Cached = cached;
        Failed = failed;
    }

    /// <summary>
    /// Gets the culture of the chunk window just processed.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets the number of keys processed so far (across all cultures).
    /// </summary>
    public int ProcessedKeys { get; }

    /// <summary>
    /// Gets the total number of distinct requested keys across all requested cultures.
    /// </summary>
    public int TotalKeys { get; }

    /// <summary>
    /// Gets the running number of keys translated by the service so far.
    /// </summary>
    public int Translated { get; }

    /// <summary>
    /// Gets the running number of keys served from cache or translation memory so far.
    /// </summary>
    public int Cached { get; }

    /// <summary>
    /// Gets the running number of failed keys so far.
    /// </summary>
    public int Failed { get; }
}
