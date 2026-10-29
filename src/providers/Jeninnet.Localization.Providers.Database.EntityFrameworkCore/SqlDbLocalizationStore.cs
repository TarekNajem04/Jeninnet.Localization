//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#pragma warning disable CA1001 // No disposal lifecycle by design: the store is a process-lifetime singleton (AddSingleton) and the admin gate below only serializes rare admin mutations; see DebouncedFileReloader precedent.

namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore;

/// <summary>
/// Provides an implementation of <see cref="ILocalizationStore"/> that retrieves and manages
/// localization entries from a SQL Server (or other EF Core–supported database) using Entity Framework Core.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SqlDbLocalizationStore"/> class.
/// <para>
/// Beyond the governed administration path, the store persists accountability
/// state: every applied governed mutation appends one redacted, hash-chained
/// audit record per mutated row (<c>AuditRecords</c>), writes replay journal rows for
/// as-of-version reads (<c>StringVersions</c>), and advances the single-row catalog
/// version counter (<c>CatalogState</c>) — all inside the same transaction as the
/// catalog change. Raw resource-set lifecycle CRUD (Create, Read, Update, Delete) is not versioned and leaves no
/// audit records.
/// </para>
/// </remarks>
/// <param name="contextFactory">
/// The factory used to create new instances of the data context.
/// </param>
/// <exception cref="ArgumentNullException">
/// Thrown if <paramref name="contextFactory"/> is <see langword="null"/>.
/// </exception>
public class SqlDbLocalizationStore(IDbContextFactory<LocalizationDataContext> contextFactory) : IResourceAdminStore, IResourceHistoryStore {
    private const int CATALOG_STATE_ROW_ID = 1;

    private const int VERSION_BUMP_RETRY_LIMIT = 5;

    private readonly IDbContextFactory<LocalizationDataContext> _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    private readonly SemaphoreSlim _adminGate = new(1, 1);

    /// <inheritdoc/>
    public bool SupportsAdministration => true;

    /// <inheritdoc/>
    public event EventHandler<ResourceAdminAuditEventArgs>? Audited;

    /// <inheritdoc/>
    public async Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        // Check whether any entries exist matching the resource set & culture.
        var exists = await dbContext.LocalizedStrings
                                    .AnyAsync(e => e.ResourceSet == resourceSet && e.Culture == culture, cancellationToken)
                                    .ConfigureAwait(false);

        if(!exists) {
            return null;
        }

