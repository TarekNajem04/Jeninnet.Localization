//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_TRANSLATION_AZURE
namespace Jeninnet.Localization.Translation.Azure;
#elif JENINNET_TRANSLATION_DEEPL
namespace Jeninnet.Localization.Translation.DeepL;
#elif JENINNET_CACHING
namespace Jeninnet.Localization.Providers.Caching;
#else
#error Compile SingleFlight sources with JENINNET_TRANSLATION_AZURE, JENINNET_TRANSLATION_DEEPL, or JENINNET_CACHING defined.
#endif

/// <summary>
/// Shared single-flight coalescing: keys already in flight are joined (awaited) instead of
/// re-acquired, while keys a caller claims are acquired as one batch. Canonical single-key
/// acquisitions follow the Model C lifecycle: the shared acquisition is provider-owned and
/// detached from caller tokens, each caller awaits with its own token, and an in-flight entry
/// lives from installation until terminal completion, so one caller cancelling never cancels
/// the shared work nor affects other callers.
/// </summary>
internal static class SingleFlight {
    /// <summary>
    /// Resolves cache-missed keys through the shared per-(key, culture) single-flight mechanism.
    /// </summary>
    /// <param name="keys">The cache-missed keys the caller is responsible for.</param>
    /// <param name="targetCulture">The culture to acquire the keys for.</param>
    /// <param name="inFlight">The shared in-flight entries.</param>
    /// <param name="acquireBatch">Acquires the claimed keys as one batch.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The resolved templates for every requested key.</returns>
    internal static async Task<IReadOnlyDictionary<string, string?>> CoalesceAsync(
        string[] keys,
        string targetCulture,
        ConcurrentDictionary<(string, string), Lazy<Task<string?>>> inFlight,
        Func<string[], string, CancellationToken, Task<IReadOnlyDictionary<string, string?>>> acquireBatch,
        CancellationToken cancellationToken) {
        var joined = new Dictionary<string, Task<string?>>(keys.Length);
        var pending = new List<(string Key, TaskCompletionSource<string?> Source, (string, string) CacheKey, Lazy<Task<string?>> Entry)>();
        var toAcquire = new List<string>(keys.Length);

        foreach(var key in keys) {
            var cacheKey = (key, targetCulture);
            var source = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownEntry = new Lazy<Task<string?>>(() => source.Task, LazyThreadSafetyMode.ExecutionAndPublication);
            var entry = inFlight.GetOrAdd(cacheKey, ownEntry);
            if(ReferenceEquals(entry, ownEntry)) {
                pending.Add((key, source, cacheKey, ownEntry));
                toAcquire.Add(key);
            } else {
                joined[key] = entry.Value;
            }
        }

        var results = new Dictionary<string, string?>(keys.Length);
        var batchTask = toAcquire.Count > 0
            ? acquireBatch([.. toAcquire], targetCulture, cancellationToken)
            : null;

        try {
            foreach(var pair in joined) {
                results[pair.Key] = await pair.Value.WaitAsync(cancellationToken);
            }

            if(batchTask is not null) {
                var batch = await batchTask;
                foreach(var pair in batch) {
                    results[pair.Key] = pair.Value;
                }

                foreach(var (key, source, _, _) in pending) {
                    source.TrySetResult(batch[key]);
                }
            }

            return results;
        }
        catch(Exception ex) {
            foreach(var (_, source, _, _) in pending) {
                source.TrySetException(ex);
            }

            throw;
        }
        finally {
            foreach(var (_, _, cacheKey, entry) in pending) {
                inFlight.TryRemove(new KeyValuePair<(string, string), Lazy<Task<string?>>>(cacheKey, entry));
            }
        }
    }

    /// <summary>
    /// Creates a provider-owned detached in-flight entry for a canonical single-key lookup.
    /// The acquisition is detached from caller tokens and the entry is removed
    /// exactly when the acquisition reaches terminal completion (success, failure, or cancellation).
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="inFlight">The shared in-flight entries.</param>
    /// <param name="acquireSingle">Acquires one key outside any caller token.</param>
    /// <returns>The detached in flight entry.</returns>
    internal static Lazy<Task<string?>> CreateDetachedEntry(
        (string, string) cacheKey,
        ConcurrentDictionary<(string, string), Lazy<Task<string?>>> inFlight,
        Func<(string, string), CancellationToken, Task<string?>> acquireSingle) {
        Lazy<Task<string?>> entry = null!;
        entry = new Lazy<Task<string?>>(
            () => {
                var task = acquireSingle(cacheKey, CancellationToken.None);
                _ = task.ContinueWith(
                    _ => inFlight.TryRemove(new KeyValuePair<(string, string), Lazy<Task<string?>>>(cacheKey, entry)),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return task;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
        return entry;
    }

    /// <summary>
    /// Awaits a shared in-flight acquisition with the caller's token.
    /// </summary>
    /// <param name="inFlight">The shared acquisition.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The awaited template.</returns>
    internal static async Task<string?> AwaitWithCallerTokenAsync(Task<string?> inFlight, CancellationToken cancellationToken)
        => await inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);
}
