//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// The documented redaction rule of the resource-history audit trail:
/// template text is never persisted in audit storage in plain text. Audit records
/// carry only the lowercase hexadecimal SHA-256 (Secure Hash Algorithm, 256-bit) hash of the UTF-8 (Unicode Transformation Format, 8-bit) template bytes,
/// so a candidate value can be checked against the trail while the template itself
/// stays out of audit storage.
/// <para>
/// The rule is deliberately unsalted so the same template hashes identically across
/// records and stores — the trail's purpose is equality/change detection, not
/// secrecy of low-entropy values against offline brute force. Treat audit storage
/// with the same confidentiality class as the catalog itself.
/// </para>
/// </summary>
public static class ResourceValueHash {
    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 hash of the UTF-8 bytes of
    /// <paramref name="value"/> — the exact rule audit records use for their
    /// before/after value hashes.
    /// </summary>
    /// <param name="value">The template text to hash.</param>
    /// <returns>A 64-character lowercase hexadecimal hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Compute(string value) {
        ArgumentNullException.ThrowIfNull(value);
#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
#else
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
#endif
    }
}