        // Return a new resource set instance that wraps queries for the specified resourceSet and culture.
        return new DbLocalizationResourceSet(_contextFactory, resourceSet, culture);
    }

    /// <inheritdoc/>
    public async Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var resourceSets = await dbContext.LocalizedStrings
                                          .Where(e => e.Culture == culture)
                                          .GroupBy(e => e.ResourceSet)
                                          .Select(g => g.Key)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);

        return [.. resourceSets.Select(rs => (ILocalizationResourceSet)new DbLocalizationResourceSet(_contextFactory, rs, culture))];
    }

    /// <inheritdoc/>
    public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
        using var dbContext = _contextFactory.CreateDbContext();
        // Synchronously check if any entry exists with the given resourceSet and culture.
        var exists = dbContext.LocalizedStrings.Any(e => e.ResourceSet == resourceSet && e.Culture == culture);

        if(exists) {
            localizationResourceSet = new DbLocalizationResourceSet(_contextFactory, resourceSet, culture);
            return true;
        }

        localizationResourceSet = null;
        return false;
    }

    /// <inheritdoc/>
    public async Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entry = await dbContext.LocalizedStrings
                                   .FirstOrDefaultAsync(e => e.ResourceSet == resourceSet && e.Culture == culture && e.Name == name, cancellationToken)
                                   .ConfigureAwait(false);

        return entry ?? LocalizedStringEntry.Missing(resourceSet, culture, name);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entries = await dbContext.LocalizedStrings
                                     .Where(e => e.ResourceSet == resourceSet && e.Culture == culture)
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        return entries.ToDictionary(e => e.Name, e => e);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entries = await dbContext.LocalizedStrings
                                     .Where(e => e.Culture == culture)
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        // Culture-wide retrieval spans every resource set for the culture, so the same key can
        // legitimately exist in more than one set. Merge by name, letting later entries overwrite
        // earlier ones (matching the store decorator's culture-snapshot semantics) instead of
        // letting Dictionary construction fail on a duplicate key.
        var result = new Dictionary<string, LocalizedStringEntry>(entries.Count);

        foreach(var entry in entries) {
            result[entry.Name] = entry;
        }

        return result;
    }

    /// <inheritdoc/>
    public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
        using var dbContext = _contextFactory.CreateDbContext();
        // Synchronously retrieve the entry.
        entry = dbContext.LocalizedStrings
                         .FirstOrDefault(e => e.ResourceSet == resourceSet && e.Culture == culture && e.Name == name);

        return entry is not null;
    }

    /// <inheritdoc/>
    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await dbContext.LocalizedStrings
                              .Select(static e => e.Culture)
                              .Distinct()
                              .ToListAsync(cancellationToken)
                              .ConfigureAwait(false);
    }

    #region Resource Administration
    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version, the replay journal row,
    /// and the redacted hash-chained audit record inside the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateValue(value);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var exists = await dbContext.LocalizedStrings
                                        .AnyAsync(e => e.ResourceSet == resourceSet && e.Culture == culture && e.Name == name, cancellationToken)
                                        .ConfigureAwait(false);

            if(exists) {
                return Conflict(
                    ResourceAdminOperation.CreateEntry,
                    ResourceAdminConflictKind.AlreadyExists,
                    resourceSet,
                    culture,
                    name,
                    $"Entry '{name}' already exists in resource set '{resourceSet}' for culture '{culture}'.",
                    dryRun: effective.DryRun);
            }

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.CreateEntry, affectedEntries: 1);
            }

            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value);
            var auditEntry = new ResourceAdminAuditEventArgs(
                new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.CreateEntry),
                new ResourceAdminAuditRow(resourceSet, culture, name, newKey: null, entry.Id),
                oldValue: null,
                newValue: value);

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                dbContext.LocalizedStrings.Add(entry);
                dbContext.StringVersions.Add(CreateJournalUpsert(version, resourceSet, culture, name, entry.Id, value));
                await AppendAuditAsync(dbContext, version, [auditEntry], cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            RaiseAudit(auditEntry);

            return Applied(ResourceAdminOperation.CreateEntry, affectedEntries: 1);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version, the replay journal row
    /// (an upsert carrying the new template under the preserved identity), and the
    /// redacted hash-chained audit record inside the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateValue(value);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var existing = await dbContext.LocalizedStrings
                                          .FirstOrDefaultAsync(e => e.ResourceSet == resourceSet && e.Culture == culture && e.Name == name, cancellationToken)
                                          .ConfigureAwait(false);

            if(existing is null) {
                return Conflict(
                    ResourceAdminOperation.UpdateEntry,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture,
                    name,
                    $"Entry '{name}' does not exist in resource set '{resourceSet}' for culture '{culture}'.",
                    dryRun: effective.DryRun);
            }

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.UpdateEntry, affectedEntries: 1);
            }

            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var oldValue = existing.Value;
            var auditEntry = new ResourceAdminAuditEventArgs(
                new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.UpdateEntry),
                new ResourceAdminAuditRow(resourceSet, culture, name, newKey: null, existing.Id),
                oldValue,
                newValue: value);

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                existing.Value = value;
                dbContext.StringVersions.Add(CreateJournalUpsert(version, resourceSet, culture, name, existing.Id, value));
                await AppendAuditAsync(dbContext, version, [auditEntry], cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            RaiseAudit(auditEntry);

            return Applied(ResourceAdminOperation.UpdateEntry, affectedEntries: 1);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version, the replay journal
    /// tombstone, and the redacted hash-chained audit record inside the same
    /// transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        ResourceAdminValidation.ValidateKey(name);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var existing = await dbContext.LocalizedStrings
                                          .FirstOrDefaultAsync(e => e.ResourceSet == resourceSet && e.Culture == culture && e.Name == name, cancellationToken)
                                          .ConfigureAwait(false);

            if(existing is null) {
                return Conflict(
                    ResourceAdminOperation.DeleteEntry,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture,
                    name,
                    $"Entry '{name}' does not exist in resource set '{resourceSet}' for culture '{culture}'.",
                    dryRun: effective.DryRun);
            }

            var note = await dbContext.StringNotes
                                      .FirstOrDefaultAsync(n => n.ResourceSet == resourceSet && n.Name == name, cancellationToken)
                                      .ConfigureAwait(false);
            var isLastCultureRow = !await dbContext.LocalizedStrings
                                                   .AnyAsync(e => e.ResourceSet == resourceSet && e.Name == name && e.Culture != culture, cancellationToken)
                                                   .ConfigureAwait(false);
            var noteFalls = note is not null && isLastCultureRow;

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.DeleteEntry, affectedEntries: 1 + (noteFalls ? 1 : 0));
            }

            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var auditContext = new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.DeleteEntry);
            var auditEntries = new List<ResourceAdminAuditEventArgs> {
                new(
                    auditContext,
                    new ResourceAdminAuditRow(resourceSet, culture, name, newKey: null, existing.Id),
                    oldValue: existing.Value,
                    newValue: null)
            };

            if(noteFalls) {
                auditEntries.Add(new ResourceAdminAuditEventArgs(
                    auditContext,
                    new ResourceAdminAuditRow(resourceSet, culture: string.Empty, key: name, newKey: null, entryId: note!.Id),
                    oldValue: note.Note,
                    newValue: null));
            }

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                dbContext.LocalizedStrings.Remove(existing);
                dbContext.StringVersions.Add(CreateJournalTombstone(version, resourceSet, culture, name, existing.Id));
                if(noteFalls) {
                    dbContext.StringNotes.Remove(note!);
                }

                await AppendAuditAsync(dbContext, version, auditEntries, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach(var entry in auditEntries) {
                RaiseAudit(entry);
            }

            return Applied(ResourceAdminOperation.DeleteEntry, affectedEntries: 1 + (noteFalls ? 1 : 0));
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version and, per culture, the
    /// replay journal tombstone plus upsert under the preserved identity, and one
    /// redacted hash-chained audit record per row — all inside the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(oldName);
        ResourceAdminValidation.ValidateKey(newName);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var sources = (await dbContext.LocalizedStrings
                                          .Where(e => e.ResourceSet == resourceSet && e.Name == oldName)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false))
                .OrderBy(static e => e.Culture, StringComparer.Ordinal)
                .ToList();

            if(sources.Count == 0) {
                return Conflict(
                    ResourceAdminOperation.RenameKey,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture: null,
                    key: oldName,
                    $"Key '{oldName}' exists in no culture of resource set '{resourceSet}'.",
                    dryRun: effective.DryRun);
            }

            var targets = await dbContext.LocalizedStrings
                                          .Where(e => e.ResourceSet == resourceSet && e.Name == newName)
                                          .Select(static e => e.Culture)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);
            var clash = sources.Select(static e => e.Culture).FirstOrDefault(culture => targets.Contains(culture, StringComparer.Ordinal));

            if(clash is not null) {
                return Conflict(
                    ResourceAdminOperation.RenameKey,
                    ResourceAdminConflictKind.TargetExists,
                    resourceSet,
                    culture: null,
                    key: oldName,
                    $"Key '{newName}' already exists in culture '{clash}' of resource set '{resourceSet}'; the rename was refused atomically.",
                    dryRun: effective.DryRun);
            }

            var note = await dbContext.StringNotes
                                      .FirstOrDefaultAsync(n => n.ResourceSet == resourceSet && n.Name == oldName, cancellationToken)
                                      .ConfigureAwait(false);

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.RenameKey, sources.Count + (note is null ? 0 : 1));
            }

            // LocalizedStringEntry keys are init-only: the rename re-keys the same
            // identities (delete + insert) inside one transaction so concurrent writers
            // can never observe a split key. Row-per-row audit carries the old values.
            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var auditContext = new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.RenameKey);
            var auditEntries = sources.ConvertAll(source => new ResourceAdminAuditEventArgs(
                auditContext,
                new ResourceAdminAuditRow(resourceSet, source.Culture, source.Name, newKey: newName, source.Id),
                oldValue: source.Value,
                newValue: null));

            if(note is not null) {
                auditEntries.Add(new ResourceAdminAuditEventArgs(
                    auditContext,
                    new ResourceAdminAuditRow(resourceSet, culture: string.Empty, key: oldName, newKey: newName, entryId: note.Id),
                    oldValue: null,
                    newValue: null));
            }

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                var renamed = sources.ConvertAll(source =>
                    new LocalizedStringEntry(source.Id, source.ResourceSet, source.Culture, newName, source.Value));
                dbContext.LocalizedStrings.RemoveRange(sources);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                dbContext.LocalizedStrings.AddRange(renamed);

                note?.Name = newName;

                foreach(var source in sources) {
                    dbContext.StringVersions.Add(CreateJournalTombstone(version, resourceSet, source.Culture, source.Name, source.Id));
                    dbContext.StringVersions.Add(CreateJournalUpsert(version, resourceSet, source.Culture, newName, source.Id, source.Value));
                }

                await AppendAuditAsync(dbContext, version, auditEntries, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach(var auditEntry in auditEntries) {
                RaiseAudit(auditEntry);
            }

            return Applied(ResourceAdminOperation.RenameKey, sources.Count + (note is null ? 0 : 1));
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version, one replay journal
    /// tombstone per row, and one redacted hash-chained audit record per row —
    /// all inside the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateCulture(culture);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var rows = (await dbContext.LocalizedStrings
                                        .Where(e => e.ResourceSet == resourceSet && e.Culture == culture)
                                        .ToListAsync(cancellationToken)
                                        .ConfigureAwait(false))
                .OrderBy(static e => e.Name, StringComparer.Ordinal)
                .ToList();

            if(rows.Count == 0) {
                return Conflict(
                    ResourceAdminOperation.DeleteResourceSet,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture,
                    key: null,
                    $"Resource set '{resourceSet}' exists in no culture entry for '{culture}'.",
                    dryRun: effective.DryRun);
            }

            var notes = await dbContext.StringNotes
                                       .Where(n => n.ResourceSet == resourceSet)
                                       .ToListAsync(cancellationToken)
                                       .ConfigureAwait(false);
            var survivingKeys = await dbContext.LocalizedStrings
                                               .Where(e => e.ResourceSet == resourceSet && e.Culture != culture)
                                               .Select(static e => e.Name)
                                               .Distinct()
                                               .ToListAsync(cancellationToken)
                                               .ConfigureAwait(false);
            var surviving = survivingKeys.ToHashSet(StringComparer.Ordinal);
            var doomedNotes = notes
                .Where(note => !surviving.Contains(note.Name))
                .OrderBy(static note => note.Name, StringComparer.Ordinal)
                .ToList();

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.DeleteResourceSet, rows.Count + doomedNotes.Count);
            }

            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var auditContext = new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.DeleteResourceSet);
            var auditEntries = rows.ConvertAll(row => new ResourceAdminAuditEventArgs(
                auditContext,
                new ResourceAdminAuditRow(resourceSet, row.Culture, row.Name, newKey: null, row.Id),
                oldValue: row.Value,
                newValue: null));

            auditEntries.AddRange(doomedNotes.Select(note => new ResourceAdminAuditEventArgs(
                auditContext,
                new ResourceAdminAuditRow(resourceSet, culture: string.Empty, key: note.Name, newKey: null, entryId: note.Id),
                oldValue: note.Note,
                newValue: null)));

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                dbContext.LocalizedStrings.RemoveRange(rows);
                if(doomedNotes.Count > 0) {
                    dbContext.StringNotes.RemoveRange(doomedNotes);
                }

                foreach(var row in rows) {
                    dbContext.StringVersions.Add(CreateJournalTombstone(version, resourceSet, row.Culture, row.Name, row.Id));
                }

                await AppendAuditAsync(dbContext, version, auditEntries, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach(var auditEntry in auditEntries) {
                RaiseAudit(auditEntry);
            }

            return Applied(ResourceAdminOperation.DeleteResourceSet, rows.Count + doomedNotes.Count);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version and, per row, the replay
    /// journal tombstone (old set) plus upsert (new set) under the preserved
    /// identity, and one redacted hash-chained audit record per row — all inside
    /// the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(oldResourceSet);
        ResourceAdminValidation.ValidateResourceSet(newResourceSet);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var sources = (await dbContext.LocalizedStrings
                                          .Where(e => e.ResourceSet == oldResourceSet)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false))
                .OrderBy(static e => e.Culture, StringComparer.Ordinal)
                .ThenBy(static e => e.Name, StringComparer.Ordinal)
                .ToList();

            if(sources.Count == 0) {
                return Conflict(
                    ResourceAdminOperation.RenameResourceSet,
                    ResourceAdminConflictKind.NotFound,
                    oldResourceSet,
                    culture: null,
                    key: null,
                    $"Resource set '{oldResourceSet}' exists in no culture.",
                    dryRun: effective.DryRun);
            }

            var targets = await dbContext.LocalizedStrings
                                          .Where(e => e.ResourceSet == newResourceSet)
                                          .Select(static e => e.Culture)
                                          .ToListAsync(cancellationToken)
                                          .ConfigureAwait(false);
            var clash = sources.Select(static e => e.Culture).FirstOrDefault(culture => targets.Contains(culture, StringComparer.Ordinal));

            if(clash is not null) {
                return Conflict(
                    ResourceAdminOperation.RenameResourceSet,
                    ResourceAdminConflictKind.TargetExists,
                    oldResourceSet,
                    culture: null,
                    key: null,
                    $"Resource set '{newResourceSet}' already exists in culture '{clash}'; the rename was refused atomically.",
                    dryRun: effective.DryRun);
            }

            var notes = await dbContext.StringNotes
                                       .Where(n => n.ResourceSet == oldResourceSet)
                                       .ToListAsync(cancellationToken)
                                       .ConfigureAwait(false);

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.RenameResourceSet, sources.Count + notes.Count);
            }

            // Same delete + insert re-keying as RenameKeyAsync: identities are preserved.
            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var auditContext = new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.RenameResourceSet);
            var auditEntries = sources.ConvertAll(source => new ResourceAdminAuditEventArgs(
                auditContext,
                new ResourceAdminAuditRow(newResourceSet, source.Culture, source.Name, newKey: null, source.Id),
                oldValue: source.Value,
                newValue: null));

            auditEntries.AddRange(notes.Select(note => new ResourceAdminAuditEventArgs(
                auditContext,
                new ResourceAdminAuditRow(newResourceSet, culture: string.Empty, key: note.Name, newKey: null, entryId: note.Id),
                oldValue: null,
                newValue: null)));

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                var renamed = sources.ConvertAll(source =>
                    new LocalizedStringEntry(source.Id, newResourceSet, source.Culture, source.Name, source.Value));
                dbContext.LocalizedStrings.RemoveRange(sources);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                dbContext.LocalizedStrings.AddRange(renamed);

                foreach(var note in notes) {
                    note.ResourceSet = newResourceSet;
                }

                foreach(var source in sources) {
                    dbContext.StringVersions.Add(CreateJournalTombstone(version, oldResourceSet, source.Culture, source.Name, source.Id));
                    dbContext.StringVersions.Add(CreateJournalUpsert(version, newResourceSet, source.Culture, source.Name, source.Id, source.Value));
                }

                await AppendAuditAsync(dbContext, version, auditEntries, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach(var auditEntry in auditEntries) {
                RaiseAudit(auditEntry);
            }

            return Applied(ResourceAdminOperation.RenameResourceSet, sources.Count + notes.Count);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are key-level and language-agnostic; reads are available regardless of the
    /// administration capability (notes are data, not mutation).
    /// </remarks>
    public async Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var note = await dbContext.StringNotes
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(n => n.ResourceSet == resourceSet && n.Name == name, cancellationToken)
                                  .ConfigureAwait(false);

        return note?.Note;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Notes are key-level and language-agnostic; reads are available regardless of the
    /// administration capability (notes are data, not mutation).
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var notes = await dbContext.StringNotes
                                   .AsNoTracking()
                                   .Where(n => n.ResourceSet == resourceSet)
                                   .OrderBy(static n => n.Name)
                                   .ToListAsync(cancellationToken)
                                   .ConfigureAwait(false);

        return notes.ToDictionary(static note => note.Name, static note => note.Note, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version and the redacted
    /// hash-chained audit record inside the same transaction. Notes never enter
    /// the string replay journal (as-of-version reads replay templates only).
    /// </remarks>
    public async Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateNote(note);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var keyExists = await dbContext.LocalizedStrings
                                           .AnyAsync(e => e.ResourceSet == resourceSet && e.Name == name, cancellationToken)
                                           .ConfigureAwait(false);

            if(!keyExists) {
                return Conflict(
                    ResourceAdminOperation.SetNote,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture: null,
                    key: name,
                    $"Key '{name}' exists in no culture of resource set '{resourceSet}'; notes attach to existing keys only.",
                    dryRun: effective.DryRun);
            }

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.SetNote, affectedEntries: 1);
            }

            var existing = await dbContext.StringNotes
                                          .FirstOrDefaultAsync(n => n.ResourceSet == resourceSet && n.Name == name, cancellationToken)
                                          .ConfigureAwait(false);
            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var noteId = existing?.Id ?? Guid.NewGuid();
            var auditEntry = new ResourceAdminAuditEventArgs(
                new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.SetNote),
                new ResourceAdminAuditRow(resourceSet, culture: string.Empty, key: name, newKey: null, entryId: noteId),
                oldValue: existing?.Note,
                newValue: note);

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                if(existing is null) {
                    dbContext.StringNotes.Add(new LocalizedStringNote { Id = noteId, ResourceSet = resourceSet, Name = name, Note = note });
                } else {
                    existing.Note = note;
                }

                await AppendAuditAsync(dbContext, version, [auditEntry], cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            RaiseAudit(auditEntry);

            return Applied(ResourceAdminOperation.SetNote, affectedEntries: 1);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// On commit, the mutation appends the catalog version and the redacted
    /// hash-chained audit record inside the same transaction.
    /// </remarks>
    public async Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
        ThrowIfNotAdministering();
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);
        var effective = ResourceAdminValidation.NormalizeOptions(options);

        await _adminGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var existing = await dbContext.StringNotes
                                          .FirstOrDefaultAsync(n => n.ResourceSet == resourceSet && n.Name == name, cancellationToken)
                                          .ConfigureAwait(false);

            if(existing is null) {
                return Conflict(
                    ResourceAdminOperation.DeleteNote,
                    ResourceAdminConflictKind.NotFound,
                    resourceSet,
                    culture: null,
                    key: name,
                    $"Key '{name}' has no translator note in resource set '{resourceSet}'.",
                    dryRun: effective.DryRun);
            }

            if(effective.DryRun) {
                return Preview(ResourceAdminOperation.DeleteNote, affectedEntries: 1);
            }

            var committedAt = DateTimeOffset.UtcNow;
            var actor = ResourceAdminValidation.NormalizeActor(effective.Actor);
            var auditEntry = new ResourceAdminAuditEventArgs(
                new ResourceAdminAuditContext(committedAt, actor, ResourceAdminOperation.DeleteNote),
                new ResourceAdminAuditRow(resourceSet, culture: string.Empty, key: name, newKey: null, entryId: existing.Id),
                oldValue: existing.Note,
                newValue: null);

            await using(var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)) {
                var version = await BeginMutationBatchAsync(dbContext, cancellationToken).ConfigureAwait(false);
                dbContext.StringNotes.Remove(existing);
                await AppendAuditAsync(dbContext, version, [auditEntry], cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            RaiseAudit(auditEntry);

            return Applied(ResourceAdminOperation.DeleteNote, affectedEntries: 1);
        }
        finally {
            _adminGate.Release();
        }
    }

    /// <summary>
    /// Performs the applied operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="affectedEntries">The affected entries.</param>
    /// <returns>The applied result.</returns>
    private static ResourceAdminResult Applied(ResourceAdminOperation operation, int affectedEntries) =>
        new(operation, applied: true, dryRun: false, affectedEntries, conflict: null);

    /// <summary>
    /// Performs the preview operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="affectedEntries">The affected entries.</param>
    /// <returns>The preview result.</returns>
    private static ResourceAdminResult Preview(ResourceAdminOperation operation, int affectedEntries) =>
        new(operation, applied: false, dryRun: true, affectedEntries, conflict: null);

    /// <summary>
    /// Performs the conflict operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="key">The message key.</param>
    /// <param name="detail">The detail.</param>
    /// <param name="dryRun">The dry run.</param>
    /// <returns>The conflict result.</returns>
    private static ResourceAdminResult Conflict(
        ResourceAdminOperation operation,
        ResourceAdminConflictKind kind,
        string resourceSet,
        string? culture,
        string? key,
        string detail,
        bool dryRun) =>
        new(operation, applied: false, dryRun, affectedEntries: 0, new ResourceAdminConflict(kind, resourceSet, culture, key, detail));

    /// <summary>
    /// Raises audit.
    /// </summary>
    /// <param name="entry">The entry.</param>
    private void RaiseAudit(ResourceAdminAuditEventArgs entry) => Audited?.Invoke(this, entry);

    /// <summary>
    /// Performs the throw if not administering operation.
    /// </summary>
    private void ThrowIfNotAdministering() {
        if(!SupportsAdministration) {
            throw new StoreReadOnlyException(
                $"Store '{GetType().FullName}' has resource administration disabled and rejects the governed write path.");
        }
    }
    #endregion Resource Administration

    #region Resource History
    /// <inheritdoc/>
    public bool SupportsHistory => true;

    /// <inheritdoc/>
    public async Task<long> GetCurrentVersionAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var state = await dbContext.CatalogState
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(static row => row.Id == CATALOG_STATE_ROW_ID, cancellationToken)
                                   .ConfigureAwait(false);

        return state?.Version ?? 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ResourceHistoryRecord>> GetHistoryAsync(long sinceVersion = 0, CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await dbContext.AuditRecords
                                  .AsNoTracking()
                                  .Where(record => record.Version > sinceVersion)
                                  .OrderBy(static record => record.Sequence)
                                  .ToListAsync(cancellationToken)
                                  .ConfigureAwait(false);

        return [.. rows.Select(static row => new ResourceHistoryRecord(
            row.Sequence,
            row.Version,
            new ResourceAdminAuditContext(row.Timestamp, row.Actor, row.Operation),
            new ResourceAdminAuditRow(row.ResourceSet, row.Culture, row.Key, row.NewKey, row.EntryId),
            row.OldValueHash,
            row.NewValueHash))];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Replay reduces the append-only journal: for every key the latest journal row
    /// at or before the requested version wins — an upsert reproduces the entry, a
    /// tombstone (deletion or rename source) removes it. Journal rows store
    /// catalog values as-is (the same confidentiality class as the live catalog);
    /// audit storage is the redacted layer.
    /// </remarks>
    public async Task<Dictionary<string, LocalizedStringEntry>> GetStringsAsOfVersionAsync(string resourceSet, string culture, long version, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentOutOfRangeException.ThrowIfNegative(version);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var journalRows = await dbContext.StringVersions
                                         .AsNoTracking()
                                         .Where(row => row.ResourceSet == resourceSet && row.Culture == culture && row.Version <= version)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        var result = new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);

        foreach(var group in journalRows.GroupBy(static row => row.Name, StringComparer.Ordinal)) {
            var latest = group
                .OrderByDescending(static row => row.Version)
                .ThenByDescending(static row => row.Sequence)
                .First();

            if(latest.Value is not null) {
                result[latest.Name] = new LocalizedStringEntry(latest.EntryId, resourceSet, culture, latest.Name, latest.Value);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Verification walks the persisted trail in append order and recomputes every
    /// chain hash. Any modification, insertion, or removal breaks the chain unless
    /// every subsequent record's chain hash is rewritten as well; such a full-tail
    /// rewrite is only detectable against an externally retained anchor, which is
    /// out of scope (deferred finding).
    /// </remarks>
    public async Task<ResourceHistoryVerification> VerifyHistoryAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await dbContext.AuditRecords
                                  .AsNoTracking()
                                  .OrderBy(static record => record.Sequence)
                                  .ToListAsync(cancellationToken)
                                  .ConfigureAwait(false);

        var previousHash = string.Empty;
        var verified = 0;

        foreach(var row in rows) {
            var expectedChainHash = ComputeChainHash(row);

            if(!string.Equals(row.PreviousHash, previousHash, StringComparison.Ordinal) ||
               !string.Equals(row.ChainHash, expectedChainHash, StringComparison.Ordinal)) {
                return new ResourceHistoryVerification(isValid: false, verifiedRecords: verified, firstInvalidSequence: row.Sequence);
            }

            previousHash = row.ChainHash;
            verified++;
        }

        return new ResourceHistoryVerification(isValid: true, verifiedRecords: verified, firstInvalidSequence: null);
    }

    /// <summary>
    /// Advances the single-row catalog version counter and returns the version for
    /// the current mutation batch. Increments use an optimistic conditional update
    /// so concurrent writers cannot hand two batches the same version; the state row
    /// is created lazily by the first governed mutation.
    /// </summary>
    /// <param name="dbContext">The context opened for the mutation; must be inside the mutation transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The version assigned to this mutation batch.</returns>
    /// <exception cref="DbUpdateConcurrencyException">Thrown when the version counter cannot be advanced after repeated contention.</exception>
    private static async Task<long> BeginMutationBatchAsync(LocalizationDataContext dbContext, CancellationToken cancellationToken) {
        for(var attempt = 0; attempt < VERSION_BUMP_RETRY_LIMIT; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await dbContext.CatalogState
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(static row => row.Id == CATALOG_STATE_ROW_ID, cancellationToken)
                                       .ConfigureAwait(false);

            if(state is null) {
                var candidate = new LocalizationCatalogState { Id = CATALOG_STATE_ROW_ID, Version = 1 };
                dbContext.CatalogState.Add(candidate);

                try {
                    await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return 1;
                }
                catch(DbUpdateException) {
                    // Another writer created the state row first: detach and retry the increment path.
                    dbContext.Entry(candidate).State = EntityState.Detached;
                    continue;
                }
            }

            var updated = await dbContext.CatalogState
                                         .Where(row => row.Id == CATALOG_STATE_ROW_ID && row.Version == state.Version)
                                         .ExecuteUpdateAsync(
                                             static setters => setters.SetProperty(static row => row.Version, static row => row.Version + 1),
                                             cancellationToken)
                                         .ConfigureAwait(false);

            if(updated == 1) {
                return state.Version + 1;
            }
        }

        throw new DbUpdateConcurrencyException(
            "The catalog version counter could not be advanced because another writer is mutating the catalog concurrently. Retry the mutation.");
    }

    /// <summary>
    /// Builds and appends the redacted, hash-chained audit records for one mutation
    /// batch. Records are added to the context (not saved); the caller saves them
    /// inside the mutation transaction so trail and catalog commit atomically.
    /// </summary>
    /// <param name="dbContext">The context opened for the mutation.</param>
    /// <param name="version">The batch version shared by every record.</param>
    /// <param name="entries">The per-row audit entries, in ordinal row order.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    private static async Task AppendAuditAsync(LocalizationDataContext dbContext, long version, List<ResourceAdminAuditEventArgs> entries, CancellationToken cancellationToken) {
        if(entries.Count == 0) {
            return;
        }

        var previousHash = await dbContext.AuditRecords
                                          .AsNoTracking()
                                          .OrderByDescending(static record => record.Sequence)
                                          .Select(static record => record.ChainHash)
                                          .FirstOrDefaultAsync(cancellationToken)
                                          .ConfigureAwait(false) ?? string.Empty;

        foreach(var entry in entries) {
            var record = new LocalizationAuditRecord {
                Version = version,
                Timestamp = entry.Timestamp,
                Actor = entry.Actor,
                Operation = entry.Operation,
                ResourceSet = entry.ResourceSet,
                Culture = entry.Culture,
                Key = entry.Key,
                NewKey = entry.NewKey,
                EntryId = entry.EntryId,
                OldValueHash = entry.OldValue is null ? null : ResourceValueHash.Compute(entry.OldValue),
                NewValueHash = entry.NewValue is null ? null : ResourceValueHash.Compute(entry.NewValue),
                PreviousHash = previousHash,
            };

            record.ChainHash = ComputeChainHash(record);
            previousHash = record.ChainHash;
            dbContext.AuditRecords.Add(record);
        }
    }

    /// <summary>
    /// Creates a replay journal row recording a key's value at a version (an upsert).
    /// </summary>
    /// <param name="version">The batch version.</param>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="name">The key name.</param>
    /// <param name="entryId">The persisted identity of the row.</param>
    /// <param name="value">The template value at this version.</param>
    /// <returns>The journal row.</returns>
    private static LocalizedStringVersion CreateJournalUpsert(long version, string resourceSet, string culture, string name, Guid entryId, string value) => new() {
        Version = version,
        ResourceSet = resourceSet,
        Culture = culture,
        Name = name,
        EntryId = entryId,
        Value = value,
    };

    /// <summary>
    /// Creates a replay journal tombstone recording a key's removal (deletion or
    /// rename source) at a version.
    /// </summary>
    /// <param name="version">The batch version.</param>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="name">The key name.</param>
    /// <param name="entryId">The persisted identity of the removed row.</param>
    /// <returns>The journal row.</returns>
    private static LocalizedStringVersion CreateJournalTombstone(long version, string resourceSet, string culture, string name, Guid entryId) => new() {
        Version = version,
        ResourceSet = resourceSet,
        Culture = culture,
        Name = name,
        EntryId = entryId,
        Value = null,
    };

    /// <summary>
    /// Computes the chain hash of one audit record: SHA-256 (Secure Hash Algorithm, 256-bit) over length-prefixed
    /// fields (previous chain hash, version, UTC timestamp, actor, operation,
    /// resource set, culture, key, rename target, entry identity, redacted value
    /// hashes), lowercase hexadecimal.
    /// </summary>
    /// <param name="record">The record to hash (its current field values).</param>
    /// <returns>The lowercase hexadecimal chain hash.</returns>
    private static string ComputeChainHash(LocalizationAuditRecord record) {
        var builder = new StringBuilder();
        AppendChainField(builder, record.PreviousHash);
        AppendChainField(builder, record.Version.ToString(CultureInfo.InvariantCulture));
        AppendChainField(builder, record.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AppendChainField(builder, record.Actor);
        AppendChainField(builder, ((int)record.Operation).ToString(CultureInfo.InvariantCulture));
        AppendChainField(builder, record.ResourceSet);
        AppendChainField(builder, record.Culture);
        AppendChainField(builder, record.Key);
        AppendChainField(builder, record.NewKey);
        AppendChainField(builder, record.EntryId.ToString("D"));
        AppendChainField(builder, record.OldValueHash);
        AppendChainField(builder, record.NewValueHash);

#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
#else
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
#endif
    }

    /// <summary>
    /// Appends one length-prefixed field to the chain-hash canonical form so field
    /// boundaries can never be forged by delimiter injection.
    /// </summary>
    /// <param name="builder">The canonical-form builder.</param>
    /// <param name="value">The field value, or <see langword="null"/>.</param>
    private static void AppendChainField(StringBuilder builder, string? value) => builder.Append(value is null ? -1 : value.Length).Append(':').Append(value).Append('\n');
    #endregion Resource History

    #region Private Nested Class: DbLocalizationResourceSet
    /// <summary>
    /// Represents a db localization resource set.
    /// </summary>
    /// <param name="contextFactory">The context factory.</param>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    private sealed class DbLocalizationResourceSet(IDbContextFactory<LocalizationDataContext> contextFactory, string resourceSet, string culture) : ILocalizationResourceSet {
        private readonly IDbContextFactory<LocalizationDataContext> _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

        /// <inheritdoc/>
        public string ResourceSet { get; } = resourceSet;

        /// <inheritdoc/>
        public string Culture { get; } = culture;

        /// <inheritdoc/>
        public CultureInfo GetCultureInfo() => new(Culture);

        /// <inheritdoc/>
        public async Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var entry = await dbContext.LocalizedStrings
                                       .FirstOrDefaultAsync(
                                           e => e.ResourceSet == ResourceSet && e.Culture == Culture && e.Name == name,
                                           cancellationToken
                                       )
                                       .ConfigureAwait(false);

            return entry ?? LocalizedStringEntry.Missing(ResourceSet, Culture, name);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var entries = await dbContext.LocalizedStrings
                                         .Where(e => e.ResourceSet == ResourceSet && e.Culture == Culture)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

            return entries.ToDictionary(e => e.Name, e => e);
        }

        /// <inheritdoc/>
        public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            using var dbContext = _contextFactory.CreateDbContext();
            entry = dbContext.LocalizedStrings
                             .FirstOrDefault(e => e.ResourceSet == ResourceSet && e.Culture == Culture && e.Name == name);

            return entry is not null;
        }

        /// <inheritdoc/>
        public async Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.Update(entry);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.UpdateRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.Remove(entry);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            dbContext.LocalizedStrings.RemoveRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion Private Nested Class: DbLocalizationResourceSet
}
