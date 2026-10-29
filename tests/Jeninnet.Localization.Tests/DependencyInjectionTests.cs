//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the dependency injection tests.
/// </summary>
[TestClass]
public class DependencyInjectionTests {
    /// <summary>
    /// Verifies that add localization registers provider selector as IMessage provider.
    /// </summary>
    [TestMethod]
    public void AddLocalization_RegistersProviderSelectorAsIMessageProvider() {
        var services = new ServiceCollection();

        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var provider = services.BuildServiceProvider();

        var resolved = provider.GetService<IMessageProvider>();
        resolved.Should().BeOfType<LocalizationProviderSelector>();
    }

    /// <summary>
    /// Verifies that add localization registers default localizer.
    /// </summary>
    [TestMethod]
    public void AddLocalization_RegistersDefaultLocalizer() {
        var services = new ServiceCollection();

        services.AddSingleton(new Jeninnet.Localization.Abstractions.Options.LocalizationOptions { DefaultCulture = "en" });
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var provider = services.BuildServiceProvider();

        var localizer = provider.GetService<IMessageLocalizer>();
        localizer.Should().BeOfType<DefaultMessageLocalizer>();
    }

    /// <summary>
    /// Verifies that add localization registers default formatter.
    /// </summary>
    [TestMethod]
    public void AddLocalization_RegistersDefaultFormatter() {
        var services = new ServiceCollection();

        services.AddSingleton(new Jeninnet.Localization.Abstractions.Options.LocalizationOptions { DefaultCulture = "en" });
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var provider = services.BuildServiceProvider();

        var formatter = provider.GetService<IMessageFormatter>();
        formatter.Should().BeOfType<DefaultMessageFormatter>();
    }

