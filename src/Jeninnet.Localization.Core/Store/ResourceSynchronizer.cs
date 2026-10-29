//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Governed code-first resource synchronization: applies
/// <see cref="ResourceDeclaration"/> values through the
/// <see cref="IResourceAdminStore"/> path — validation, conflicts, dry-run, actor,
/// per-row audit — with no parallel write mechanism and no delete. Validate-then-apply:
/// the whole input (options, cultures, rename map, declarations, duplicates) is
/// validated before any store call; a read-only store rejects with the typed
/// <see cref="StoreReadOnlyException"/>.
/// </summary>
/// <remarks>
/// <para>
/// Semantics: adds only by default (differing values are
/// reported <see cref="ResourceSynchronizationStatus.Skipped"/> unless
/// <see cref="ResourceSynchronizationOptions.AllowUpdates"/> is set); updates never
/// touch non-default culture values (translations); declared notes are applied on
/// create and upserted only when updates are allowed (a declaration without a note
/// never touches existing notes); missing target-culture entries are skipped by
/// default (fallback preserved) or created empty as untranslated placeholders when
/// <see cref="ResourceSynchronizationOptions.CreateMissingTargetCultures"/> is set;
/// renames are explicit (never inferred) and apply through the governed cross-culture
/// atomic <see cref="IResourceAdminStore.RenameKeyAsync"/>.
/// </para>
/// <para>
/// Idempotency: a second run mutates nothing, audits nothing, and reports every entry
/// <see cref="ResourceSynchronizationStatus.Unchanged"/>. Dry-run previews use the
/// store's own dry-run (which never mutates and never audits).
/// </para>
/// </remarks>
public sealed class ResourceSynchronizer : IResourceSynchronizer {
    private readonly IResourceAdminStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSynchronizer"/> class.
    /// </summary>
    /// <param name="store">The administering store every mutation flows through.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public ResourceSynchronizer(IResourceAdminStore store) {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ResourceSynchronizationReport> SynchronizeAsync(IReadOnlyList<ResourceDeclaration> declarations, ResourceSynchronizationOptions options, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Renames);
        cancellationToken.ThrowIfCancellationRequested();

        var defaultCulture = ValidateCulture(options.DefaultCulture, nameof(options));
        var targetCultures = options.TargetCultures
            .Select(static culture => ValidateCulture(culture, nameof(options)))
            .Where(culture => !culture.Equals(defaultCulture, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static culture => culture, StringComparer.Ordinal)
            .ToList();

        foreach(var rename in options.Renames) {
            ResourceAdminValidation.ValidateKey(rename.Key);
            ResourceAdminValidation.ValidateKey(rename.Value);
            if(rename.Key.Equals(rename.Value, StringComparison.Ordinal)) {
                throw new ArgumentException($"The rename map maps key '{rename.Key}' to itself.", nameof(options));
            }
        }

        var declaredCells = new HashSet<string>(StringComparer.Ordinal);
        foreach(var declaration in declarations) {
            ArgumentNullException.ThrowIfNull(declaration);
            ResourceAdminValidation.ValidateResourceSet(declaration.ResourceSet);
            ResourceAdminValidation.ValidateKey(declaration.Key);
            ResourceAdminValidation.ValidateValue(declaration.DefaultTemplate);
            if(declaration.Note is not null) {
                ResourceAdminValidation.ValidateNote(declaration.Note);
            }

            var cell = declaration.ResourceSet + '\u0004' + declaration.Key;
            if(!declaredCells.Add(cell)) {
                throw new ArgumentException($"Duplicate declaration for resource set '{declaration.ResourceSet}' key '{declaration.Key}'.", nameof(declarations));
            }
        }

        _ = _store.RequireAdmin();
        var callOptions = new ResourceAdminOptions {
            DryRun = options.DryRun,
            Actor = ResourceAdminValidation.NormalizeActor(options.Actor),
        };
        var cultures = new List<string>(targetCultures.Count + 1) { defaultCulture };
        cultures.AddRange(targetCultures);

        var entries = new List<ResourceSynchronizationEntry>();
        var groups = declarations
            .GroupBy(static declaration => declaration.ResourceSet, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal);
        foreach(var group in groups) {
            var request = new ResourceSetSynchronizationRequest(group.Key, group, defaultCulture, targetCultures, cultures, callOptions, options, entries, cancellationToken);
            await SynchronizeResourceSetAsync(request).ConfigureAwait(false);
        }

        var ordered = entries
            .OrderBy(static entry => entry.ResourceSet, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Culture, StringComparer.Ordinal)
            .ThenBy(static entry => (int)entry.Status)
            .ToList();
        return new ResourceSynchronizationReport(options.DryRun, defaultCulture, ordered);
    }

