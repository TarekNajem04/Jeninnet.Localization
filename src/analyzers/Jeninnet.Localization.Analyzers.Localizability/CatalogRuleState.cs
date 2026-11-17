//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Collections.Immutable;
using System.Threading;

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// Per-compilation state for the catalog-backed rules (JLNLOC004–JLNLOC006):
/// the union of visible catalog keys and templates plus the referenced keys.
/// Built once per compilation from AdditionalFiles; operation actions record
/// references under a lock while concurrent execution is enabled.
/// </summary>
internal sealed class CatalogRuleState {
    private readonly object _sync = new();
    private readonly HashSet<string> _referenced = [with(StringComparer.Ordinal)];

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRuleState"/> class.
    /// </summary>
    /// <param name="catalogs">The catalog path to key facts.</param>
    private CatalogRuleState(Dictionary<string, CatalogKeyReader.CatalogKeys> catalogs) {
        Catalogs = catalogs;
        Keys = new HashSet<string>(catalogs.SelectMany(static pair => pair.Value.Keys), StringComparer.Ordinal);
        Templates = catalogs
            .SelectMany(static pair => pair.Value.Templates)
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the catalog path to key facts in AdditionalFiles order.
    /// </summary>
    internal IReadOnlyDictionary<string, CatalogKeyReader.CatalogKeys> Catalogs { get; }

    /// <summary>
    /// Gets the union of visible catalog keys (ordinal, case-sensitive).
    /// </summary>
    internal HashSet<string> Keys { get; }

    /// <summary>
    /// Gets the first template per visible key.
    /// </summary>
    internal IReadOnlyDictionary<string, string> Templates { get; }

    /// <summary>
    /// Gets whether any catalog keys are visible to the compilation. Malformed or
    /// empty catalogs contribute no keys, and the catalog-backed rules stay silent
    /// without key evidence.
    /// </summary>
    internal bool HasKeys => Keys.Count > 0;

    /// <summary>
    /// Creates the state by reading every catalog AdditionalFile once.
    /// </summary>
    /// <param name="files">The additional files.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The state.</returns>
    internal static CatalogRuleState Create(ImmutableArray<AdditionalText> files, CancellationToken cancellationToken) {
        var catalogs = new Dictionary<string, CatalogKeyReader.CatalogKeys>(StringComparer.Ordinal);
        foreach(var file in files) {
            cancellationToken.ThrowIfCancellationRequested();
            if(file?.Path is not { } path || !CatalogKeyReader.IsCatalogFile(path) || catalogs.ContainsKey(path)) {
                continue;
            }

            var text = file.GetText(cancellationToken)?.ToString();
            if(text is null) {
                continue;
            }

            catalogs[path] = CatalogKeyReader.Read(path, text);
        }

        return new CatalogRuleState(catalogs);
    }

    /// <summary>
    /// Records a referenced key.
    /// </summary>
    /// <param name="key">The key.</param>
    internal void AddReference(string key) {
        lock(_sync) {
            _referenced.Add(key);
        }
    }

    /// <summary>
    /// Snapshots the referenced keys.
    /// </summary>
    /// <returns>The referenced keys.</returns>
    internal HashSet<string> ReferencedSnapshot() {
        lock(_sync) {
            return new HashSet<string>(_referenced, StringComparer.Ordinal);
        }
    }
}