    /// <summary>
    /// Verifies that add localization configure throws on null services.
    /// </summary>
    [TestMethod]
    public void AddLocalization_Configure_ThrowsOnNullServices() {
        var act = static () => ((IServiceCollection)null!).AddLocalization((Action<LocalizationOptions>)(static _ => { }));

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add localization configure throws on null configure.
    /// </summary>
    [TestMethod]
    public void AddLocalization_Configure_ThrowsOnNullConfigure() {
        var services = new ServiceCollection();

        var act = () => services.AddLocalization((Action<LocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add localization configure throws when options already is registered.
    /// </summary>
    [TestMethod]
    public void AddLocalization_Configure_ThrowsWhenOptionsAlreadyRegistered() {
        var services = new ServiceCollection();
        services.AddSingleton(new LocalizationOptions { DefaultCulture = "en" });

        var act = () => services.AddLocalization(static options => options.DefaultCulture = "ar");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that add localization configure throws on invalid default culture.
    /// </summary>
    [TestMethod]
    public void AddLocalization_Configure_ThrowsOnInvalidDefaultCulture() {
        var services = new ServiceCollection();

        var act = () => services.AddLocalization(static options => options.DefaultCulture = "!");

        act.Should().Throw<CultureNotFoundException>();
    }

    /// <summary>
    /// Verifies that add localization configure registers configured default culture.
    /// </summary>
    [TestMethod]
    public async Task AddLocalization_Configure_RegistersConfiguredDefaultCultureAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new CultureKeyedProvider(new Dictionary<string, string> { ["en"] = "Default Template" }));
        services.AddLocalization(static options => options.DefaultCulture = "en");

        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("ar"), CancellationToken.None);

        result.Should().Be("Default Template");
    }

    /// <summary>
    /// Verifies that throws when multiple providers are is registered.
    /// </summary>
    [TestMethod]
    public void ThrowsWhenMultipleProvidersAreRegistered() {
        var services = new ServiceCollection();

        services.AddSingleton<IInnerMessageProvider>(new MockProvider("a"));
        services.AddSingleton<IInnerMessageProvider>(new MockProvider("b"));

        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IMessageProvider>();

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that delegates to the single registered provider.
    /// </summary>
    [TestMethod]
    public async Task DelegatesToTheSingleRegisteredProviderAsync() {
        var services = new ServiceCollection();

        services.AddSingleton<IInnerMessageProvider>(new MockProvider("FromMock"));
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var sp = services.BuildServiceProvider();

        var selector = sp.GetRequiredService<IMessageProvider>();
        var result = await selector.GetMessageTemplateAsync("key", CultureInfo.CurrentCulture, CancellationToken.None);

        result.Should().Be("FromMock");
    }

    /// <summary>
    /// Verifies that try add singleton prevents duplicate registrations.
    /// </summary>
    [TestMethod]
    public void TryAddSingleton_PreventsDuplicateRegistrations() {
        var services = new ServiceCollection();

        services.AddSingleton<IMessageLocalizer>(new CustomLocalizer());
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        var provider = services.BuildServiceProvider();

        var localizer = provider.GetService<IMessageLocalizer>();
        localizer.Should().BeOfType<CustomLocalizer>();
    }

    /// <summary>
    /// Verifies that add memory localization throws on null services.
    /// </summary>
    [TestMethod]
    public void AddMemoryLocalization_ThrowsOnNullServices() {
        var act = static () => ((IServiceCollection)null!).AddMemoryLocalization(new MemoryLocalizationOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add memory localization throws on null options.
    /// </summary>
    [TestMethod]
    public void AddMemoryLocalization_ThrowsOnNullOptions() {
        var services = new ServiceCollection();

        var act = () => services.AddMemoryLocalization((MemoryLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add memory localization throws on null messages.
    /// </summary>
    [TestMethod]
    public void AddMemoryLocalization_ThrowsOnNullMessages() {
        var services = new ServiceCollection();
        var options = new MemoryLocalizationOptions { Messages = null! };

        var act = () => services.AddMemoryLocalization(options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add memory localization throws on null configure.
    /// </summary>
    [TestMethod]
    public void AddMemoryLocalization_ThrowsOnNullConfigure() {
        var services = new ServiceCollection();

        var act = () => services.AddMemoryLocalization((Action<MemoryLocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add memory localization configure action provides messages.
    /// </summary>
    [TestMethod]
    public async Task AddMemoryLocalization_ConfigureAction_ProvidesMessagesAsync() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(static options => options.Messages = new Dictionary<string, string> {
            ["Welcome"] = "Welcome!"
        });

        await using var provider = services.BuildServiceProvider();
        var messagesProvider = provider.GetRequiredService<IInnerMessageProvider>();

        (await messagesProvider.GetMessageTemplateAsync("Welcome", new CultureInfo("en"), CancellationToken.None)).Should().Be("Welcome!");
    }

    /// <summary>
    /// Verifies that add json localization throws on null services.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ThrowsOnNullServices() {
        var act = static () => ((IServiceCollection)null!).AddJsonLocalization(new JsonLocalizationOptions { FilePath = "x.json" });

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add json localization throws on null options.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ThrowsOnNullOptions() {
        var services = new ServiceCollection();

        var act = () => services.AddJsonLocalization((JsonLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add json localization throws on null configure.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ThrowsOnNullConfigure() {
        var services = new ServiceCollection();

        var act = () => services.AddJsonLocalization((Action<JsonLocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add json localization throws on empty file path.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ThrowsOnEmptyFilePath() {
        var services = new ServiceCollection();
        var options = new JsonLocalizationOptions { FilePath = "" };

        var act = () => services.AddJsonLocalization(options);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add json localization throws on missing file.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalization_ThrowsOnMissingFile() {
        var services = new ServiceCollection();
        var options = new JsonLocalizationOptions { FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json") };

        var act = () => services.AddJsonLocalization(options);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that add json localization configure action resolves provider.
    /// </summary>
    [TestMethod]
    public async Task AddJsonLocalization_ConfigureAction_ResolvesProviderAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, """{"WELCOME":"Welcome!"}""", TestContext?.CancellationToken ?? CancellationToken.None);
            var services = new ServiceCollection();
            services.AddJsonLocalization(options => options.FilePath = tempFile);

            await using var provider = services.BuildServiceProvider();
            var messagesProvider = provider.GetRequiredService<IInnerMessageProvider>();

            messagesProvider.Should().BeOfType<JsonMessageProvider>();
            (await messagesProvider.GetMessageTemplateAsync("WELCOME", new CultureInfo("en"), CancellationToken.None)).Should().Be("Welcome!");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that add json localization providers explicit cultures throws when source file missing.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_ExplicitCultures_ThrowsWhenSourceFileMissing() {
        var services = new ServiceCollection();

        var act = () => services.AddJsonLocalizationProviders(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), ["en"]);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that add json localization providers auto discovery throws on missing directory.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_AutoDiscovery_ThrowsOnMissingDirectory() {
        var services = new ServiceCollection();

        var act = () => services.AddJsonLocalizationProviders(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        act.Should().Throw<DirectoryNotFoundException>();
    }

    /// <summary>
    /// Verifies that add json localization providers throws when no json files.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_ThrowsWhenNoJsonFiles() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            directory.CreateSubdirectory("Localization");
            var services = new ServiceCollection();

            var act = () => services.AddJsonLocalizationProviders(directory.FullName);

            act.Should().Throw<InvalidOperationException>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that add json localization providers builder throws when no cultures.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_Builder_ThrowsWhenNoCultures() {
        var services = new ServiceCollection();

        var act = () => services.AddJsonLocalizationProviders(static _ => { });

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that add json localization providers builder resolves culture aware provider.
    /// </summary>
    [TestMethod]
    public void AddJsonLocalizationProviders_Builder_ResolvesCultureAwareProvider() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            var localization = directory.CreateSubdirectory("Localization");
            File.WriteAllText(Path.Combine(localization.FullName, "en.json"), """{"WELCOME":"Welcome!"}""");
            File.WriteAllText(Path.Combine(localization.FullName, "ar.json"), """{"WELCOME":"أهلاً"}""");
            var builder = Path.Combine(localization.FullName, "{0}.json");
            var services = new ServiceCollection();
            services.AddJsonLocalizationProviders(options => options
                .AddCulture("en", string.Format(CultureInfo.InvariantCulture, builder, "en"))
                .AddCulture("ar", string.Format(CultureInfo.InvariantCulture, builder, "ar")));

            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<CultureAwareMessageProvider>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that json localization provider builder add cultures rejects invalid culture.
    /// </summary>
    [TestMethod]
    public void JsonLocalizationProviderBuilder_AddCultures_RejectsInvalidCulture() {
        var builder = new JsonLocalizationProviderBuilder();

        var act = () => builder.AddCultures("base", ["en", null!]);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that json localization provider builder add cultures rejects empty array.
    /// </summary>
    [TestMethod]
    public void JsonLocalizationProviderBuilder_AddCultures_RejectsEmptyArray() {
        var builder = new JsonLocalizationProviderBuilder();

        var act = () => builder.AddCultures("base", []);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that xml localization provider builder add cultures rejects invalid culture.
    /// </summary>
    [TestMethod]
    public void XmlLocalizationProviderBuilder_AddCultures_RejectsInvalidCulture() {
        var builder = new XmlLocalizationProviderBuilder();

        var act = () => builder.AddCultures("base", ["en", ""]);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that xml localization provider builder add cultures rejects empty array.
    /// </summary>
    [TestMethod]
    public void XmlLocalizationProviderBuilder_AddCultures_RejectsEmptyArray() {
        var builder = new XmlLocalizationProviderBuilder();

        var act = () => builder.AddCultures("base", []);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add xml localization throws on null services.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ThrowsOnNullServices() {
        var act = static () => ((IServiceCollection)null!).AddXmlLocalization(new XmlLocalizationOptions { FilePath = "x.xml" });

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add xml localization throws on null options.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ThrowsOnNullOptions() {
        var services = new ServiceCollection();

        var act = () => services.AddXmlLocalization((XmlLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add xml localization throws on null configure.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ThrowsOnNullConfigure() {
        var services = new ServiceCollection();

        var act = () => services.AddXmlLocalization((Action<XmlLocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add xml localization throws on empty file path.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ThrowsOnEmptyFilePath() {
        var services = new ServiceCollection();
        var options = new XmlLocalizationOptions { FilePath = "" };

        var act = () => services.AddXmlLocalization(options);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add xml localization throws on missing file.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalization_ThrowsOnMissingFile() {
        var services = new ServiceCollection();
        var options = new XmlLocalizationOptions { FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.xml") };

        var act = () => services.AddXmlLocalization(options);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that add xml localization configure action resolves provider.
    /// </summary>
    [TestMethod]
    public async Task AddXmlLocalization_ConfigureAction_ResolvesProviderAsync() {
        var tempFile = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(tempFile, "<messages><WELCOME>Welcome!</WELCOME></messages>", TestContext?.CancellationToken ?? CancellationToken.None);
            var services = new ServiceCollection();
            services.AddXmlLocalization(options => options.FilePath = tempFile);

            await using var provider = services.BuildServiceProvider();
            var messagesProvider = provider.GetRequiredService<IInnerMessageProvider>();

            messagesProvider.Should().BeOfType<XmlMessageProvider>();
            (await messagesProvider.GetMessageTemplateAsync("WELCOME", new CultureInfo("en"), CancellationToken.None)).Should().Be("Welcome!");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that add xml localization providers explicit cultures throws when source file missing.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_ExplicitCultures_ThrowsWhenSourceFileMissing() {
        var services = new ServiceCollection();

        var act = () => services.AddXmlLocalizationProviders(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), ["en"]);

        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Verifies that add xml localization providers auto discovery throws on missing directory.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_AutoDiscovery_ThrowsOnMissingDirectory() {
        var services = new ServiceCollection();

        var act = () => services.AddXmlLocalizationProviders(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        act.Should().Throw<DirectoryNotFoundException>();
    }

    /// <summary>
    /// Verifies that add xml localization providers throws when no xml files.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_ThrowsWhenNoXmlFiles() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            directory.CreateSubdirectory("Localization");
            var services = new ServiceCollection();

            var act = () => services.AddXmlLocalizationProviders(directory.FullName);

            act.Should().Throw<InvalidOperationException>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that add xml localization providers builder throws when no cultures.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_Builder_ThrowsWhenNoCultures() {
        var services = new ServiceCollection();

        var act = () => services.AddXmlLocalizationProviders(static _ => { });

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that add xml localization providers builder resolves culture aware provider.
    /// </summary>
    [TestMethod]
    public void AddXmlLocalizationProviders_Builder_ResolvesCultureAwareProvider() {
        var directory = Directory.CreateTempSubdirectory();
        try {
            var localization = directory.CreateSubdirectory("Localization");
            File.WriteAllText(Path.Combine(localization.FullName, "en.xml"), "<messages><WELCOME>Welcome!</WELCOME></messages>");
            File.WriteAllText(Path.Combine(localization.FullName, "ar.xml"), "<messages><WELCOME>أهلاً</WELCOME></messages>");
            var builder = Path.Combine(localization.FullName, "{0}.xml");
            var services = new ServiceCollection();
            services.AddXmlLocalizationProviders(options => options
                .AddCulture("en", string.Format(CultureInfo.InvariantCulture, builder, "en"))
                .AddCulture("ar", string.Format(CultureInfo.InvariantCulture, builder, "ar")));

            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<CultureAwareMessageProvider>();
        }
        finally {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null services.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullServices() {
        var act = static () => ((IServiceCollection)null!).AddResXLocalization(new ResXLocalizationOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null services string overload.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullServices_StringOverload() {
        var act = static () => ((IServiceCollection)null!).AddResXLocalization("BaseName", typeof(DependencyInjectionTests).Assembly);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null services configure overload.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullServices_ConfigureOverload() {
        var act = static () => ((IServiceCollection)null!).AddResXLocalization(static _ => { });

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null options.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullOptions() {
        var services = new ServiceCollection();

        var act = () => services.AddResXLocalization((ResXLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null configure.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullConfigure() {
        var services = new ServiceCollection();

        var act = () => services.AddResXLocalization((Action<ResXLocalizationOptions>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on empty base name.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnEmptyBaseName() {
        var services = new ServiceCollection();
        var options = new ResXLocalizationOptions {
            BaseName = "",
            ResourceAssembly = typeof(DependencyInjectionTests).Assembly
        };

        var act = () => services.AddResXLocalization(options);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization throws on null assembly.
    /// </summary>
    [TestMethod]
    public void AddResXLocalization_ThrowsOnNullAssembly() {
        var services = new ServiceCollection();
        var options = new ResXLocalizationOptions {
            BaseName = "Some.Base",
            ResourceAssembly = null!
        };

        var act = () => services.AddResXLocalization(options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization missing resource base registers provider first use throws.
    /// </summary>
    [TestMethod]
    public async Task AddResXLocalization_MissingResourceBase_RegistersProvider_FirstUseThrowsAsync() {
        var services = new ServiceCollection();
        services.AddResXLocalization("Jeninnet.Localization.Tests.NoSuchResource", typeof(DependencyInjectionTests).Assembly);

        await using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IInnerMessageProvider>();

        provider.Should().BeOfType<ResXMessageProvider>();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None);

        await act.Should().ThrowAsync<System.Resources.MissingManifestResourceException>();
    }

    /// <summary>
    /// Verifies that add res XLocalization configure action resolves provider.
    /// </summary>
    [TestMethod]
    public async Task AddResXLocalization_ConfigureAction_ResolvesProviderAsync() {
        var services = new ServiceCollection();
        services.AddResXLocalization(options => {
            options.BaseName = "Jeninnet.Localization.Tests.TestResources";
            options.ResourceAssembly = typeof(DependencyInjectionTests).Assembly;
        });

        await using var serviceProvider = services.BuildServiceProvider();
        var provider = serviceProvider.GetRequiredService<IInnerMessageProvider>();

        provider.Should().BeOfType<ResXMessageProvider>();
        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("en"), CancellationToken.None)).Should().Be("Hello from ResX");
        ReferenceEquals(serviceProvider.GetRequiredService<IInnerMessageProvider>(), provider).Should().BeTrue();
    }

    /// <summary>
    /// Provides mock.
    /// </summary>
    /// <param name="template">The message template.</param>
    private sealed class MockProvider(string? template = null) : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>(template ?? "MockResult");
    }

    /// <summary>
    /// Provides culture keyed.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class CultureKeyedProvider(IReadOnlyDictionary<string, string> templates) : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(templates.TryGetValue(culture.Name, out var template) ? template : null);
    }

    /// <summary>
    /// Represents a custom localizer.
    /// </summary>
    private sealed class CustomLocalizer : IMessageLocalizer {
        /// <summary>
        /// Performs the localize operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize.</returns>
        public ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>("Custom");

        /// <summary>
        /// Performs the localize operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize.</returns>
        public ValueTask<string?> LocalizeAsync(string key, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
            => LocalizeAsync(key, parameters, CultureInfo.CurrentUICulture, cancellationToken);

        /// <summary>
        /// Performs the localize plural operation.
        /// </summary>
        /// <param name="singularKey">The singular key.</param>
        /// <param name="pluralKey">The plural key.</param>
        /// <param name="count">The count.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize plural.</returns>
        public ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>("Custom");

        /// <summary>
        /// Performs the localize plural operation.
        /// </summary>
        /// <param name="singularKey">The singular key.</param>
        /// <param name="pluralKey">The plural key.</param>
        /// <param name="count">The count.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize plural.</returns>
        public ValueTask<string?> LocalizePluralAsync(string singularKey, string pluralKey, decimal count, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
            => LocalizePluralAsync(singularKey, pluralKey, count, parameters, CultureInfo.CurrentUICulture, cancellationToken);

        /// <summary>
        /// Performs the localize with context operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="context">The context.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize with context.</returns>
        public ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<string?>("Custom");

        /// <summary>
        /// Performs the localize with context operation.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="context">The context.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is localize with context.</returns>
        public ValueTask<string?> LocalizeWithContextAsync(string key, string context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
            => LocalizeWithContextAsync(key, context, parameters, CultureInfo.CurrentUICulture, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
