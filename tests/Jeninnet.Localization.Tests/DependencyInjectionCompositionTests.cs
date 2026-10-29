//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the dependency-injection composition tests.
/// </summary>
[TestClass]
public class DependencyInjectionCompositionTests {
    /// <summary>
    /// Verifies that add memory localization composes memory provider only.
    /// </summary>
    [TestMethod]
    public void AddMemoryLocalization_ComposesMemoryProviderOnly() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> { ["key"] = "value" });

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
            .Which.Should().BeOfType<MemoryMessageProvider>();
        provider.GetService<IMessageProvider>().Should().BeNull();
    }

    /// <summary>
    /// Verifies that add json localization composes json provider only.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ComposesJsonProviderOnly() {
        var path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, "{\"key\":\"value\"}");
            var services = new ServiceCollection();
            services.AddJsonLocalization(path);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
                .Which.Should().BeOfType<JsonMessageProvider>();
        }
        finally {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies that add json localization providers composes culture aware provider.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_ComposesCultureAwareProvider() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            var localization = directory.CreateSubdirectory("Localization");
            File.WriteAllText(Path.Combine(localization.FullName, "en.json"), "{}");
            File.WriteAllText(Path.Combine(localization.FullName, "ar.json"), "{}");
            var services = new ServiceCollection();
            services.AddJsonLocalizationProviders(directory.FullName, ["en", "ar"]);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
                .Which.Should().BeOfType<CultureAwareMessageProvider>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that add xml localization composes xml provider only.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ComposesXmlProviderOnly() {
        var path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, "<messages><key>value</key></messages>");
            var services = new ServiceCollection();
            services.AddXmlLocalization(path);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
                .Which.Should().BeOfType<XmlMessageProvider>();
        }
        finally {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies that add xml localization providers composes culture aware provider.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_ComposesCultureAwareProvider() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            var localization = directory.CreateSubdirectory("Localization");
            File.WriteAllText(Path.Combine(localization.FullName, "en.xml"), "<messages />");
            File.WriteAllText(Path.Combine(localization.FullName, "ar.xml"), "<messages />");
            var services = new ServiceCollection();
            services.AddXmlLocalizationProviders(directory.FullName, ["en", "ar"]);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
                .Which.Should().BeOfType<CultureAwareMessageProvider>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that add res XLocalization composes res XProvider.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ComposesResXProvider() {
        var services = new ServiceCollection();
        services.AddResXLocalization(new ResXLocalizationOptions {
            BaseName = "Jeninnet.Localization.Core.Messages",
            ResourceAssembly = typeof(DefaultMessageLocalizer).Assembly
        });

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
            .Which.Should().BeOfType<ResXMessageProvider>();
    }

    /// <summary>
    /// Verifies that add sql server localization composes sql server provider.
    /// </summary>
    [TestMethod]
    public void AddSqlServerLocalization_ComposesSqlServerProvider() {
        var services = new ServiceCollection();
        services.AddSqlServerLocalization("Server=localhost;Database=localization;");

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
            .Which.Should().BeOfType<SqlServerMessageProvider>();
    }

    /// <summary>
    /// Verifies that add database providers compose only their own provider.
    /// </summary>
    [TestMethod]
    public void AddDatabaseProviders_ComposeOnlyTheirOwnProvider() {
        var cases = new (Action<IServiceCollection> Register, Type ProviderType)[] {
            (services => services.AddMySqlLocalization("Server=localhost;Database=localization;"), typeof(MySqlMessageProvider)),
            (services => services.AddPostgreSqlLocalization("Host=localhost;Database=localization;"), typeof(PostgreSqlMessageProvider)),
            (services => services.AddSqliteLocalization("Data Source=localization.db;"), typeof(SqliteMessageProvider))
        };

        foreach(var (register, providerType) in cases) {
            var services = new ServiceCollection();
            register(services);

            using var provider = services.BuildServiceProvider();
            provider.GetServices<IInnerMessageProvider>().Should().ContainSingle()
                .Which.GetType().Should().Be(providerType);
        }
    }

    /// <summary>
    /// Verifies that add entity framework localization store composes store and factory.
    /// </summary>
    [TestMethod]
    public void AddEntityFrameworkLocalizationStore_ComposesStoreAndFactory() {
        var services = new ServiceCollection();
        services.AddEntityFrameworkLocalizationStore("Server=localhost;Database=localization;");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILocalizationStore>().Should().BeOfType<SqlDbLocalizationStore>();
        provider.GetRequiredService<IDbContextFactory<LocalizationDataContext>>().Should().NotBeNull();
        provider.GetService<IInnerMessageProvider>().Should().BeNull();
    }

    /// <summary>
    /// Verifies that add mvc localized validation composes factory and metadata provider.
    /// </summary>
    [TestMethod]
    public void AddMvcLocalizedValidation_ComposesFactoryAndMetadataProvider() {
        var services = new ServiceCollection();
        services.AddEntityFrameworkLocalizationStore("Server=localhost;Database=localization;");
        services.AddMvcLocalizedValidation();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStringLocalizerFactory>().Should().BeOfType<CustomStringLocalizerFactory>();
        provider.GetRequiredService<IOptions<MvcOptions>>().Value.ModelMetadataDetailsProviders
            .Should().ContainSingle(static item => item is LocalizedValidationMetadataProvider);
        provider.GetService<IInnerMessageProvider>().Should().BeNull();
    }

    /// <summary>
    /// Verifies that translation provider participates as canonical provider.
    /// </summary>
    [TestMethod]
    public void TranslationProviderParticipatesAsCanonicalProvider() {
        var google = new ServiceCollection();
        CoreJeninnetLocalizationExtensions.AddLocalization(google);
        google.AddGoogleTranslationLocalization(static options => options.ApiKey = "key");
        using(var sp = google.BuildServiceProvider()) {
            sp.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<GoogleTranslationProvider>();
            sp.GetRequiredService<IMessageProvider>().Should().BeOfType<LocalizationProviderSelector>();
        }

        var azure = new ServiceCollection();
        CoreJeninnetLocalizationExtensions.AddLocalization(azure);
        azure.AddAzureTranslationLocalization(static options => options.ApiKey = "key");
        using(var sp = azure.BuildServiceProvider()) {
            sp.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<AzureTranslationProvider>();
            sp.GetRequiredService<IMessageProvider>().Should().BeOfType<LocalizationProviderSelector>();
        }
    }

    /// <summary>
    /// Verifies that memory plus translation throws when provider selector is resolved.
    /// </summary>
    [TestMethod]
    public void MemoryPlusTranslation_ThrowsWhenProviderSelectorResolved() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> { ["key"] = "value" });
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        services.AddAzureTranslationLocalization(static options => options.ApiKey = "key");
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IMessageProvider>();
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that canonical apis expose current names and internal infrastructure.
    /// </summary>
    [TestMethod]
    public void CanonicalApis_ExposeCurrentNamesAndInternalInfrastructure() {
        var sqlServer = typeof(SqlServerMessageProvider).Assembly.GetType(
            "Jeninnet.Localization.Providers.Database.SqlServer.Extensions.ServiceCollectionExtensions")!;
        var mvc = typeof(LocalizedValidationMetadataProvider).Assembly.GetType(
            "Jeninnet.Localization.Integrations.Mvc.Validation.Extensions.JeninnetLocalizationExtensions")!;
        var ef = typeof(SqlDbLocalizationStore).Assembly.GetType(
            "Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions.ServiceCollectionExtensions")!;
        var core = typeof(Jeninnet.Localization.Core.Extensions.JeninnetLocalizationExtensions);

        sqlServer.GetMethods(BindingFlags.Public | BindingFlags.Static).Should()
            .Contain(static method => method.Name == "AddSqlServerLocalization");
        mvc.GetMethods(BindingFlags.Public | BindingFlags.Static).Should()
            .Contain(static method => method.Name == "AddMvcLocalizedValidation");
        ef.GetMethods(BindingFlags.Public | BindingFlags.Static).Should()
            .Contain(static method => method.Name == "AddEntityFrameworkLocalizationStore");

        sqlServer.GetMethods().Should().NotContain(static method => method.Name == "AddMsSqlLocalization");
        mvc.GetMethods().Should().NotContain(static method => method.Name == "AddDynamicLocalization");
        ef.GetMethods().Should().NotContain(static method => method.Name == "AddDynamicLocalization");
        core.GetMethods().Should().NotContain(static method => method.Name == "AddDynamicLocalization");

        var azureExt = typeof(AzureTranslationProvider).Assembly.GetType(
            "Jeninnet.Localization.Translation.Azure.Extensions.ServiceCollectionExtensions")!;
        var azureMethods = azureExt.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                   .Select(static m => m.Name).Distinct().ToArray();
        azureMethods.Should().Contain("AddAzureTranslationLocalization", "§17 canonical name for Azure");
        azureMethods.Should().NotContain("AddMicrosoftTranslation", "§17 obsolete name removed");
        azureMethods.Should().NotContain("AddMicrosoftTranslationLocalization", "§17 obsolete name removed");

        var googleExt = typeof(GoogleTranslationProvider).Assembly.GetType(
            "Jeninnet.Localization.Translation.Google.Extensions.ServiceCollectionExtensions")!;
        var googleMethods = googleExt.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                     .Select(static m => m.Name).Distinct().ToArray();
        googleMethods.Should().Contain("AddGoogleTranslationLocalization", "§17 canonical name for Google");
        googleMethods.Should().NotContain("AddGoogleTranslation", "§17 obsolete name removed");
    }

    /// <summary>
    /// Verifies that registration apis do not expose obsolete configuration parameters.
    /// </summary>
    [TestMethod]
    public void RegistrationApis_DoNotExposeObsoleteConfigurationParameters() {
        var registrationTypes = new[] {
            typeof(Jeninnet.Localization.Core.Extensions.JeninnetLocalizationExtensions),
            typeof(Jeninnet.Localization.Providers.Memory.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Json.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Xml.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.ResX.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Database.SqlServer.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Database.MySql.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Database.PostgreSQL.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Providers.Database.SQLite.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Translation.Google.Extensions.ServiceCollectionExtensions),
            typeof(Jeninnet.Localization.Translation.Azure.Extensions.ServiceCollectionExtensions)
        };
        var mvc = typeof(LocalizedValidationMetadataProvider).Assembly.GetType(
            "Jeninnet.Localization.Integrations.Mvc.Validation.Extensions.JeninnetLocalizationExtensions")!;

        foreach(var type in registrationTypes.Append(mvc)) {
            type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static method => method.Name.StartsWith("Add", StringComparison.Ordinal))
                .SelectMany(static method => method.GetParameters())
                .Should().NotContain(
                    static parameter => parameter.ParameterType.FullName == "Microsoft.Extensions.Configuration.IConfiguration",
                    $"[Architecture] {type.FullName} must not expose an obsolete IConfiguration parameter");
        }

        var ef = typeof(SqlDbLocalizationStore).Assembly.GetType(
            "Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions.ServiceCollectionExtensions")!;
        ef.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == "AddEntityFrameworkLocalizationStore")
            .SelectMany(static method => method.GetParameters())
            .Should().Contain(
                static parameter => parameter.ParameterType.FullName == "Microsoft.Extensions.Configuration.IConfiguration",
                "[Architecture] EF named-connection-string IConfiguration overload is the documented exception and must be preserved");
    }
}
