//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Governed resource-administration API: CRUD (Create, Read, Update, Delete) + rename round-trips on the
/// EF/SQLite store, read-only rejection with the typed error, dry-run previews that
/// change nothing, validation rejections, per-row audit entries, and parallel-writer
/// rename isolation. Each test owns an isolated in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class ResourceAdminApiTests {
    private const string ACTOR = "resource-admin-suite";

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Verifies that create entry round trips through store and resource set.
    /// </summary>
    [TestMethod]
    public async Task CreateEntry_RoundTrips_ThroughStoreAndResourceSetAsync() {
        await using var fixture = CreateFixture();
        var before = DateTimeOffset.UtcNow;
        var options = new ResourceAdminOptions { Actor = ACTOR };

        var first = await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", options, Cancellation);
        var second = await fixture.Admin.CreateEntryAsync("AdminSet", "nl-NL", "Greeting", "Hallo", options, Cancellation);

        first.Operation.Should().Be(ResourceAdminOperation.CreateEntry);
        first.Applied.Should().BeTrue();
        first.DryRun.Should().BeFalse();
        first.AffectedEntries.Should().Be(1);
        first.Conflict.Should().BeNull();
        second.Applied.Should().BeTrue();

        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");
        (await fixture.Store.GetStringAsync("AdminSet", "nl-NL", "Greeting", Cancellation)).Value.Should().Be("Hallo");
        (await fixture.Store.GetResourceSetAsync("AdminSet", "en-US", Cancellation)).Should().NotBeNull();
        (await fixture.Store.GetAllStringsAsync("AdminSet", "en-US", Cancellation)).Keys.Should().BeEquivalentTo("Greeting");
        (await fixture.Store.GetSupportedCulturesAsync(Cancellation)).Should().BeEquivalentTo("en-US", "nl-NL");

        var after = DateTimeOffset.UtcNow;
        fixture.Audits.Count.Should().Be(2);
        fixture.Audits[0].Operation.Should().Be(ResourceAdminOperation.CreateEntry);
        fixture.Audits[0].Actor.Should().Be(ACTOR);
        fixture.Audits[0].ResourceSet.Should().Be("AdminSet");
        fixture.Audits[0].Culture.Should().Be("en-US");
        fixture.Audits[0].Key.Should().Be("Greeting");
        fixture.Audits[0].NewKey.Should().BeNull();
        fixture.Audits[0].EntryId.Should().NotBe(Guid.Empty);
        fixture.Audits[0].OldValue.Should().BeNull();
        fixture.Audits[0].NewValue.Should().Be("Hello");
        (fixture.Audits[0].Timestamp >= before && fixture.Audits[0].Timestamp <= after).Should().BeTrue("the audit entry carries the commit moment");
        fixture.Audits[1].Culture.Should().Be("nl-NL");
        fixture.Audits[1].NewValue.Should().Be("Hallo");
    }

    /// <summary>
    /// Verifies that create entry empty value is valid stored value.
    /// </summary>
    [TestMethod]
    public async Task CreateEntry_EmptyValue_IsValidStoredValueAsync() {
        await using var fixture = CreateFixture();

        var result = await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Empty", string.Empty, null, Cancellation);

        result.Applied.Should().BeTrue();
        var entry = await fixture.Store.GetStringAsync("AdminSet", "en-US", "Empty", Cancellation);
        entry.IsMissing().Should().BeFalse();
        entry.Value.Should().Be(string.Empty);
    }

    /// <summary>
    /// Verifies that create entry duplicate reports already exists and changes nothing.
    /// </summary>
    [TestMethod]
    public async Task CreateEntry_Duplicate_ReportsAlreadyExistsAndChangesNothingAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Other", null, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.CreateEntry);
        result.Applied.Should().BeFalse();
        result.DryRun.Should().BeFalse();
        result.AffectedEntries.Should().Be(0);
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.AlreadyExists);
        result.Conflict.ResourceSet.Should().Be("AdminSet");
        result.Conflict.Culture.Should().Be("en-US");
        result.Conflict.Key.Should().Be("Greeting");
        (result.Conflict.Detail.Length > 0).Should().BeTrue("the conflict explains the rejection");
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that update entry existing applies new template and audits both values.
    /// </summary>
    [TestMethod]
    public async Task UpdateEntry_Existing_AppliesNewTemplateAndAuditsBothValuesAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.UpdateEntryAsync("AdminSet", "en-US", "Greeting", "Hi", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.UpdateEntry);
        result.Applied.Should().BeTrue();
        result.AffectedEntries.Should().Be(1);
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hi");
        fixture.Audits.Count.Should().Be(1);
        fixture.Audits[0].Operation.Should().Be(ResourceAdminOperation.UpdateEntry);
        fixture.Audits[0].Actor.Should().Be(ACTOR);
        fixture.Audits[0].OldValue.Should().Be("Hello");
        fixture.Audits[0].NewValue.Should().Be("Hi");
    }

    /// <summary>
    /// Verifies that update entry missing reports not found.
    /// </summary>
    [TestMethod]
    public async Task UpdateEntry_Missing_ReportsNotFoundAsync() {
        await using var fixture = CreateFixture();

        var result = await fixture.Admin.UpdateEntryAsync("AdminSet", "en-US", "Missing", "Value", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that delete entry existing removes and reads missing.
    /// </summary>
    [TestMethod]
    public async Task DeleteEntry_Existing_RemovesAndReadsMissingAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.DeleteEntryAsync("AdminSet", "en-US", "Greeting", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.DeleteEntry);
        result.Applied.Should().BeTrue();
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).IsMissing().Should().BeTrue();
        fixture.Audits.Count.Should().Be(1);
        fixture.Audits[0].Operation.Should().Be(ResourceAdminOperation.DeleteEntry);
        fixture.Audits[0].OldValue.Should().Be("Hello");
        fixture.Audits[0].NewValue.Should().BeNull();
    }

    /// <summary>
    /// Verifies that delete entry missing reports not found.
    /// </summary>
    [TestMethod]
    public async Task DeleteEntry_Missing_ReportsNotFoundAsync() {
        await using var fixture = CreateFixture();

        var result = await fixture.Admin.DeleteEntryAsync("AdminSet", "en-US", "Missing", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);
    }

    /// <summary>
    /// Verifies that rename key across cultures is atomic and audited per row.
    /// </summary>
    [TestMethod]
    public async Task RenameKey_AcrossCultures_IsAtomicAndAuditedPerRowAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "nl-NL", "Greeting", "Hallo", null, Cancellation);
        var beforeIds = await fixture.Store.GetAllStringsAsync("AdminSet", "en-US", Cancellation);
        var beforeNlIds = await fixture.Store.GetAllStringsAsync("AdminSet", "nl-NL", Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameKeyAsync("AdminSet", "Greeting", "Welcome", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.RenameKey);
        result.Applied.Should().BeTrue();
        result.AffectedEntries.Should().Be(2);
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Welcome", Cancellation)).Value.Should().Be("Hello");
        (await fixture.Store.GetStringAsync("AdminSet", "nl-NL", "Welcome", Cancellation)).Value.Should().Be("Hallo");
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).IsMissing().Should().BeTrue();
        (await fixture.Store.GetStringAsync("AdminSet", "nl-NL", "Greeting", Cancellation)).IsMissing().Should().BeTrue();

        fixture.Audits.Count.Should().Be(2);
        fixture.Audits[0].Culture.Should().Be("en-US");
        fixture.Audits[1].Culture.Should().Be("nl-NL");
        foreach(var audit in fixture.Audits) {
            audit.Operation.Should().Be(ResourceAdminOperation.RenameKey);
            audit.Actor.Should().Be(ACTOR);
            audit.ResourceSet.Should().Be("AdminSet");
            audit.Key.Should().Be("Greeting");
            audit.NewKey.Should().Be("Welcome");
            audit.NewValue.Should().BeNull();
        }

        fixture.Audits[0].OldValue.Should().Be("Hello");
        fixture.Audits[1].OldValue.Should().Be("Hallo");
        fixture.Audits[0].EntryId.Should().Be(beforeIds["Greeting"].Id);
        fixture.Audits[1].EntryId.Should().Be(beforeNlIds["Greeting"].Id);
    }

    /// <summary>
    /// Verifies that rename key missing source reports not found and changes nothing.
    /// </summary>
    [TestMethod]
    public async Task RenameKey_MissingSource_ReportsNotFoundAndChangesNothingAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Kept", "Kept-Value", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameKeyAsync("AdminSet", "Missing", "Target", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);
        result.Conflict.Culture.Should().BeNull("a key rename spans cultures");
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Kept", Cancellation)).Value.Should().Be("Kept-Value");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that rename key target exists in one culture refuses atomically.
    /// </summary>
    [TestMethod]
    public async Task RenameKey_TargetExistsInOneCulture_RefusesAtomicallyAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Source", "EN-Source", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "nl-NL", "Source", "NL-Source", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Target", "EN-Target", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameKeyAsync("AdminSet", "Source", "Target", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.TargetExists);
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Source", Cancellation)).Value.Should().Be("EN-Source");
        (await fixture.Store.GetStringAsync("AdminSet", "nl-NL", "Source", Cancellation)).Value.Should().Be("NL-Source",
            "the unaffected culture must not be renamed when another culture conflicts");
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Target", Cancellation)).Value.Should().Be("EN-Target");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that delete resource set scoped to culture pair.
    /// </summary>
    [TestMethod]
    public async Task DeleteResourceSet_ScopedToCulturePairAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "A", "EN-A", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "B", "EN-B", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "nl-NL", "A", "NL-A", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.DeleteResourceSetAsync("AdminSet", "en-US", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.DeleteResourceSet);
        result.Applied.Should().BeTrue();
        result.AffectedEntries.Should().Be(2);
        (await fixture.Store.GetResourceSetAsync("AdminSet", "en-US", Cancellation)).Should().BeNull();
        (await fixture.Store.GetStringAsync("AdminSet", "nl-NL", "A", Cancellation)).Value.Should().Be("NL-A",
            "the sibling culture is out of scope and stays intact");
        fixture.Audits.Count.Should().Be(2);
        foreach(var audit in fixture.Audits) {
            audit.Operation.Should().Be(ResourceAdminOperation.DeleteResourceSet);
            audit.Actor.Should().Be(ACTOR);
            audit.ResourceSet.Should().Be("AdminSet");
            audit.Culture.Should().Be("en-US");
        }
    }

    /// <summary>
    /// Verifies that delete resource set missing reports not found.
    /// </summary>
    [TestMethod]
    public async Task DeleteResourceSet_Missing_ReportsNotFoundAsync() {
        await using var fixture = CreateFixture();

        var result = await fixture.Admin.DeleteResourceSetAsync("AdminSet", "en-US", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);
    }

    /// <summary>
    /// Verifies that rename resource set across cultures is atomic.
    /// </summary>
    [TestMethod]
    public async Task RenameResourceSet_AcrossCultures_IsAtomicAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("OldSet", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("OldSet", "nl-NL", "Greeting", "Hallo", null, Cancellation);
        var beforeEn = await fixture.Store.GetStringAsync("OldSet", "en-US", "Greeting", Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameResourceSetAsync("OldSet", "NewSet", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.RenameResourceSet);
        result.Applied.Should().BeTrue();
        result.AffectedEntries.Should().Be(2);
        (await fixture.Store.GetStringAsync("NewSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");
        (await fixture.Store.GetStringAsync("NewSet", "nl-NL", "Greeting", Cancellation)).Value.Should().Be("Hallo");
        (await fixture.Store.GetResourceSetAsync("OldSet", "en-US", Cancellation)).Should().BeNull();
        fixture.Audits.Count.Should().Be(2);
        foreach(var audit in fixture.Audits) {
            audit.Operation.Should().Be(ResourceAdminOperation.RenameResourceSet);
            audit.ResourceSet.Should().Be("NewSet");
            audit.Key.Should().Be("Greeting");
        }

        fixture.Audits[0].EntryId.Should().Be(beforeEn.Id);
    }

    /// <summary>
    /// Verifies that rename resource set target exists refuses atomically.
    /// </summary>
    [TestMethod]
    public async Task RenameResourceSet_TargetExists_RefusesAtomicallyAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("OldSet", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("OldSet", "nl-NL", "Greeting", "Hallo", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("NewSet", "en-US", "Other", "Other-Value", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameResourceSetAsync("OldSet", "NewSet", null, Cancellation);

        result.Applied.Should().BeFalse();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.TargetExists);
        (await fixture.Store.GetStringAsync("OldSet", "nl-NL", "Greeting", Cancellation)).Value.Should().Be("Hallo",
            "no culture may move when the target set already occupies a culture");
        (await fixture.Store.GetStringAsync("NewSet", "en-US", "Other", Cancellation)).Value.Should().Be("Other-Value");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that read only store rejects with typed error.
    /// </summary>
    [TestMethod]
    public void ReadOnlyStore_RejectsWithTypedError() {
        ILocalizationStore store = new ReadOnlyLocalizationStore();

        store.SupportsAdministration().Should().BeFalse();

        Action require = () => store.RequireAdmin();
        require.Should().Throw<StoreReadOnlyException>();
    }

    /// <summary>
    /// Verifies that read only store direct admin call throws typed error.
    /// </summary>
    [TestMethod]
    public async Task ReadOnlyStore_DirectAdminCall_ThrowsTypedErrorAsync() {
        var inner = new ReadOnlyLocalizationStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        ILocalizationStore store = new CachedLocalizationStoreDecorator(inner, cache);

        store.SupportsAdministration().Should().BeFalse("the decorator honestly reports the inner read-only store");

        Func<Task> create = () => store.RequireAdmin().CreateEntryAsync("AdminSet", "en-US", "Key", "Value", null, Cancellation);
        await create.Should().ThrowAsync<StoreReadOnlyException>();

        Func<Task> decorated = () => ((IResourceAdminStore)store).RenameKeyAsync("AdminSet", "A", "B", null, Cancellation);
        await decorated.Should().ThrowAsync<StoreReadOnlyException>();
    }

    /// <summary>
    /// Verifies that decorator forwards admin invalidates cache and forwards audit.
    /// </summary>
    [TestMethod]
    public async Task Decorator_ForwardsAdmin_InvalidatesCache_AndForwardsAuditAsync() {
        await using var fixture = CreateFixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorated = new CachedLocalizationStoreDecorator(fixture.Store, cache);
        var forwarded = new List<ResourceAdminAuditEventArgs>();
        decorated.Audited += (_, entry) => forwarded.Add(entry);

        decorated.SupportsAdministration.Should().BeTrue();
        await decorated.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);
        (await decorated.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");

        await decorated.UpdateEntryAsync("AdminSet", "en-US", "Greeting", "Hi", null, Cancellation);
        (await decorated.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hi",
            "the applied mutation must invalidate the decorator cache");

        forwarded.Count.Should().Be(2);
        forwarded[0].Operation.Should().Be(ResourceAdminOperation.CreateEntry);
        forwarded[1].Operation.Should().Be(ResourceAdminOperation.UpdateEntry);
        forwarded[1].NewValue.Should().Be("Hi");
    }

    /// <summary>
    /// Verifies that decorator forwards the translator note surface.
    /// </summary>
    [TestMethod]
    public async Task Decorator_ForwardsTheTranslatorNoteSurfaceAsync() {
        await using var fixture = CreateFixture();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorated = new CachedLocalizationStoreDecorator(fixture.Store, cache);
        var forwarded = new List<ResourceAdminAuditEventArgs>();
        decorated.Audited += (_, entry) => forwarded.Add(entry);

        await decorated.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        await decorated.SetNoteAsync("AdminSet", "Greeting", "Hint.", new ResourceAdminOptions { Actor = ACTOR }, Cancellation);

        (await decorated.GetNoteAsync("AdminSet", "Greeting", Cancellation)).Should().Be("Hint.");
        var notes = await decorated.GetNotesAsync("AdminSet", Cancellation);
        notes["Greeting"].Should().Be("Hint.");
        forwarded[1].Operation.Should().Be(ResourceAdminOperation.SetNote, "the audit hook forwards through the decorator");

        var deleted = await decorated.DeleteNoteAsync("AdminSet", "Greeting", null, Cancellation);
        deleted.Applied.Should().BeTrue();
        (await decorated.GetNoteAsync("AdminSet", "Greeting", Cancellation)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that read only store note mutations throw typed error and reads report no notes.
    /// </summary>
    [TestMethod]
    public async Task ReadOnlyStore_NoteMutationsThrowTypedError_AndReadsReportNoNotesAsync() {
        var inner = new ReadOnlyLocalizationStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new CachedLocalizationStoreDecorator(inner, cache);

        Func<Task> setNote = () => store.SetNoteAsync("AdminSet", "Key", "Hint.", null, Cancellation);
        Func<Task> deleteNote = () => store.DeleteNoteAsync("AdminSet", "Key", null, Cancellation);
        await setNote.Should().ThrowAsync<StoreReadOnlyException>();
        await deleteNote.Should().ThrowAsync<StoreReadOnlyException>();

        (await store.GetNoteAsync("AdminSet", "Key", Cancellation)).Should().BeNull("a store without the admin contract has no notes");
        (await store.GetNotesAsync("AdminSet", Cancellation)).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that dry run create and rename change nothing and raise no audit.
    /// </summary>
    [TestMethod]
    public async Task DryRun_CreateAndRename_ChangeNothingAndRaiseNoAuditAsync() {
        await using var fixture = CreateFixture();
        var previewOptions = new ResourceAdminOptions { DryRun = true, Actor = ACTOR };

        var create = await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", previewOptions, Cancellation);

        create.Applied.Should().BeFalse();
        create.DryRun.Should().BeTrue();
        create.AffectedEntries.Should().Be(1);
        create.Conflict.Should().BeNull();
        (await fixture.Store.GetResourceSetAsync("AdminSet", "en-US", Cancellation)).Should().BeNull("a dry-run changes nothing");

        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("AdminSet", "nl-NL", "Greeting", "Hallo", null, Cancellation);
        fixture.Audits.Clear();

        var rename = await fixture.Admin.RenameKeyAsync("AdminSet", "Greeting", "Welcome", previewOptions, Cancellation);

        rename.Applied.Should().BeFalse();
        rename.DryRun.Should().BeTrue();
        rename.AffectedEntries.Should().Be(2);
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Welcome", Cancellation)).IsMissing().Should().BeTrue();
        fixture.Audits.Should().BeEmpty("a dry-run raises no audit entries");
    }

    /// <summary>
    /// Verifies that dry run conflict preview reports conflict without applying.
    /// </summary>
    [TestMethod]
    public async Task DryRun_ConflictPreview_ReportsConflictWithoutApplyingAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.CreateEntryAsync(
            "AdminSet", "en-US", "Greeting", "Other", new ResourceAdminOptions { DryRun = true }, Cancellation);

        result.Applied.Should().BeFalse();
        result.DryRun.Should().BeTrue();
        result.Conflict.Should().NotBeNull();
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.AlreadyExists);
        (await fixture.Store.GetStringAsync("AdminSet", "en-US", "Greeting", Cancellation)).Value.Should().Be("Hello");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that validation invalid identifiers throw argument exception.
    /// </summary>
    [TestMethod]
    public async Task Validation_InvalidIdentifiers_ThrowArgumentExceptionAsync() {
        await using var fixture = CreateFixture();

        Func<Task> emptySet = () => fixture.Admin.CreateEntryAsync(string.Empty, "en-US", "Key", "Value", null, Cancellation);
        await emptySet.Should().ThrowAsync<ArgumentException>();

        Func<Task> whitespaceKey = () => fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "   ", "Value", null, Cancellation);
        await whitespaceKey.Should().ThrowAsync<ArgumentException>();

        Func<Task> badCulture = () => fixture.Admin.CreateEntryAsync("AdminSet", "not-a-culture!!", "Key", "Value", null, Cancellation);
        await badCulture.Should().ThrowAsync<ArgumentException>();

        Func<Task> nullValue = () => fixture.Admin.CreateEntryAsync("AdminSet", "en-US", "Key", null!, null, Cancellation);
        await nullValue.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullSet = () => fixture.Admin.DeleteResourceSetAsync(null!, "en-US", null, Cancellation);
        await nullSet.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> renameBlank = () => fixture.Admin.RenameKeyAsync("AdminSet", "A", string.Empty, null, Cancellation);
        await renameBlank.Should().ThrowAsync<ArgumentException>();

        (await fixture.Store.GetSupportedCulturesAsync(Cancellation)).Should().BeEmpty("rejected mutations persist nothing");
        fixture.Audits.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that concurrent renames serialize to single winner without split.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentRenames_SerializeToSingleWinner_WithoutSplitAsync() {
        await using var fixture = CreateFixture();
        await fixture.Admin.CreateEntryAsync("ConcSet", "en-US", "Key", "EN", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("ConcSet", "nl-NL", "Key", "NL", null, Cancellation);
        fixture.Audits.Clear();

        var tasks = new List<Task<ResourceAdminResult>>();
        for(var index = 0; index < 8; index++) {
            var target = index % 2 == 0 ? "Alpha" : "Beta";
            tasks.Add(fixture.Admin.RenameKeyAsync("ConcSet", "Key", target, null, Cancellation));
        }

        var results = await Task.WhenAll(tasks);

        results.Count(static result => result.Applied).Should().Be(1, "concurrent writers cannot interleave a rename");
        foreach(var result in results.Where(static result => !result.Applied)) {
            result.Conflict.Should().NotBeNull();
        }

        var enAlpha = await fixture.Store.GetStringAsync("ConcSet", "en-US", "Alpha", Cancellation);
        var enBeta = await fixture.Store.GetStringAsync("ConcSet", "en-US", "Beta", Cancellation);
        var nlAlpha = await fixture.Store.GetStringAsync("ConcSet", "nl-NL", "Alpha", Cancellation);
        var nlBeta = await fixture.Store.GetStringAsync("ConcSet", "nl-NL", "Beta", Cancellation);
        var winner = !enAlpha.IsMissing() ? "Alpha" : "Beta";

        (!enAlpha.IsMissing() ^ !enBeta.IsMissing()).Should().BeTrue("exactly one rename target exists in en-US");
        (!nlAlpha.IsMissing() ^ !nlBeta.IsMissing()).Should().BeTrue("exactly one rename target exists in nl-NL");
        (winner == "Alpha" ? !nlAlpha.IsMissing() : !nlBeta.IsMissing()).Should().BeTrue(
            "both cultures moved together: no split key is observable");

        fixture.Audits.Count.Should().Be(2);
        foreach(var audit in fixture.Audits) {
            audit.NewKey.Should().Be(winner);
        }
    }

    /// <summary>
    /// Verifies that supports administration flag advertises per store.
    /// </summary>
    [TestMethod]
    public async Task SupportsAdministration_Flag_AdvertisesPerStoreAsync() {
        await using var fixture = CreateFixture();

        fixture.Store.SupportsAdministration.Should().BeTrue();
        fixture.Admin.Should().NotBeNull();

        ILocalizationStore readOnly = new ReadOnlyLocalizationStore();
        readOnly.SupportsAdministration().Should().BeFalse();
    }

    /// <summary>
    /// Creates fixture.
    /// </summary>
    /// <returns>The fixture result.</returns>
    private static SqliteAdminFixture CreateFixture() => new();

    /// <summary>
    /// Provides test infrastructure for sqlite admin.
    /// </summary>
    private sealed class SqliteAdminFixture : IAsyncDisposable {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// Initializes a new instance of the SqliteAdminFixture class.
        /// </summary>
        public SqliteAdminFixture() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<LocalizationDataContext>()
                .UseSqlite(_connection)
                .Options;
            using(var context = new LocalizationDataContext(options)) {
                context.Database.EnsureCreated();
            }

            Store = new SqlDbLocalizationStore(new InlineDbContextFactory(options));
            Store.Audited += (_, entry) => {
                lock(Audits) {
                    Audits.Add(entry);
                }
            };
        }

        /// <summary>
        /// Gets the store.
        /// </summary>
        public SqlDbLocalizationStore Store { get; }

        /// <summary>
        /// Gets the admin.
        /// </summary>
        public SqlDbLocalizationStore Admin => Store;

        /// <summary>
        /// Gets the audits.
        /// </summary>
        public List<ResourceAdminAuditEventArgs> Audits { get; } = [];

        /// <summary>
        /// Performs the dispose operation.
        /// </summary>
        public async ValueTask DisposeAsync() {
            await _connection.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Creates inline db context.
    /// </summary>
    /// <param name="options">The options instance.</param>
    private sealed class InlineDbContextFactory(DbContextOptions<LocalizationDataContext> options) : IDbContextFactory<LocalizationDataContext> {
        /// <summary>
        /// Creates db context.
        /// </summary>
        /// <returns>The db context result.</returns>
        public LocalizationDataContext CreateDbContext() => new(options);

        /// <summary>
        /// Creates db context.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is db context.</returns>
        public Task<LocalizationDataContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    /// <summary>
    /// Stores read only localization.
    /// </summary>
    private sealed class ReadOnlyLocalizationStore : ILocalizationStore {
        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(resourceSet);
            ArgumentException.ThrowIfNullOrEmpty(culture);
            return Task.FromResult<ILocalizationResourceSet?>(null);
        }

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrEmpty(culture);
            return Task.FromResult(new List<ILocalizationResourceSet>());
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            ArgumentNullException.ThrowIfNull(resourceSet);
            ArgumentException.ThrowIfNullOrEmpty(culture);
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
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(resourceSet);
            ArgumentException.ThrowIfNullOrEmpty(culture);
            ArgumentNullException.ThrowIfNull(name);
            return Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(resourceSet);
            ArgumentException.ThrowIfNullOrEmpty(culture);
            return Task.FromResult(new Dictionary<string, LocalizedStringEntry>());
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrEmpty(culture);
            return Task.FromResult(new Dictionary<string, LocalizedStringEntry>());
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            ArgumentNullException.ThrowIfNull(resourceSet);
            ArgumentException.ThrowIfNullOrEmpty(culture);
            ArgumentNullException.ThrowIfNull(name);
            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new List<string>());
        }
    }
}
