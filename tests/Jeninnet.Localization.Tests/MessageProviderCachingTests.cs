//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the message provider caching tests: the in-memory decorator serves repeats
/// without re-acquiring, honors TTL and bounds, forwards refresh/preload/multi-key,
/// and owns its acquisition events; the distributed decorator mirrors the contract
/// over <see cref="IDistributedCache"/> with generation-namespaced keys.
/// </summary>
[TestClass]
public class MessageProviderCachingTests {
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    // ─── In-memory: hits, misses, TTL ────────────────────────────────────

    /// <summary>
    /// Verifies that hit serves from cache without inner call.
    /// </summary>
    [TestMethod]
    public async Task Hit_ServesFromCacheWithoutInnerCallAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner);

        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        inner.SingleCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that null template is cached.
    /// </summary>
    [TestMethod]
    public async Task NullTemplate_IsCachedAsync() {
        var inner = new StubSingleProvider([]);
        var provider = CreateCached(inner);

        (await provider.GetMessageTemplateAsync("Missing", _german, CancellationToken.None)).Should().BeNull();
        (await provider.GetMessageTemplateAsync("Missing", _german, CancellationToken.None)).Should().BeNull();
        inner.SingleCalls.Should().Be(1, "a cached miss must not re-acquire");
    }

    /// <summary>
    /// Verifies that TTL expiry refetches.
    /// </summary>
    [TestMethod]
    public async Task TtlExpiry_RefetchesAsync() {
        var clock = new ManualTimeProvider();
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner, clock, static options => options.CacheExpiration = TimeSpan.FromMinutes(5));

        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        clock.Advance(TimeSpan.FromMinutes(6));
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        inner.SingleCalls.Should().Be(2);
    }

    /// <summary>
    /// Verifies that max entries evicts oldest.
    /// </summary>
    [TestMethod]
    public async Task MaxEntries_EvictsOldestAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2", ["C"] = "3" });
        var provider = CreateCached(inner, TimeProvider.System, static options => options.MaxEntries = 2);

        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);
        await provider.GetMessageTemplateAsync("B", _german, CancellationToken.None);
        await provider.GetMessageTemplateAsync("C", _german, CancellationToken.None);
        await provider.GetMessageTemplateAsync("B", _german, CancellationToken.None);

        inner.SingleCalls.Should().Be(3, "the oldest entry must have been evicted");
        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);
        inner.SingleCalls.Should().Be(4, "A was evicted by C and must re-acquire");
    }

    /// <summary>
    /// Verifies that concurrent first lookups share one acquisition.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentFirstLookups_ShareOneAcquisitionAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner);

        var lookups = Enumerable.Range(0, 32)
            .Select(_ => provider.GetMessageTemplateAsync("A", _german, CancellationToken.None).AsTask())
            .ToArray();
        var results = await Task.WhenAll(lookups);

        results.All(static result => result == "1").Should().BeTrue("all concurrent first lookups observe the same published snapshot");
        inner.SingleCalls.Should().Be(1);
    }

    // ─── In-memory: refresh, preload, multi-key ───────────────────────────

    /// <summary>
    /// Verifies that refresh clears and forwards.
    /// </summary>
    [TestMethod]
    public async Task Refresh_ClearsAndForwardsAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner);

        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        await provider.RefreshAsync(CancellationToken.None);
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        inner.SingleCalls.Should().Be(2);
        inner.RefreshCalls.Should().Be(1, "refresh forwards to a refreshable inner provider");
    }

    /// <summary>
    /// Verifies that preload fills cache.
    /// </summary>
    [TestMethod]
    public async Task Preload_FillsCacheAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" });
        var provider = CreateCached(inner);

        await provider.PreloadAsync(["A", "B"], _german, CancellationToken.None);
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        (await provider.GetMessageTemplateAsync("B", _german, CancellationToken.None)).Should().Be("2");
        inner.SingleCalls.Should().Be(2, "preload fans out once per key through the single-key inner path");
    }

    /// <summary>
    /// Verifies that multi key uses inner batch.
    /// </summary>
    [TestMethod]
    public async Task MultiKey_UsesInnerBatchAsync() {
        var inner = new StubMultiProvider(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" });
        var provider = CreateCached(inner);

        var result = await provider.GetMessageTemplatesAsync(["A", "B"], _german, CancellationToken.None);

        result["A"].Should().Be("1");
        result["B"].Should().Be("2");
        inner.BatchCalls.Should().Be(1);
        inner.SingleCalls.Should().Be(0);

        _ = await provider.GetMessageTemplatesAsync(["A", "B"], _german, CancellationToken.None);
        inner.BatchCalls.Should().Be(1, "the second batch is fully cached");
    }

    /// <summary>
    /// Verifies that multi key fans out when inner lacks batch.
    /// </summary>
    [TestMethod]
    public async Task MultiKey_FansOut_WhenInnerLacksBatchAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" });
        var provider = CreateCached(inner);

        var result = await provider.GetMessageTemplatesAsync(["A", "B", "Missing"], _german, CancellationToken.None);

        result["A"].Should().Be("1");
        result["B"].Should().Be("2");
        result["Missing"].Should().BeNull();
        inner.SingleCalls.Should().Be(3);
    }

    // ─── In-memory: events, validation, contract ──────────────────────────

    /// <summary>
    /// Verifies that success raises translation acquired with payload.
    /// </summary>
    [TestMethod]
    public async Task Events_Success_RaisesTranslationAcquiredWithPayloadAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner);
        ProviderAcquisitionEventArgs? raised = null;
        provider.TranslationAcquired += (_, args) => raised = args;

        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);
        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);

        raised.Should().NotBeNull();
        raised!.Templates["A"].Should().Be("1");
        raised.Culture.Should().Be(_german);
    }

    /// <summary>
    /// Verifies that failure raises acquisition failed.
    /// </summary>
    [TestMethod]
    public async Task Events_Failure_RaisesAcquisitionFailedAsync() {
        var inner = new StubSingleProvider([], throwOnKey: "A");
        var provider = CreateCached(inner);
        ProviderAcquisitionFailedEventArgs? raised = null;
        provider.AcquisitionFailed += (_, args) => raised = args;

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        raised.Should().NotBeNull();
        raised!.Culture.Should().Be(_german);
    }

    /// <summary>
    /// Verifies that invariant culture returns null without inner.
    /// </summary>
    [TestMethod]
    public async Task InvariantCulture_ReturnsNullWithoutInnerAsync() {
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = CreateCached(inner);

        (await provider.GetMessageTemplateAsync("A", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
        inner.SingleCalls.Should().Be(0);
    }

    /// <summary>
    /// Verifies that supports reports all capabilities.
    /// </summary>
    [TestMethod]
    public void Supports_ReportsAllCapabilities() {
        var provider = CreateCached(new StubSingleProvider([]));

        provider.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        provider.Supports(ProviderFeature.EventsListening).Should().BeTrue();
        provider.Supports(ProviderFeature.Preload).Should().BeTrue();
        provider.Supports(ProviderFeature.Refresh).Should().BeTrue();
        provider.Supports(ProviderFeature.Caching).Should().BeTrue();
        provider.Supports(ProviderFeature.None).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that ctor null inner throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullInner_ThrowsArgumentNullException() {
        var act = () => new CachedMessageProvider(null!, Options.Create(new MessageProviderCacheOptions()));

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ctor invalid options throws invalid operation exception.
    /// </summary>
    [TestMethod]
    public void Ctor_InvalidOptions_ThrowsInvalidOperationException() {
        var options = new MessageProviderCacheOptions { CacheExpiration = TimeSpan.Zero };

        var act = () => new CachedMessageProvider(new StubSingleProvider([]), Options.Create(options));

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("CacheExpiration");
    }

    // ─── Distributed ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that distributed hit serves from cache.
    /// </summary>
    [TestMethod]
    public async Task Distributed_Hit_ServesFromCacheAsync() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = new DistributedCachedMessageProvider(inner, cache, Options.Create(new MessageProviderCacheOptions()));

        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        inner.SingleCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that distributed null template round trips.
    /// </summary>
    [TestMethod]
    public async Task Distributed_NullTemplate_RoundTripsAsync() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var inner = new StubSingleProvider([]);
        var provider = new DistributedCachedMessageProvider(inner, cache, Options.Create(new MessageProviderCacheOptions()));

        (await provider.GetMessageTemplateAsync("Missing", _german, CancellationToken.None)).Should().BeNull();
        (await provider.GetMessageTemplateAsync("Missing", _german, CancellationToken.None)).Should().BeNull();
        inner.SingleCalls.Should().Be(1, "a cached miss must not re-acquire");
    }

    /// <summary>
    /// Verifies that distributed refresh invalidates.
    /// </summary>
    [TestMethod]
    public async Task Distributed_Refresh_InvalidatesAsync() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = new DistributedCachedMessageProvider(inner, cache, Options.Create(new MessageProviderCacheOptions()));

        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        await provider.RefreshAsync(CancellationToken.None);
        (await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None)).Should().Be("1");
        inner.SingleCalls.Should().Be(2);
        inner.RefreshCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that distributed multi key batches and caches.
    /// </summary>
    [TestMethod]
    public async Task Distributed_MultiKey_BatchesAndCachesAsync() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var inner = new StubMultiProvider(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" });
        var provider = new DistributedCachedMessageProvider(inner, cache, Options.Create(new MessageProviderCacheOptions()));

        var first = await provider.GetMessageTemplatesAsync(["A", "B"], _german, CancellationToken.None);
        first["A"].Should().Be("1");
        var second = await provider.GetMessageTemplatesAsync(["A", "B"], _german, CancellationToken.None);
        second["B"].Should().Be("2");
        inner.BatchCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that distributed events report decorator acquisitions.
    /// </summary>
    [TestMethod]
    public async Task Distributed_Events_ReportDecoratorAcquisitionsAsync() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var inner = new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" });
        var provider = new DistributedCachedMessageProvider(inner, cache, Options.Create(new MessageProviderCacheOptions()));
        var acquired = 0;
        var failed = 0;
        provider.TranslationAcquired += (_, _) => acquired++;
        provider.AcquisitionFailed += (_, _) => failed++;

        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);
        await provider.GetMessageTemplateAsync("A", _german, CancellationToken.None);

        acquired.Should().Be(1, "only the external acquisition raises an event");
        failed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that distributed supports reports all capabilities.
    /// </summary>
    [TestMethod]
    public void Distributed_Supports_ReportsAllCapabilities() {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var provider = new DistributedCachedMessageProvider(
            new StubSingleProvider([]),
            cache,
            Options.Create(new MessageProviderCacheOptions()));

        provider.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        provider.Supports(ProviderFeature.EventsListening).Should().BeTrue();
        provider.Supports(ProviderFeature.Preload).Should().BeTrue();
        provider.Supports(ProviderFeature.Refresh).Should().BeTrue();
        provider.Supports(ProviderFeature.Caching).Should().BeTrue();
        provider.Supports(ProviderFeature.None).Should().BeFalse();
    }

    // ─── Registration ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that registration resolves decorator as inner provider.
    /// </summary>
    [TestMethod]
    public void Registration_ResolvesDecoratorAsInnerProvider() {
        var services = new ServiceCollection();
        services.AddCachedMessageProvider(static _ => new StubSingleProvider(new Dictionary<string, string?> { ["A"] = "1" }));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<CachedMessageProvider>();
    }

    /// <summary>
    /// Verifies that registration invalid options fails at registration.
    /// </summary>
    [TestMethod]
    public void Registration_InvalidOptions_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddCachedMessageProvider(
            static _ => new StubSingleProvider([]),
            static options => options.CacheExpiration = TimeSpan.Zero);

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("CacheExpiration");
    }

    /// <summary>
    /// Verifies that distributed registration resolves decorator as inner provider.
    /// </summary>
    [TestMethod]
    public void DistributedRegistration_ResolvesDecoratorAsInnerProvider() {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddDistributedCachedMessageProvider(static _ => new StubSingleProvider([]));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<DistributedCachedMessageProvider>();
    }

    /// <summary>
    /// Creates cached.
    /// </summary>
    /// <param name="inner">The inner.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <returns>The cached result.</returns>
    private static CachedMessageProvider CreateCached(
        IInnerMessageProvider inner,
        TimeProvider? clock = null,
        Action<MessageProviderCacheOptions>? configure = null) {
        var options = new MessageProviderCacheOptions();
        configure?.Invoke(options);

        return clock is null
            ? new CachedMessageProvider(inner, Options.Create(options))
            : new CachedMessageProvider(inner, Options.Create(options), clock);
    }

    /// <summary>
    /// Represents a stub single key inner provider.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <param name="throwOnKey">The key that throws, or <see langword="null"/>.</param>
    private sealed class StubSingleProvider(Dictionary<string, string?> templates, string? throwOnKey = null)
        : IInnerMessageProvider, IRefreshableMessageProvider {
        /// <summary>
        /// Gets or sets the single calls.
        /// </summary>
        public int SingleCalls { get; set; }

        /// <summary>
        /// Gets or sets the refresh calls.
        /// </summary>
        public int RefreshCalls { get; set; }

        /// <inheritdoc />
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            SingleCalls++;
            if(key == throwOnKey) {
                throw new InvalidOperationException("stub failure");
            }

            templates.TryGetValue(key, out var template);
            return ValueTask.FromResult(template);
        }

        /// <inheritdoc />
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
            RefreshCalls++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Represents a stub multi key inner provider.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <param name="throwOnKey">The key that throws, or <see langword="null"/>.</param>
    private sealed class StubMultiProvider(Dictionary<string, string?> templates, string? throwOnKey = null)
        : IInnerMessageProvider, IMultiKeyMessageProvider, IRefreshableMessageProvider {
        /// <summary>
        /// Gets or sets the single calls.
        /// </summary>
        public int SingleCalls { get; set; }

        /// <summary>
        /// Gets or sets the batch calls.
        /// </summary>
        public int BatchCalls { get; set; }

        /// <summary>
        /// Gets or sets the refresh calls.
        /// </summary>
        public int RefreshCalls { get; set; }

        /// <inheritdoc />
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            SingleCalls++;
            if(key == throwOnKey) {
                throw new InvalidOperationException("stub failure");
            }

            templates.TryGetValue(key, out var template);
            return ValueTask.FromResult(template);
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(IReadOnlyCollection<string> keys, CultureInfo culture, CancellationToken cancellationToken = default) {
            BatchCalls++;
            var result = new Dictionary<string, string?>();
            foreach(var key in keys.Distinct()) {
                if(key == throwOnKey) {
                    throw new InvalidOperationException("stub failure");
                }

                templates.TryGetValue(key, out var template);
                result[key] = template;
            }

            return ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(result);
        }

        /// <inheritdoc />
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
            RefreshCalls++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Handles manual time provider.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider {
        private DateTimeOffset _now = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Performs the advance operation.
        /// </summary>
        /// <param name="delta">The delta.</param>
        public void Advance(TimeSpan delta) => _now += delta;

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
