//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Hosts.Desktop.Tests;

/// <summary>
/// Verifies that every desktop reference host composition registers the canonical reference stack
/// through its single composition root: exactly one inner message provider, a working migration
/// bridge, and eager rejection of invalid or duplicated localizations.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class DesktopHostCompositionTests {
    private static readonly string[] _hosts = ["WinForms", "Wpf", "BlazorHybrid"];

    /// <summary>
    /// Verifies that every desktop host composition registers exactly one inner message provider.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_RegistersExactlyOneInnerMessageProvider() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();

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
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        await using var json = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();
        await using var xml = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.XML, temp.Path).BuildServiceProvider();
        await using var memory = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.MEMORY, string.Empty).BuildServiceProvider();
        await using var resx = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.RES_X, string.Empty).BuildServiceProvider();
        await using var sqlite = Compose(new ServiceCollection(), "WinForms", ReferenceProviderNames.SQLITE, temp.Path).BuildServiceProvider();

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
    /// Verifies that every desktop host composition resolves the canonical pipeline services.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_ResolvesTheCanonicalPipelineServices() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            using var services = Compose(new ServiceCollection(), host, ReferenceProviderNames.JSON, temp.Path).BuildServiceProvider();

            Assert.IsNotNull(services.GetService<IMessageLocalizer>(), host);
            Assert.IsNotNull(services.GetService<IMessageFormatter>(), host);
            Assert.IsNotNull(services.GetService<ILocalizationStore>(), host);
            Assert.IsNotNull(services.GetService<IStringLocalizer<CreateOrderRequest>>(), host);
        }
    }

    /// <summary>
    /// Verifies that blazor hybrid composition registers the jeninnet static culture composition.
    /// </summary>
    [TestMethod]
    public void BlazorHybridComposition_RegistersTheJeninnetStaticCultureComposition() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
        var services = Compose(new ServiceCollection(), "BlazorHybrid", ReferenceProviderNames.JSON, temp.Path);

        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(BlazorCultureService) && descriptor.Lifetime == ServiceLifetime.Scoped, services,
            "the Blazor Hybrid host resolves the culture service from the WebView scope so its JavaScript interop has a WebView context");
        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(IBlazorCultureStore) &&
                descriptor.ImplementationType == typeof(BlazorLocalStorageCultureStore) &&
                descriptor.Lifetime == ServiceLifetime.Scoped,
            services,
            "the Blazor Hybrid host wires the local-storage culture store in the WebView scope");
    }

    /// <summary>
    /// Verifies that bridge string localizer is culture aware through every desktop host composition.
    /// </summary>
    [TestMethod]
    public void BridgeStringLocalizer_IsCultureAwareThroughEveryDesktopHostComposition() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();
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
    /// Verifies that memory provider stays culture agnostic in every desktop host.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_StaysCultureAgnosticInEveryDesktopHostAsync() {
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
    /// Verifies that every desktop host composition rejects unknown providers and is empty when translation is no longer is deferred.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_RejectsUnknownProvidersAndIsEmptyWhenTranslationIsNoLongerDeferred() {
        foreach(var host in _hosts) {
            _ = Assert.ThrowsExactly<ArgumentException>(() => Compose(new ServiceCollection(), host, "Bogus", string.Empty));
        }

        Assert.IsEmpty(ReferenceProviderNames.DeferredProviders, "Translation providers are credential-gated and must not be deferred.");
    }

    /// <summary>
    /// Verifies that every desktop host composition classifies server providers as configuration errors.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_ClassifiesServerProvidersAsConfigurationErrors() {
        // The desktop hosts consume no server credentials (NOT APPROPRIATE in the host matrix), so a
        // server provider selection is a configuration error rather than a supported composition.
        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.InfrastructureGatedProviders) {
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    Compose(new ServiceCollection(), host, provider, string.Empty));
            }
        }
    }

    /// <summary>
    /// Verifies that every desktop host composition classifies translation providers as configuration errors.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_ClassifiesTranslationProvidersAsConfigurationErrors() {
        // The desktop hosts expose no translation credential path (NOT APPROPRIATE in the host matrix),
        // so a translation provider selection without an API key is a configuration error.
        foreach(var host in _hosts) {
            foreach(var provider in ReferenceProviderNames.CredentialGatedProviders) {
                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    Compose(new ServiceCollection(), host, provider, string.Empty));
            }
        }
    }

    /// <summary>
    /// Verifies that every desktop host composition rejects ASecond composition.
    /// </summary>
    [TestMethod]
    public void EveryDesktopHostComposition_RejectsASecondComposition() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();

        foreach(var host in _hosts) {
            var services = new ServiceCollection();
            _ = Compose(services, host, ReferenceProviderNames.JSON, temp.Path);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => Compose(services, host, ReferenceProviderNames.JSON, temp.Path));
        }
    }

    /// <summary>
    /// Verifies that sqlite selection registers only the sqlite inner provider across desktop hosts.
    /// </summary>
    [TestMethod]
    public void SqliteSelection_RegistersOnlyTheSqliteInnerProviderAcrossDesktopHosts() {
        using var temp = DesktopHostTestPaths.CreateLocalizationDirectory();

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
    /// <returns>The compose result.</returns>
    private static IServiceCollection Compose(IServiceCollection services, string host, string provider, string contentRootPath) {
        void Configure(ReferenceLocalizationOptions options) {
            options.Provider = provider;
            if(!string.IsNullOrEmpty(contentRootPath)) {
                options.ContentRootPath = contentRootPath;
                options.SqliteDataDirectory = Path.Combine(contentRootPath, "Data");
            }
        }

        return host switch {
            "WinForms" => services.AddReferenceWinForms(Configure),
            "Wpf" => services.AddReferenceWpf(Configure),
            "BlazorHybrid" => services.AddReferenceBlazorHybrid(Configure),
            _ => throw new InvalidOperationException($"Unknown reference desktop host '{host}'."),
        };
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
