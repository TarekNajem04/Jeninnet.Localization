//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Deterministic tests for the EF Core resource-set store provider
/// (<c>Jeninnet.Localization.Providers.Database.EntityFrameworkCore</c>).
/// </summary>
[TestClass]
public class EntityFrameworkCoreStoreTests {
    /// <summary>
    /// Verifies that add entity framework localization store rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void AddEntityFrameworkLocalizationStore_RejectsEmptyConnectionString() {
        var services = new ServiceCollection();

        Action act = () => services.AddEntityFrameworkLocalizationStore("");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add entity framework localization store rejects null connection string.
    /// </summary>
    [TestMethod]
    public void AddEntityFrameworkLocalizationStore_RejectsNullConnectionString() {
        var services = new ServiceCollection();

        Action act = () => services.AddEntityFrameworkLocalizationStore(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add entity framework localization store rejects missing named connection string.
    /// </summary>
    [TestMethod]
    public void AddEntityFrameworkLocalizationStore_RejectsMissingNamedConnectionString() {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddEntityFrameworkLocalizationStore(configuration);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add entity framework localization store registers store from named connection string.
    /// </summary>
    [TestMethod]
    public void AddEntityFrameworkLocalizationStore_RegistersStoreFromNamedConnectionString() {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:LocalizationDb"] = "Server=localhost;Database=localization;"
            })
            .Build();

        services.AddEntityFrameworkLocalizationStore(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILocalizationStore>().Should().BeOfType<SqlDbLocalizationStore>();
        provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>().Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that localization data context model maps store entry schema.
    /// </summary>
    [TestMethod]
    public void LocalizationDataContext_ModelMapsStoreEntrySchema() {
        var options = new DbContextOptionsBuilder<LocalizationDataContext>()
            .UseSqlServer("Server=localhost;Database=localization;")
            .Options;

        using var dbContext = new LocalizationDataContext(options);

        // Building the model is performed lazily and does not open a connection.
        var entityType = dbContext.Model.FindEntityType(typeof(LocalizedStringEntry));
        entityType.Should().NotBeNull();

        entityType!.GetTableName().Should().Be("LocalizedStrings");

        entityType!.GetProperties()
                   .Select(static p => p.Name)
                   .Should()
                   .BeEquivalentTo("Id", "ResourceSet", "Culture", "Name", "Value");

        entityType.FindPrimaryKey()!.Properties
                  .Select(static p => p.Name)
                  .Should()
                  .BeEquivalentTo("Id");
    }
}
