//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching.Options;

/// <summary>
/// Construction-snapshot options for the message-provider cache decorators.
/// </summary>
public class MessageProviderCacheOptions {
    /// <summary>
    /// The default time a resolved message stays in the decorator's cache.
    /// </summary>
    public static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// The default maximum number of entries retained in the decorator's cache.
    /// </summary>
    public const int DefaultMaxEntries = 4096;

    /// <summary>
    /// Gets or sets the time that resolved messages remain in the cache. Defaults to
    /// <see cref="DefaultCacheExpiration"/>.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = DefaultCacheExpiration;

    /// <summary>
    /// Gets or sets the maximum number of entries retained in the cache. Bounded to prevent
    /// unbounded memory growth under high key cardinality. Defaults to
    /// <see cref="DefaultMaxEntries"/>. When the bound is reached, the entry closest to
    /// expiry is evicted first. Applies to the in-memory decorator; the distributed
    /// decorator namespaces entries by cache generation instead of enumerating them.
    /// </summary>
    public int MaxEntries { get; set; } = DefaultMaxEntries;

    /// <summary>
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(CacheExpiration <= TimeSpan.Zero) {
            throw new InvalidOperationException("MessageProviderCacheOptions CacheExpiration must be positive.");
        }

        if(MaxEntries < 1) {
            throw new InvalidOperationException("MessageProviderCacheOptions MaxEntries must be at least 1.");
        }
    }
}
