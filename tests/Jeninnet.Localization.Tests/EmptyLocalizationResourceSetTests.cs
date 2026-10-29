//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the empty localization resource set tests.
/// </summary>
[TestClass]
public sealed class EmptyLocalizationResourceSetTests {
    private const string RESOURCE_SET = "MissingResourceSet";
    private const string CULTURE = "en-US";
    private const string UNSUPPORTED_MESSAGE = "EmptyLocalizationResourceSet does not support CRUD operations.";

    /// <summary>
    /// Verifies that resource set exposes configured name and culture.
    /// </summary>
    [TestMethod]
    public void ResourceSet_ExposesConfiguredNameAndCulture() {
        var resourceSet = CreateResourceSet();

        resourceSet.ResourceSet.Should().Be(RESOURCE_SET);
        resourceSet.Culture.Should().Be(CULTURE);
        resourceSet.GetCultureInfo().Name.Should().Be(CULTURE);
    }

    /// <summary>
    /// Verifies that get string async returns missing entry.
    /// </summary>
    [TestMethod]
    public async Task GetStringAsync_ReturnsMissingEntryAsync() {
        var resourceSet = CreateResourceSet();

        var result = await resourceSet.GetStringAsync("MissingKey", CancellationToken.None);

        result.ResourceSet.Should().Be(RESOURCE_SET);
        result.Culture.Should().Be(CULTURE);
        result.Name.Should().Be("MissingKey");
        result.Value.Should().Be("[MISSING:MissingKey]");
        result.ResourceNotFound.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that get all strings async returns empty dictionary.
    /// </summary>
    [TestMethod]
    public async Task GetAllStringsAsync_ReturnsEmptyDictionaryAsync() {
        var resourceSet = CreateResourceSet();

        var result = await resourceSet.GetAllStringsAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that try get string returns false and null entry.
    /// </summary>
    [TestMethod]
    public void TryGetString_ReturnsFalseAndNullEntry() {
        var resourceSet = CreateResourceSet();

        var found = resourceSet.TryGetString("MissingKey", out var entry);

        found.Should().BeFalse();
        entry.Should().BeNull();
    }

    /// <summary>
    /// Verifies that add async single entry throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task AddAsync_SingleEntry_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.AddAsync(CreateEntry(), TestContext.CancellationToken));

    /// <summary>
    /// Verifies that add async bulk entries throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task AddAsync_BulkEntries_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.AddAsync([CreateEntry()], TestContext.CancellationToken));

    /// <summary>
    /// Verifies that update async single entry throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task UpdateAsync_SingleEntry_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.UpdateAsync(CreateEntry(), TestContext.CancellationToken));

    /// <summary>
    /// Verifies that update async bulk entries throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task UpdateAsync_BulkEntries_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.UpdateAsync([CreateEntry()], TestContext.CancellationToken));

    /// <summary>
    /// Verifies that delete async single entry throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_SingleEntry_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.DeleteAsync(CreateEntry(), TestContext.CancellationToken));

    /// <summary>
    /// Verifies that delete async bulk entries throws not supported exception.
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_BulkEntries_ThrowsNotSupportedExceptionAsync() =>
        await AssertUnsupportedAsync(resourceSet => resourceSet.DeleteAsync([CreateEntry()], TestContext.CancellationToken));

    /// <summary>
    /// Verifies that unsupported operation throws not supported when token is canceled.
    /// </summary>
    [TestMethod]
    public async Task UnsupportedOperation_ThrowsNotSupportedWhenTokenIsCanceledAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await AssertUnsupportedAsync(resourceSet => resourceSet.AddAsync(CreateEntry(), cancellation.Token));
    }

    /// <summary>
    /// Creates resource set.
    /// </summary>
    /// <returns>The resource set result.</returns>
    private static EmptyLocalizationResourceSet CreateResourceSet() => new(RESOURCE_SET, CULTURE);

    /// <summary>
    /// Creates entry.
    /// </summary>
    /// <returns>The entry result.</returns>
    private static LocalizedStringEntry CreateEntry() =>
        new(Guid.NewGuid(), RESOURCE_SET, CULTURE, "Key", "Value");

    /// <summary>
    /// Asserts unsupported.
    /// </summary>
    /// <param name="operation">The operation.</param>
    private static async Task AssertUnsupportedAsync(Func<EmptyLocalizationResourceSet, Task> operation) {
        var resourceSet = CreateResourceSet();
        Func<Task> act = async () => await operation(resourceSet);

        var exception = (await act.Should().ThrowAsync<NotSupportedException>()).Exception;

        exception.Message.Should().Be(UNSUPPORTED_MESSAGE);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
