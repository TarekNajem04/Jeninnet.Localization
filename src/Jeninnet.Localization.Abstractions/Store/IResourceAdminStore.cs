//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Governed write path for message catalogs: create, update, rename, and delete
/// keys and resource sets behind a per-store capability flag, with validation, conflict
/// reporting, a storage-agnostic audit hook, and dry-run mode — plus the per-key
/// translator notes that move and die with the key lifecycle. This is the single
/// API the future editor, the XLIFF (XML Localization Interchange File Format) importer, and the translation batcher share.
/// <para>
/// Capability advertisement is two-level, mirroring the provider capability model: the
/// type-level statement is implementing this interface; the instance-level honest
/// statement is <see cref="SupportsAdministration"/>. A store that does not implement
/// this interface — or that reports <see langword="false"/> because administration is
/// disabled by configuration — is read-only and rejects through
/// <see cref="StoreReadOnlyException"/>, never silently.
/// </para>
/// <para>
/// Rename operations (<see cref="RenameKeyAsync"/>, <see cref="RenameResourceSetAsync"/>)
/// are cross-culture atomic per store semantics: either every affected culture commits
/// or the result reports a conflict and nothing changes.
/// </para>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Contract shape: one capability interface extending <see cref="ILocalizationStore"/>
/// with ten governed mutations (catalog CRUD (Create, Read, Update, Delete)/rename/delete plus translator-note set/delete;
/// note reads are un-gated, note mutations are gated like every other member), drafted in
/// Abstractions per the cross-assembly contract rule. Raw
/// <see cref="ILocalizationResourceSet"/> CRUD stays as the low-level lifecycle path;
/// the governed path adds validation, conflict reporting, audit, and dry-run.
/// </item>
/// <item>Capability advertisement is two-level: the type-level statement (implementing
/// this interface) plus the instance-level honest flag
/// <see cref="SupportsAdministration"/> (a configuration-disabled store honestly
/// reports <see langword="false"/>). Both levels funnel through
/// <see cref="LocalizationStoreAdministration.RequireAdmin"/>, so read-only stores
/// reject with the typed <see cref="StoreReadOnlyException"/>, never silently.
/// </item>
/// <item>Validation is single-owned by <see cref="ResourceAdminValidation"/> (key shape,
/// culture codes); expected domain conflicts are returned in
/// <see cref="ResourceAdminResult"/>, never thrown; the audit hook is a
/// storage-agnostic event (one entry per mutated row) whose persistence is a later
/// operational concern; dry-run previews change nothing and raise no audit entries.
/// </item>
/// </list>
/// </remarks>
/// </summary>
public interface IResourceAdminStore : ILocalizationStore {
    /// <summary>
    /// Gets whether this store instance currently supports governed administration.
    /// An implementation may honestly report <see langword="false"/> when administration
    /// is disabled by configuration; every mutation then throws
    /// <see cref="StoreReadOnlyException"/>.
    /// </summary>
    bool SupportsAdministration { get; }

    /// <summary>
    /// Raised once per mutated row after a mutation commits, in ordinal culture order
    /// for multi-row mutations. Dry-run previews raise no entries. Audit persistence
    /// is a later operational concern; this hook is storage-agnostic by design.
    /// </summary>
    event EventHandler<ResourceAdminAuditEventArgs>? Audited;

    /// <summary>
    /// Creates one entry. Reports an <see cref="ResourceAdminConflictKind.AlreadyExists"/>
    /// conflict when the key already exists for the resource set and culture.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="name">The message key.</param>
    /// <param name="value">The template value (empty is valid; <see langword="null"/> is rejected).</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier or culture code is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the template of one existing entry. Reports a
    /// <see cref="ResourceAdminConflictKind.NotFound"/> conflict when the key is missing.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="name">The message key.</param>
    /// <param name="value">The new template value (empty is valid; <see langword="null"/> is rejected).</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier or culture code is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one entry. Reports a <see cref="ResourceAdminConflictKind.NotFound"/>
    /// conflict when the key is missing.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="name">The message key.</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier or culture code is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a key across every culture of a resource set atomically: either every
    /// culture commits or the result reports a conflict and nothing changes. Reports
    /// <see cref="ResourceAdminConflictKind.NotFound"/> when the source key exists in no
    /// culture, and <see cref="ResourceAdminConflictKind.TargetExists"/> when the target
    /// key already exists in any affected culture.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="oldName">The source message key.</param>
    /// <param name="newName">The target message key.</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry of one resource-set/culture pair. A pair with no persisted
    /// entries does not exist and reports a <see cref="ResourceAdminConflictKind.NotFound"/>
    /// conflict. (Resource sets come into existence through entry creation; there is no
    /// empty-set creation because a set exists only while it has persisted entries.)
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture code.</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier or culture code is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a resource set across every culture atomically: either every culture
    /// commits or the result reports a conflict and nothing changes. Reports
    /// <see cref="ResourceAdminConflictKind.NotFound"/> when the source set exists in no
    /// culture, and <see cref="ResourceAdminConflictKind.TargetExists"/> when the target
    /// set already exists in any affected culture.
    /// </summary>
    /// <param name="oldResourceSet">The source resource-set name.</param>
    /// <param name="newResourceSet">The target resource-set name.</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the translator note of one key, or <see langword="null"/> when the
    /// key has no note. Notes are key-level (language-agnostic) and live beside the
    /// governed catalog; reads do not require administration to be enabled.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="name">The message key.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is the note text, or <see langword="null"/> when the key has no note.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every translator note of one resource set, keyed by message key in
    /// ordinal key order; empty when the set has no notes. Reads do not require
    /// administration to be enabled.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task whose result is the note map (never <see langword="null"/>).</returns>
    /// <exception cref="ArgumentException">Thrown when the resource-set name is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceSet"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the translator note of one key: the upsert always
    /// applies when the key exists (mirroring <see cref="UpdateEntryAsync"/>), and reports
    /// <see cref="ResourceAdminConflictKind.NotFound"/> when the key exists in no culture
    /// (no orphan notes). The note moves and dies with the key's lifecycle
    /// (<see cref="RenameKeyAsync"/>, <see cref="DeleteEntryAsync"/>,
    /// <see cref="DeleteResourceSetAsync"/>, <see cref="RenameResourceSetAsync"/>).
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="name">The message key.</param>
    /// <param name="note">The note text (empty is valid; <see langword="null"/> is rejected).</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the translator note of one key. Reports
    /// <see cref="ResourceAdminConflictKind.NotFound"/> when the key has no note.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="name">The message key.</param>
    /// <param name="options">Dry-run and actor options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The mutation outcome, including any conflict.</returns>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    /// <exception cref="StoreReadOnlyException">Thrown when the store does not support administration.</exception>
    Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default);
}
