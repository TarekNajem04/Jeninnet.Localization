//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Hosts.Tests;

/// <summary>
/// Verifies that every reference host composition registers the canonical reference stack through
/// its single composition root: exactly one inner message provider, a working migration bridge, and
/// eager rejection of invalid or duplicated localizations.
/// </summary>
[TestClass]
public sealed class HostCompositionTests {
    private static readonly string[] _hosts = ["Api", "Mvc", "Blazor"];

    /// <summary>
    /// Verifies that every host composition registers exactly one inner message provider.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RegistersExactlyOneInnerMessageProvider() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.SupportedProviders) {
                using var services = Compose(new ServiceCollection(), host, provider, temp.Path).BuildServiceProvider();

                Assert.HasCount(1, services.GetServices<IInnerMessageProvider>(), $"Host {host} with provider {provider}.");
            }
        }
    }

    /// <summary>
    /// Verifies that provider selection changes the inner message provider implementation.
    /// </summary>
    [TestMethod]
    public async Task ProviderSelection_ChangesTheInnerMessageProviderImplementationAsync() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();
        await using var json = Compose(new ServiceCollection(), "Api", ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
        await using var xml = Compose(new ServiceCollection(), "Api", ReferenceProviderNames.XML, temp.Path).BuildServiceProvider();
        await using var memory = Compose(new ServiceCollection(), "Api", ReferenceProviderNames.MEMORY, string.Empty).BuildServiceProvider();
        await using var resx = Compose(new ServiceCollection(), "Api", ReferenceProviderNames.RES_X, string.Empty).BuildServiceProvider();
        await using var sqlite = Compose(new ServiceCollection(), "Api", ReferenceProviderNames.SQLITE, temp.Path).BuildServiceProvider();

        var jsonProvider = json.GetRequiredService<IInnerMessageProvider>();
        var xmlProvider = xml.GetRequiredService<IInnerMessageProvider>();
        var memoryProvider = memory.GetRequiredService<IInnerMessageProvider>();
        var resxProvider = resx.GetRequiredService<IInnerMessageProvider>();
        var sqliteProvider = sqlite.GetRequiredService<IInnerMessageProvider>();

        // File providers are dispatched through the culture-aware wrapper, while the in-memory table
        // provider remains a plain culture-agnostic implementation.
        Assert.IsInstanceOfType<CultureAwareMessageProvider>(jsonProvider);
        Assert.IsInstanceOfType<CultureAwareMessageProvider>(xmlProvider);
        Assert.IsInstanceOfType<MemoryMessageProvider>(memoryProvider);
        Assert.IsInstanceOfType<ResXMessageProvider>(resxProvider);
        Assert.IsInstanceOfType<SqliteMessageProvider>(sqliteProvider);

        // Each format resolves the same key to the matching per-culture content.
        var culture = new CultureInfo("ar");
        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await jsonProvider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, culture, TestContext.CancellationToken));
        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await xmlProvider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, culture, TestContext.CancellationToken));
        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await resxProvider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, culture, TestContext.CancellationToken));
        Assert.AreEqual(
            LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE],
            await sqliteProvider.GetMessageTemplateAsync(ReferenceKeys.BOOK_PROPHET_TITLE, culture, TestContext.CancellationToken));
    }

    /// <summary>
    /// Verifies that every host composition resolves the canonical pipeline services.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_ResolvesTheCanonicalPipelineServices() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();

            Assert.IsNotNull(services.GetService<IMessageLocalizer>(), host);
            Assert.IsNotNull(services.GetService<IMessageFormatter>(), host);
            Assert.IsNotNull(services.GetService<ILocalizationStore>(), host);
            Assert.IsNotNull(services.GetService<IStringLocalizer<CreateOrderRequest>>(), host);
        }
    }

    /// <summary>
    /// Verifies that mvc composition registers the jeninnet view localizer.
    /// </summary>
    [TestMethod]
    public void MvcComposition_RegistersTheJeninnetViewLocalizer() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();
        using var services = Compose(new ServiceCollection(), "Mvc", ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();

        var viewLocalizer = services.GetRequiredService<IViewLocalizer>();

        Assert.IsInstanceOfType<ViewMessageLocalizer>(viewLocalizer,
            "the MVC host converts its Razor views to the view-scoped Jeninnet bridge");
    }

    /// <summary>
    /// Verifies that blazor server composition registers the jeninnet circuit culture composition.
    /// </summary>
    [TestMethod]
    public void BlazorServerComposition_RegistersTheJeninnetCircuitCultureComposition() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();
        var services = Compose(new ServiceCollection(), "Blazor", ReferenceProviderNames.JSON, temp.Path);

        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(BlazorCultureService) && descriptor.Lifetime == ServiceLifetime.Scoped, services,
            "the Blazor Server host registers the circuit-scoped Jeninnet culture service");
        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(CircuitHandler) && descriptor.ImplementationType == typeof(BlazorCircuitCultureHandler), services,
            "the circuit culture handler is registered in the CircuitHandler enumerable");
        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(IBlazorCultureStore) &&
                descriptor.ImplementationType == typeof(BlazorCookieCultureStore) &&
                descriptor.Lifetime == ServiceLifetime.Scoped,
            services,
            "the Blazor Server host wires the cookie culture store as scoped because it uses the circuit-scoped IJSRuntime");
    }

    /// <summary>
    /// Verifies that bridge string localizer is culture aware through every host composition.
    /// </summary>
    [TestMethod]
    public void BridgeStringLocalizer_IsCultureAwareThroughEveryHostComposition() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            foreach(var host in _hosts) {
                using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
                var localizer = services.GetRequiredService<IStringLocalizer<CreateOrderRequest>>();

                var value = localizer[ReferenceKeys.CATALOG_HEADING].Value;
                Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.CATALOG_HEADING], value, host);
            }
        }
        finally {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.AreEqual(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Verifies that memory provider stays culture agnostic in every host.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_StaysCultureAgnosticInEveryHostAsync() {
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            foreach(var host in _hosts) {
                await using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.MEMORY, string.Empty).BuildServiceProvider();
                var localizer = services.GetRequiredService<IMessageLocalizer>();

                var value = await localizer.LocalizeAsync(
                    ReferenceKeys.CATALOG_HEADING,
                    new Dictionary<string, object?>(),
                    new CultureInfo("ar"), TestContext.CancellationToken);
                Assert.AreEqual(
                    LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE)[ReferenceKeys.CATALOG_HEADING],
                    value,
                    host);
            }
        }
        finally {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        Assert.AreEqual(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Verifies that every host composition rejects unknown providers and is empty when translation is no longer is deferred.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RejectsUnknownProvidersAndIsEmptyWhenTranslationIsNoLongerDeferred() {
        foreach(var host in _hosts) {
            _ = Assert.ThrowsExactly<ArgumentException>(() => Compose(new ServiceCollection(), host, "Bogus", string.Empty));
        }

        Assert.IsEmpty(ReferenceProviderNames.DeferredProviders, "Translation providers are credential-gated and must not be deferred.");
    }

    /// <summary>
    /// Verifies that every host composition requires an api key for translation providers.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RequiresAnApiKeyForTranslationProviders() {
        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.CredentialGatedProviders) {
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    Compose(new ServiceCollection(), host, provider, string.Empty));
            }
        }
    }

    /// <summary>
    /// Verifies that every host composition registers exactly one translation provider when api key is configured.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RegistersExactlyOneTranslationProviderWhenApiKeyIsConfigured() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.CredentialGatedProviders) {
                using var services = Compose(new ServiceCollection(), host, provider, temp.Path, "placeholder-key").BuildServiceProvider();
                var providers = services.GetServices<IInnerMessageProvider>().ToArray();

                Assert.HasCount(1, providers, $"Host {host} with provider {provider}.");

                if(provider == ReferenceProviderNames.AZURE_TRANSLATION) {
                    Assert.IsInstanceOfType<AzureTranslationProvider>(providers[0], host);
                } else {
                    Assert.IsInstanceOfType<GoogleTranslationProvider>(providers[0], host);
                }

                Assert.IsNotNull(services.GetService<IMessageLocalizer>(), host);
                Assert.IsNotNull(services.GetService<IMessageFormatter>(), host);
                Assert.IsNotNull(services.GetService<ILocalizationStore>(), host);
                Assert.IsNotNull(services.GetService<IStringLocalizer<CreateOrderRequest>>(), host);
            }
        }
    }

    /// <summary>
    /// Verifies that every host composition requires AConnection string for server providers.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RequiresAConnectionStringForServerProviders() {
        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.InfrastructureGatedProviders) {
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    Compose(new ServiceCollection(), host, provider, string.Empty));
            }
        }
    }

    /// <summary>
    /// Verifies that every host composition rejects ASecond composition.
    /// </summary>
    [TestMethod]
    public void EveryHostComposition_RejectsASecondComposition() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            var services = new ServiceCollection();
            _ = Compose(services, host, ReferenceProviderNames.JSON, temp.Path);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => Compose(services, host, ReferenceProviderNames.JSON, temp.Path));
        }
    }

    /// <summary>
    /// Verifies that sqlite selection registers only the sqlite inner provider across hosts.
    /// </summary>
    [TestMethod]
    public void SqliteSelection_RegistersOnlyTheSqliteInnerProviderAcrossHosts() {
        using var temp = HostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.SQLITE, temp.Path).BuildServiceProvider();

            var providers = services.GetServices<IInnerMessageProvider>().ToArray();
            Assert.HasCount(1, providers, host);
            Assert.IsInstanceOfType<SqliteMessageProvider>(providers[0], host);
            Assert.IsFalse(providers[0] is MemoryMessageProvider, host);
            Assert.IsFalse(providers[0] is ResXMessageProvider, host);

            Assert.IsNotNull(services.GetService<IMessageLocalizer>(), host);
            Assert.IsNotNull(services.GetService<IMessageFormatter>(), host);
            Assert.IsNotNull(services.GetService<ILocalizationStore>(), host);
            Assert.IsNotNull(services.GetService<IStringLocalizer<CreateOrderRequest>>(), host);
        }
    }

    /// <summary>
    /// Performs the compose operation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="host">The host.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="contentRootPath">The content root path.</param>
    /// <param name="translationApiKey">The translation api key.</param>
    /// <returns>The compose result.</returns>
    private static IServiceCollection Compose(IServiceCollection services, string host, string provider, string contentRootPath, string? translationApiKey = null) {
        void Configure(ReferenceLocalizationOptions options) {
            options.Provider = provider;
            if(!string.IsNullOrEmpty(contentRootPath)) {
                options.ContentRootPath = contentRootPath;
                options.SqliteDataDirectory = Path.Combine(contentRootPath, "Data");
            }

            if(!string.IsNullOrEmpty(translationApiKey)) {
                options.AzureTranslation.ApiKey = translationApiKey;
                options.GoogleTranslation.ApiKey = translationApiKey;
            }
        }

        return host switch {
            "Api" => services.AddReferenceApi(Configure),
            "Mvc" => services.AddReferenceMvc(Configure),
            "Blazor" => services.AddReferenceBlazor(Configure),
            _ => throw new InvalidOperationException($"Unknown reference host '{host}'."),
        };
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
