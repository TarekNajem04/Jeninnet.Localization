//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Extension methods for <see cref="LocalizedStringEntry"/>.
/// </summary>
public static class LocalizedStringEntryExtensions {
    /// <summary>
    /// Determines whether the entry is a missing (fallback) value.
    /// </summary>
    /// <param name="entry">The entry to check.</param>
    /// <returns><c>true</c> if the entry was not found in the original resource; otherwise, <c>false</c>.</returns>
    public static bool IsMissing(this LocalizedStringEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.ResourceNotFound;
    }
}
