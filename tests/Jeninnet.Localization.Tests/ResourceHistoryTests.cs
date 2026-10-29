//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Persisted audit trail and versioned resource sets on the EF/SQLite store:
/// trail completeness and ordering, monotonic batch versions, replay-faithful
/// as-of-version reads, the documented redaction rule (no template text in audit
/// storage), hash-chain tamper detection, typed capability reporting, decorator
/// forwarding, and cancellation. Each test owns an isolated in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class ResourceHistoryTests {
    private const string ACTOR = "resource-history-suite";

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Verifies that trail completeness every governed mutation leaves ordered records.
    /// </summary>
    [TestMethod]
    public async Task TrailCompleteness_EveryGovernedMutation_LeavesOrderedRecordsAsync() {
        await using var fixture = new SqliteHistoryFixture();

        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "B", "B1", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A2", Options(), Cancellation);
        await fixture.Admin.DeleteEntryAsync("SetA", "en-US", "B", Options(), Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "nl-NL", "A", "A-nl", Options(), Cancellation);
        await fixture.Admin.RenameKeyAsync("SetA", "A", "A2", Options(), Cancellation);
        await fixture.Admin.DeleteResourceSetAsync("SetA", "en-US", Options(), Cancellation);
        await fixture.Admin.RenameResourceSetAsync("SetA", "SetC", Options(), Cancellation);

        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);

        history.Count.Should().Be(9, "every applied mutation contributes one record per mutated row");
        history.Select(static record => record.Sequence).Should().BeInAscendingOrder("the trail is ordered by append sequence");
        history.Select(static record => record.Version).Should().BeInAscendingOrder("versions are monotonic across batches");
        history.Select(static record => record.Version).Distinct().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);

        history.Where(static record => record.Version == 6).Should().HaveCount(2, "the key rename touched two cultures and shares one version");
        history.Where(static record => record.Version == 6).Select(static record => record.NewKey!).Should().AllBe("A2");

        history[5].Operation.Should().Be(ResourceAdminOperation.RenameKey);
        history[5].Actor.Should().Be(ACTOR);
        history[5].OldValueHash.Should().Be(ResourceValueHash.Compute("A2"), "the rename record redacts the source value");
        history[8].Operation.Should().Be(ResourceAdminOperation.RenameResourceSet);
        history[8].ResourceSet.Should().Be("SetC", "set-rename records carry the new set name");

        var captured = fixture.Audits;
        captured.Count.Should().Be(history.Count, "the persisted trail matches the raised audit hook one-to-one");
        for(var index = 0; index < captured.Count; index++) {
            history[index].Operation.Should().Be(captured[index].Operation);
            history[index].Actor.Should().Be(captured[index].Actor);
            history[index].ResourceSet.Should().Be(captured[index].ResourceSet);
            history[index].Culture.Should().Be(captured[index].Culture);
            history[index].Key.Should().Be(captured[index].Key);
            history[index].NewKey.Should().Be(captured[index].NewKey);
            history[index].EntryId.Should().Be(captured[index].EntryId);
        }

        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(8);
    }

    /// <summary>
    /// Verifies that trail reads filter exclusively by version.
    /// </summary>
    [TestMethod]
    public async Task TrailReads_FilterExclusivelyByVersionAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A2", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A3", Options(), Cancellation);

        (await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation)).Should().HaveCount(3);
        (await fixture.Store.GetHistoryAsync(0, Cancellation)).Should().HaveCount(3);
        var afterFirst = await fixture.Store.GetHistoryAsync(1, Cancellation);
        afterFirst.Should().HaveCount(2);
        afterFirst.Should().OnlyContain(static record => record.Version > 1);
        (await fixture.Store.GetHistoryAsync(3, Cancellation)).Should().BeEmpty("the bound is exclusive");
    }

    /// <summary>
    /// Verifies that as of reads replay the catalog exactly.
    /// </summary>
    [TestMethod]
    public async Task AsOfReads_ReplayTheCatalogExactlyAsync() {
        await using var fixture = new SqliteHistoryFixture();

        var coordinates = new (string Set, string Culture)[] { ("SetA", "en-US"), ("SetA", "nl-NL"), ("SetB", "en-US"), ("SetB", "nl-NL") };
        var live = coordinates.ToDictionary(static coordinate => coordinate, static _ => new Dictionary<string, string>(StringComparer.Ordinal));
        var snapshots = new List<Dictionary<(string Set, string Culture), Dictionary<string, string>>>();

        void Snapshot() => snapshots.Add(live.ToDictionary(static pair => pair.Key, static pair => new Dictionary<string, string>(pair.Value, StringComparer.Ordinal)));

        Snapshot();

        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", Options(), Cancellation);
        live[("SetA", "en-US")]["Greeting"] = "Hello";
        Snapshot();

        await fixture.Admin.CreateEntryAsync("SetA", "nl-NL", "Greeting", "Hallo", Options(), Cancellation);
        live[("SetA", "nl-NL")]["Greeting"] = "Hallo";
        Snapshot();

        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "Greeting", "Hi", Options(), Cancellation);
        live[("SetA", "en-US")]["Greeting"] = "Hi";
        Snapshot();

        await fixture.Admin.DeleteEntryAsync("SetA", "nl-NL", "Greeting", Options(), Cancellation);
        live[("SetA", "nl-NL")].Remove("Greeting");
        Snapshot();

        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Farewell", "Bye", Options(), Cancellation);
        live[("SetA", "en-US")]["Farewell"] = "Bye";
        Snapshot();

        await fixture.Admin.RenameKeyAsync("SetA", "Greeting", "Welcome", Options(), Cancellation);
        var greetingValue = live[("SetA", "en-US")]["Greeting"];
        live[("SetA", "en-US")].Remove("Greeting");
        live[("SetA", "en-US")]["Welcome"] = greetingValue;
        Snapshot();

        var renamedId = (await fixture.Store.GetStringAsync("SetA", "en-US", "Welcome", Cancellation)).Id;

        await fixture.Admin.RenameResourceSetAsync("SetA", "SetB", Options(), Cancellation);
        foreach(var culture in new[] { "en-US", "nl-NL" }) {
            foreach(var pair in live[("SetA", culture)]) {
                live[("SetB", culture)][pair.Key] = pair.Value;
            }

            live[("SetA", culture)].Clear();
        }

        Snapshot();

        await fixture.Admin.DeleteResourceSetAsync("SetB", "en-US", Options(), Cancellation);
        live[("SetB", "en-US")].Clear();
        Snapshot();

        var currentVersion = await fixture.Store.GetCurrentVersionAsync(Cancellation);
        currentVersion.Should().Be(8);
        snapshots.Count.Should().Be((int)currentVersion + 1, "one snapshot per version including the pre-history zero");

        for(var version = 0; version <= currentVersion; version++) {
            foreach(var coordinate in coordinates) {
                var asOf = await fixture.Store.GetStringsAsOfVersionAsync(coordinate.Set, coordinate.Culture, version, Cancellation);
                var asOfValues = asOf.ToDictionary(static pair => pair.Key, static pair => pair.Value.Value, StringComparer.Ordinal);
                asOfValues.Should().Equal(
                    snapshots[version][coordinate],
                    $"(set '{coordinate.Set}', culture '{coordinate.Culture}') must replay exactly at version {version}");
            }
        }

        (await fixture.Store.GetStringsAsOfVersionAsync("SetA", "en-US", 0, Cancellation)).Should().BeEmpty("version 0 is the empty pre-history state");

        var welcomeAtSeven = await fixture.Store.GetStringsAsOfVersionAsync("SetB", "en-US", 7, Cancellation);
        welcomeAtSeven["Welcome"].Id.Should().Be(renamedId, "as-of reads preserve persisted identities across renames");

        var liveNow = await fixture.Store.GetStringsAsOfVersionAsync("SetB", "nl-NL", currentVersion, Cancellation);
        liveNow.Should().Equal(await fixture.Store.GetAllStringsAsync("SetB", "nl-NL", Cancellation),
            "as of the current version the replay equals the live catalog");
    }

    /// <summary>
    /// Verifies that redaction audit storage never contains template text.
    /// </summary>
    [TestMethod]
    public async Task Redaction_AuditStorageNeverContainsTemplateTextAsync() {
        await using var fixture = new SqliteHistoryFixture();
        const string secret = "super-secret-token-AKIA1234567890";

        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Token", "public-value", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "Token", secret, Options(), Cancellation);

        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);
        history.Should().HaveCount(2);
        history[1].OldValueHash.Should().Be(ResourceValueHash.Compute("public-value"));
        history[1].NewValueHash.Should().Be(ResourceValueHash.Compute(secret));
        history[1].OldValueHash.Should().NotBe("public-value");
        history[1].NewValueHash.Should().NotBe(secret);

        await using var context = fixture.CreateContext();
        var rawRecords = await context.AuditRecords.OrderBy(static record => record.Sequence).ToListAsync(Cancellation);
        rawRecords.Should().HaveCount(2);

        foreach(var record in rawRecords) {
            foreach(var field in new[] { record.Actor, record.ResourceSet, record.Culture, record.Key, record.NewKey, record.OldValueHash, record.NewValueHash, record.PreviousHash, record.ChainHash }) {
                (field ?? string.Empty).Should().NotContain(secret, "secret-bearing values must never land in audit storage in plain text");
                (field ?? string.Empty).Should().NotContain("public-value", "no template text lands in audit storage, secret or not");
            }
        }
    }

    /// <summary>
    /// Verifies that tamper detection untouched trail verifies and modifications are is detected.
    /// </summary>
    [TestMethod]
    public async Task TamperDetection_UntouchedTrailVerifiesAndModificationsAreDetectedAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "B", "B1", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A2", Options(), Cancellation);

        var valid = await fixture.Store.VerifyHistoryAsync(Cancellation);
        valid.IsValid.Should().BeTrue();
        valid.VerifiedRecords.Should().Be(3);
        valid.FirstInvalidSequence.Should().BeNull();

        long tamperedSequence;
        await using(var context = fixture.CreateContext()) {
            var middle = await context.AuditRecords.OrderBy(static record => record.Sequence).Skip(1).FirstAsync(Cancellation);
            tamperedSequence = middle.Sequence;
            middle.Actor = "tampered-actor";
            await context.SaveChangesAsync(Cancellation);
        }

        var afterFieldTamper = await fixture.Store.VerifyHistoryAsync(Cancellation);
        afterFieldTamper.IsValid.Should().BeFalse("modifying a persisted record breaks the hash chain");
        afterFieldTamper.FirstInvalidSequence.Should().Be(tamperedSequence);
        afterFieldTamper.VerifiedRecords.Should().Be(1);
    }

    /// <summary>
    /// Verifies that tamper detection record removal is detected.
    /// </summary>
    [TestMethod]
    public async Task TamperDetection_RecordRemovalIsDetectedAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "B", "B1", Options(), Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "C", "C1", Options(), Cancellation);

        long nextSequence;
        await using(var context = fixture.CreateContext()) {
            var middle = await context.AuditRecords.OrderBy(static record => record.Sequence).Skip(1).FirstAsync(Cancellation);
            nextSequence = await context.AuditRecords.OrderBy(static record => record.Sequence).Skip(2).Select(static record => record.Sequence).FirstAsync(Cancellation);
            context.AuditRecords.Remove(middle);
            await context.SaveChangesAsync(Cancellation);
        }

        var verification = await fixture.Store.VerifyHistoryAsync(Cancellation);
        verification.IsValid.Should().BeFalse("removing a record breaks the chain linkage of its successor");
        verification.FirstInvalidSequence.Should().Be(nextSequence);
        verification.VerifiedRecords.Should().Be(1);
    }

    /// <summary>
    /// Verifies that capability read only store reports absent and rejects is typed.
    /// </summary>
    [TestMethod]
    public async Task Capability_ReadOnlyStore_ReportsAbsentAndRejectsTypedAsync() {
        ILocalizationStore readOnly = new ReadOnlyLocalizationStore();

        readOnly.SupportsHistory().Should().BeFalse();
        ((Func<IResourceHistoryStore>)(() => readOnly.RequireHistory())).Should().Throw<StoreHistoryUnsupportedException>();

        var decorated = new CachedLocalizationStoreDecorator(readOnly, new MemoryCache(new MemoryCacheOptions()));
        decorated.SupportsHistory.Should().BeFalse("the decorator honestly reports the inner history-less store");

        Func<Task> act = () => decorated.GetHistoryAsync(cancellationToken: Cancellation);
        (await act.Should().ThrowAsync<StoreHistoryUnsupportedException>()).Which.Message.Should().Contain("does not support resource history");
    }

    /// <summary>
    /// Verifies that capability history store reports present and decorator forwards.
    /// </summary>
    [TestMethod]
    public async Task Capability_HistoryStore_ReportsPresentAndDecoratorForwardsAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A2", Options(), Cancellation);

        ((ILocalizationStore)fixture.Store).SupportsHistory().Should().BeTrue();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorated = new CachedLocalizationStoreDecorator(fixture.Store, cache);
        decorated.SupportsHistory.Should().BeTrue();

        (await decorated.GetCurrentVersionAsync(Cancellation)).Should().Be(2);
        (await decorated.GetHistoryAsync(cancellationToken: Cancellation)).Should().HaveCount(2, "history reads forward live, never cached");
        (await decorated.GetStringsAsOfVersionAsync("SetA", "en-US", 1, Cancellation))["A"].Value.Should().Be("A1");
        (await decorated.VerifyHistoryAsync(Cancellation)).IsValid.Should().BeTrue();

        var required = ((ILocalizationStore)decorated).RequireHistory();
        required.Should().BeSameAs(decorated, "the decorator itself honestly fronts the inner history capability");
    }

    /// <summary>
    /// Verifies that cancellation pre canceled history reads reject.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_PreCanceled_HistoryReadsRejectAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        Func<Task> currentVersion = () => fixture.Store.GetCurrentVersionAsync(source.Token);
        await currentVersion.Should().ThrowAsync<OperationCanceledException>();
        Func<Task> history = () => fixture.Store.GetHistoryAsync(cancellationToken: source.Token);
        await history.Should().ThrowAsync<OperationCanceledException>();
        Func<Task> asOf = () => fixture.Store.GetStringsAsOfVersionAsync("SetA", "en-US", 1, source.Token);
        await asOf.Should().ThrowAsync<OperationCanceledException>();
        Func<Task> verify = () => fixture.Store.VerifyHistoryAsync(source.Token);
        await verify.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Verifies that dry run and conflicts leave no trail and no version.
    /// </summary>
    [TestMethod]
    public async Task DryRunAndConflicts_LeaveNoTrailAndNoVersionAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", new ResourceAdminOptions { DryRun = true, Actor = ACTOR }, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        var conflict = await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "Missing", "X", Options(), Cancellation);

        conflict.Applied.Should().BeFalse();
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(1, "only the applied batch advanced the version");
        (await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation)).Should().HaveCount(1, "dry-runs and conflicts leave no trail");
    }

    /// <summary>
    /// Verifies that raw resource set crud is not is versioned.
    /// </summary>
    [TestMethod]
    public async Task RawResourceSetCrud_IsNotVersionedAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);

        var resourceSet = (await fixture.Store.GetResourceSetAsync("SetA", "en-US", Cancellation))!;
        await resourceSet.AddAsync(new LocalizedStringEntry(Guid.NewGuid(), "SetA", "en-US", "Raw", "Raw-Value"), Cancellation);

        (await fixture.Store.GetStringAsync("SetA", "en-US", "Raw", Cancellation)).Value.Should().Be("Raw-Value");
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(1, "raw lifecycle writes are outside the governed accountability model");
        (await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation)).Should().HaveCount(1);
        (await fixture.Store.GetStringsAsOfVersionAsync("SetA", "en-US", 1, Cancellation)).Should().NotContainKey("Raw",
            "as-of reads replay governed mutations only");
    }

    /// <summary>
    /// Verifies that concurrent mutations assign distinct monotonic versions.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentMutations_AssignDistinctMonotonicVersionsAsync() {
        await using var fixture = new SqliteHistoryFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Key", "v0", Options(), Cancellation);

        var tasks = Enumerable.Range(1, 8)
            .Select(index => fixture.Admin.UpdateEntryAsync("SetA", "en-US", "Key", $"v{index}", Options(), Cancellation))
            .ToList();
        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(static result => result.Applied);
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(9, "each applied batch takes exactly the next version");
        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);
        history.Should().HaveCount(9);
        history.Select(static record => record.Version).Distinct().Should().HaveCount(9, "no two batches share a version");
    }

    /// <summary>
    /// Performs the options operation.
    /// </summary>
    /// <returns>The options result.</returns>
    private static ResourceAdminOptions Options() => new() { Actor = ACTOR };

    /// <summary>
    /// Provides test infrastructure for sqlite history.
    /// </summary>
    private sealed class SqliteHistoryFixture : IAsyncDisposable {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// Initializes a new instance of the SqliteHistoryFixture class.
        /// </summary>
        public SqliteHistoryFixture() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<LocalizationDataContext>()
                .UseSqlite(_connection)
                .Options;
            using(var context = new LocalizationDataContext(options)) {
                context.Database.EnsureCreated();
            }

            Factory = new InlineDbContextFactory(options);
            Store = new SqlDbLocalizationStore(Factory);
            Store.Audited += (_, entry) => {
                lock(Audits) {
                    Audits.Add(entry);
                }
            };
        }

        /// <summary>
        /// Gets the factory.
        /// </summary>
        public InlineDbContextFactory Factory { get; }

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
        /// Creates context.
        /// </summary>
        /// <returns>The context result.</returns>
        public LocalizationDataContext CreateContext() => Factory.CreateDbContext();

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
