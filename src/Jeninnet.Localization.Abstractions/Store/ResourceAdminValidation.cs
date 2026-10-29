//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Single-owner validation for the governed resource-administration path.
/// Every administering store validates through these members so key shape and culture
/// codes are rejected identically regardless of storage. Validation failures throw in
/// the <see cref="ArgumentException"/> family; expected domain conflicts (duplicate or
/// missing keys) are reported in <see cref="ResourceAdminResult"/>, never thrown.
/// </summary>
public static class ResourceAdminValidation {
    /// <summary>
    /// Validates a resource-set name: non-null, non-empty, and non-whitespace.
    /// </summary>
    /// <param name="resourceSet">The resource-set name to validate.</param>
    /// <returns>The validated resource-set name, unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceSet"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceSet"/> is empty or whitespace.</exception>
    public static string ValidateResourceSet(string resourceSet) {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceSet);
        return resourceSet;
    }

    /// <summary>
    /// Validates a message key: non-null, non-empty, and non-whitespace.
    /// An empty template value is a valid stored value, but an empty key never is.
    /// </summary>
    /// <param name="name">The message key to validate.</param>
    /// <returns>The validated key, unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    public static string ValidateKey(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name;
    }

    /// <summary>
    /// Validates a culture code: non-empty and resolvable through
    /// <see cref="CultureInfo.GetCultureInfo(string)"/>. Codes are stored verbatim
    /// after validation (no normalization), preserving exact-match lookup semantics.
    /// </summary>
    /// <param name="culture">The culture code to validate.</param>
    /// <returns>The validated culture code, unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture"/> is empty, whitespace, or not a known culture.</exception>
    public static string ValidateCulture(string culture) {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        try {
            _ = CultureInfo.GetCultureInfo(culture);
        }
        catch(CultureNotFoundException exception) {
            throw new ArgumentException($"Unknown culture code '{culture}'.", nameof(culture), exception);
        }

        return culture;
    }

    /// <summary>
    /// Validates a template value: non-null. An empty string is a valid stored value.
    /// </summary>
    /// <param name="value">The template value to validate.</param>
    /// <returns>The validated value, unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ValidateValue(string value) {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    /// <summary>
    /// Validates a translator note: non-null. An empty string is a valid
    /// stored value, mirroring template validation.
    /// </summary>
    /// <param name="note">The note text to validate.</param>
    /// <returns>The validated note text, unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="note"/> is <see langword="null"/>.</exception>
    public static string ValidateNote(string note) {
        ArgumentNullException.ThrowIfNull(note);
        return note;
    }

    /// <summary>
    /// Normalizes per-call options: <see langword="null"/> becomes a default instance
    /// (persisting mutation, anonymous actor).
    /// </summary>
    /// <param name="options">The caller-supplied options, or <see langword="null"/>.</param>
    /// <returns><paramref name="options"/> when non-null; otherwise a default instance.</returns>
    public static ResourceAdminOptions NormalizeOptions(ResourceAdminOptions? options) => options ?? new ResourceAdminOptions();

    /// <summary>
    /// Normalizes the recorded actor: <see langword="null"/> becomes empty (anonymous).
    /// </summary>
    /// <param name="actor">The caller-supplied actor, or <see langword="null"/>.</param>
    /// <returns><paramref name="actor"/> when non-null; otherwise <see cref="string.Empty"/>.</returns>
    public static string NormalizeActor(string? actor) => actor ?? string.Empty;
}
