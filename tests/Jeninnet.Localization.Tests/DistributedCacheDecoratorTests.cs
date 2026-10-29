//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the distributed store cache: cross-node sharing,
/// generation-stamp invalidation, per-node single-flight, versioned serialization,
/// TTL (Time To Live) expiry, and cancellation. The shared backend is a <see cref="TestDistributedCache"/>
/// (in-memory <see cref="IDistributedCache"/> with a controllable logical clock);
/// no real Redis is involved.
/// </summary>
[TestClass]
public sealed class DistributedCacheDecoratorTests {
    private const string PREFIX = "Jeninnet.Localization";

    /// <summary>
    /// Performs the test token operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The test token result.</returns>
    private static CancellationToken TestToken(TestContext? context) =>
        context?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Creates node.
    /// </summary>
    /// <param name="inner">The inner.</param>
    /// <param name="cache">The cache.</param>
    /// <param name="options">The options instance.</param>
    /// <returns>The node result.</returns>
    private static DistributedLocalizationStoreDecorator CreateNode(
        DistributedTestStore inner,
        TestDistributedCache cache,
        DistributedStoreCacheOptions? options = null) =>
        new(inner, cache, options);

    /// <summary>
    /// Verifies that two nodes share entries second node skips backend.
    /// </summary>
    [TestMethod]
    public async Task TwoNodes_ShareEntries_SecondNodeSkipsBackendAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();

        var node1 = CreateNode(inner, cache);
        var node2 = CreateNode(inner, cache);

