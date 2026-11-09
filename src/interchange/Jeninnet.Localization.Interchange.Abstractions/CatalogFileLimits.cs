//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Abstractions;

/// <summary>
/// Bounds the size of catalog files accepted by the interchange readers.
/// Translation handoffs arrive from outside the repository, so an unbounded read turns a
/// hostile file into an out-of-memory condition. The cap is generous (64 MiB): real catalogs
/// are kilobytes, and anything larger is rejected with a file diagnostic before loading.
/// </summary>
internal static class CatalogFileLimits {
    /// <summary>
    /// The maximum accepted catalog file size, in bytes (64 MiB).
    /// </summary>
    internal const long MAX_CATALOG_BYTES = 67_108_864;

    /// <summary>
    /// Throws when the file at <paramref name="path"/> exceeds <see cref="MAX_CATALOG_BYTES"/>.
    /// Missing or inaccessible files are ignored here: the subsequent read surfaces the real error.
    /// </summary>
    /// <param name="path">The candidate catalog file path.</param>
    /// <param name="createException">Builds the format-specific exception for an oversized file.</param>
    internal static void ThrowIfTooLarge(string path, Func<string, Exception> createException) {
        long length;
        try {
            length = new FileInfo(path).Length;
        }
        catch(IOException) {
            return;
        }
        catch(UnauthorizedAccessException) {
            return;
        }

        if(length > MAX_CATALOG_BYTES) {
            throw createException($"Catalog file exceeds the {MAX_CATALOG_BYTES}-byte limit.");
        }
    }
}
