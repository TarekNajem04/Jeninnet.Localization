//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the custom string localizer tests.
/// </summary>
[TestClass]
public class CustomStringLocalizerTests {
    /// <summary>
    /// Verifies that basic resolution returns value.
    /// </summary>
    [TestMethod]
    public void BasicResolution_ReturnsValue() {
        // Arrange
        var mockResourceSet = new MockResourceSet();
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Welcome", "Hello World");
        mockResourceSet.SetupGetString("Welcome", entry);

        var localizer = new CustomStringLocalizer(mockResourceSet);

        // Act
        var result = localizer["Welcome"];

        // Assert
        Assert.AreEqual("Hello World", result.Value);
    }

    /// <summary>
    /// Verifies that factory uses synchronous store probe without blocking on async lookup.
    /// </summary>
    [TestMethod]
    public void Factory_UsesSynchronousStoreProbeWithoutBlockingOnAsyncLookup() {
        var resourceSet = new MockResourceSet();
        resourceSet.SetupGetString("Welcome", new LocalizedStringEntry(
            Guid.NewGuid(), FactoryResource.NAME, "en-US", "Welcome", "Hello World"));
        var factory = new CustomStringLocalizerFactory(new MockStore(resourceSet));

        var localizer = factory.Create(typeof(FactoryResource));

        Assert.AreEqual("Hello World", localizer["Welcome"].Value);
    }

    /// <summary>
    /// Verifies that localizer synchronous framework bridge preserves missing fallback.
    /// </summary>
    [TestMethod]
    public void Localizer_SynchronousFrameworkBridgePreservesMissingFallback() {
        var localizer = new CustomStringLocalizer(new MockResourceSet());

        var result = localizer["Missing"];

        Assert.AreEqual("[MISSING:Missing]", result.Value);
        Assert.IsTrue(result.ResourceNotFound);
    }

    /// <summary>
    /// Verifies that localizer hit path does not enter async contract.
    /// </summary>
    [TestMethod]
    public void Localizer_HitPathDoesNotEnterAsyncContract() {
        var resourceSet = new MockResourceSet();
        resourceSet.SetupGetString("Welcome", new LocalizedStringEntry(
            Guid.NewGuid(), "TestSet", "en-US", "Welcome", "Hello World"));
        resourceSet.SetupGetStringAsyncThrows(new InvalidOperationException("GetStringAsync must not be reached on the synchronous hit path."));

        var localizer = new CustomStringLocalizer(resourceSet);

        var result = localizer["Welcome"];

        Assert.AreEqual("Hello World", result.Value);
        Assert.IsFalse(result.ResourceNotFound);
    }

    /// <summary>
    /// Verifies that localizer miss bridge preserves original exception semantics.
    /// </summary>
    [TestMethod]
    public void Localizer_MissBridge_PreservesOriginalExceptionSemantics() {
        var resourceSet = new MockResourceSet();
        resourceSet.SetupGetStringAsyncThrows(new InvalidOperationException("store failure"));

        var localizer = new CustomStringLocalizer(resourceSet);

        var act = () => _ = localizer["Missing"];

        var exception = act.Should().Throw<InvalidOperationException>().Exception;

        Assert.AreEqual("store failure", exception.Message);
    }

    /// <summary>
    /// Verifies that localizer get all strings resolves through the synchronous framework bridge.
    /// </summary>
    [TestMethod]
    public void Localizer_GetAllStrings_ResolvesThroughTheSynchronousFrameworkBridge() {
        var resourceSet = new MockResourceSet();
        resourceSet.SetupGetString("Welcome", new LocalizedStringEntry(
            Guid.NewGuid(), "TestSet", "en-US", "Welcome", "Hello World"));
        resourceSet.SetupGetString("Goodbye", new LocalizedStringEntry(
            Guid.NewGuid(), "TestSet", "en-US", "Goodbye", "See you later"));

        var localizer = new CustomStringLocalizer(resourceSet);

        var results = localizer.GetAllStrings(includeParentCultures: false).ToList();

        results.Should().HaveCount(2);
        results.Select(static s => s.Name).Should().BeEquivalentTo("Welcome", "Goodbye");
        results.Select(static s => s.Value).Should().BeEquivalentTo("Hello World", "See you later");
    }

    /// <summary>
    /// Verifies that protected constructor reading resource set before assignment throws invalid operation exception.
    /// </summary>
    [TestMethod]
    public void ProtectedConstructor_ReadingResourceSetBeforeAssignment_ThrowsInvalidOperationException() {
        var localizer = new DerivedLocalizer();

        var act = () => _ = localizer.ResourceSet;

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that protected constructor assigning null resource set throws argument null exception.
    /// </summary>
    [TestMethod]
    public void ProtectedConstructor_AssigningNullResourceSet_ThrowsArgumentNullException() {
        var localizer = new DerivedLocalizer();

        var act = () => localizer.ResourceSet = null!;

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that protected constructor localizer works after valid assignment.
    /// </summary>
    [TestMethod]
    public void ProtectedConstructor_LocalizerWorksAfterValidAssignment() {
        var localizer = new DerivedLocalizer {
            ResourceSet = new MockResourceSet()
        };

        var result = localizer["Missing"];

        result.Value.Should().Be("[MISSING:Missing]");
        result.ResourceNotFound.Should().BeTrue();
    }

    /// <summary>
    /// Represents a derived localizer.
    /// </summary>
    private sealed class DerivedLocalizer : CustomStringLocalizer {
        /// <summary>
        /// Gets or sets the resource set.
        /// </summary>
        public ILocalizationResourceSet ResourceSet {
            get => LocalizationResourceSet;
            set => LocalizationResourceSet = value;
        }
    }

    /// <summary>
    /// Represents a factory resource.
    /// </summary>
    private static class FactoryResource {
        /// <summary>
        /// Stores the NAME.
        /// </summary>
        public const string NAME = nameof(FactoryResource);
    }

    /// <summary>
    /// Stores mock.
    /// </summary>
    /// <param name="mockResourceSet">The mock resource set.</param>
    private sealed class MockStore(MockResourceSet mockResourceSet) : ILocalizationStore {
        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The factory must use the synchronous store probe.");

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult<List<ILocalizationResourceSet>>([mockResourceSet]);

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            localizationResourceSet = mockResourceSet;
            return true;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string> { "en-US" });
    }

    /// <summary>
    /// Test double for a resource set.
    /// </summary>
    private sealed class MockResourceSet : ILocalizationResourceSet {
        private readonly Dictionary<string, LocalizedStringEntry> _entries = [];
        private Exception? _getStringAsyncException;
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => "TestSet";
        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => "en-US";

        /// <summary>
        /// Performs the setup get string operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="entry">The entry.</param>
        public void SetupGetString(string key, LocalizedStringEntry entry) => _entries[key] = entry;
        /// <summary>
        /// Performs the setup get string async throws operation.
        /// </summary>
        /// <param name="exception">The exception.</param>
        public void SetupGetStringAsyncThrows(Exception exception) => _getStringAsyncException = exception;
        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new("en-US");
        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            if(_getStringAsyncException is not null) {
                return Task.FromException<LocalizedStringEntry>(_getStringAsyncException);
            }

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
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
}
