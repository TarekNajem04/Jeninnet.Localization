//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.MissingKeys;

/// <summary>
/// Thread-safe in-memory <see cref="IMissingKeySink"/>: aggregates misses by
/// (key, culture) with a hit count and the first call site, behind a sampling cap so
/// hot paths cannot grow memory without bound. Entries beyond
/// <see cref="MaxEntries"/> are dropped and counted in <see cref="DroppedCount"/>.
/// </summary>
public sealed class MissingKeyCollector : IMissingKeySink {
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private readonly Dictionary<MissingKeyId, MissingKeyEntry> _entries = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingKeyCollector"/> class.
    /// </summary>
    /// <param name="maxEntries">The maximum distinct (key, culture) entries retained.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxEntries"/> is less than 1.</exception>
    public MissingKeyCollector(int maxEntries = 1000) {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        MaxEntries = maxEntries;
    }

    /// <summary>
    /// Gets the maximum distinct (key, culture) entries retained.
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    /// Gets how many misses were dropped because the cap was reached.
    /// </summary>
    public int DroppedCount { get; private set; }

    /// <inheritdoc/>
    public void ReportMiss(string key, string culture, string? callSite) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        lock(_sync) {
            var id = new MissingKeyId(key, culture);
            if(_entries.TryGetValue(id, out var existing)) {
                existing.HitCount++;
                return;
            }

            if(_entries.Count >= MaxEntries) {
                DroppedCount++;
                return;
            }

            _entries[id] = new MissingKeyEntry(key, culture, callSite ?? string.Empty);
        }
    }

    /// <summary>
    /// Returns the aggregated entries in ordinal (key, culture) order.
    /// </summary>
    /// <returns>A point-in-time snapshot of the collected misses.</returns>
    public IReadOnlyList<MissingKeyEntry> GetSnapshot() {
        lock(_sync) {
            return [.. _entries.Values
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Culture, StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Clears all entries and resets the dropped count.
    /// </summary>
    public void Clear() {
        lock(_sync) {
            _entries.Clear();
            DroppedCount = 0;
        }
    }

    /// <summary>
    /// Represents a missing key id.
    /// </summary>
    /// <param name="Key">The key.</param>
    /// <param name="Culture">The culture.</param>
    private readonly record struct MissingKeyId(string Key, string Culture);
}
