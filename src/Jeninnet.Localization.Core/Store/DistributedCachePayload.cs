//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core.Store;

/// <summary>
/// Versioned serialization contract for distributed resource-set payloads.
/// <list type="bullet">
/// <item>Every payload carries a format version (<see cref="CURRENT_VERSION"/>). Entries written
/// by an older or newer format are ignored on read, never misread.</item>
/// <item>Serialization is trimming-safe: all (de)serialization flows through the
/// source-generated <see cref="DistributedCacheJsonContext"/> (no reflection fallback),
/// coordinating with  AOT (Ahead-of-Time)/trimming readiness.</item>
/// <item>Only existing entries are persisted; missing (negative) entries stay node-local,
/// mirroring the in-memory snapshot rule that excludes missing entries.</item>
/// </list>
/// </summary>
internal static class DistributedCachePayload {
    /// <summary>
    /// The only payload format version this build reads. Payloads stamped with any
    /// other version are treated as a cache miss and refetched from the backend.
    /// </summary>
    internal const int CURRENT_VERSION = 1;

    /// <summary>
    /// Performs the serialize operation.
    /// </summary>
    /// <param name="resourceSet">The resource set.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="generation">The generation.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The serialize sequence.</returns>
    internal static byte[] Serialize(string resourceSet, string culture, long generation, IReadOnlyDictionary<string, LocalizedStringEntry> entries) {
        var payload = new DistributedResourceSetPayload {
            V = CURRENT_VERSION,
            Gen = generation,
            Rs = resourceSet,
            Cu = culture,
            E = [.. entries.Values
                .Where(static entry => !entry.IsMissing())
                .Select(static entry => new DistributedCacheEntryDto {
                    Id = entry.Id.ToString("D", CultureInfo.InvariantCulture),
                    Name = entry.Name,
                    Value = entry.Value,
                })],
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, DistributedCacheJsonContext.Default.DistributedResourceSetPayload);
    }

    /// <summary>
    /// Deserializes a payload, returning <see langword="null"/> for every skew or
    /// corruption signal (wrong version, malformed JSON, coordinate mismatch, malformed
    /// entry). Callers treat <see langword="null"/> as a cache miss and refetch —
    /// a stale or foreign entry is never surfaced.
    /// </summary>
    /// <param name="bytes">The cached payload bytes.</param>
    /// <param name="resourceSet">The expected resource set.</param>
    /// <param name="culture">The expected culture.</param>
    /// <returns>The generation and entries, or <see langword="null"/> on any skew or corruption signal.</returns>
    internal static (long Generation, Dictionary<string, LocalizedStringEntry> Entries)? Deserialize(byte[] bytes, string resourceSet, string culture) {
        DistributedResourceSetPayload? payload;

        try {
            payload = JsonSerializer.Deserialize(bytes, DistributedCacheJsonContext.Default.DistributedResourceSetPayload);
        }
        catch(JsonException) {
            return null;
        }
        catch(NotSupportedException) {
            return null;
        }
        catch(ArgumentException) {
            return null;
        }

        if(payload is null || payload.V != CURRENT_VERSION || payload.E is null) {
            return null;
        }

        if(payload.Gen < 0) {
            return null;
        }

        if(!StringComparer.Ordinal.Equals(payload.Rs, resourceSet) ||
           !StringComparer.OrdinalIgnoreCase.Equals(payload.Cu, culture)) {
            return null;
        }

        var entries = new Dictionary<string, LocalizedStringEntry>(payload.E.Count, StringComparer.Ordinal);

        foreach(var dto in payload.E) {
            if(dto is null || string.IsNullOrEmpty(dto.Name) || dto.Value is null || !Guid.TryParse(dto.Id, out var id)) {
                return null;
            }

            if(!entries.TryAdd(dto.Name, new LocalizedStringEntry(id, resourceSet, culture, dto.Name, dto.Value))) {
                return null;
            }
        }

        return (payload.Gen, entries);
    }

    /// <summary>
    /// Performs the serialize index operation.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The serialize index sequence.</returns>
    internal static byte[] SerializeIndex(DistributedCultureIndex index) =>
        JsonSerializer.SerializeToUtf8Bytes(index, DistributedCacheJsonContext.Default.DistributedCultureIndex);

    /// <summary>
    /// Performs the deserialize index operation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The deserialize index result.</returns>
    internal static DistributedCultureIndex? DeserializeIndex(byte[] bytes) {
        try {
            var index = JsonSerializer.Deserialize(bytes, DistributedCacheJsonContext.Default.DistributedCultureIndex);
            return index?.V == CURRENT_VERSION && index.Sets is not null ? index : null;
        }
        catch(JsonException) {
            return null;
        }
        catch(NotSupportedException) {
            return null;
        }
        catch(ArgumentException) {
            return null;
        }
    }
}

/// <summary>
/// Persisted resource-set payload. Short property names keep cluster payloads small;
/// the shape is versioned by <see cref="DistributedCachePayload.CURRENT_VERSION"/>.
/// </summary>
internal sealed class DistributedResourceSetPayload {
    /// <summary>
    /// Gets or sets the v.
    /// </summary>
    public int V { get; set; }

    /// <summary>
    /// Gets or sets the gen.
    /// </summary>
    public long Gen { get; set; }

    /// <summary>
    /// Gets or sets the rs.
    /// </summary>
    public string Rs { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cu.
    /// </summary>
    public string Cu { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the e.
    /// </summary>
    public List<DistributedCacheEntryDto> E { get; set; } = [];
}

/// <summary>
/// Persisted cache entry DTO (existing entries only; missing entries stay node-local).
/// </summary>
internal sealed class DistributedCacheEntryDto {
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Persisted per-culture set-name index backing <see cref="IDistributedStoreCacheInvalidator.InvalidateCultureAsync"/>.
/// Best-effort bookkeeping: an incomplete index only falls back to TTL (Time To Live)/generation invalidation.
/// </summary>
internal sealed class DistributedCultureIndex {
    /// <summary>
    /// Gets or sets the v.
    /// </summary>
    public int V { get; set; } = DistributedCachePayload.CURRENT_VERSION;

    /// <summary>
    /// Gets or sets the sets.
    /// </summary>
    public List<string> Sets { get; set; } = [];
}

/// <summary>
/// Trimming-safe source-generated JSON context for the distributed cache contract.
/// </summary>
[JsonSerializable(typeof(DistributedResourceSetPayload))]
[JsonSerializable(typeof(DistributedCultureIndex))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified, WriteIndented = false)]
internal sealed partial class DistributedCacheJsonContext : JsonSerializerContext;
