//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

internal static class ResourceAdminAccess {
    public static IResourceAdminStore GetWritableAdmin(ILocalizationStore store) {
        if(store is IResourceAdminStore admin && admin.SupportsAdministration) {
            return admin;
        }

        throw new StoreReadOnlyException(
            $"Store '{store.GetType().FullName}' does not support resource administration and rejects the governed write path.");
    }

    public static async Task<string?> GetNoteAsync(
        ILocalizationStore store,
        string resourceSet,
        string name,
        CancellationToken cancellationToken
    ) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);

        return store is IResourceAdminStore admin
            ? await admin.GetNoteAsync(resourceSet, name, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public static async Task<IReadOnlyDictionary<string, string>> GetNotesAsync(
        ILocalizationStore store,
        string resourceSet,
        CancellationToken cancellationToken
    ) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);

        return store is IResourceAdminStore admin
            ? await admin.GetNotesAsync(resourceSet, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static async Task<ResourceAdminResult> SetNoteAsync(
        ILocalizationStore store,
        string resourceSet,
        string name,
        string note,
        ResourceAdminOptions? options,
        CancellationToken cancellationToken
    ) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);
        ResourceAdminValidation.ValidateNote(note);

        return await GetWritableAdmin(store).SetNoteAsync(resourceSet, name, note, options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ResourceAdminResult> DeleteNoteAsync(
        ILocalizationStore store,
        string resourceSet,
        string name,
        ResourceAdminOptions? options,
        CancellationToken cancellationToken
    ) {
        ResourceAdminValidation.ValidateResourceSet(resourceSet);
        ResourceAdminValidation.ValidateKey(name);

        return await GetWritableAdmin(store).DeleteNoteAsync(resourceSet, name, options, cancellationToken).ConfigureAwait(false);
    }
}
