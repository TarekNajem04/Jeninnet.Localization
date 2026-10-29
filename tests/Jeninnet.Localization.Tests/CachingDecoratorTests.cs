//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the caching decorator tests.
/// </summary>
[TestClass]
public sealed class CachingDecoratorTests : IDisposable {
    private MemoryCache? _cache;

    /// <summary>
    /// Performs the initialize operation.
    /// </summary>
    [TestInitialize]
    public void Initialize() => _cache = new MemoryCache(new MemoryCacheOptions());

    /// <summary>
    /// Performs the cleanup operation.
    /// </summary>
    [TestCleanup]
    public void Cleanup() => _cache?.Dispose();

    /// <summary>
    /// Verifies that get string async cache hit does not call inner.
    /// </summary>
    [TestMethod]
    public async Task GetStringAsync_CacheHit_DoesNotCallInnerAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerResourceSet = new MockResourceSet();
        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value");

        mockInnerResourceSet.SetupGetString("Key", entry);

        // Act
        var firstResult = await decorator.GetStringAsync("Key", cancellationToken);
        var secondResult = await decorator.GetStringAsync("Key", cancellationToken);

        // Assert
        firstResult.Value.Should().Be("Value");
        secondResult.Value.Should().Be("Value");
        mockInnerResourceSet.GetStringCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that update async invalidates cache.
    /// </summary>
    [TestMethod]
    public async Task UpdateAsync_InvalidatesCacheAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerResourceSet = new MockResourceSet();
        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "InitialValue");
        var updatedEntry = new LocalizedStringEntry(entry.Id, "TestSet", "en-US", "Key", "UpdatedValue");

        mockInnerResourceSet.SetupGetString("Key", entry);

        // Act
        var initialResult = await decorator.GetStringAsync("Key", cancellationToken);
        await decorator.UpdateAsync(updatedEntry, cancellationToken);
        var updatedResult = await decorator.GetStringAsync("Key", cancellationToken);

        // Assert
        initialResult.Value.Should().Be("InitialValue");
        updatedResult.Value.Should().Be("UpdatedValue");
        mockInnerResourceSet.UpdateCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that get resource set async same resource set different cultures returns culture specific sets.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_SameResourceSetDifferentCultures_ReturnsCultureSpecificSetsAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        var englishSet = new MockResourceSet("Common", "en-US");
        englishSet.SetupGetString("Welcome", new LocalizedStringEntry(Guid.NewGuid(), "Common", "en-US", "Welcome", "Hello"));
        var arabicSet = new MockResourceSet("Common", "ar-SA");
        arabicSet.SetupGetString("Welcome", new LocalizedStringEntry(Guid.NewGuid(), "Common", "ar-SA", "Welcome", "مرحبا"));
        mockInnerStore.SetupResourceSet(englishSet);
        mockInnerStore.SetupResourceSet(arabicSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var englishEntry = await decorator.GetStringAsync("Common", "en-US", "Welcome", cancellationToken);
        var arabicEntry = await decorator.GetStringAsync("Common", "ar-SA", "Welcome", cancellationToken);

        // Assert
        englishEntry.Value.Should().Be("Hello");
        arabicEntry.Value.Should().Be("مرحبا");
    }

    /// <summary>
    /// Verifies that get supported cultures async forwards to inner not derived from cache.
    /// </summary>
    [TestMethod]
    public async Task GetSupportedCulturesAsync_ForwardsToInner_NotDerivedFromCacheAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        mockInnerStore.SetSupportedCultures("en-US", "ar-SA");

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var cultures = await decorator.GetSupportedCulturesAsync(cancellationToken);

        // Assert
        cultures.Should().HaveCount(2);
        cultures.Should().Contain("en-US");
        cultures.Should().Contain("ar-SA");
        mockInnerStore.SupportedCulturesCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that reload async invalidates cached resource set.
    /// </summary>
    [TestMethod]
    public async Task ReloadAsync_InvalidatesCachedResourceSetAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Key", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "InitialValue"));
        mockInnerStore.SetupResourceSet(mockInnerResourceSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var initialResult = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);
        mockInnerResourceSet.SetupGetString("Key", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "UpdatedValue"));
        await decorator.ReloadAsync(cancellationToken);
        var reloadedResult = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);

        // Assert
        initialResult.Value.Should().Be("InitialValue");
        reloadedResult.Value.Should().Be("UpdatedValue");
        mockInnerStore.ReloadCallCount.Should().Be(1);
        mockInnerResourceSet.GetStringCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that reload async when inner throws keeps last known good cache.
    /// </summary>
    [TestMethod]
    public async Task ReloadAsync_WhenInnerThrows_KeepsLastKnownGoodCacheAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Key", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "InitialValue"));
        mockInnerStore.SetupResourceSet(mockInnerResourceSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var initialResult = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);
        mockInnerStore.ThrowOnReload = true;
        await ((Func<Task>)(() => decorator.ReloadAsync(cancellationToken))).Should().ThrowAsync<InvalidOperationException>();
        mockInnerStore.ThrowOnReload = false;
        var cachedResult = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);

        // Assert
        initialResult.Value.Should().Be("InitialValue");
        cachedResult.Value.Should().Be("InitialValue");
        mockInnerResourceSet.GetStringCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that get all strings async returns snapshot without missing entries.
    /// </summary>
    [TestMethod]
    public async Task GetAllStringsAsync_ReturnsSnapshotWithoutMissingEntriesAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Hello", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Hello", "Hello"));
        mockInnerResourceSet.SetupGetString("World", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "World", "World"));

        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);

        // Act
        var missingEntry = await decorator.GetStringAsync("Absent", cancellationToken);
        var allStrings = await decorator.GetAllStringsAsync(cancellationToken);
        allStrings["Hello"] = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Hello", "Mutated");
        var snapshot = await decorator.GetAllStringsAsync(cancellationToken);

        // Assert
        missingEntry.IsMissing().Should().BeTrue();
        allStrings.Should().HaveCount(2);
        allStrings.Keys.Should().Contain("Hello");
        allStrings.Keys.Should().Contain("World");
        allStrings.Keys.Should().NotContain("Absent");
        snapshot["Hello"].Value.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that add async replaces previously missing entry.
    /// </summary>
    [TestMethod]
    public async Task AddAsync_ReplacesPreviouslyMissingEntryAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerResourceSet = new MockResourceSet();
        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value");

        // Act
        var missingEntry = await decorator.GetStringAsync("Key", cancellationToken);
        await decorator.AddAsync(entry, cancellationToken);
        var addedResult = await decorator.GetStringAsync("Key", cancellationToken);

        // Assert
        missingEntry.IsMissing().Should().BeTrue();
        addedResult.Value.Should().Be("Value");
        addedResult.IsMissing().Should().BeFalse();
    }

    /// <summary>
    /// Verifies that get resource set async concurrent culture requests completes without exception.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_ConcurrentCultureRequests_CompletesWithoutExceptionAsync() {
        // Arrange
        var mockInnerStore = new MockLocalizationStore();
        var englishSet = new MockResourceSet("Common", "en-US");
        englishSet.SetupGetString("Welcome", new LocalizedStringEntry(Guid.NewGuid(), "Common", "en-US", "Welcome", "Hello"));
        var arabicSet = new MockResourceSet("Common", "ar-SA");
        arabicSet.SetupGetString("Welcome", new LocalizedStringEntry(Guid.NewGuid(), "Common", "ar-SA", "Welcome", "مرحبا"));
        mockInnerStore.SetupResourceSet(englishSet);
        mockInnerStore.SetupResourceSet(arabicSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var tasks = Enumerable.Range(0, 100).Select(index => Task.Run(async () => {
            var culture = index % 2 == 0 ? "en-US" : "ar-SA";
            var expectedValue = index % 2 == 0 ? "Hello" : "مرحبا";
            var entry = await decorator.GetStringAsync("Common", culture, "Welcome", CancellationToken.None);
            entry.Value.Should().Be(expectedValue);
        }, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Verifies that get string async concurrent distinct keys completes without exception.
    /// </summary>
    [TestMethod]
    public async Task GetStringAsync_ConcurrentDistinctKeys_CompletesWithoutExceptionAsync() {
        // Arrange
        var mockInnerResourceSet = new MockResourceSet();

        for(var index = 0; index < 200; index++) {
            mockInnerResourceSet.SetupGetString($"Key{index}", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", $"Key{index}", $"Value{index}"));
        }

        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);

        // Act
        var tasks = Enumerable.Range(0, 200).Select(index => Task.Run(async () => {
            var entry = await decorator.GetStringAsync($"Key{index}", CancellationToken.None);
            entry.Value.Should().Be($"Value{index}");
        }, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Verifies that get resource set async same key and culture concurrent misses completes without exception.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_SameKeyAndCulture_ConcurrentMisses_CompletesWithoutExceptionAsync() {
        // Arrange
        var mockInnerStore = new MockLocalizationStore();
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Welcome", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Welcome", "Hello"));
        mockInnerStore.SetupResourceSet(mockInnerResourceSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(async () => {
            var entry = await decorator.GetStringAsync("TestSet", "en-US", "Welcome", CancellationToken.None);
            entry.Value.Should().Be("Hello");
        }, CancellationToken.None)).ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Verifies that get all strings async after individual lookups returns complete set.
    /// </summary>
    [TestMethod]
    public async Task GetAllStringsAsync_AfterIndividualLookups_ReturnsCompleteSetAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Alpha", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Alpha", "Alpha"));
        mockInnerResourceSet.SetupGetString("Beta", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Beta", "Beta"));
        mockInnerResourceSet.SetupGetString("Gamma", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Gamma", "Gamma"));

        var decorator = new CachedLocalizationResourceSetDecorator(mockInnerResourceSet, _cache!);

        // Act
        await decorator.GetStringAsync("Alpha", cancellationToken);
        await decorator.GetStringAsync("Beta", cancellationToken);
        var allStrings = await decorator.GetAllStringsAsync(cancellationToken);

        // Assert
        allStrings.Should().HaveCount(3);
        allStrings.Keys.Should().Contain("Alpha");
        allStrings.Keys.Should().Contain("Beta");
        allStrings.Keys.Should().Contain("Gamma");
    }

    /// <summary>
    /// Verifies that get supported cultures async after reload reflects inner store.
    /// </summary>
    [TestMethod]
    public async Task GetSupportedCulturesAsync_AfterReload_ReflectsInnerStoreAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        mockInnerStore.SetSupportedCultures("en-US");

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        var beforeReload = await decorator.GetSupportedCulturesAsync(cancellationToken);
        mockInnerStore.SetSupportedCultures("en-US", "ar-SA");
        await decorator.ReloadAsync(cancellationToken);
        var afterReload = await decorator.GetSupportedCulturesAsync(cancellationToken);

        // Assert
        beforeReload.Should().HaveCount(1);
        afterReload.Should().HaveCount(2);
        afterReload.Should().Contain("en-US");
        afterReload.Should().Contain("ar-SA");
    }

    /// <summary>
    /// Verifies that get resource set async inner failure does not publish partial state.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_InnerFailure_DoesNotPublishPartialStateAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Key", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value"));
        mockInnerStore.SetupResourceSet(mockInnerResourceSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        mockInnerStore.GetResourceSetThunk = (_, _, _) => Task.FromException<ILocalizationResourceSet?>(new InvalidOperationException("Inner failure."));
        await ((Func<Task>)(() => decorator.GetResourceSetAsync("TestSet", "en-US", cancellationToken))).Should().ThrowAsync<InvalidOperationException>();

        mockInnerStore.GetResourceSetThunk = null;
        var recoveredEntry = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);

        // Assert
        recoveredEntry.Value.Should().Be("Value");
    }

    /// <summary>
    /// Verifies that get resource set async cancellation during miss does not publish partial state.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_CancellationDuringMiss_DoesNotPublishPartialStateAsync() {
        // Arrange
        var cancellationToken = TestContext?.CancellationToken ?? CancellationToken.None;
        var mockInnerStore = new MockLocalizationStore();
        var mockInnerResourceSet = new MockResourceSet();
        mockInnerResourceSet.SetupGetString("Key", new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value"));
        mockInnerStore.SetupResourceSet(mockInnerResourceSet);

        var decorator = new CachedLocalizationStoreDecorator(mockInnerStore, _cache!);

        // Act
        mockInnerStore.GetResourceSetThunk = (_, _, token) =>
            token.IsCancellationRequested
                ? Task.FromCanceled<ILocalizationResourceSet?>(token)
                : throw new InvalidOperationException("Expected a pre-canceled token.");

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await ((Func<Task>)(() => decorator.GetResourceSetAsync("TestSet", "en-US", cancellationSource.Token))).Should().ThrowAsync<OperationCanceledException>();

        mockInnerStore.GetResourceSetThunk = null;
        var recoveredEntry = await decorator.GetStringAsync("TestSet", "en-US", "Key", cancellationToken);

        // Assert
        recoveredEntry.Value.Should().Be("Value");
    }

    /// <summary>
    /// Releases the resources used by the current instance.
    /// </summary>
    public void Dispose() => _cache?.Dispose();

    /// <summary>
    /// Test double for a resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    private sealed class MockResourceSet(string resourceSet = "TestSet", string culture = "en-US") : ILocalizationResourceSet {
        private readonly Dictionary<string, LocalizedStringEntry> _entries = [];

        /// <summary>
        /// Gets or sets the get string count.
        /// </summary>
        public int GetStringCount { get; private set; }
        /// <summary>
        /// Gets or sets the update count.
        /// </summary>
        public int UpdateCount { get; private set; }

        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;
        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Performs the setup get string operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="entry">The entry.</param>
        public void SetupGetString(string key, LocalizedStringEntry entry) => _entries[key] = entry;

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
            GetStringCount++;
            return Task.FromResult(_entries.TryGetValue(name, out var entry) ? entry : LocalizedStringEntry.Missing(ResourceSet, Culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_entries);
        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) => _entries.TryGetValue(name, out entry);
        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) {
            UpdateCount++;
            _entries[entry.Name] = entry;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Stores mock localization.
    /// </summary>
    private sealed class MockLocalizationStore : ILocalizationStore {
        private readonly Dictionary<(string Name, string Culture), ILocalizationResourceSet> _resourceSets = [];
        private readonly List<string> _supportedCultures = [];

        /// <summary>
        /// Gets or sets the get resource set call count.
        /// </summary>
        public int GetResourceSetCallCount { get; private set; }
        /// <summary>
        /// Gets or sets the get resource set for culture call count.
        /// </summary>
        public int GetResourceSetForCultureCallCount { get; private set; }
        /// <summary>
        /// Gets or sets the reload call count.
        /// </summary>
        public int ReloadCallCount { get; private set; }
        /// <summary>
        /// Gets or sets the supported cultures call count.
        /// </summary>
        public int SupportedCulturesCallCount { get; private set; }
        /// <summary>
        /// Gets or sets the throw on reload.
        /// </summary>
        public bool ThrowOnReload { get; set; }

        /// <summary>
        /// Gets or sets the get resource set thunk.
        /// </summary>
        public Func<string, string, CancellationToken, Task<ILocalizationResourceSet?>>? GetResourceSetThunk { get; set; }

        /// <summary>
        /// Performs the setup resource set operation.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        public void SetupResourceSet(ILocalizationResourceSet resourceSet) => _resourceSets[(resourceSet.ResourceSet, resourceSet.Culture)] = resourceSet;

        /// <summary>
        /// Sets supported cultures.
        /// </summary>
        /// <param name="cultures">The cultures.</param>
        public void SetSupportedCultures(params string[] cultures) {
            _supportedCultures.Clear();
            _supportedCultures.AddRange(cultures);
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            GetResourceSetCallCount++;

            if(GetResourceSetThunk is not null) {
                return GetResourceSetThunk(resourceSet, culture, cancellationToken);
            }

            return Task.FromResult(_resourceSets.TryGetValue((resourceSet, culture), out var set) ? set : null);
        }

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            GetResourceSetForCultureCallCount++;
            var matching = _resourceSets.Values.Where(set => set.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(matching);
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            if(_resourceSets.TryGetValue((resourceSet, culture), out localizationResourceSet)) {
                return true;
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
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
            var set = _resourceSets.GetValueOrDefault((resourceSet, culture));

            if(set is null) {
                return Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));
            }

            return set.GetStringAsync(name, cancellationToken);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var set = _resourceSets.GetValueOrDefault((resourceSet, culture));
            return set is null ? [] : await set.GetAllStringsAsync(cancellationToken);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();

            foreach(var resourceSet in _resourceSets.Values.Where(set => set.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))) {
                foreach(var entry in await resourceSet.GetAllStringsAsync(cancellationToken)) {
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
            if(_resourceSets.TryGetValue((resourceSet, culture), out var set)) {
                return set.TryGetString(name, out entry);
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
            SupportedCulturesCallCount++;
            return Task.FromResult(new List<string>(_supportedCultures));
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