        // Act
        var first = await node1.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Changed");
        var shared = await node2.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        first.Value.Should().Be("Hello");
        shared.Value.Should().Be("Hello", "the second node shares the first node's distributed entry");
        inner.GetResourceSetCallCount.Should().Be(1, "the second node must not touch the backend");
    }

    /// <summary>
    /// Verifies that generation bump invalidates across nodes.
    /// </summary>
    [TestMethod]
    public async Task GenerationBump_InvalidatesAcrossNodesAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();

        var node1 = CreateNode(inner, cache);
        var node2 = CreateNode(inner, cache);

        // Act
        (await node1.GetGenerationAsync(cancellationToken)).Should().Be(0, "no bump happened yet");
        await node1.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Fresh");

        var stale = await node2.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        var generation = await node1.BumpGenerationAsync(cancellationToken);
        var fresh = await node2.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        stale.Value.Should().Be("Hello");
        generation.Should().Be(1);
        fresh.Value.Should().Be("Fresh", "the generation bump invalidates the shared entry on every node");
        inner.GetResourceSetCallCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that stampede concurrent misses issue single backend call.
    /// </summary>
    [TestMethod]
    public async Task Stampede_ConcurrentMisses_IssueSingleBackendCallAsync() {
        // Arrange
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        inner.ArtificialDelayMs = 200;
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () => {
            var entry = await node.GetStringAsync("Common", "en-US", "Welcome", CancellationToken.None);
            entry.Value.Should().Be("Hello");
        }, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        inner.GetResourceSetCallCount.Should().Be(1, "per-node single-flight holds across the distributed layer");
    }

    /// <summary>
    /// Verifies that version skew old payload ignored served from backend.
    /// </summary>
    [TestMethod]
    public async Task VersionSkew_OldPayloadIgnored_ServedFromBackendAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        await cache.SetAsync(
            $"{PREFIX}:rs:v1:Common:en-us",
            Encoding.UTF8.GetBytes("""{"V":0,"Gen":0,"Rs":"Common","Cu":"en-US","E":[{"Id":"00000000-0000-0000-0000-000000000000","Name":"Welcome","Value":"Stale"}]}"""),
            new DistributedCacheEntryOptions(),
            cancellationToken);

        // Act
        var entry = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Hello", "an older format version is ignored, never misread");
        inner.GetResourceSetCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that corrupt payload ignored never throws.
    /// </summary>
    [TestMethod]
    public async Task CorruptPayload_IgnoredNeverThrowsAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        await cache.SetAsync(
            $"{PREFIX}:rs:v1:Common:en-us",
            [0x00, 0x01, 0x02, 0xFF, 0xFE],
            new DistributedCacheEntryOptions(),
            cancellationToken);

        // Act
        var entry = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that coordinate mismatch never misread.
    /// </summary>
    [TestMethod]
    public async Task CoordinateMismatch_NeverMisreadAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        inner.Seed("Other", "en-US", "Welcome", "Foreign");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        await node.GetStringAsync("Other", "en-US", "Welcome", cancellationToken);
        var foreignBytes = await cache.GetAsync($"{PREFIX}:rs:v1:Other:en-us", cancellationToken);
        foreignBytes.Should().NotBeNull();
        await cache.SetAsync($"{PREFIX}:rs:v1:Common:en-us", foreignBytes!, new DistributedCacheEntryOptions(), cancellationToken);

        // Act
        var entry = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Hello", "a payload stored under foreign coordinates is never surfaced");
    }

    /// <summary>
    /// Verifies that cancellation pre canceled does not publish partial state.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_PreCanceled_DoesNotPublishPartialStateAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await ((Func<Task>)(() => node.GetResourceSetAsync("Common", "en-US", source.Token))).Should().ThrowAsync<OperationCanceledException>();

        var recovered = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        recovered.Value.Should().Be("Hello");
        inner.GetResourceSetCallCount.Should().Be(1, "the canceled miss published nothing");
    }

    /// <summary>
    /// Verifies that per key invalidation refetches from backend.
    /// </summary>
    [TestMethod]
    public async Task PerKeyInvalidation_RefetchesFromBackendAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Updated");
        await node.InvalidateEntryAsync("Common", "en-US", cancellationToken);
        var entry = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Updated");
        inner.GetResourceSetCallCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that per culture invalidation removes indexed sets.
    /// </summary>
    [TestMethod]
    public async Task PerCultureInvalidation_RemovesIndexedSetsAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        inner.Seed("Errors", "en-US", "Required", "Required");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        await node.GetResourceSetsAsync("en-US", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Hi");
        inner.Write("Errors", "en-US", "Required", "Must fill");
        await node.InvalidateCultureAsync("en-US", cancellationToken);

        var common = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        var errors = await node.GetStringAsync("Errors", "en-US", "Required", cancellationToken);

        // Assert
        common.Value.Should().Be("Hi");
        errors.Value.Should().Be("Must fill");
    }

    /// <summary>
    /// Verifies that reload bumps generation across nodes.
    /// </summary>
    [TestMethod]
    public async Task Reload_BumpsGenerationAcrossNodesAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();

        var node1 = CreateNode(inner, cache);
        var node2 = CreateNode(inner, cache);

        // Act
        await node1.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Reloaded");
        await node1.ReloadAsync(cancellationToken);
        var entry = await node2.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Reloaded", "Reload bumps the shared generation so every node converges");
        inner.ReloadCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that reload when inner throws keeps last known good cache.
    /// </summary>
    [TestMethod]
    public async Task Reload_WhenInnerThrows_KeepsLastKnownGoodCacheAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.ThrowOnReload = true;
        await ((Func<Task>)(() => node.ReloadAsync(cancellationToken))).Should().ThrowAsync<InvalidOperationException>();

        inner.ThrowOnReload = false;
        inner.Write("Common", "en-US", "Welcome", "Changed");
        var cached = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        cached.Value.Should().Be("Hello", "a failed reload keeps last-known-good shared state");
        (await node.GetGenerationAsync(cancellationToken)).Should().Be(0, "a failed reload never bumps the generation");
    }

    /// <summary>
    /// Verifies that ttl expiry refetches from backend.
    /// </summary>
    [TestMethod]
    public async Task TtlExpiry_RefetchesFromBackendAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache, new DistributedStoreCacheOptions { Expiration = TimeSpan.FromMinutes(1) });

        // Act
        await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        inner.Write("Common", "en-US", "Welcome", "Refreshed");
        cache.Advance(TimeSpan.FromMinutes(2));
        var entry = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        entry.Value.Should().Be("Refreshed");
        inner.GetResourceSetCallCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that admin update invalidates shared entry.
    /// </summary>
    [TestMethod]
    public async Task AdminUpdate_InvalidatesSharedEntryAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();

        var node1 = CreateNode(inner, cache);
        var node2 = CreateNode(inner, cache);

        // Act
        await node1.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        var result = await node2.UpdateEntryAsync("Common", "en-US", "Welcome", "Admin", cancellationToken: cancellationToken);
        var entry = await node1.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        result.Applied.Should().BeTrue();
        entry.Value.Should().Be("Admin", "an applied governed write invalidates the shared entry");
    }

    /// <summary>
    /// Verifies that null resource set never is cached.
    /// </summary>
    [TestMethod]
    public async Task NullResourceSet_NeverCachedAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act
        (await node.GetResourceSetAsync("Missing", "en-US", cancellationToken)).Should().BeNull();
        (await node.GetResourceSetAsync("Missing", "en-US", cancellationToken)).Should().BeNull();

        // Assert
        inner.GetResourceSetCallCount.Should().Be(2, "null resource sets never touch the shared cache");
    }

    /// <summary>
    /// Verifies that stale completion during bump does not repopulate.
    /// </summary>
    [TestMethod]
    public async Task StaleCompletion_DuringBump_DoesNotRepopulateAsync() {
        // Arrange
        var cancellationToken = TestToken(TestContext);
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        inner.ArtificialDelayMs = 300;
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        // Act: start a slow backend fetch, bump mid-flight, then let it complete.
        var slow = node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        await Task.Delay(50, CancellationToken.None);
        await node.BumpGenerationAsync(cancellationToken);
        (await slow).Value.Should().Be("Hello", "the caller is still served from backend data");

        var after = await node.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);

        // Assert
        after.Value.Should().Be("Hello");
        inner.GetResourceSetCallCount.Should().Be(2, "the pre-bump completion was discarded, not republished");
    }

    /// <summary>
    /// Verifies that sync try get bypasses shared cache.
    /// </summary>
    [TestMethod]
    public void SyncTryGet_BypassesSharedCache() {
        // Arrange
        var inner = new DistributedTestStore();
        inner.Seed("Common", "en-US", "Welcome", "Hello");
        var cache = new TestDistributedCache();
        var node = CreateNode(inner, cache);

        node.GetStringAsync("Common", "en-US", "Welcome", CancellationToken.None).GetAwaiter().GetResult();
        inner.Write("Common", "en-US", "Welcome", "Backend");

        // Act
        var found = node.TryGetString("Common", "en-US", "Welcome", out var entry);

        // Assert
        found.Should().BeTrue();
        entry!.Value.Should().Be("Backend", "synchronous lookups bypass the async-only shared cache by design");
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    #region Fakes

    /// <summary>
    /// In-memory <see cref="IDistributedCache"/> with a controllable logical clock
    /// (<see cref="Advance"/>) for deterministic TTL tests. No Redis involved.
    /// </summary>
    private sealed class TestDistributedCache : IDistributedCache {
        private readonly object _sync = new();
        private readonly Dictionary<string, (byte[] Value, DateTimeOffset? ExpiresAt)> _entries = new(StringComparer.Ordinal);
        private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

        /// <summary>
        /// Performs the advance operation.
        /// </summary>
        /// <param name="delta">The delta.</param>
        public void Advance(TimeSpan delta) {
            lock(_sync) {
                _utcNow += delta;
            }
        }

        /// <summary>
        /// Performs the get operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <returns>The get result.</returns>
        public byte[]? Get(string key) {
            lock(_sync) {
                return Read(key);
            }
        }

        /// <summary>
        /// Performs the get operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task whose result is get.</returns>
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) {
            token.ThrowIfCancellationRequested();

            lock(_sync) {
                return Task.FromResult(Read(key));
            }
        }

        /// <summary>
        /// Performs the refresh operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        public void Refresh(string key) {
        }

        /// <summary>
        /// Performs the refresh operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="token">The cancellation token.</param>
        public Task RefreshAsync(string key, CancellationToken token = default) {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the remove operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        public void Remove(string key) {
            lock(_sync) {
                _entries.Remove(key);
            }
        }

        /// <summary>
        /// Performs the remove operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="token">The cancellation token.</param>
        public Task RemoveAsync(string key, CancellationToken token = default) {
            token.ThrowIfCancellationRequested();

            lock(_sync) {
                _entries.Remove(key);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the set operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options instance.</param>
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) {
            lock(_sync) {
                Write(key, value, options);
            }
        }

        /// <summary>
        /// Performs the set operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="token">The cancellation token.</param>
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) {
            token.ThrowIfCancellationRequested();

            lock(_sync) {
                Write(key, value, options);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the read operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <returns>The read result.</returns>
        private byte[]? Read(string key) {
            if(!_entries.TryGetValue(key, out var stored)) {
                return null;
            }

            if(stored.ExpiresAt is not null && stored.ExpiresAt <= _utcNow) {
                _entries.Remove(key);
                return null;
            }

            return (byte[])stored.Value.Clone();
        }

        /// <summary>
        /// Performs the write operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options instance.</param>
        private void Write(string key, byte[] value, DistributedCacheEntryOptions options) {
            DateTimeOffset? expiresAt = null;

            if(options.AbsoluteExpiration is not null) {
                expiresAt = options.AbsoluteExpiration.Value;
            } else if(options.AbsoluteExpirationRelativeToNow is not null) {
                expiresAt = _utcNow + options.AbsoluteExpirationRelativeToNow.Value;
            }

            _entries[key] = ((byte[])value.Clone(), expiresAt);
        }
    }

    /// <summary>
    /// Dictionary-backed store where every <c>GetResourceSetAsync</c> returns a live
    /// view, so backend mutations are visible on the next refetch.
    /// </summary>
    private sealed class DistributedTestStore : IResourceAdminStore {
        private readonly object _sync = new();
        private readonly Dictionary<(string Set, string Culture, string Name), string> _data = [];
        private readonly Dictionary<(string Set, string Name), string> _notes = [];

        /// <summary>
        /// Gets or sets the artificial delay ms.
        /// </summary>
        public int ArtificialDelayMs { get; set; }

        /// <summary>
        /// Gets or sets the throw on reload.
        /// </summary>
        public bool ThrowOnReload { get; set; }

        /// <summary>
        /// Gets or sets the get resource set call count.
        /// </summary>
        public int GetResourceSetCallCount { get; private set; }

        /// <summary>
        /// Gets or sets the reload call count.
        /// </summary>
        public int ReloadCallCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether supports administration.
        /// </summary>
        public bool SupportsAdministration => true;

        /// <summary>
        /// Raised when audited.
        /// </summary>
        public event EventHandler<ResourceAdminAuditEventArgs>? Audited {
            add => _audited += value;
            remove => _audited -= value;
        }

        private EventHandler<ResourceAdminAuditEventArgs>? _audited;

        /// <summary>
        /// Performs the seed operation.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Seed(string resourceSet, string culture, string name, string value) {
            lock(_sync) {
                _data[(resourceSet, culture, name)] = value;
            }
        }

        /// <summary>
        /// Performs the write operation.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Write(string resourceSet, string culture, string name, string value) => Seed(resourceSet, culture, name, value);

        /// <summary>
        /// Performs the snapshot operation.
        /// </summary>
        /// <returns>The snapshot result.</returns>
        public Dictionary<(string Set, string Culture, string Name), string> Snapshot() {
            lock(_sync) {
                return new Dictionary<(string Set, string Culture, string Name), string>(_data);
            }
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public async Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            GetResourceSetCallCount++;

            if(ArtificialDelayMs > 0) {
                await Task.Delay(ArtificialDelayMs, cancellationToken).ConfigureAwait(false);
            }

            lock(_sync) {
                return _data.Keys.Any(key => key.Set == resourceSet && key.Culture == culture)
                    ? new TestResourceSet(this, resourceSet, culture)
                    : null;
            }
        }

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            lock(_sync) {
                return Task.FromResult(
                    _data.Keys
                        .Where(key => key.Culture == culture)
                        .Select(key => key.Set)
                        .Distinct()
                        .Select<string, ILocalizationResourceSet>(set => new TestResourceSet(this, set, culture))
                        .ToList());
            }
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            lock(_sync) {
                if(_data.Keys.Any(key => key.Set == resourceSet && key.Culture == culture)) {
                    localizationResourceSet = new TestResourceSet(this, resourceSet, culture);
                    return true;
                }
            }

            localizationResourceSet = null;
            return false;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public async Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
            var set = await GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);
            return set is null
                ? LocalizedStringEntry.Missing(resourceSet, culture, name)
                : await set.GetStringAsync(name, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var set = await GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);
            return set is null ? [] : await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();

            foreach(var set in await GetResourceSetsAsync(culture, cancellationToken).ConfigureAwait(false)) {
                foreach(var entry in await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false)) {
                    result[entry.Key] = entry.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            if(TryGetResourceSet(resourceSet, culture, out var set) && set.TryGetString(name, out entry)) {
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) {
            ReloadCallCount++;
            return ThrowOnReload ? Task.FromException(new InvalidOperationException("Reload failed.")) : Task.CompletedTask;
        }

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            lock(_sync) {
                return Task.FromResult(_data.Keys.Select(static key => key.Culture).Distinct().ToList());
            }
        }

        /// <summary>
        /// Creates entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is entry.</returns>
        public Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                if(_data.ContainsKey((resourceSet, culture, name))) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.CreateEntry, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.AlreadyExists, resourceSet, culture, name, "Exists.")));
                }

                _data[(resourceSet, culture, name)] = value;
                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.CreateEntry, true, false, 1, null));
            }
        }

        /// <summary>
        /// Updates entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is entry.</returns>
        public Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                if(!_data.ContainsKey((resourceSet, culture, name))) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.UpdateEntry, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, culture, name, "Missing.")));
                }

                _data[(resourceSet, culture, name)] = value;
                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.UpdateEntry, true, false, 1, null));
            }
        }

        /// <summary>
        /// Deletes entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is entry.</returns>
        public Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                if(!_data.Remove((resourceSet, culture, name))) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteEntry, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, culture, name, "Missing.")));
                }

                if(!_data.Keys.Any(key => key.Set == resourceSet && key.Name == name)) {
                    _notes.Remove((resourceSet, name));
                }

                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteEntry, true, false, 1, null));
            }
        }

        /// <summary>
        /// Performs the rename key operation.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is rename key.</returns>
        public Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                var affected = _data.Keys.Where(key => key.Set == resourceSet && (key.Name == oldName || key.Name == newName)).ToList();

                if(!affected.Any(key => key.Name == oldName)) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameKey, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, null, oldName, "Missing.")));
                }

                if(affected.Any(key => key.Name == newName)) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameKey, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.TargetExists, resourceSet, null, newName, "Exists.")));
                }

                foreach(var key in affected.Where(key => key.Name == oldName).ToList()) {
                    _data[(key.Set, key.Culture, newName)] = _data[key];
                    _data.Remove(key);
                }

                if(_notes.Remove((resourceSet, oldName), out var note)) {
                    _notes[(resourceSet, newName)] = note;
                }

                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameKey, true, false, 1, null));
            }
        }

        /// <summary>
        /// Deletes resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                var doomed = _data.Keys.Where(key => key.Set == resourceSet && key.Culture == culture).ToList();

                if(doomed.Count == 0) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteResourceSet, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, culture, null, "Missing.")));
                }

                foreach(var key in doomed) {
                    _data.Remove(key);
                }

                foreach(var note in _notes.Keys.Where(note => note.Set == resourceSet).ToList()) {
                    if(!_data.Keys.Any(key => key.Set == resourceSet && key.Name == note.Name)) {
                        _notes.Remove(note);
                    }
                }

                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteResourceSet, true, false, doomed.Count, null));
            }
        }

        /// <summary>
        /// Performs the rename resource set operation.
        /// </summary>
        /// <param name="oldResourceSet">The old resource set.</param>
        /// <param name="newResourceSet">The new resource set.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is rename resource set.</returns>
        public Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            lock(_sync) {
                var affected = _data.Keys.Where(key => key.Set == oldResourceSet || key.Set == newResourceSet).ToList();

                if(!affected.Any(key => key.Set == oldResourceSet)) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameResourceSet, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, oldResourceSet, null, null, "Missing.")));
                }

                if(affected.Any(key => key.Set == newResourceSet)) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameResourceSet, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.TargetExists, newResourceSet, null, null, "Exists.")));
                }

                foreach(var key in affected.Where(key => key.Set == oldResourceSet).ToList()) {
                    _data[(newResourceSet, key.Culture, key.Name)] = _data[key];
                    _data.Remove(key);
                }

                foreach(var note in _notes.Keys.Where(note => note.Set == oldResourceSet).ToList()) {
                    _notes[(newResourceSet, note.Name)] = _notes[note];
                    _notes.Remove(note);
                }

                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.RenameResourceSet, true, false, 1, null));
            }
        }

        /// <summary>
        /// Gets note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            lock(_sync) {
                return Task.FromResult(_notes.TryGetValue((resourceSet, name), out var note) ? note : null);
            }
        }

        /// <summary>
        /// Gets notes.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is notes.</returns>
        public Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            lock(_sync) {
                IReadOnlyDictionary<string, string> notes = _notes
                    .Where(pair => pair.Key.Set == resourceSet)
                    .OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)
                    .ToDictionary(static pair => pair.Key.Name, static pair => pair.Value, StringComparer.Ordinal);
                return Task.FromResult(notes);
            }
        }

        /// <summary>
        /// Sets note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="note">The note.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            lock(_sync) {
                if(!_data.Keys.Any(key => key.Set == resourceSet && key.Name == name)) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.SetNote, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, null, name, "Missing.")));
                }

                _notes[(resourceSet, name)] = note;
                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.SetNote, true, false, 1, null));
            }
        }

        /// <summary>
        /// Deletes note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            lock(_sync) {
                if(!_notes.Remove((resourceSet, name))) {
                    return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteNote, false, false, 0,
                        new ResourceAdminConflict(ResourceAdminConflictKind.NotFound, resourceSet, null, name, "Missing.")));
                }

                return Task.FromResult(new ResourceAdminResult(ResourceAdminOperation.DeleteNote, true, false, 1, null));
            }
        }
    }

    /// <summary>
    /// Represents a test resource set.
    /// </summary>
    /// <param name="store">The localization store.</param>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    private sealed class TestResourceSet(DistributedTestStore store, string resourceSet, string culture) : ILocalizationResourceSet {
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            var snapshot = store.Snapshot();
            return Task.FromResult(snapshot.TryGetValue((resourceSet, culture, name), out var value)
                ? new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value)
                : LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            var snapshot = store.Snapshot();
            return Task.FromResult(snapshot
                .Where(pair => pair.Key.Set == resourceSet && pair.Key.Culture == culture)
                .ToDictionary(
                    static pair => pair.Key.Name,
                    pair => new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, pair.Key.Name, pair.Value),
                    StringComparer.Ordinal));
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            var snapshot = store.Snapshot();

            if(snapshot.TryGetValue((resourceSet, culture, name), out var value)) {
                entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            store.Seed(resourceSet, culture, entry.Name, entry.Value);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) {
            foreach(var entry in entries) {
                store.Seed(resourceSet, culture, entry.Name, entry.Value);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) =>
            AddAsync(entry, cancellationToken);

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) =>
            AddAsync(entries, cancellationToken);

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            store.Snapshot();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    #endregion Fakes
}
