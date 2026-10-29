//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The typed rejection for a resource-administration attempt against a store that
/// cannot administer resources: either the store does not implement
/// <see cref="IResourceAdminStore"/> or it honestly reports
/// <see cref="IResourceAdminStore.SupportsAdministration"/> as <see langword="false"/>.
/// Read-only stores reject with this type — never silently and never with only a
/// framework-level <see cref="NotSupportedException"/> — so editors, the XLIFF (XML Localization Interchange File Format) importer,
/// and the translation batcher can distinguish "read-only store" from other failures.
/// </summary>
public class StoreReadOnlyException : NotSupportedException {
    /// <inheritdoc/>
    public StoreReadOnlyException(string message) : base(message) { }

    /// <inheritdoc/>
    public StoreReadOnlyException() { }

    /// <inheritdoc/>
    public StoreReadOnlyException(string? message, Exception? innerException) : base(message, innerException) { }
}
