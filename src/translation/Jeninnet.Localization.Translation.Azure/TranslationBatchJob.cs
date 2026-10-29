//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Governed batch translation for backfilling catalogs: translates keys across
/// cultures with progress reporting, cancellation, dry-run estimates, and a per-key
/// success/failure ledger.
/// </summary>
/// <remarks>
/// <para>
/// The job reuses the provider pipeline instead of bypassing it: every chunk window goes
/// through the owning <see cref="AzureTranslationProvider"/> (cache, translation memory,
/// glossary, resilience), so estimates, ledgers, and idempotent re-runs observe exactly
/// what the provider resolves. The job performs no writes of its own — it produces
/// translated payloads plus a ledger for the  apply path — so cancellation never
/// leaves orphaned writes behind.
/// </para>
/// <para>
/// Execution is sequential (one chunk window at a time, cultures in requested order,
/// keys in first-seen order): the ledger order is deterministic, failures never abort
/// the batch, and the provider's rate limiter sees a steady pace. Windows match the
/// provider's chunking (<see cref="AzureTranslationProvider.MAX_CHUNK_SIZE"/>), so a
/// dry-run estimate and the real run group keys identically.
/// </para>
/// <para>
/// Ledger provenance (translated vs. cached) is determined by peeking the provider's local
/// layers before each window. That classification is exact for a dedicated backfill and
/// best-effort under concurrent provider use. A throwing <see cref="IProgress{T}"/>
/// handler aborts the run with that exception (caller's contract, like event handlers).
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="TranslationBatchJob"/> class.
/// </remarks>
/// <param name="provider">The Azure translation Provider the job drives. Held live (not snapshotted): the job must observe the provider's current cache and translation memory.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
public sealed class TranslationBatchJob(AzureTranslationProvider provider) {
    private readonly AzureTranslationProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Estimates a batch without any HTTP request, cache write, or event: how many keys are
    /// already free and how many paid translation calls the real run would make.
    /// </summary>
    /// <param name="keys">The message keys to translate.</param>
    /// <param name="cultures">The target cultures to translate for.</param>
    /// <param name="cancellationToken">Token to cancel the estimate.</param>
    /// <returns>The point-in-time estimate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keys"/> or <paramref name="cultures"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a key or culture entry is null, or a culture is invariant.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<TranslationBatchEstimate> EstimateAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<CultureInfo> cultures,
        CancellationToken cancellationToken = default) {
        var (distinctKeys, distinctCultures) = Prepare(keys, cultures);

        var freeKeys = 0;
        var billableCalls = 0;
        foreach(var culture in distinctCultures) {
            foreach(var window in Windows(distinctKeys)) {
                cancellationToken.ThrowIfCancellationRequested();

                var misses = 0;
                foreach(var key in window) {
                    if(_provider.TryPeekLocalTemplate(key, culture.Name, out _)) {
                        freeKeys++;
                    } else {
                        misses++;
                    }
                }

                if(misses > 0) {
                    billableCalls++;
                }
            }
        }

        return Task.FromResult(new TranslationBatchEstimate(
            distinctKeys.Length * distinctCultures.Length,
            freeKeys,
            (distinctKeys.Length * distinctCultures.Length) - freeKeys,
            billableCalls));
    }

    /// <summary>
    /// Runs a translation batch: one provider call per chunk window per culture, a ledger entry
    /// per distinct key per culture, and progress after every window. Chunk failures are recorded
    /// in the ledger and never abort the batch.
    /// </summary>
    /// <param name="keys">The message keys to translate. Duplicates are translated once.</param>
    /// <param name="cultures">The target cultures to translate for.</param>
    /// <param name="progress">Optional progress sink, reported after every chunk window.</param>
    /// <param name="cancellationToken">Token to cancel the run. Cancellation stops the job promptly with a partial ledger (see <see cref="TranslationBatchCancelledException"/>).</param>
    /// <returns>The completed ledger.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keys"/> or <paramref name="cultures"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a key or culture entry is null, or a culture is invariant.</exception>
    /// <exception cref="TranslationBatchCancelledException">Thrown when <paramref name="cancellationToken"/> is cancelled; carries the partial ledger.</exception>
    public Task<TranslationBatchResult> RunAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<CultureInfo> cultures,
        IProgress<TranslationBatchProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        var (distinctKeys, distinctCultures) = Prepare(keys, cultures);

        return RunCoreAsync(distinctKeys, distinctCultures, progress, cancellationToken);
    }

    /// <summary>
    /// Runs core.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="cultures">The cultures.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is core.</returns>
    private async Task<TranslationBatchResult> RunCoreAsync(
        string[] keys,
        CultureInfo[] cultures,
        IProgress<TranslationBatchProgress>? progress,
        CancellationToken cancellationToken) {
        var total = keys.Length * cultures.Length;
        var entries = new List<TranslationBatchEntry>(total);
        var translated = 0;
        var cached = 0;
        var failed = 0;

        try {
            foreach(var culture in cultures) {
                foreach(var window in Windows(keys)) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var delta = await RunWindowAsync(window, culture, entries, cancellationToken);
                    translated += delta.Translated;
                    cached += delta.Cached;
                    failed += delta.Failed;

                    progress?.Report(new TranslationBatchProgress(culture, entries.Count, total, translated, cached, failed));
                }
            }
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw new TranslationBatchCancelledException(
                "The translation batch run was cancelled; the ledger completed so far is preserved.",
                new TranslationBatchResult([.. entries]),
                cancellationToken);
        }

        return new TranslationBatchResult([.. entries]);
    }

    /// <summary>
    /// Resolves one chunk window through the provider and appends its ledger entries.
    /// </summary>
    /// <param name="window">The keys in the chunk window.</param>
    /// <param name="culture">The target culture of the window.</param>
    /// <param name="entries">The ledger receiving the window entries.</param>
    /// <param name="cancellationToken">Token to cancel the provider call.</param>
    /// <returns>The counts of translated, cached, and failed entries appended.</returns>
    private async Task<WindowDelta> RunWindowAsync(
        string[] window,
        CultureInfo culture,
        List<TranslationBatchEntry> entries,
        CancellationToken cancellationToken) {
        var preHit = SnapshotPreHits(window, culture);

        IReadOnlyDictionary<string, string?> resolved;
        try {
            resolved = await _provider.GetMessageTemplatesAsync(window, culture, cancellationToken);
        }
        catch(Exception ex) when(!cancellationToken.IsCancellationRequested) {
            // Chunk failure (transport, timeout budget, open breaker): ledger records,
            // the job completes. Caller cancellation is not a chunk failure.
            foreach(var key in window) {
                entries.Add(new TranslationBatchEntry(key, culture, TranslationBatchStatus.Failed, null, ReasonFor(ex)));
            }

            return new WindowDelta(0, 0, window.Length);
        }

        var translated = 0;
        var cached = 0;
        for(var index = 0; index < window.Length; index++) {
            if(preHit[index]) {
                entries.Add(new TranslationBatchEntry(window[index], culture, TranslationBatchStatus.Cached, resolved[window[index]], null));
                cached++;
            } else {
                entries.Add(new TranslationBatchEntry(window[index], culture, TranslationBatchStatus.Translated, resolved[window[index]], null));
                translated++;
            }
        }

        return new WindowDelta(translated, cached, 0);
    }

    /// <summary>
    /// Captures whether each key in a window is already resolvable from the provider's local layers.
    /// </summary>
    /// <param name="window">The keys in the chunk window.</param>
    /// <param name="culture">The target culture of the window.</param>
    /// <returns>A per-key flag that is <see langword="true"/> when the key was cached before the call.</returns>
    private bool[] SnapshotPreHits(string[] window, CultureInfo culture) {
        var preHit = new bool[window.Length];
        for(var index = 0; index < window.Length; index++) {
            preHit[index] = _provider.TryPeekLocalTemplate(window[index], culture.Name, out _);
        }

        return preHit;
    }

    /// <summary>
    /// Performs the prepare operation.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <param name="cultures">The cultures.</param>
    /// <returns>The prepare result.</returns>
    private static (string[] Keys, CultureInfo[] Cultures) Prepare(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<CultureInfo> cultures) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(cultures);

        if(keys.Any(static key => key is null)) {
            throw new ArgumentException("The batch keys must not contain null entries.", nameof(keys));
        }

        if(cultures.Any(static culture => culture is null)) {
            throw new ArgumentException("The batch cultures must not contain null entries.", nameof(cultures));
        }

        if(cultures.Any(static culture => culture.Equals(CultureInfo.InvariantCulture))) {
            throw new ArgumentException("Batch translation targets must be specific cultures; invariant has no translation.", nameof(cultures));
        }

        return ([.. keys.Distinct()], [.. cultures.DistinctBy(static culture => culture.Name)]);
    }

    /// <summary>
    /// Performs the windows operation.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <returns>The windows sequence.</returns>
    private static IEnumerable<string[]> Windows(string[] keys) {
        for(var offset = 0; offset < keys.Length; offset += AzureTranslationProvider.MAX_CHUNK_SIZE) {
            yield return keys.Skip(offset).Take(AzureTranslationProvider.MAX_CHUNK_SIZE).ToArray();
        }
    }

    /// <summary>
    /// The ledger deltas produced by one chunk window.
    /// </summary>
    /// <param name="Translated">The number of freshly translated entries.</param>
    /// <param name="Cached">The number of entries served from the local cache.</param>
    /// <param name="Failed">The number of entries recorded as failed.</param>
    private readonly record struct WindowDelta(int Translated, int Cached, int Failed);

    /// <summary>
    /// Performs the reason for operation.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The reason for text.</returns>
    private static string ReasonFor(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";
}
