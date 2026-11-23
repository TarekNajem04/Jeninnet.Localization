//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching;

/// <summary>
/// Distributed <see cref="ICacheStore"/>: entries are keyed by decorator identity, cache
/// generation, culture, and a SHA-256 hash of the key, so keys of any shape stay within store
/// key limits. Values are stored as <c>0x01 + UTF-8 bytes</c>; a <see langword="null"/> template
/// is stored as a single <c>0x00</c> byte so cached misses stay distinguishable from absent
/// entries. TTL enforcement belongs to the distributed store.
/// </summary>
internal sealed class DistributedCacheStore : ICacheStore {
    private static readonly byte[] _nullMarker = [0x00];

    private readonly string _identity = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _cacheExpiration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache backing the decorator.</param>
    /// <param name="cacheExpiration">The time that resolved messages remain in the cache.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cache"/> is null.</exception>
    internal DistributedCacheStore(IDistributedCache cache, TimeSpan cacheExpiration) {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheExpiration = cacheExpiration;
    }

    /// <inheritdoc />
    public bool TryGet(string key, string culture, out string? template) {
        // The distributed store may perform network I/O; the synchronous fast path is memory-only.
        template = null;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<(bool Hit, string? Template)> TryGetAsync(string key, string culture, int generation, CancellationToken cancellationToken) {
        var payload = await _cache.GetAsync(StoreKey(key, culture, generation), cancellationToken);
        if(payload is null) {
            return (false, null);
        }

        return (true, DecodeTemplate(payload));
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        IReadOnlyDictionary<string, string?> result,
        string culture,
        int generation,
        Func<int> getCurrentGeneration,
        Lock syncRoot,
        CancellationToken cancellationToken) {
        string[] storeKeys;
        byte[][] payloads;
        lock(syncRoot) {
            if(generation != getCurrentGeneration()) {
                return;
            }

            storeKeys = new string[result.Count];
            payloads = new byte[result.Count][];
            var index = 0;
            foreach(var pair in result) {
                storeKeys[index] = StoreKey(pair.Key, culture, generation);
                payloads[index] = EncodeTemplate(pair.Value);
                index++;
            }
        }

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheExpiration };
        for(var index = 0; index < storeKeys.Length; index++) {
            await _cache.SetAsync(storeKeys[index], payloads[index], options, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Invalidate() {
        // Entries are keyed by generation, so pre-refresh entries are unreachable after the
        // generation advances; they expire naturally. Nothing local to drop.
    }

    /// <summary>
    /// Builds the distributed store key: decorator identity, generation, culture, and a SHA-256
    /// hash of the message key, so keys of any shape stay within store key limits.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="generation">The cache generation.</param>
    /// <returns>The store key.</returns>
    private string StoreKey(string key, string targetCulture, int generation) {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(targetCulture + "\n" + key)));
        return $"jloc-mpc:{_identity}:{generation}:{hash}";
    }

    /// <summary>
    /// Encodes a template for the distributed store: a null template becomes a single
    /// <c>0x00</c> byte so cached misses stay distinguishable from absent entries.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The payload.</returns>
    private static byte[] EncodeTemplate(string? template) {
        if(template is null) {
            return [.. _nullMarker];
        }

        var bytes = Encoding.UTF8.GetBytes(template);
        var payload = new byte[bytes.Length + 1];
        payload[0] = 0x01;
        bytes.CopyTo(payload, 1);
        return payload;
    }

    /// <summary>
    /// Decodes a distributed store payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The template.</returns>
    private static string? DecodeTemplate(byte[] payload) {
        if(payload.Length == 0 || payload[0] == 0x00) {
            return null;
        }

        return Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
    }
}
