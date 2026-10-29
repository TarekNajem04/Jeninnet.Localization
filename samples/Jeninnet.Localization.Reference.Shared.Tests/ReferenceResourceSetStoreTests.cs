//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the reference resource set store tests.
/// </summary>
[TestClass]
public sealed class ReferenceResourceSetStoreTests {
    private readonly ReferenceResourceSetStore _store = new();

    /// <summary>
    /// Verifies that get resource set async resolves supported cultures and rejects others.
    /// </summary>
    [TestMethod]
    public async Task GetResourceSetAsync_ResolvesSupportedCulturesAndRejectsOthersAsync() {
        var en = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "en", TestContext.CancellationToken);
        Assert.IsNotNull(en);
        Assert.AreEqual(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, en.ResourceSet);
        Assert.AreEqual("en", en.Culture);

        var unsupported = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "fr", TestContext.CancellationToken);
        Assert.IsNull(unsupported);
    }

    /// <summary>
    /// Verifies that try get resource set on demand for any resource set name.
    /// </summary>
    [TestMethod]
    public void TryGetResourceSet_OnDemandForAnyResourceSetName() {
        var found = _store.TryGetResourceSet("Any.Resource.Set.Name", "ar-SY", out var set);

        Assert.IsTrue(found);
        Assert.IsNotNull(set);
        Assert.AreEqual("Any.Resource.Set.Name", set.ResourceSet);
        Assert.AreEqual("ar-SY", set.Culture);
    }

    /// <summary>
    /// Verifies that get set string async returns content or missing deterministically.
    /// </summary>
    [TestMethod]
    public async Task GetSetStringAsync_ReturnsContentOrMissingDeterministicallyAsync() {
        var set = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "en", TestContext.CancellationToken)
            ?? throw new InvalidOperationException("Expected an 'en' resource set.");

        var found = await set.GetStringAsync(ReferenceKeys.BOOK_PROPHET_TITLE, TestContext.CancellationToken);
        Assert.IsFalse(found.IsMissing());
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.BOOK_PROPHET_TITLE], found.Value);
        Assert.AreEqual(set.ResourceSet, found.ResourceSet);
        Assert.AreEqual("en", found.Culture);

        var regionalSet = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "ar-SY", TestContext.CancellationToken)
            ?? throw new InvalidOperationException("Expected an 'ar-SY' resource set.");
        var regional = await regionalSet.GetStringAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, TestContext.CancellationToken);
        Assert.IsTrue(regional.IsMissing());
        Assert.AreEqual($"[MISSING:{ReferenceKeys.DEMO_REGIONAL_NOTE}]", regional.ToString());
    }

    /// <summary>
    /// Verifies that get string async entry ids are stable across lookups.
    /// </summary>
    [TestMethod]
    public async Task GetStringAsync_EntryIdsAreStableAcrossLookupsAsync() {
        var first = await _store.GetStringAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "ar", ReferenceKeys.BOOK_CAIRO_TRILOGY_TITLE, TestContext.CancellationToken);
        var second = await _store.GetStringAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "ar", ReferenceKeys.BOOK_CAIRO_TRILOGY_TITLE, TestContext.CancellationToken);

        Assert.AreEqual(first.Id, second.Id);
    }

    /// <summary>
    /// Verifies that get string async returns missing for unknown culture.
    /// </summary>
    [TestMethod]
    public async Task GetStringAsync_ReturnsMissingForUnknownCultureAsync() {
        var entry = await _store.GetStringAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "fr", ReferenceKeys.BOOK_PROPHET_TITLE, TestContext.CancellationToken);

        Assert.IsTrue(entry.IsMissing());
    }

    /// <summary>
    /// Verifies that get all strings async reflects the culture content.
    /// </summary>
    [TestMethod]
    public async Task GetAllStringsAsync_ReflectsTheCultureContentAsync() {
        var entries = await _store.GetAllStringsAsync("Any.Resource.Set", "ar-SY", TestContext.CancellationToken);

        Assert.HasCount(LocalizationContent.GetTemplates("ar-SY").Count, entries);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.APPLICATION_TITLE], entries[ReferenceKeys.APPLICATION_TITLE].Value);
    }

    /// <summary>
    /// Verifies that get all strings for culture async reflects the culture content.
    /// </summary>
    [TestMethod]
    public async Task GetAllStringsForCultureAsync_ReflectsTheCultureContentAsync() {
        var entries = await _store.GetAllStringsForCultureAsync("en", TestContext.CancellationToken);

        Assert.HasCount(LocalizationContent.GetTemplates("en").Count, entries);
    }

    /// <summary>
    /// Verifies that get supported cultures async matches supported cultures in order.
    /// </summary>
    [TestMethod]
    public async Task GetSupportedCulturesAsync_MatchesSupportedCulturesInOrderAsync() {
        var cultures = await _store.GetSupportedCulturesAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual([.. ReferenceCultures.SupportedCultures], [.. cultures]);
    }

    /// <summary>
    /// Verifies that reload async completes without mutation.
    /// </summary>
    [TestMethod]
    public async Task ReloadAsync_CompletesWithoutMutationAsync() {
        await _store.ReloadAsync(TestContext.CancellationToken);
        Assert.HasCount(LocalizationContent.GetTemplates("en").Count, await _store.GetAllStringsForCultureAsync("en", TestContext.CancellationToken));
    }

    /// <summary>
    /// Verifies that set mutation methods throw not is supported.
    /// </summary>
    [TestMethod]
    public async Task SetMutationMethods_ThrowNotSupportedAsync() {
        var set = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "en", TestContext.CancellationToken)
            ?? throw new InvalidOperationException("Expected an 'en' resource set.");
        var entry = await set.GetStringAsync(ReferenceKeys.APPLICATION_TITLE, TestContext.CancellationToken);
        LocalizedStringEntry[] entries = [entry];

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.AddAsync(entry, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.AddAsync(entries, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.UpdateAsync(entry, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.UpdateAsync(entries, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.DeleteAsync(entry, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => set.DeleteAsync(entries, TestContext.CancellationToken));
    }

    /// <summary>
    /// Verifies that get culture info matches the set culture.
    /// </summary>
    [TestMethod]
    public async Task GetCultureInfo_MatchesTheSetCultureAsync() {
        var set = await _store.GetResourceSetAsync(ReferenceResourceSetStore.DEFAULT_RESOURCE_SET, "ar-SY", TestContext.CancellationToken)
            ?? throw new InvalidOperationException("Expected an 'ar-SY' resource set.");

        Assert.AreEqual(new CultureInfo("ar-SY"), set.GetCultureInfo());
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
