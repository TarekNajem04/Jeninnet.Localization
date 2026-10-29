//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// Immutable, content-backed localization store for the reference appliance. Every resource set
/// name resolves to the same per-culture template table seeded from <see cref="LocalizationContent"/>,
/// and lookups re-probe the store on every call (no store-level cache). CRUD (Create, Read, Update, Delete) is intentionally
/// unsupported: mutation stays outside the reference appliance, mirroring the library's
/// <c>EmptyLocalizationResourceSet</c> pattern.
/// </summary>
public sealed class ReferenceResourceSetStore : ILocalizationStore {
    /// <summary>
    /// The conventional default resource-set name shared by the reference appliance.
    /// </summary>
    public const string DEFAULT_RESOURCE_SET = "Jeninnet.Localization.Reference";

    private const string NOT_SUPPORTED_MESSAGE = "ReferenceResourceSet is immutable and does not support CRUD operations.";

    /// <inheritdoc/>
    public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) => ResolveAsync(resourceSet, culture, cancellationToken);

    /// <inheritdoc/>
    public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrEmpty(culture);

        var result = LocalizationContent.TryGetTemplates(culture, out _)
            ? new List<ILocalizationResourceSet> { new ReferenceResourceSet(DEFAULT_RESOURCE_SET, culture) }
            : [];
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentException.ThrowIfNullOrEmpty(culture);

        if(LocalizationContent.TryGetTemplates(culture, out _)) {
            localizationResourceSet = new ReferenceResourceSet(resourceSet, culture);
            return true;
        }

        localizationResourceSet = null;
        return false;
    }

    /// <inheritdoc/>
    public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(name);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = LocalizationContent.TryGetString(culture, name, out var value)
            ? new LocalizedStringEntry(CreateStableId(resourceSet, culture, name), resourceSet, culture, name, value)
            : LocalizedStringEntry.Missing(resourceSet, culture, name);
        return Task.FromResult(entry);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentException.ThrowIfNullOrEmpty(culture);

        var result = new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);
        if(LocalizationContent.TryGetTemplates(culture, out var templates)) {
            foreach(var (key, value) in templates) {
                result[key] = new LocalizedStringEntry(CreateStableId(resourceSet, culture, key), resourceSet, culture, key, value);
            }
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) =>
        GetAllStringsAsync(DEFAULT_RESOURCE_SET, culture, cancellationToken);

    /// <inheritdoc/>
    public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentNullException.ThrowIfNull(name);

        if(LocalizationContent.TryGetString(culture, name, out var value)) {
            entry = new LocalizedStringEntry(CreateStableId(resourceSet, culture, name), resourceSet, culture, name, value);
            return true;
        }

        entry = null;
        return false;
    }

    /// <inheritdoc/>
    public Task ReloadAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LocalizationContent.CultureCodes.ToList());
    }

    /// <summary>
    /// Performs the resolve operation.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is resolve.</returns>
    private static Task<ILocalizationResourceSet?> ResolveAsync(string resourceSet, string culture, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentException.ThrowIfNullOrEmpty(culture);

        ILocalizationResourceSet? set = LocalizationContent.TryGetTemplates(culture, out _)
            ? new ReferenceResourceSet(resourceSet, culture)
            : null;
        return Task.FromResult(set);
    }

    /// <summary>
    /// Creates stable id.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="name">The name.</param>
    /// <returns>The stable id result.</returns>
    private static Guid CreateStableId(string resourceSet, string culture, string name) {
        var seed = $"{resourceSet}\u001f{culture}\u001f{name}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// A read-only, content-backed resource set that re-probes <see cref="LocalizationContent"/>
    /// on every lookup.
    /// </summary>
    /// <param name="resourceSet">The resource-set name surfaced by the set.</param>
    /// <param name="culture">The culture code resolved through the content.</param>
    private sealed class ReferenceResourceSet(string resourceSet, string culture) : ILocalizationResourceSet {
        /// <inheritdoc/>
        public string ResourceSet { get; } = resourceSet;

        /// <inheritdoc/>
        public string Culture { get; } = culture;

        /// <inheritdoc/>
        public CultureInfo GetCultureInfo() => new(Culture);

        /// <inheritdoc/>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(name);
            cancellationToken.ThrowIfCancellationRequested();

            var entry = LocalizationContent.TryGetString(Culture, name, out var value)
                ? new LocalizedStringEntry(CreateStableId(ResourceSet, Culture, name), ResourceSet, Culture, name, value)
                : LocalizedStringEntry.Missing(ResourceSet, Culture, name);
            return Task.FromResult(entry);
        }

        /// <inheritdoc/>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal);
            if(LocalizationContent.TryGetTemplates(Culture, out var templates)) {
                foreach(var (key, value) in templates) {
                    result[key] = new LocalizedStringEntry(CreateStableId(ResourceSet, Culture, key), ResourceSet, Culture, key, value);
                }
            }

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public bool TryGetString(string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            ArgumentNullException.ThrowIfNull(name);

            if(LocalizationContent.TryGetString(Culture, name, out var value)) {
                entry = new LocalizedStringEntry(CreateStableId(ResourceSet, Culture, name), ResourceSet, Culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <inheritdoc/>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

        /// <inheritdoc/>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

        /// <inheritdoc/>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

        /// <inheritdoc/>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

        /// <inheritdoc/>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);

        /// <inheritdoc/>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(NOT_SUPPORTED_MESSAGE);
    }
}
