//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Options for the <see cref="DistributedLocalizationStoreDecorator"/>.
/// The decorator mirrors the in-memory <see cref="CachedLocalizationStoreDecorator"/>
/// semantics (TTL (Time To Live), per-node single-flight) across processes through any
/// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// implementation (Redis-compatible; no Redis dependency).
/// </summary>
public sealed class DistributedStoreCacheOptions {
    /// <summary>
    /// Gets or sets the time-to-live applied to every distributed entry.
    /// Bounds worst-case staleness after a lost invalidation. Defaults to 30 minutes,
    /// mirroring <see cref="CachedLocalizationResourceSetDecorator"/>.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the key prefix for every distributed entry (generation stamp,
    /// resource-set payloads, culture indexes). Distinct prefixes isolate tenants
    /// or applications sharing one cache cluster.
    /// </summary>
    public string KeyPrefix { get; set; } = "Jeninnet.Localization";

    /// <summary>
    /// Validates the configured values.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a value is invalid.</exception>
    public void Validate() {
        if(Expiration <= TimeSpan.Zero) {
            throw new InvalidOperationException(
                $"{nameof(Expiration)} must be positive whenever the distributed store cache is used.");
        }

        if(string.IsNullOrWhiteSpace(KeyPrefix)) {
            throw new InvalidOperationException(
                $"{nameof(KeyPrefix)} must not be empty whenever the distributed store cache is used.");
        }

        if(KeyPrefix.EndsWith(':')) {
            throw new InvalidOperationException(
                $"{nameof(KeyPrefix)} must not end with ':'; the decorator inserts separators itself.");
        }
    }
}