    /// <summary>
    /// Synchronizes one resource set: loads the current values and notes, applies renames, then
    /// synchronizes every declared key.
    /// </summary>
    /// <param name="request">The resource-set synchronization request.</param>
    private async Task SynchronizeResourceSetAsync(ResourceSetSynchronizationRequest request) {
        var declaredKeys = new HashSet<string>(request.Declarations.Select(static declaration => declaration.Key), StringComparer.Ordinal);

        var values = new Dictionary<string, Dictionary<string, LocalizedStringEntry>>(StringComparer.Ordinal);
        foreach(var culture in request.Cultures) {
            values[culture] = await _store.GetAllStringsAsync(request.ResourceSet, culture, request.CancellationToken).ConfigureAwait(false);
        }

        var notes = await _store.GetNotesAsync(request.ResourceSet, request.CancellationToken).ConfigureAwait(false);

        var session = new SynchronizationSession(this, request.CallOptions, request.Options, request.Entries, request.CancellationToken);
        await session.ApplyRenamesAsync(request.ResourceSet, declaredKeys, values, request.Cultures).ConfigureAwait(false);

        foreach(var declaration in request.Declarations.OrderBy(static declaration => declaration.Key, StringComparer.Ordinal)) {
            var keyRequest = new KeySynchronizationRequest(request.ResourceSet, declaration, request.DefaultCulture, request.TargetCultures, values, notes, request.CallOptions, request.Options, request.Entries, request.CancellationToken);
            await SynchronizeKeyAsync(keyRequest).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Synchronizes one declared key through a fresh session.
    /// </summary>
    /// <param name="request">The key synchronization request.</param>
    private async Task SynchronizeKeyAsync(KeySynchronizationRequest request) {
        var session = new SynchronizationSession(this, request.CallOptions, request.Options, request.Entries, request.CancellationToken);
        await session.SynchronizeKeyAsync(request.ResourceSet, request.Declaration, request.DefaultCulture, request.TargetCultures, request.Values, request.Notes).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a declared note and records the updated or conflicting entry.
    /// </summary>
    /// <param name="request">The note application request.</param>
    private async Task ApplyNoteAsync(NoteApplicationRequest request) {
        var noteResult = await _store.SetNoteAsync(request.ResourceSet, request.Key, request.Note, request.CallOptions, request.CancellationToken).ConfigureAwait(false);
        if(noteResult.Conflict is not null) {
            request.Entries.Add(new ResourceSynchronizationEntry(
                request.ResourceSet,
                request.Key,
                request.Culture,
                ResourceSynchronizationStatus.Conflict,
                $"note not applied: {noteResult.Conflict.Detail}"));
        } else {
            request.Entries.Add(new ResourceSynchronizationEntry(
                request.ResourceSet,
                request.Key,
                request.Culture,
                ResourceSynchronizationStatus.Updated,
                request.Options.DryRun ? "note would be updated" : "note updated"));
        }
    }

    /// <summary>
    /// Carries the shared state of one synchronization run so the per-resource-set and per-key
    /// steps can be split into focused helpers without changing any public or internal signature.
    /// </summary>
    /// <param name="owner">The synchronizer owning the administering store.</param>
    /// <param name="callOptions">The governed call options for every store operation.</param>
    /// <param name="options">The caller-supplied synchronization options.</param>
    /// <param name="entries">The report entries accumulated by this run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private sealed class SynchronizationSession(
        ResourceSynchronizer owner,
        ResourceAdminOptions callOptions,
        ResourceSynchronizationOptions options,
        List<ResourceSynchronizationEntry> entries,
        CancellationToken cancellationToken) {
        /// <summary>
        /// Applies the explicit renames declared for the resource set before key synchronization.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="declaredKeys">The keys declared for the resource set.</param>
        /// <param name="values">The current store values by culture.</param>
        /// <param name="cultures">The culture names to query, default culture first.</param>
        public async Task ApplyRenamesAsync(
            string resourceSet,
            HashSet<string> declaredKeys,
            Dictionary<string, Dictionary<string, LocalizedStringEntry>> values,
            List<string> cultures) {
            foreach(var rename in options.Renames.OrderBy(static pair => pair.Key, StringComparer.Ordinal)) {
                await ApplyRenameAsync(resourceSet, rename.Key, rename.Value, declaredKeys, values, cultures).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Applies one explicit rename when the new key is declared, the old key is not, and the
        /// old key exists in at least one culture.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="oldKey">The existing key to rename.</param>
        /// <param name="newKey">The declared key to rename to.</param>
        /// <param name="declaredKeys">The keys declared for the resource set.</param>
        /// <param name="values">The current store values by culture.</param>
        /// <param name="cultures">The culture names to query, default culture first.</param>
        private async Task ApplyRenameAsync(
            string resourceSet,
            string oldKey,
            string newKey,
            HashSet<string> declaredKeys,
            Dictionary<string, Dictionary<string, LocalizedStringEntry>> values,
            List<string> cultures) {
            if(!declaredKeys.Contains(newKey)) {
                return;
            }

            if(declaredKeys.Contains(oldKey)) {
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    newKey,
                    string.Empty,
                    ResourceSynchronizationStatus.Skipped,
                    $"rename not applied: old key '{oldKey}' is still declared in this resource set"));
                return;
            }

            if(!cultures.Any(culture => values[culture].ContainsKey(oldKey))) {
                return;
            }

            var result = await owner._store.RenameKeyAsync(resourceSet, oldKey, newKey, callOptions, cancellationToken).ConfigureAwait(false);
            if(result.Conflict is not null) {
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    newKey,
                    string.Empty,
                    ResourceSynchronizationStatus.Conflict,
                    $"rename from '{oldKey}' refused: {result.Conflict.Detail}"));
                return;
            }

            entries.Add(new ResourceSynchronizationEntry(
                resourceSet,
                newKey,
                string.Empty,
                ResourceSynchronizationStatus.Renamed,
                options.DryRun ? $"would be renamed from '{oldKey}'" : $"renamed from '{oldKey}'"));
            foreach(var culture in cultures) {
                if(values[culture].Remove(oldKey, out var moved)) {
                    values[culture][newKey] = moved;
                }
            }
        }

        /// <summary>
        /// Synchronizes one declared key across the default and target cultures.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="declaration">The declared key, template, and optional note.</param>
        /// <param name="defaultCulture">The default culture.</param>
        /// <param name="targetCultures">The target cultures.</param>
        /// <param name="values">The current store values by culture.</param>
        /// <param name="notes">The current notes by key.</param>
        public async Task SynchronizeKeyAsync(
            string resourceSet,
            ResourceDeclaration declaration,
            string defaultCulture,
            IReadOnlyList<string> targetCultures,
            Dictionary<string, Dictionary<string, LocalizedStringEntry>> values,
            IReadOnlyDictionary<string, string> notes) {
            var key = declaration.Key;

            if(!values[defaultCulture].TryGetValue(key, out var existing)) {
                await CreateDefaultCultureEntryAsync(resourceSet, defaultCulture, key, declaration).ConfigureAwait(false);
            } else {
                await SynchronizeExistingEntryAsync(resourceSet, defaultCulture, key, existing, declaration, notes).ConfigureAwait(false);
            }

            await SynchronizeTargetCulturesAsync(resourceSet, key, targetCultures, values).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the default-culture entry for a key absent from the store.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="defaultCulture">The default culture.</param>
        /// <param name="key">The declared key.</param>
        /// <param name="declaration">The declared key, template, and optional note.</param>
        private async Task CreateDefaultCultureEntryAsync(
            string resourceSet,
            string defaultCulture,
            string key,
            ResourceDeclaration declaration) {
            var create = await owner._store.CreateEntryAsync(resourceSet, defaultCulture, key, declaration.DefaultTemplate, callOptions, cancellationToken).ConfigureAwait(false);
            if(create.Conflict is not null) {
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    key,
                    defaultCulture,
                    ResourceSynchronizationStatus.Conflict,
                    create.Conflict.Detail));
            } else {
                var createDetail = (options.DryRun, declaration.Note) switch {
                    (true, null) => "would be created",
                    (true, _) => "would be created with the declared note",
                    (false, null) => "created",
                    (false, _) => "created with the declared note",
                };
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    key,
                    defaultCulture,
                    ResourceSynchronizationStatus.Created,
                    createDetail));
                if(declaration.Note is not null && !options.DryRun) {
                    var noteResult = await owner._store.SetNoteAsync(resourceSet, key, declaration.Note, callOptions, cancellationToken).ConfigureAwait(false);
                    if(noteResult.Conflict is not null) {
                        entries.Add(new ResourceSynchronizationEntry(
                            resourceSet,
                            key,
                            defaultCulture,
                            ResourceSynchronizationStatus.Conflict,
                            $"note not applied: {noteResult.Conflict.Detail}"));
                    }
                }
            }
        }

        /// <summary>
        /// Synchronizes the default-culture entry for a key already present in the store.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="defaultCulture">The default culture.</param>
        /// <param name="key">The declared key.</param>
        /// <param name="existing">The existing default-culture entry.</param>
        /// <param name="declaration">The declared key, template, and optional note.</param>
        /// <param name="notes">The current notes by key.</param>
        private async Task SynchronizeExistingEntryAsync(
            string resourceSet,
            string defaultCulture,
            string key,
            LocalizedStringEntry existing,
            ResourceDeclaration declaration,
            IReadOnlyDictionary<string, string> notes) {
            var valueDiffers = !string.Equals(existing.Value, declaration.DefaultTemplate, StringComparison.Ordinal);
            notes.TryGetValue(key, out var existingNote);
            var noteDiffers = declaration.Note is not null && !string.Equals(existingNote, declaration.Note, StringComparison.Ordinal);

            if(!valueDiffers && !noteDiffers) {
                entries.Add(new ResourceSynchronizationEntry(resourceSet, key, defaultCulture, ResourceSynchronizationStatus.Unchanged, "store matches the declaration"));
            } else if(!options.AllowUpdates) {
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    key,
                    defaultCulture,
                    ResourceSynchronizationStatus.Skipped,
                    valueDiffers ? "value differs; updates not allowed" : "note differs; updates not allowed"));
            } else if(valueDiffers) {
                await UpdateDefaultCultureEntryAsync(resourceSet, defaultCulture, key, declaration, noteDiffers).ConfigureAwait(false);
            } else {
                var request = new NoteApplicationRequest(resourceSet, key, declaration.Note!, defaultCulture, callOptions, options, entries, cancellationToken);
                await owner.ApplyNoteAsync(request).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Updates the default-culture value of an existing key and applies its note when needed.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="defaultCulture">The default culture.</param>
        /// <param name="key">The declared key.</param>
        /// <param name="declaration">The declared key, template, and optional note.</param>
        /// <param name="noteDiffers">Whether the declared note differs from the stored note.</param>
        private async Task UpdateDefaultCultureEntryAsync(
            string resourceSet,
            string defaultCulture,
            string key,
            ResourceDeclaration declaration,
            bool noteDiffers) {
            var update = await owner._store.UpdateEntryAsync(resourceSet, defaultCulture, key, declaration.DefaultTemplate, callOptions, cancellationToken).ConfigureAwait(false);
            if(update.Conflict is not null) {
                entries.Add(new ResourceSynchronizationEntry(resourceSet, key, defaultCulture, ResourceSynchronizationStatus.Conflict, update.Conflict.Detail));
            } else {
                entries.Add(new ResourceSynchronizationEntry(
                    resourceSet,
                    key,
                    defaultCulture,
                    ResourceSynchronizationStatus.Updated,
                    options.DryRun ? "would be updated" : "updated"));

                if(noteDiffers) {
                    var request = new NoteApplicationRequest(resourceSet, key, declaration.Note!, defaultCulture, callOptions, options, entries, cancellationToken);
                    await owner.ApplyNoteAsync(request).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Reports or creates the target-culture entries for one declared key.
        /// </summary>
        /// <param name="resourceSet">The resource set being synchronized.</param>
        /// <param name="key">The declared key.</param>
        /// <param name="targetCultures">The target cultures.</param>
        /// <param name="values">The current store values by culture.</param>
        private async Task SynchronizeTargetCulturesAsync(
            string resourceSet,
            string key,
            IReadOnlyList<string> targetCultures,
            Dictionary<string, Dictionary<string, LocalizedStringEntry>> values) {
            foreach(var culture in targetCultures) {
                if(values[culture].ContainsKey(key)) {
                    entries.Add(new ResourceSynchronizationEntry(resourceSet, key, culture, ResourceSynchronizationStatus.Unchanged, "existing entry left untouched"));
                } else if(options.CreateMissingTargetCultures) {
                    var create = await owner._store.CreateEntryAsync(resourceSet, culture, key, string.Empty, callOptions, cancellationToken).ConfigureAwait(false);
                    if(create.Conflict is not null) {
                        entries.Add(new ResourceSynchronizationEntry(resourceSet, key, culture, ResourceSynchronizationStatus.Conflict, create.Conflict.Detail));
                    } else {
                        entries.Add(new ResourceSynchronizationEntry(
                            resourceSet,
                            key,
                            culture,
                            ResourceSynchronizationStatus.Created,
                            options.DryRun ? "would be created as untranslated (empty)" : "created as untranslated (empty)"));
                    }
                } else {
                    entries.Add(new ResourceSynchronizationEntry(resourceSet, key, culture, ResourceSynchronizationStatus.Skipped, "target-culture entry not created; fallback applies"));
                }
            }
        }
    }

    /// <summary>
    /// Validates culture.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The culture text.</returns>
    private static string ValidateCulture(string culture, string parameterName) {
        if(string.IsNullOrWhiteSpace(culture)) {
            throw new ArgumentException("A culture code is required and must not be empty.", parameterName);
        }

        return ResourceAdminValidation.ValidateCulture(culture);
    }

    /// <summary>
    /// The inputs of one resource-set synchronization step.
    /// </summary>
    /// <param name="ResourceSet">The resource set being synchronized.</param>
    /// <param name="Declarations">The declarations of the resource set.</param>
    /// <param name="DefaultCulture">The default culture.</param>
    /// <param name="TargetCultures">The target cultures, excluding the default culture.</param>
    /// <param name="Cultures">The culture names to query, default culture first.</param>
    /// <param name="CallOptions">The governed call options for every store operation.</param>
    /// <param name="Options">The caller-supplied synchronization options.</param>
    /// <param name="Entries">The report entries accumulated by this run.</param>
    /// <param name="CancellationToken">The cancellation token.</param>
    private readonly record struct ResourceSetSynchronizationRequest(
        string ResourceSet,
        IEnumerable<ResourceDeclaration> Declarations,
        string DefaultCulture,
        IReadOnlyList<string> TargetCultures,
        List<string> Cultures,
        ResourceAdminOptions CallOptions,
        ResourceSynchronizationOptions Options,
        List<ResourceSynchronizationEntry> Entries,
        CancellationToken CancellationToken);

    /// <summary>
    /// The inputs of one declared-key synchronization step.
    /// </summary>
    /// <param name="ResourceSet">The resource set being synchronized.</param>
    /// <param name="Declaration">The declared key, template, and optional note.</param>
    /// <param name="DefaultCulture">The default culture.</param>
    /// <param name="TargetCultures">The target cultures.</param>
    /// <param name="Values">The current store values by culture.</param>
    /// <param name="Notes">The current notes by key.</param>
    /// <param name="CallOptions">The governed call options for every store operation.</param>
    /// <param name="Options">The caller-supplied synchronization options.</param>
    /// <param name="Entries">The report entries accumulated by this run.</param>
    /// <param name="CancellationToken">The cancellation token.</param>
    private readonly record struct KeySynchronizationRequest(
        string ResourceSet,
        ResourceDeclaration Declaration,
        string DefaultCulture,
        IReadOnlyList<string> TargetCultures,
        Dictionary<string, Dictionary<string, LocalizedStringEntry>> Values,
        IReadOnlyDictionary<string, string> Notes,
        ResourceAdminOptions CallOptions,
        ResourceSynchronizationOptions Options,
        List<ResourceSynchronizationEntry> Entries,
        CancellationToken CancellationToken);

    /// <summary>
    /// The inputs of one declared-note application step.
    /// </summary>
    /// <param name="ResourceSet">The resource set being synchronized.</param>
    /// <param name="Key">The declared key.</param>
    /// <param name="Note">The note to apply.</param>
    /// <param name="Culture">The culture reported on the resulting entry.</param>
    /// <param name="CallOptions">The governed call options for every store operation.</param>
    /// <param name="Options">The caller-supplied synchronization options.</param>
    /// <param name="Entries">The report entries accumulated by this run.</param>
    /// <param name="CancellationToken">The cancellation token.</param>
    private readonly record struct NoteApplicationRequest(
        string ResourceSet,
        string Key,
        string Note,
        string Culture,
        ResourceAdminOptions CallOptions,
        ResourceSynchronizationOptions Options,
        List<ResourceSynchronizationEntry> Entries,
        CancellationToken CancellationToken);
}
