//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Code-first resource synchronization: declarations applied
/// through the governed  path on the EF/SQLite store — idempotency (second run
/// mutates and audits nothing), dry-run purity, the explicit update/note gates,
/// target-culture semantics (never modified; skipped or created as empty
/// placeholders), explicit mapped renames with conflict honesty, read-only rejection,
/// validate-then-apply failure behavior, and attribute discovery. Each test owns an
/// isolated in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class ResourceSynchronizerTests {
    private const string ACTOR = "pp-12-suite";

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Performs the options operation.
    /// </summary>
    /// <param name="defaultCulture">The default culture.</param>
    /// <param name="targetCultures">The target cultures.</param>
    /// <param name="dryRun">The dry run.</param>
    /// <param name="allowUpdates">The allow updates.</param>
    /// <param name="createMissingTargetCultures">The create missing target cultures.</param>
    /// <param name="renames">The renames.</param>
    /// <returns>The options result.</returns>
    private static ResourceSynchronizationOptions Options(
        string defaultCulture = "en",
        IReadOnlyList<string>? targetCultures = null,
        bool dryRun = false,
        bool allowUpdates = false,
        bool createMissingTargetCultures = false,
        IReadOnlyDictionary<string, string>? renames = null) =>
        new() {
            DefaultCulture = defaultCulture,
            TargetCultures = targetCultures ?? [],
            DryRun = dryRun,
            AllowUpdates = allowUpdates,
            CreateMissingTargetCultures = createMissingTargetCultures,
            Renames = renames ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Actor = ACTOR,
        };

    /// <summary>
    /// Verifies that synchronize creates missing entries through the governed path.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_CreatesMissingEntries_ThroughTheGovernedPathAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        var declarations = new List<ResourceDeclaration> {
            new("Cart", "Cart.Add", "Add to cart"),
            new("Cart", "Cart.Remove", "Remove"),
            new("Orders", "Cart.Add", "Order add"),
        };

        var report = await synchronizer.SynchronizeAsync(declarations, Options(), Cancellation);

        report.DryRun.Should().BeFalse();
        report.DefaultCulture.Should().Be("en");
        report.Created.Should().Be(3);
        report.Total.Should().Be(3);
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Add", Cancellation)).Value.Should().Be("Add to cart");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Remove", Cancellation)).Value.Should().Be("Remove");
        (await fixture.Store.GetStringAsync("Orders", "en", "Cart.Add", Cancellation)).Value.Should().Be("Order add");
        fixture.Audits.Count.Should().Be(3);
        fixture.Audits.Should().OnlyContain(static audit => audit.Operation == ResourceAdminOperation.CreateEntry && audit.Actor == ACTOR);
    }

    /// <summary>
    /// Verifies that synchronize second run mutates nothing and audits nothing.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_SecondRun_MutatesNothing_AndAuditsNothingAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        var declarations = new List<ResourceDeclaration> { new("Cart", "Cart.Add", "Add to cart") };

        var first = await synchronizer.SynchronizeAsync(declarations, Options(), Cancellation);
        var auditsAfterFirst = fixture.Audits.Count;
        var second = await synchronizer.SynchronizeAsync(declarations, Options(), Cancellation);

        first.Created.Should().Be(1);
        second.Unchanged.Should().Be(1);
        second.Total.Should().Be(1);
        fixture.Audits.Count.Should().Be(auditsAfterFirst, "an idempotent run raises no audit entries");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Add", Cancellation)).Value.Should().Be("Add to cart");
    }

    /// <summary>
    /// Verifies that synchronize dry run mutates nothing and audits nothing.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_DryRun_MutatesNothing_AndAuditsNothingAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        var declarations = new List<ResourceDeclaration> { new("Cart", "Cart.Add", "Add to cart") };

        var preview = await synchronizer.SynchronizeAsync(declarations, Options(dryRun: true), Cancellation);

        preview.DryRun.Should().BeTrue();
        preview.Created.Should().Be(1);
        preview.Entries[0].Detail.Should().Be("would be created");
        fixture.Audits.Should().BeEmpty("dry-run previews raise no audit entries");
        (await fixture.Store.GetAllStringsAsync("Cart", "en", Cancellation)).Should().BeEmpty();

        var applied = await synchronizer.SynchronizeAsync(declarations, Options(), Cancellation);

        applied.Created.Should().Be(1);
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Add", Cancellation)).Value.Should().Be("Add to cart");
    }

    /// <summary>
    /// Verifies that synchronize differing value without allow updates reports is skipped.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_DifferingValue_WithoutAllowUpdates_ReportsSkippedAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.Add", "Stored value", null, Cancellation);
        var auditsBefore = fixture.Audits.Count;

        var report = await synchronizer.SynchronizeAsync([new ResourceDeclaration("Cart", "Cart.Add", "Declared value")], Options(), Cancellation);

        report.Skipped.Should().Be(1);
        report.Entries[0].Detail.Should().Be("value differs; updates not allowed");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Add", Cancellation)).Value.Should().Be("Stored value");
        fixture.Audits.Count.Should().Be(auditsBefore, "a skipped entry mutates nothing");
    }

    /// <summary>
    /// Verifies that synchronize differing value with allow updates through governed path.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_DifferingValue_WithAllowUpdates_UpdatesThroughGovernedPathAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.Add", "Stored value", null, Cancellation);

        var report = await synchronizer.SynchronizeAsync([new ResourceDeclaration("Cart", "Cart.Add", "Declared value")], Options(allowUpdates: true), Cancellation);

        report.Updated.Should().Be(1);
        report.Entries[0].Detail.Should().Be("updated");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Add", Cancellation)).Value.Should().Be("Declared value");
        fixture.Audits.Should().Contain(static audit => audit.Operation == ResourceAdminOperation.UpdateEntry && audit.Actor == ACTOR && audit.NewValue == "Declared value");
    }

    /// <summary>
    /// Verifies that synchronize declared note applied on create and upserted only with allow updates.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_DeclaredNote_AppliedOnCreate_AndUpsertedOnlyWithAllowUpdatesAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);

        var created = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart") { Note = "First note" }],
            Options(),
            Cancellation);

        created.Created.Should().Be(1);
        created.Entries[0].Detail.Should().Be("created with the declared note");
        (await fixture.Admin.GetNoteAsync("Cart", "Cart.Add", Cancellation)).Should().Be("First note");

        var unchanged = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart") { Note = "First note" }],
            Options(),
            Cancellation);

        unchanged.Unchanged.Should().Be(1);

        var skipped = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart") { Note = "Second note" }],
            Options(),
            Cancellation);

        skipped.Skipped.Should().Be(1);
        skipped.Entries[0].Detail.Should().Be("note differs; updates not allowed");
        (await fixture.Admin.GetNoteAsync("Cart", "Cart.Add", Cancellation)).Should().Be("First note");

        var updated = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart") { Note = "Second note" }],
            Options(allowUpdates: true),
            Cancellation);

        updated.Updated.Should().Be(1);
        updated.Entries[0].Detail.Should().Be("note updated");
        (await fixture.Admin.GetNoteAsync("Cart", "Cart.Add", Cancellation)).Should().Be("Second note");

        var noNote = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart")],
            Options(allowUpdates: true),
            Cancellation);

        noNote.Unchanged.Should().Be(1);
        (await fixture.Admin.GetNoteAsync("Cart", "Cart.Add", Cancellation)).Should().Be("Second note", "a declaration without a note never touches existing notes");
    }

    /// <summary>
    /// Verifies that synchronize target cultures are never modified and skipped by default.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_TargetCultures_AreNeverModified_AndSkippedByDefaultAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "ar", "Cart.Add", "أضف إلى السلة", null, Cancellation);

        var report = await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "Add to cart")],
            Options(targetCultures: ["ar", "nl"]),
            Cancellation);

        report.Entries.Select(static entry => (entry.Culture, entry.Status, entry.Detail)).Should().Equal(
            ("ar", ResourceSynchronizationStatus.Unchanged, "existing entry left untouched"),
            ("en", ResourceSynchronizationStatus.Created, "created"),
            ("nl", ResourceSynchronizationStatus.Skipped, "target-culture entry not created; fallback applies"));
        (await fixture.Store.GetStringAsync("Cart", "ar", "Cart.Add", Cancellation)).Value.Should().Be("أضف إلى السلة");
        (await fixture.Store.GetAllStringsAsync("Cart", "nl", Cancellation)).Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that synchronize create missing target cultures creates empty placeholders.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_CreateMissingTargetCultures_CreatesEmptyPlaceholdersAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        var declarations = new List<ResourceDeclaration> { new("Cart", "Cart.Add", "Add to cart") };

        var preview = await synchronizer.SynchronizeAsync(declarations, Options(targetCultures: ["ar"], dryRun: true, createMissingTargetCultures: true), Cancellation);

        preview.Created.Should().Be(2);
        preview.Entries.Should().Contain(static entry => entry.Culture == "ar" && entry.Status == ResourceSynchronizationStatus.Created && entry.Detail == "would be created as untranslated (empty)");
        (await fixture.Store.GetAllStringsAsync("Cart", "ar", Cancellation)).Should().BeEmpty();

        var applied = await synchronizer.SynchronizeAsync(declarations, Options(targetCultures: ["ar"], createMissingTargetCultures: true), Cancellation);

        applied.Created.Should().Be(2);
        (await fixture.Store.GetStringAsync("Cart", "ar", "Cart.Add", Cancellation)).Value.Should().BeEmpty();

        var second = await synchronizer.SynchronizeAsync(declarations, Options(targetCultures: ["ar"], createMissingTargetCultures: true), Cancellation);

        second.Unchanged.Should().Be(2);
    }

    /// <summary>
    /// Verifies that synchronize rename applies explicit map once.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_Rename_AppliesExplicitMap_OnceAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.Old", "Add to cart", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("Cart", "ar", "Cart.Old", "أضف إلى السلة", null, Cancellation);
        var declarations = new List<ResourceDeclaration> { new("Cart", "Cart.New", "Add to cart") };
        var renames = new Dictionary<string, string>(StringComparer.Ordinal) { ["Cart.Old"] = "Cart.New" };

        var report = await synchronizer.SynchronizeAsync(declarations, Options(targetCultures: ["ar"], renames: renames), Cancellation);

        report.Renamed.Should().Be(1);
        report.Entries.Should().Contain(static entry => entry.Status == ResourceSynchronizationStatus.Renamed && entry.Key == "Cart.New" && entry.Culture.Length == 0 && entry.Detail == "renamed from 'Cart.Old'");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.New", Cancellation)).Value.Should().Be("Add to cart");
        (await fixture.Store.GetStringAsync("Cart", "ar", "Cart.New", Cancellation)).Value.Should().Be("أضف إلى السلة");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Old", Cancellation)).IsMissing().Should().BeTrue();
        fixture.Audits.Should().Contain(static audit => audit.Operation == ResourceAdminOperation.RenameKey);

        var second = await synchronizer.SynchronizeAsync(declarations, Options(targetCultures: ["ar"], renames: renames), Cancellation);

        second.Renamed.Should().Be(0, "the rename applies once");
        second.Unchanged.Should().Be(2);
    }

    /// <summary>
    /// Verifies that synchronize rename target exists conflict is reported and other keys is proceed.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_Rename_TargetExistsConflict_IsReported_AndOtherKeysProceedAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.Old", "Old", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.New", "New", null, Cancellation);
        var declarations = new List<ResourceDeclaration> {
            new("Cart", "Cart.New", "New"),
            new("Cart", "Cart.Other", "Other"),
        };
        var renames = new Dictionary<string, string>(StringComparer.Ordinal) { ["Cart.Old"] = "Cart.New" };

        var report = await synchronizer.SynchronizeAsync(declarations, Options(renames: renames), Cancellation);

        report.Conflicts.Should().Be(1);
        report.Entries.Should().Contain(static entry => entry.Status == ResourceSynchronizationStatus.Conflict && entry.Key == "Cart.New" && entry.Detail.StartsWith("rename from 'Cart.Old' refused:", StringComparison.Ordinal));
        report.Created.Should().Be(1, "a conflict on one key never aborts the others");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Old", Cancellation)).Value.Should().Be("Old");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.New", Cancellation)).Value.Should().Be("New");
    }

    /// <summary>
    /// Verifies that synchronize rename old key still declared is skipped.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_Rename_OldKeyStillDeclared_IsSkippedAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);
        await fixture.Admin.CreateEntryAsync("Cart", "en", "Cart.Old", "Old", null, Cancellation);
        var declarations = new List<ResourceDeclaration> {
            new("Cart", "Cart.Old", "Old"),
            new("Cart", "Cart.New", "New"),
        };
        var renames = new Dictionary<string, string>(StringComparer.Ordinal) { ["Cart.Old"] = "Cart.New" };

        var report = await synchronizer.SynchronizeAsync(declarations, Options(renames: renames), Cancellation);

        report.Renamed.Should().Be(0);
        report.Skipped.Should().Be(1);
        report.Entries.Should().Contain(static entry => entry.Status == ResourceSynchronizationStatus.Skipped && entry.Key == "Cart.New" && entry.Detail == "rename not applied: old key 'Cart.Old' is still declared in this resource set");
        (await fixture.Store.GetStringAsync("Cart", "en", "Cart.Old", Cancellation)).Value.Should().Be("Old");
    }

    /// <summary>
    /// Verifies that synchronize read only store rejects with typed error before any read.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_ReadOnlyStore_RejectsWithTypedError_BeforeAnyReadAsync() {
        var store = new DisabledAdminStore();
        var synchronizer = new ResourceSynchronizer(store);

        Func<Task> act = async () => await synchronizer.SynchronizeAsync([new ResourceDeclaration("Cart", "Cart.Add", "Add")], Options(), Cancellation);

        await act.Should().ThrowAsync<StoreReadOnlyException>();
        store.ReadsAttempted.Should().BeFalse("the capability check precedes every store read");
    }

    /// <summary>
    /// Verifies that synchronize invalid input fails before any store call.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_InvalidInput_FailsBeforeAnyStoreCallAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);

        Func<Task> duplicates = async () => await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "One"), new ResourceDeclaration("Cart", "Cart.Add", "Two")],
            Options(),
            Cancellation);
        await duplicates.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyCulture = async () => await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "One")],
            Options(defaultCulture: ""),
            Cancellation);
        await emptyCulture.Should().ThrowAsync<ArgumentException>();

        Func<Task> unknownCulture = async () => await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "One")],
            Options(defaultCulture: "not-a-culture!!"),
            Cancellation);
        await unknownCulture.Should().ThrowAsync<ArgumentException>();

        Func<Task> selfRename = async () => await synchronizer.SynchronizeAsync(
            [new ResourceDeclaration("Cart", "Cart.Add", "One")],
            Options(renames: new Dictionary<string, string>(StringComparer.Ordinal) { ["Cart.Add"] = "Cart.Add" }),
            Cancellation);
        await selfRename.Should().ThrowAsync<ArgumentException>();

        Func<Task> nullDeclarations = async () => await synchronizer.SynchronizeAsync(null!, Options(), Cancellation);
        await nullDeclarations.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullOptions = async () => await synchronizer.SynchronizeAsync([new ResourceDeclaration("Cart", "Cart.Add", "One")], null!, Cancellation);
        await nullOptions.Should().ThrowAsync<ArgumentNullException>();

        fixture.Audits.Should().BeEmpty("validate-then-apply fails before any store call");
    }

    /// <summary>
    /// Verifies that synchronize empty declarations returns empty report.
    /// </summary>
    [TestMethod]
    public async Task Synchronize_EmptyDeclarations_ReturnsEmptyReportAsync() {
        await using var fixture = CreateFixture();
        var synchronizer = new ResourceSynchronizer(fixture.Admin);

        var report = await synchronizer.SynchronizeAsync([], Options(), Cancellation);

        report.Total.Should().Be(0);
        report.Created.Should().Be(0);
        report.Conflicts.Should().Be(0);
    }

    /// <summary>
    /// Verifies that discovery collects attribute declarations deterministically.
    /// </summary>
    [TestMethod]
    public void Discovery_CollectsAttributeDeclarations_Deterministically() {
        var declarations = LocalizedResourceDiscovery.FromType(typeof(DeclaredSample));

        declarations.Select(static declaration => (declaration.ResourceSet, declaration.Key, declaration.DefaultTemplate, declaration.Note)).Should().Equal(
            ("DiscoverySet", "Discovery.Field", "Field template", null),
            ("DiscoverySet", "Discovery.Method", "Method template", null),
            ("DiscoverySet", "Discovery.Property", "Property template", null),
            ("DiscoverySet", "Discovery.Type", "Type template", "type note"));

        var fromAssembly = LocalizedResourceDiscovery.FromAssembly(typeof(ResourceSynchronizerTests).Assembly);

        fromAssembly
            .Select(static declaration => (declaration.ResourceSet, declaration.Key, declaration.DefaultTemplate, declaration.Note))
            .Should().BeEquivalentTo(declarations.Select(static declaration => (declaration.ResourceSet, declaration.Key, declaration.DefaultTemplate, declaration.Note)));
    }

    /// <summary>
    /// Represents a declared sample.
    /// </summary>
    [LocalizedResource("DiscoverySet", "Discovery.Type", "Type template", Note = "type note")]
    private static class DeclaredSample {
        /// <summary>
        /// Stores the field.
        /// </summary>
        [LocalizedResource("DiscoverySet", "Discovery.Field", "Field template")]
        public static readonly string Field = "x";

        /// <summary>
        /// Performs the method operation.
        /// </summary>
        /// <returns>The method text.</returns>
        [LocalizedResource("DiscoverySet", "Discovery.Method", "Method template")]
        public static string Method() => "x";

        /// <summary>
        /// Gets the property.
        /// </summary>
        [LocalizedResource("DiscoverySet", "Discovery.Property", "Property template")]
        public static string Property => "x";
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
    /// Stores disabled admin.
    /// </summary>
    private sealed class DisabledAdminStore : IResourceAdminStore {
        /// <summary>
        /// Gets or sets the reads attempted.
        /// </summary>
        public bool ReadsAttempted { get; private set; }

        /// <summary>
        /// Gets a value indicating whether supports administration.
        /// </summary>
        public bool SupportsAdministration => false;

        /// <summary>
        /// Raised when audited.
        /// </summary>
        public event EventHandler<ResourceAdminAuditEventArgs>? Audited {
            add => _ = value;
            remove => _ = value;
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) => Read<ILocalizationResourceSet?>();

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) => Read<List<ILocalizationResourceSet>>();

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            ReadsAttempted = true;
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
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) => Read<LocalizedStringEntry>();

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) => Read<Dictionary<string, LocalizedStringEntry>>();

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) => Read<Dictionary<string, LocalizedStringEntry>>();

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [NotNullWhen(true)] out LocalizedStringEntry? entry) {
            ReadsAttempted = true;
            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Read<object>();

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) => Read<List<string>>();

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
        public Task<ResourceAdminResult> CreateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

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
        public Task<ResourceAdminResult> UpdateEntryAsync(string resourceSet, string culture, string name, string value, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Deletes entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is entry.</returns>
        public Task<ResourceAdminResult> DeleteEntryAsync(string resourceSet, string culture, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Performs the rename key operation.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is rename key.</returns>
        public Task<ResourceAdminResult> RenameKeyAsync(string resourceSet, string oldName, string newName, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Deletes resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ResourceAdminResult> DeleteResourceSetAsync(string resourceSet, string culture, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Performs the rename resource set operation.
        /// </summary>
        /// <param name="oldResourceSet">The old resource set.</param>
        /// <param name="newResourceSet">The new resource set.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is rename resource set.</returns>
        public Task<ResourceAdminResult> RenameResourceSetAsync(string oldResourceSet, string newResourceSet, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Gets note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<string?> GetNoteAsync(string resourceSet, string name, CancellationToken cancellationToken = default) => Read<string?>();

        /// <summary>
        /// Gets notes.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is notes.</returns>
        public Task<IReadOnlyDictionary<string, string>> GetNotesAsync(string resourceSet, CancellationToken cancellationToken = default) => Read<IReadOnlyDictionary<string, string>>();

        /// <summary>
        /// Sets note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="note">The note.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<ResourceAdminResult> SetNoteAsync(string resourceSet, string name, string note, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Deletes note.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is note.</returns>
        public Task<ResourceAdminResult> DeleteNoteAsync(string resourceSet, string name, ResourceAdminOptions? options = null, CancellationToken cancellationToken = default) => Write<ResourceAdminResult>();

        /// <summary>
        /// Performs the read operation.
        /// </summary>
        /// <typeparam name="T">The type of t.</typeparam>
        /// <returns>A task whose result is read.</returns>
        private Task<T> Read<T>() {
            ReadsAttempted = true;
            throw new InvalidOperationException("the read-only store must be rejected before any read");
        }

        /// <summary>
        /// Performs the write operation.
        /// </summary>
        /// <typeparam name="T">The type of t.</typeparam>
        /// <returns>A task whose result is write.</returns>
        private static Task<T> Write<T>() =>
            throw new StoreReadOnlyException("Store 'DisabledAdminStore' does not support resource administration and rejects the governed write path.");
    }
}
