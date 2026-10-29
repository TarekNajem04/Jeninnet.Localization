//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Translator notes on the EF/SQLite store: the governed admin surface
/// (create/update/delete, conflicts, dry-run, validation), key-lifecycle cascades
/// (rename, last-row deletion, set delete/rename — no orphan notes), accountability
/// (version bump, redacted hash-chained audit records with empty culture, no journal
/// rows, tamper verification), and read semantics. Each test owns an isolated
/// in-memory SQLite database.
/// </summary>
[TestClass]
public sealed class MessageNoteTests {
    private const string ACTOR = "message-note-suite";

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
    /// <returns>The options result.</returns>
    private static ResourceAdminOptions Options() => new() { Actor = ACTOR };

    // ------------------------------------------------------------- direct surface

    /// <summary>
    /// Verifies that set note creates and reads back through the admin surface.
    /// </summary>
    [TestMethod]
    public async Task SetNote_CreatesAndReadsBack_ThroughTheAdminSurfaceAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.SetNoteAsync("SetA", "Greeting", "Friendly tone; shown on the home page.", Options(), Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.SetNote);
        result.Applied.Should().BeTrue();
        result.DryRun.Should().BeFalse();
        result.AffectedEntries.Should().Be(1);
        result.Conflict.Should().BeNull();

        (await fixture.Admin.GetNoteAsync("SetA", "Greeting", Cancellation)).Should().Be("Friendly tone; shown on the home page.");
        var notes = await fixture.Admin.GetNotesAsync("SetA", Cancellation);
        notes.ContainsKey("Greeting").Should().BeTrue();
        notes["Greeting"].Should().Be("Friendly tone; shown on the home page.");

