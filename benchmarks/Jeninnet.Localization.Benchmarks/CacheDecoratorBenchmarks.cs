//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Collections.Concurrent;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jeninnet.Localization.Abstractions;
using Jeninnet.Localization.Providers.Caching;
using Jeninnet.Localization.Providers.Caching.Options;
using Jeninnet.Localization.Providers.Memory;
using Jeninnet.Localization.Providers.Memory.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Jeninnet.Localization.Benchmarks;

/// <summary>
/// Decorator overhead (W2-01 caching layer): direct inner lookup versus the in-memory
/// TTL decorator versus the distributed decorator over an in-memory stub. All three
/// read the same warmed key, so the benchmarks time the steady-state hit path:
/// provider call, cache-hit check, and distributed payload encode/decode.
/// Short-run job keeps local iterations fast.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class CacheDecoratorBenchmarks {
    private const int KeyCount = 100;

    private MemoryMessageProvider _inner = null!;
    private CachedMessageProvider _memoryCached = null!;
    private DistributedCachedMessageProvider _distributedCached = null!;
    private CultureInfo _culture = null!;

    /// <summary>
    /// Builds the inner provider and both warmed decorators once per process.
    /// </summary>
    [GlobalSetup]
    public void Setup() {
        var messages = new Dictionary<string, string>(StringComparer.Ordinal);
        for(var i = 0; i < KeyCount; i++) {
            messages[$"Key.{i:000}"] = $"Value {i}";
        }

        _inner = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = messages });
        var options = Options.Create(new MessageProviderCacheOptions());
        _memoryCached = new CachedMessageProvider(_inner, options);
        _distributedCached = new DistributedCachedMessageProvider(_inner, new InMemoryDistributedCache(), options);
        _culture = CultureInfo.GetCultureInfo("en");

        _inner.GetMessageTemplateAsync("Key.050", _culture).AsTask().GetAwaiter().GetResult();
        _memoryCached.GetMessageTemplateAsync("Key.050", _culture).AsTask().GetAwaiter().GetResult();
        _distributedCached.GetMessageTemplateAsync("Key.050", _culture).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>Direct inner-provider hit.</summary>
    [Benchmark(Baseline = true)]
    public async ValueTask<string?> DirectHitAsync() =>
        await _inner.GetMessageTemplateAsync("Key.050", _culture);

    /// <summary>In-memory decorator hit.</summary>
    [Benchmark]
    public async ValueTask<string?> MemoryCachedHitAsync() =>
        await _memoryCached.GetMessageTemplateAsync("Key.050", _culture);

    /// <summary>Distributed decorator hit over the in-memory stub.</summary>
    [Benchmark]
    public async ValueTask<string?> DistributedCachedHitAsync() =>
        await _distributedCached.GetMessageTemplateAsync("Key.050", _culture);

    /// <summary>
    /// Minimal in-memory <see cref="IDistributedCache"/> stub: no expiry enforcement
    /// (TTL belongs to the store in production; the benchmark measures the
    /// decorator's encode/decode and store round trip, not eviction).
    /// </summary>
    private sealed class InMemoryDistributedCache : IDistributedCache {
        private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

        /// <inheritdoc/>
        public byte[]? Get(string key) => _entries.TryGetValue(key, out var value) ? value : null;

        /// <inheritdoc/>
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        /// <inheritdoc/>
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _entries[key] = value;

        /// <inheritdoc/>
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Refresh(string key) {
        }

        /// <inheritdoc/>
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        /// <inheritdoc/>
        public void Remove(string key) => _entries.TryRemove(key, out _);

        /// <inheritdoc/>
        public Task RemoveAsync(string key, CancellationToken token = default) {
            Remove(key);
            return Task.CompletedTask;
        }
    }
}
