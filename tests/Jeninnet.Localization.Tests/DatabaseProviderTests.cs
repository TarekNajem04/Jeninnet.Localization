//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the database provider tests.
/// </summary>
[TestClass]
public class DatabaseProviderTests {
    private const string TABLE_NAME = "Messages";

    /// <summary>
    /// Verifies that database providers are inherited from base class.
    /// </summary>
    [TestMethod]
    public void DatabaseProviders_AreInheritedFromBaseClass() {
        var providerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => a.FullName?.StartsWith("Jeninnet.Localization.Providers.Database.", StringComparison.Ordinal) == true &&
                        !a.FullName.Contains("Abstractions", StringComparison.Ordinal))
            .SelectMany(static a => a.GetTypes())
            .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                        t.Name.EndsWith("MessageProvider", StringComparison.Ordinal));

        foreach(var provider in providerTypes) {
            var isSubclass = provider.BaseType?.IsGenericType == true &&
                             provider.BaseType.GetGenericTypeDefinition() == typeof(DatabaseMessageProviderBase<>);
            isSubclass.Should().BeTrue($"Provider {provider.Name} must inherit from DatabaseMessageProviderBase<>");
        }
    }

    /// <summary>
    /// Verifies that database localization options has connection string and table name.
    /// </summary>
    [TestMethod]
    public void DatabaseLocalizationOptions_HasConnectionStringAndTableName() {
        var optionsType = typeof(DatabaseMessageProviderBase<DatabaseLocalizationOptions>).Assembly
            .GetTypes()
            .FirstOrDefault(static t => t.Name == "DatabaseLocalizationOptions");

        optionsType.Should().NotBeNull();
        optionsType!.GetProperty("ConnectionString").Should().NotBeNull();
        optionsType.GetProperty("TableName").Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that database localization options default table name is localization messages.
    /// </summary>
    [TestMethod]
    public void DatabaseLocalizationOptions_DefaultTableNameIsLocalizationMessages() {
        var options = new SqliteLocalizationOptions {
            ConnectionString = "Data Source=test.db;"
        };

        options.TableName.Should().Be("LocalizationMessages");
    }

    /// <summary>
    /// Verifies that database localization options accepts valid table names.
    /// </summary>
    [TestMethod]
    public void DatabaseLocalizationOptions_AcceptsValidTableNames() {
        var options = new SqliteLocalizationOptions {
            ConnectionString = "Data Source=test.db;",
            TableName = TABLE_NAME
        };

        options.TableName.Should().Be(TABLE_NAME);

        options.TableName = "dbo.LocalizationMessages";
        options.TableName.Should().Be("dbo.LocalizationMessages");

        options.TableName = "app.Dictionaries.LocalizationMessages";
        options.TableName.Should().Be("app.Dictionaries.LocalizationMessages");
    }

    /// <summary>
    /// Verifies that database localization options rejects invalid table names.
    /// </summary>
    [TestMethod]
    public void DatabaseLocalizationOptions_RejectsInvalidTableNames() {
        var options = new SqliteLocalizationOptions {
            ConnectionString = "Data Source=test.db;"
        };

        var invalids = new[] {
            "",
            "  ",
            "1Messages",
            "Messages; DROP TABLE",
            "Messages Messages",
            "Messages-",
            ".dbo.LocalizationMessages",
            "dbo..LocalizationMessages",
            "a.b.c.d"
        };

        foreach(var invalid in invalids) {
            Action act = () => options.TableName = invalid;
            act.Should().Throw<ArgumentException>($"table name '{invalid}' must be rejected");
        }
    }

    /// <summary>
    /// Verifies that database localization options requires connection string.
    /// </summary>
    [TestMethod]
    public void DatabaseLocalizationOptions_RequiresConnectionString() {
        var options = new SqliteLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => _ = new SqliteMessageProvider(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that sql server message provider rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void SqlServerMessageProvider_RejectsEmptyConnectionString() {
        var options = new SqlServerLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => _ = new SqlServerMessageProvider(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that sql server message provider accepts schema qualified table name.
    /// </summary>
    [TestMethod]
    public void SqlServerMessageProvider_AcceptsSchemaQualifiedTableName() {
        var provider = new SqlServerMessageProvider(new SqlServerLocalizationOptions {
            ConnectionString = "Server=localhost;Database=db;",
            TableName = "dbo.LocalizationMessages"
        });

        provider.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that add sql server localization rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void AddSqlServerLocalization_RejectsEmptyConnectionString() {
        var services = new ServiceCollection();
        var options = new SqlServerLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => services.AddSqlServerLocalization(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add sql server localization registers single inner provider.
    /// </summary>
    [TestMethod]
    public void AddSqlServerLocalization_RegistersSingleInnerProvider() {
        var services = new ServiceCollection();
        services.AddSqlServerLocalization("Server=localhost;Database=db;");

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
            .Which.Should().BeOfType<SqlServerMessageProvider>();
    }

    /// <summary>
    /// Verifies that my sql message provider rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void MySqlMessageProvider_RejectsEmptyConnectionString() {
        var options = new MySqlLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => _ = new MySqlMessageProvider(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that my sql message provider accepts schema qualified table name.
    /// </summary>
    [TestMethod]
    public void MySqlMessageProvider_AcceptsSchemaQualifiedTableName() {
        var provider = new MySqlMessageProvider(new MySqlLocalizationOptions {
            ConnectionString = "Server=localhost;Database=db;",
            TableName = "localization.LocalizationMessages"
        });

        provider.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that add my sql localization rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void AddMySqlLocalization_RejectsEmptyConnectionString() {
        var services = new ServiceCollection();
        var options = new MySqlLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => services.AddMySqlLocalization(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add my sql localization registers single inner provider.
    /// </summary>
    [TestMethod]
    public void AddMySqlLocalization_RegistersSingleInnerProvider() {
        var services = new ServiceCollection();
        services.AddMySqlLocalization("Server=localhost;Database=db;");

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>()
                .Should().ContainSingle()
                .Which.Should().BeOfType<MySqlMessageProvider>();
    }

    /// <summary>
    /// Verifies that postgre sql message provider rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void PostgreSqlMessageProvider_RejectsEmptyConnectionString() {
        var options = new PostgreSqlLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => _ = new PostgreSqlMessageProvider(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that postgre sql message provider accepts schema qualified table name.
    /// </summary>
    [TestMethod]
    public void PostgreSqlMessageProvider_AcceptsSchemaQualifiedTableName() {
        var provider = new PostgreSqlMessageProvider(new PostgreSqlLocalizationOptions {
            ConnectionString = "Host=localhost;Database=db;",
            TableName = "public.LocalizationMessages"
        });

        provider.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that add postgre sql localization rejects empty connection string.
    /// </summary>
    [TestMethod]
    public void AddPostgreSqlLocalization_RejectsEmptyConnectionString() {
        var services = new ServiceCollection();
        var options = new PostgreSqlLocalizationOptions {
            TableName = TABLE_NAME
        };

        Action act = () => services.AddPostgreSqlLocalization(options);
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add postgre sql localization registers single inner provider.
    /// </summary>
    [TestMethod]
    public void AddPostgreSqlLocalization_RegistersSingleInnerProvider() {
        var services = new ServiceCollection();
        services.AddPostgreSqlLocalization("Host=localhost;Database=db;");

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
            .Which.Should().BeOfType<PostgreSqlMessageProvider>();
    }

    /// <summary>
    /// Verifies that storage model has only expected properties.
    /// </summary>
    [TestMethod]
    public void StorageModel_HasOnlyExpectedProperties() {
        var entityProps = typeof(LocalizedMessageEntity).GetProperties().Select(static p => p.Name).Order().ToList();
        var dtoProps = typeof(LocalizedMessageEntry).GetProperties().Select(static p => p.Name).Order().ToList();
        var resourceDtoProps = typeof(LocalizationResource).GetProperties().Select(static p => p.Name).Order().ToList();

        entityProps.Should().BeEquivalentTo("Id", "MessageKey", "Culture", "Template");
        dtoProps.Should().BeEquivalentTo("MessageKey", "Template");
        resourceDtoProps.Should().BeEquivalentTo("Entries");
    }
}