        fixture.Audits.Count.Should().Be(1);
        fixture.Audits[0].Operation.Should().Be(ResourceAdminOperation.SetNote);
        fixture.Audits[0].Actor.Should().Be(ACTOR);
        fixture.Audits[0].ResourceSet.Should().Be("SetA");
        fixture.Audits[0].Culture.Should().Be(string.Empty, "notes are culture-agnostic");
        fixture.Audits[0].Key.Should().Be("Greeting");
        fixture.Audits[0].OldValue.Should().BeNull();
        fixture.Audits[0].NewValue.Should().Be("Friendly tone; shown on the home page.");

        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);
        history.Where(static record => record.Operation == ResourceAdminOperation.SetNote).Should().ContainSingle();
        var record = history.First(static record => record.Operation == ResourceAdminOperation.SetNote);
        record.Culture.Should().Be(string.Empty);
        record.NewValueHash.Should().Be(ResourceValueHash.Compute("Friendly tone; shown on the home page."));
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(2, "the note mutation is a governed batch");
        (await fixture.Store.VerifyHistoryAsync(Cancellation)).IsValid.Should().BeTrue("the chain covers note records");
    }

    /// <summary>
    /// Verifies that set note updates an existing note audits both values.
    /// </summary>
    [TestMethod]
    public async Task SetNote_UpdatesAnExistingNote_AuditsBothValuesAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "Greeting", "First.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.SetNoteAsync("SetA", "Greeting", "Second.", Options(), Cancellation);

        result.Applied.Should().BeTrue();
        (await fixture.Admin.GetNoteAsync("SetA", "Greeting", Cancellation)).Should().Be("Second.");
        fixture.Audits.Count.Should().Be(1);
        fixture.Audits[0].OldValue.Should().Be("First.");
        fixture.Audits[0].NewValue.Should().Be("Second.");
    }

    /// <summary>
    /// Verifies that set note empty text is AValid stored value.
    /// </summary>
    [TestMethod]
    public async Task SetNote_EmptyText_IsAValidStoredValueAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);

        var result = await fixture.Admin.SetNoteAsync("SetA", "Greeting", string.Empty, null, Cancellation);

        result.Applied.Should().BeTrue();
        (await fixture.Admin.GetNoteAsync("SetA", "Greeting", Cancellation)).Should().Be(string.Empty);
    }

    /// <summary>
    /// Verifies that set note missing key reports not found and changes nothing.
    /// </summary>
    [TestMethod]
    public async Task SetNote_MissingKey_ReportsNotFound_AndChangesNothingAsync() {
        await using var fixture = new SqliteNoteFixture();

        var result = await fixture.Admin.SetNoteAsync("SetA", "Missing", "Hint.", Options(), Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.SetNote);
        result.Applied.Should().BeFalse();
        result.AffectedEntries.Should().Be(0);
        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);
        result.Conflict.Culture.Should().BeNull("notes span cultures");
        result.Conflict.Key.Should().Be("Missing");
        (await fixture.Admin.GetNoteAsync("SetA", "Missing", Cancellation)).Should().BeNull("no orphan notes");
        fixture.Audits.Should().BeEmpty();
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(0);
    }

    /// <summary>
    /// Verifies that set note dry run previews without changing anything.
    /// </summary>
    [TestMethod]
    public async Task SetNote_DryRun_PreviewsWithoutChangingAnythingAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.SetNoteAsync("SetA", "Greeting", "Hint.", new ResourceAdminOptions { DryRun = true, Actor = ACTOR }, Cancellation);

        result.DryRun.Should().BeTrue();
        result.Applied.Should().BeFalse();
        result.AffectedEntries.Should().Be(1, "the preview reports the row it would touch");
        (await fixture.Admin.GetNoteAsync("SetA", "Greeting", Cancellation)).Should().BeNull();
        fixture.Audits.Should().BeEmpty();
        (await fixture.Store.GetCurrentVersionAsync(Cancellation)).Should().Be(1, "dry-run changes nothing");
    }

    /// <summary>
    /// Verifies that delete note removes and audits and reports not found when absent.
    /// </summary>
    [TestMethod]
    public async Task DeleteNote_RemovesAndAudits_AndReportsNotFoundWhenAbsentAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "Greeting", "Hint.", null, Cancellation);
        fixture.Audits.Clear();

        var missing = await fixture.Admin.DeleteNoteAsync("SetA", "Other", null, Cancellation);
        missing.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.NotFound);

        var result = await fixture.Admin.DeleteNoteAsync("SetA", "Greeting", Options(), Cancellation);

        result.Operation.Should().Be(ResourceAdminOperation.DeleteNote);
        result.Applied.Should().BeTrue();
        (await fixture.Admin.GetNoteAsync("SetA", "Greeting", Cancellation)).Should().BeNull();
        fixture.Audits.Count.Should().Be(1);
        fixture.Audits[0].Operation.Should().Be(ResourceAdminOperation.DeleteNote);
        fixture.Audits[0].OldValue.Should().Be("Hint.");
        fixture.Audits[0].NewValue.Should().BeNull();
    }

    /// <summary>
    /// Verifies that notes validate arguments.
    /// </summary>
    [TestMethod]
    public async Task Notes_ValidateArgumentsAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "Greeting", "Hello", null, Cancellation);

        Func<Task> nullSet = () => fixture.Admin.SetNoteAsync(null!, "Greeting", "Hint.", null, Cancellation);
        Func<Task> blankKey = () => fixture.Admin.SetNoteAsync("SetA", "  ", "Hint.", null, Cancellation);
        Func<Task> nullNote = () => fixture.Admin.SetNoteAsync("SetA", "Greeting", null!, null, Cancellation);
        Func<Task> readBlankSet = () => fixture.Admin.GetNotesAsync(" ", Cancellation);

        await nullSet.Should().ThrowAsync<ArgumentNullException>();
        await blankKey.Should().ThrowAsync<ArgumentException>();
        await nullNote.Should().ThrowAsync<ArgumentNullException>();
        await readBlankSet.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies that get note missing key returns null without conflicts.
    /// </summary>
    [TestMethod]
    public async Task GetNote_MissingKey_ReturnsNull_WithoutConflictsAsync() {
        await using var fixture = new SqliteNoteFixture();

        (await fixture.Admin.GetNoteAsync("SetA", "Absent", Cancellation)).Should().BeNull();
        (await fixture.Admin.GetNotesAsync("SetA", Cancellation)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ lifecycle

    /// <summary>
    /// Verifies that rename key moves the note atomically in the same batch.
    /// </summary>
    [TestMethod]
    public async Task RenameKey_MovesTheNoteAtomically_InTheSameBatchAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "ar", "A", "A-ar", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Rename me.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameKeyAsync("SetA", "A", "B", Options(), Cancellation);

        result.Applied.Should().BeTrue();
        result.AffectedEntries.Should().Be(3, "two culture rows plus the note row");
        (await fixture.Admin.GetNoteAsync("SetA", "B", Cancellation)).Should().Be("Rename me.");
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().BeNull("no orphan note at the old key");

        fixture.Audits.Count.Should().Be(3);
        var noteAudit = fixture.Audits[2];
        noteAudit.Operation.Should().Be(ResourceAdminOperation.RenameKey);
        noteAudit.Culture.Should().Be(string.Empty);
        noteAudit.Key.Should().Be("A");
        noteAudit.NewKey.Should().Be("B");

        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);
        var renameRecords = history.Where(static record => record.Operation == ResourceAdminOperation.RenameKey).ToList();
        renameRecords.Should().HaveCount(3);
        renameRecords.Select(static record => record.Version).Distinct().Should().ContainSingle();
        renameRecords.Count(static record => record.Culture.Length == 0).Should().Be(1);
    }

    /// <summary>
    /// Verifies that rename key target conflict leaves the note is untouched.
    /// </summary>
    [TestMethod]
    public async Task RenameKey_TargetConflict_LeavesTheNoteUntouchedAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "B", "B1", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Note A.", null, Cancellation);

        var result = await fixture.Admin.RenameKeyAsync("SetA", "A", "B", null, Cancellation);

        result.Conflict!.Kind.Should().Be(ResourceAdminConflictKind.TargetExists);
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().Be("Note A.");
        (await fixture.Admin.GetNoteAsync("SetA", "B", Cancellation)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that delete entry last culture row removes the note.
    /// </summary>
    [TestMethod]
    public async Task DeleteEntry_LastCultureRowRemovesTheNoteAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Doomed.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.DeleteEntryAsync("SetA", "en-US", "A", Options(), Cancellation);

        result.AffectedEntries.Should().Be(2, "the row plus its orphaned note");
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().BeNull();
        fixture.Audits.Count.Should().Be(2);
        fixture.Audits[1].Operation.Should().Be(ResourceAdminOperation.DeleteEntry);
        fixture.Audits[1].Culture.Should().Be(string.Empty);
        fixture.Audits[1].OldValue.Should().Be("Doomed.");
    }

    /// <summary>
    /// Verifies that delete entry other culture keeps the note.
    /// </summary>
    [TestMethod]
    public async Task DeleteEntry_OtherCultureKeepsTheNoteAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "ar", "A", "A-ar", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Survivor.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.DeleteEntryAsync("SetA", "en-US", "A", Options(), Cancellation);

        result.AffectedEntries.Should().Be(1, "the key still exists in another culture");
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().Be("Survivor.");
        fixture.Audits.Count.Should().Be(1);
    }

    /// <summary>
    /// Verifies that delete resource set removes only orphaned notes.
    /// </summary>
    [TestMethod]
    public async Task DeleteResourceSet_RemovesOnlyOrphanedNotesAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "ar", "A", "A-ar", null, Cancellation);
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "B", "B1", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Keeps ar.", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "B", "Orphaned.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.DeleteResourceSetAsync("SetA", "en-US", Options(), Cancellation);

        result.AffectedEntries.Should().Be(3, "two en-US rows plus the orphaned note of B");
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().Be("Keeps ar.");
        (await fixture.Admin.GetNoteAsync("SetA", "B", Cancellation)).Should().BeNull();
        fixture.Audits.Count.Should().Be(3);
        fixture.Audits[2].Culture.Should().Be(string.Empty);
        fixture.Audits[2].Key.Should().Be("B");
    }

    /// <summary>
    /// Verifies that rename resource set moves the notes.
    /// </summary>
    [TestMethod]
    public async Task RenameResourceSet_MovesTheNotesAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Moving.", Options(), Cancellation);
        fixture.Audits.Clear();

        var result = await fixture.Admin.RenameResourceSetAsync("SetA", "SetB", Options(), Cancellation);

        result.AffectedEntries.Should().Be(2, "the row plus its note");
        (await fixture.Admin.GetNoteAsync("SetB", "A", Cancellation)).Should().Be("Moving.");
        (await fixture.Admin.GetNoteAsync("SetA", "A", Cancellation)).Should().BeNull();
        fixture.Audits.Count.Should().Be(2);
        fixture.Audits[1].Operation.Should().Be(ResourceAdminOperation.RenameResourceSet);
        fixture.Audits[1].ResourceSet.Should().Be("SetB", "the note record carries the new set name");
        fixture.Audits[1].Culture.Should().Be(string.Empty);
    }

    // -------------------------------------------------------------- accountability

    /// <summary>
    /// Verifies that note mutation does not alter as of string reads.
    /// </summary>
    [TestMethod]
    public async Task NoteMutation_DoesNotAlterAsOfStringReadsAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", Options(), Cancellation);
        await fixture.Admin.UpdateEntryAsync("SetA", "en-US", "A", "A2", Options(), Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "Metadata only.", Options(), Cancellation);

        var asOfTwo = await fixture.Store.GetStringsAsOfVersionAsync("SetA", "en-US", 2, Cancellation);
        var current = await fixture.Store.GetStringsAsOfVersionAsync("SetA", "en-US", 3, Cancellation);

        asOfTwo["A"].Value.Should().Be("A2", "the note batch adds no journal rows");
        current.Keys.Should().BeEquivalentTo("A");
        current["A"].Value.Should().Be("A2");
    }

    /// <summary>
    /// Verifies that note text never lands in audit storage in plain text.
    /// </summary>
    [TestMethod]
    public async Task NoteText_NeverLandsInAuditStorageInPlainTextAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        const string secret = "translator-hint-never-plain";
        await fixture.Admin.SetNoteAsync("SetA", "A", secret, Options(), Cancellation);

        await using var dbContext = fixture.CreateContext();
        var records = await dbContext.AuditRecords.ToListAsync(Cancellation);

        records.Any(static record => record.OldValueHash == secret || record.NewValueHash == secret).Should().BeFalse();
        var noteRecord = records.First(static record => record.Operation == ResourceAdminOperation.SetNote);
        noteRecord.NewValueHash.Should().Be(ResourceValueHash.Compute(secret), "the audit trail carries the redacted hash only");
    }

    /// <summary>
    /// Verifies that verify history stays valid across the note lifecycle.
    /// </summary>
    [TestMethod]
    public async Task VerifyHistory_StaysValid_AcrossTheNoteLifecycleAsync() {
        await using var fixture = new SqliteNoteFixture();
        await fixture.Admin.CreateEntryAsync("SetA", "en-US", "A", "A1", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "A", "One.", null, Cancellation);
        await fixture.Admin.RenameKeyAsync("SetA", "A", "B", null, Cancellation);
        await fixture.Admin.SetNoteAsync("SetA", "B", "Two.", null, Cancellation);
        await fixture.Admin.DeleteNoteAsync("SetA", "B", null, Cancellation);
        await fixture.Admin.DeleteEntryAsync("SetA", "en-US", "B", null, Cancellation);

        var verification = await fixture.Store.VerifyHistoryAsync(Cancellation);

        verification.IsValid.Should().BeTrue("every note record participates in the chain");
        var history = await fixture.Store.GetHistoryAsync(cancellationToken: Cancellation);
        history.Select(static record => record.Version).Distinct().Should().HaveCount(6, "each governed mutation batch bumps the version");
    }

    /// <summary>
    /// Provides test infrastructure for sqlite note.
    /// </summary>
    private sealed class SqliteNoteFixture : IAsyncDisposable {
        private readonly SqliteConnection _connection;

        /// <summary>
        /// Initializes a new instance of the SqliteNoteFixture class.
        /// </summary>
        public SqliteNoteFixture() {
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
}
