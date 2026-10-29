//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// NEW-01 — genuine EF Core runtime verification for the SQL Server-backed localization store.
/// Requires a connection string through <c>JENINNET_TEST_SQLSERVER_CONNECTION</c>; when the
/// variable is unset or the engine is unreachable the test is inconclusive. The test exercises the
/// real registration path (<c>AddEntityFrameworkLocalizationStore</c>), the real context factory,
/// real EF queries against the provided test database, an independent raw-SQL mutation path proving
/// the reads are live/stateless, and the documented no-op <c>ReloadAsync</c> semantics.
/// </summary>
[TestClass]
[DoNotParallelize]
public class EntityFrameworkCoreRuntimeTests {
    private const string SQLSERVER_CONNECTION_ENV = "JENINNET_TEST_SQLSERVER_CONNECTION";

    private static readonly string[] _greetingCandidates = ["Hello", "Other-Hello"];
    private static readonly string[] _ownerCandidates = ["Alpha", "Beta"];

    /// <summary>
    /// Verifies that ef core store runtime lookup against real sql server.
    /// </summary>
    [TestMethod]
    public async Task EfCoreStore_RuntimeLookup_AgainstRealSqlServerAsync() {
        var connectionString = RequireEnvironment();

        // The store schema is created idempotently inside the provided test database via
        // EnsureCreatedAsync, so the R29 runtime path runs under the same minimal permissions
        // as the production user (no admin or dedicated-database requirements). Because
        // EnsureCreated treats ANY existing table as "schema already present", placeholder
        // tables from the sibling provider runtime test are dropped first so the EF schema
        // creation is deterministic regardless of test ordering; the login is db_owner of the
        // provided test database.
        await using(var setup = new SqlConnection(connectionString)) {
            await setup.OpenAsync(TestContext.CancellationToken);
            await ExecuteAsync(setup, """
                IF OBJECT_ID(N'dbo.LocalizedStrings', N'U') IS NOT NULL DROP TABLE dbo.LocalizedStrings;
                IF OBJECT_ID(N'dbo.LocalizationMessages', N'U') IS NOT NULL DROP TABLE dbo.LocalizationMessages;
                IF OBJECT_ID(N'dbo.AuditRecords', N'U') IS NOT NULL DROP TABLE dbo.AuditRecords;
                IF OBJECT_ID(N'dbo.StringVersions', N'U') IS NOT NULL DROP TABLE dbo.StringVersions;
                IF OBJECT_ID(N'dbo.CatalogState', N'U') IS NOT NULL DROP TABLE dbo.CatalogState;
                    IF OBJECT_ID(N'dbo.StringNotes', N'U') IS NOT NULL DROP TABLE dbo.StringNotes;
                """, TestContext.CancellationToken);
        }

        var services = new ServiceCollection();
        services.AddEntityFrameworkLocalizationStore(connectionString);
        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>();
        await using(var context = await factory.CreateDbContextAsync(TestContext.CancellationToken)) {
            await context.Database.EnsureCreatedAsync(TestContext.CancellationToken);
            context.LocalizedStrings.RemoveRange(await context.LocalizedStrings.ToListAsync(TestContext.CancellationToken));
            context.LocalizedStrings.AddRange(
                new LocalizedStringEntry(Guid.NewGuid(), "SampleModel", "en-US", "Greeting", "Hello"),
                new LocalizedStringEntry(Guid.NewGuid(), "SampleModel", "nl-NL", "Greeting", "Hallo"),
                new LocalizedStringEntry(Guid.NewGuid(), "SampleModel", "en-US", "Required.Message", "Required!"));
            await context.SaveChangesAsync(TestContext.CancellationToken);
        }

        var store = provider.GetRequiredService<ILocalizationStore>();
        ReferenceEquals(store, provider.GetRequiredService<ILocalizationStore>()).Should().BeTrue(
            "the store is a container-owned singleton using the context factory lifetime");

        // Culture-sensitive exact lookup through the real store.
        (await store.GetStringAsync("SampleModel", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");
        (await store.GetStringAsync("SampleModel", "nl-NL", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Hallo");
        (await store.GetStringAsync("SampleModel", "en-US", "Missing", TestContext.CancellationToken)).IsMissing().Should().BeTrue(
            "a missing resource surfaces the canonical missing entry");

        // Resource-set path.
        var resourceSet = await store.GetResourceSetAsync("SampleModel", "en-US", TestContext.CancellationToken);
        resourceSet.Should().NotBeNull("the seeded resource set must resolve");
        var resolvedResourceSet = resourceSet ?? throw new InvalidOperationException("the seeded resource set must resolve");
        (await resolvedResourceSet.GetStringAsync("Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");
        (await store.GetResourceSetAsync("SampleModel", "de-DE", TestContext.CancellationToken)).Should().BeNull(
            "a resource set with no persisted entries does not exist");

        string[] expectedCultures = ["en-US", "nl-NL"];
        (await store.GetSupportedCulturesAsync(TestContext.CancellationToken)).Should().BeEquivalentTo(expectedCultures);

        // Independent mutation path: raw SQL updates the persisted row.
        await using(var raw = new SqlConnection(connectionString)) {
            await raw.OpenAsync(TestContext.CancellationToken);
            (await ExecuteAsync(raw, "UPDATE LocalizedStrings SET Value = N'Updated' WHERE ResourceSet = N'SampleModel' AND Culture = N'en-US' AND Name = N'Greeting'", TestContext.CancellationToken))
                .Should().Be(1);
        }

        // Live visibility without any reload or cache invalidation.
        (await store.GetStringAsync("SampleModel", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Updated",
            "EF-backed reads are live/stateless: externally persisted changes are visible immediately");

        // The documented no-op ReloadAsync remains consistent with live reads.
        await store.ReloadAsync(TestContext.CancellationToken);
        (await store.GetStringAsync("SampleModel", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Updated",
            "ReloadAsync is a valid no-op because every read already reflects the current database state");

        // Cancellation is honored on the store path.
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        async Task LookupAsync() => await store.GetStringAsync("SampleModel", "en-US", "Greeting", cancellation.Token);

        await ((Func<Task>)LookupAsync).Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Verifies that ef core store resource set crud lifecycle and identity against real sql server.
    /// </summary>
    [TestMethod]
    public async Task EfCoreStore_ResourceSetCrudLifecycleAndIdentity_AgainstRealSqlServerAsync() {
        var connectionString = RequireEnvironment();

        await using(var setup = new SqlConnection(connectionString)) {
            await setup.OpenAsync(TestContext.CancellationToken);
            await ExecuteAsync(setup, """
                IF OBJECT_ID(N'dbo.LocalizedStrings', N'U') IS NOT NULL DROP TABLE dbo.LocalizedStrings;
                IF OBJECT_ID(N'dbo.AuditRecords', N'U') IS NOT NULL DROP TABLE dbo.AuditRecords;
                IF OBJECT_ID(N'dbo.StringVersions', N'U') IS NOT NULL DROP TABLE dbo.StringVersions;
                IF OBJECT_ID(N'dbo.CatalogState', N'U') IS NOT NULL DROP TABLE dbo.CatalogState;
                    IF OBJECT_ID(N'dbo.StringNotes', N'U') IS NOT NULL DROP TABLE dbo.StringNotes;
                """, TestContext.CancellationToken);
        }

        var services = new ServiceCollection();
        services.AddEntityFrameworkLocalizationStore(connectionString);
        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>();
        await using(var context = await factory.CreateDbContextAsync(TestContext.CancellationToken)) {
            await context.Database.EnsureCreatedAsync(TestContext.CancellationToken);
            await context.Set<LocalizedStringEntry>().AddRangeAsync(
                [
                    new LocalizedStringEntry(Guid.NewGuid(), "CrudSet", "en-US", "Greeting", "Hello"),
                    new LocalizedStringEntry(Guid.NewGuid(), "CrudSet", "nl-NL", "Greeting", "Hallo"),
                    new LocalizedStringEntry(Guid.NewGuid(), "OtherSet", "en-US", "Greeting", "Other-Hello"),
                    new LocalizedStringEntry(Guid.NewGuid(), "OtherSet", "en-US", "Shared", "Other-Shared"),
                ],
                TestContext.CancellationToken);
            await context.SaveChangesAsync(TestContext.CancellationToken);
        }

        var store = provider.GetRequiredService<ILocalizationStore>();

        // Missing resource set / culture with no data.
        (await store.GetResourceSetAsync("CrudSet", "en-US", TestContext.CancellationToken)).Should().NotBeNull("the seeded set must resolve");
        (await store.GetResourceSetAsync("CrudSet", "de-DE", TestContext.CancellationToken)).Should().BeNull("exact culture identity: a culture with no rows has no set");
        (await store.GetResourceSetsAsync("de-DE", TestContext.CancellationToken)).Should().BeEmpty("an empty culture has no resource sets");

        // Exact resource-set/culture identity and per-set isolation across distinct cultures.
        var crudSet = (await store.GetResourceSetAsync("CrudSet", "en-US", TestContext.CancellationToken))!;
        var crudSetNl = (await store.GetResourceSetAsync("CrudSet", "nl-NL", TestContext.CancellationToken))!;
        var otherSet = (await store.GetResourceSetAsync("OtherSet", "en-US", TestContext.CancellationToken))!;

        crudSet.ResourceSet.Should().Be("CrudSet");
        crudSet.Culture.Should().Be("en-US");
        crudSet.GetCultureInfo().Name.Should().Be("en-US");

        (await crudSet.GetStringAsync("Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");
        (await crudSetNl.GetStringAsync("Greeting", TestContext.CancellationToken)).Value.Should().Be("Hallo");
        (await otherSet.GetStringAsync("Greeting", TestContext.CancellationToken)).Value.Should().Be("Other-Hello",
            "each resource-set handle stays bound to its own resource set and culture (no shared-context contamination)");

        // Missing vs existing strings through store and resource-set paths.
        (await store.GetStringAsync("CrudSet", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");
        var missing = await store.GetStringAsync("CrudSet", "en-US", "Missing", TestContext.CancellationToken);
        missing.IsMissing().Should().BeTrue("a missing string surfaces the canonical missing entry");
        (await crudSet.GetStringAsync("Missing", TestContext.CancellationToken)).IsMissing().Should().BeTrue();

        // Synchronous probe paths.
        store.TryGetResourceSet("CrudSet", "en-US", out var syncSet).Should().BeTrue();
        syncSet.Should().NotBeNull();
        syncSet!.ResourceSet.Should().Be("CrudSet");
        syncSet.Culture.Should().Be("en-US");
        store.TryGetResourceSet("CrudSet", "de-DE", out _).Should().BeFalse();
        store.TryGetString("CrudSet", "en-US", "Greeting", out var syncEntry).Should().BeTrue();
        syncEntry!.Value.Should().Be("Hello");
        store.TryGetString("CrudSet", "en-US", "Missing", out _).Should().BeFalse();

        // All-string retrieval for a single resource set.
        (await store.GetAllStringsAsync("CrudSet", "en-US", TestContext.CancellationToken))
            .Keys.Should().BeEquivalentTo("Greeting");

        // Culture-wide retrieval merges every set for the culture, so a shared key
        // (here "Greeting" in CrudSet and OtherSet) must merge instead of throwing.
        var cultureSnapshot = await store.GetAllStringsForCultureAsync("en-US", TestContext.CancellationToken);
        cultureSnapshot.Keys.Should().BeEquivalentTo("Greeting", "Shared");
        cultureSnapshot["Shared"].Value.Should().Be("Other-Shared");
        _greetingCandidates.Should().Contain(cultureSnapshot["Greeting"].Value);

        (await store.GetSupportedCulturesAsync(TestContext.CancellationToken)).Should().BeEquivalentTo("en-US", "nl-NL");

        // Deterministic repeated access.
        (await store.GetStringAsync("CrudSet", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");
        (await store.GetStringAsync("CrudSet", "en-US", "Greeting", TestContext.CancellationToken)).Value.Should().Be("Hello");

        // Add single.
        var world = new LocalizedStringEntry(Guid.NewGuid(), "CrudSet", "en-US", "World", "World-Value");
        await crudSet.AddAsync(world, TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "World", TestContext.CancellationToken)).Value.Should().Be("World-Value");

        // Add bulk.
        var bulkA = new LocalizedStringEntry(Guid.NewGuid(), "CrudSet", "en-US", "BulkA", "A-Value");
        var bulkB = new LocalizedStringEntry(Guid.NewGuid(), "CrudSet", "en-US", "BulkB", "B-Value");
        await crudSet.AddAsync([bulkA, bulkB], TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "BulkA", TestContext.CancellationToken)).Value.Should().Be("A-Value");
        (await store.GetStringAsync("CrudSet", "en-US", "BulkB", TestContext.CancellationToken)).Value.Should().Be("B-Value");

        // Update single.
        world.Value = "World-Updated";
        await crudSet.UpdateAsync(world, TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "World", TestContext.CancellationToken)).Value.Should().Be("World-Updated");

        // Update bulk.
        bulkA.Value = "A-Updated";
        bulkB.Value = "B-Updated";
        await crudSet.UpdateAsync([bulkA, bulkB], TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "BulkA", TestContext.CancellationToken)).Value.Should().Be("A-Updated");
        (await store.GetStringAsync("CrudSet", "en-US", "BulkB", TestContext.CancellationToken)).Value.Should().Be("B-Updated");

        // Delete single.
        await crudSet.DeleteAsync(world, TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "World", TestContext.CancellationToken)).IsMissing().Should().BeTrue();

        // Delete bulk.
        await crudSet.DeleteAsync([bulkA, bulkB], TestContext.CancellationToken);
        (await store.GetStringAsync("CrudSet", "en-US", "BulkA", TestContext.CancellationToken)).IsMissing().Should().BeTrue();
        (await store.GetStringAsync("CrudSet", "en-US", "BulkB", TestContext.CancellationToken)).IsMissing().Should().BeTrue();
        (await store.GetAllStringsAsync("CrudSet", "en-US", TestContext.CancellationToken)).Keys.Should().BeEquivalentTo("Greeting");

        // Resource-set lifecycle: removing the last persisted row ends the set's existence.
        var seededGreeting = (await crudSet.GetAllStringsAsync(TestContext.CancellationToken)).Single(static e => e.Key == "Greeting").Value;
        await crudSet.DeleteAsync(seededGreeting, TestContext.CancellationToken);
        (await store.GetResourceSetAsync("CrudSet", "en-US", TestContext.CancellationToken)).Should().BeNull(
            "a resource set exists only while it has persisted entries");
        var enUsSets = await store.GetResourceSetsAsync("en-US", TestContext.CancellationToken);
        enUsSets.Select(static set => set.ResourceSet).Should().NotContain("CrudSet");
        enUsSets.Select(static set => set.ResourceSet).Should().Contain("OtherSet");
    }

    /// <summary>
    /// Verifies that ef core store exception propagation and cancellation against real sql server.
    /// </summary>
    [TestMethod]
    public async Task EfCoreStore_ExceptionPropagationAndCancellation_AgainstRealSqlServerAsync() {
        var connectionString = RequireEnvironment();

        await using(var setup = new SqlConnection(connectionString)) {
            await setup.OpenAsync(TestContext.CancellationToken);
            await ExecuteAsync(setup, """
                IF OBJECT_ID(N'dbo.LocalizedStrings', N'U') IS NOT NULL DROP TABLE dbo.LocalizedStrings;
                IF OBJECT_ID(N'dbo.AuditRecords', N'U') IS NOT NULL DROP TABLE dbo.AuditRecords;
                IF OBJECT_ID(N'dbo.StringVersions', N'U') IS NOT NULL DROP TABLE dbo.StringVersions;
                IF OBJECT_ID(N'dbo.CatalogState', N'U') IS NOT NULL DROP TABLE dbo.CatalogState;
                    IF OBJECT_ID(N'dbo.StringNotes', N'U') IS NOT NULL DROP TABLE dbo.StringNotes;
                """, TestContext.CancellationToken);
        }

        var services = new ServiceCollection();
        services.AddEntityFrameworkLocalizationStore(connectionString);
        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>();
        await using(var context = await factory.CreateDbContextAsync(TestContext.CancellationToken)) {
            await context.Database.EnsureCreatedAsync(TestContext.CancellationToken);
            await context.Set<LocalizedStringEntry>().AddRangeAsync(
                [
                    new LocalizedStringEntry(Guid.NewGuid(), "ExSet", "en-US", "Key", "Value"),
                    new LocalizedStringEntry(Guid.NewGuid(), "M1", "fr-FR", "Owner", "Alpha"),
                    new LocalizedStringEntry(Guid.NewGuid(), "M1", "fr-FR", "First", "1"),
                    new LocalizedStringEntry(Guid.NewGuid(), "M2", "fr-FR", "Owner", "Beta"),
                    new LocalizedStringEntry(Guid.NewGuid(), "M2", "fr-FR", "Second", "2"),
                ],
                TestContext.CancellationToken);
            await context.SaveChangesAsync(TestContext.CancellationToken);
        }

        var store = provider.GetRequiredService<ILocalizationStore>();

        // Write failures propagate rather than being converted into a silent miss.
        var exSet = (await store.GetResourceSetAsync("ExSet", "en-US", TestContext.CancellationToken))!;
        var existing = await store.GetStringAsync("ExSet", "en-US", "Key", TestContext.CancellationToken);
        Func<Task> duplicateKeyAdd = () => exSet.AddAsync(
            new LocalizedStringEntry(existing.Id, "ExSet", "en-US", "Other", "Other-Value"),
            TestContext.CancellationToken);

        await duplicateKeyAdd.Should().ThrowAsync<DbUpdateException>("a duplicate primary key must fail loudly");

        (await store.GetStringAsync("ExSet", "en-US", "Other", TestContext.CancellationToken)).IsMissing().Should().BeTrue(
            "the failed insert must not leak a partial row into the store");

        // Write cancellation is honored on the resource-set path.
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> cancelledAdd = () => exSet.AddAsync(
            new LocalizedStringEntry(Guid.NewGuid(), "ExSet", "en-US", "Cancelled", "Never"),
            cancellation.Token);

        await cancelledAdd.Should().ThrowAsync<OperationCanceledException>();

        (await store.GetStringAsync("ExSet", "en-US", "Cancelled", TestContext.CancellationToken)).IsMissing().Should().BeTrue();

        // Culture-wide retrieval across multiple sets sharing a key merges (regression: the EF
        // store previously threw via ToDictionary instead of overwriting).
        var merged = await store.GetAllStringsForCultureAsync("fr-FR", TestContext.CancellationToken);
        merged.Keys.Should().BeEquivalentTo("Owner", "First", "Second");
        merged["First"].Value.Should().Be("1");
        merged["Second"].Value.Should().Be("2");
        _ownerCandidates.Should().Contain(merged["Owner"].Value);
    }

    /// <summary>
    /// Performs the require environment operation.
    /// </summary>
    /// <returns>The require environment text.</returns>
    private static string RequireEnvironment() {
        var value = Environment.GetEnvironmentVariable(SQLSERVER_CONNECTION_ENV);
        if(string.IsNullOrWhiteSpace(value)) {
            Assert.Inconclusive($"set {SQLSERVER_CONNECTION_ENV} to a connection string to run the EF Core runtime test");
        }

        return value;
    }

    /// <summary>
    /// Performs the execute operation.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="commandText">The command text.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is execute.</returns>
    private static async Task<int> ExecuteAsync(DbConnection connection, string commandText, CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
