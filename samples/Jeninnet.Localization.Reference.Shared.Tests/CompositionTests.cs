//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the composition tests.
/// </summary>
[TestClass]
public sealed class CompositionTests {
    private static readonly string[] _knownProviderNames = [
        "Memory", "Json", "Xml", "ResX", "SQLite", "SqlServer", "PostgreSql", "MySql", "AzureTranslation", "GoogleTranslation"
    ];

    /// <summary>
    /// Verifies that json provider resolves through the full fallback chain.
    /// </summary>
    [TestMethod]
    public async Task JsonProvider_ResolvesThroughTheFullFallbackChainAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        await using var services = BuildComposition(ReferenceProviderNames.JSON, temp.Path);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var emptyParameters = new Dictionary<string, object?>();

        var regionalNote = await localizer.LocalizeAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);
        Assert.AreNotEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);

        var showcase = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], showcase);

        var regionalDescription = await localizer.LocalizeAsync(ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION], regionalDescription);

        var unregisteredCulture = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("fr"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], unregisteredCulture);

        var missing = await localizer.LocalizeAsync(ReferenceKeys.DEMO_MISSING_EVERYWHERE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.IsNull(missing);
    }

    /// <summary>
    /// Verifies that xml provider resolves through the fallback chain.
    /// </summary>
    [TestMethod]
    public async Task XmlProvider_ResolvesThroughTheFallbackChainAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        await using var services = BuildComposition(ReferenceProviderNames.XML, temp.Path);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var emptyParameters = new Dictionary<string, object?>();

        var regionalNote = await localizer.LocalizeAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);

        var showcase = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], showcase);

        var missing = await localizer.LocalizeAsync(ReferenceKeys.DEMO_MISSING_EVERYWHERE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.IsNull(missing);
    }

    /// <summary>
    /// Verifies that res XProvider resolves through the fallback chain.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_ResolvesThroughTheFallbackChainAsync() {
        await using var services = BuildComposition(ReferenceProviderNames.RES_X, string.Empty);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var emptyParameters = new Dictionary<string, object?>();

        var english = await localizer.LocalizeAsync(ReferenceKeys.APPLICATION_TITLE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.APPLICATION_TITLE], english);

        var regionalNote = await localizer.LocalizeAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);
        Assert.AreNotEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);

        var showcase = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], showcase);

        var regionalDescription = await localizer.LocalizeAsync(ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION], regionalDescription);

        var unregisteredCulture = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("fr"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], unregisteredCulture);

        var missing = await localizer.LocalizeAsync(ReferenceKeys.DEMO_MISSING_EVERYWHERE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.IsNull(missing);
    }

    /// <summary>
    /// Verifies that sqlite provider resolves through the fallback chain.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_ResolvesThroughTheFallbackChainAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        await using var services = BuildComposition(ReferenceProviderNames.SQLITE, temp.Path);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var emptyParameters = new Dictionary<string, object?>();

        var english = await localizer.LocalizeAsync(ReferenceKeys.APPLICATION_TITLE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.APPLICATION_TITLE], english);

        var regionalNote = await localizer.LocalizeAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);
        Assert.AreNotEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);

        var showcase = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], showcase);

        var regionalDescription = await localizer.LocalizeAsync(ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("ar-SY")[ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION], regionalDescription);

        var unregisteredCulture = await localizer.LocalizeAsync(ReferenceKeys.DEMO_SHOWCASE, emptyParameters, new CultureInfo("fr"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates("en")[ReferenceKeys.DEMO_SHOWCASE], unregisteredCulture);

        var missing = await localizer.LocalizeAsync(ReferenceKeys.DEMO_MISSING_EVERYWHERE, emptyParameters, new CultureInfo("en"), TestContext.CancellationToken);
        Assert.IsNull(missing);
    }

    /// <summary>
    /// Verifies that sqlite provider formats messages through the canonical pipeline.
    /// </summary>
    [TestMethod]
    public async Task SqliteProvider_FormatsMessagesThroughTheCanonicalPipelineAsync() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        await using var services = BuildComposition(ReferenceProviderNames.SQLITE, temp.Path);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var originalCulture = CultureInfo.CurrentCulture;

        try {
            // The canonical formatter substitutes values under the current culture, so pin it for a
            // deterministic expectation on any developer machine.
            CultureInfo.CurrentCulture = new CultureInfo(ReferenceCultures.DEFAULT_CULTURE);

            var orderMessage = await localizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                new Dictionary<string, object?> { ["0"] = "ORD-0001", ["1"] = 2, ["2"] = 39.98m },
                new CultureInfo(ReferenceCultures.DEFAULT_CULTURE), TestContext.CancellationToken);

            var expected = string.Format(
                CultureInfo.GetCultureInfo(ReferenceCultures.DEFAULT_CULTURE),
                LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE)[ReferenceKeys.ORDER_PLACED_MESSAGE],
                "ORD-0001",
                2,
                39.98m);
            Assert.AreEqual(expected, orderMessage, "The SQLite provider must feed the stored template unchanged into the canonical formatter.");
        }
        finally {
            CultureInfo.CurrentCulture = originalCulture;
        }

        Assert.AreEqual(originalCulture, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Verifies that memory provider is ACulture agnostic single culture demonstration.
    /// </summary>
    [TestMethod]
    public async Task MemoryProvider_IsACultureAgnosticSingleCultureDemonstrationAsync() {
        await using var services = BuildComposition(ReferenceProviderNames.MEMORY, string.Empty);
        var localizer = services.GetRequiredService<IMessageLocalizer>();
        var emptyParameters = new Dictionary<string, object?>();

        var regionalNote = await localizer.LocalizeAsync(ReferenceKeys.DEMO_REGIONAL_NOTE, emptyParameters, new CultureInfo("ar-SY"), TestContext.CancellationToken);
        Assert.AreEqual(LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE)[ReferenceKeys.DEMO_REGIONAL_NOTE], regionalNote);
    }

    /// <summary>
    /// Verifies that bridge string localizer resolves from the store under current ui culture.
    /// </summary>
    [TestMethod]
    public void BridgeStringLocalizer_ResolvesFromTheStoreUnderCurrentUiCulture() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var originalCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            using var services = BuildComposition(ReferenceProviderNames.JSON, temp.Path);
            var localizer = services.GetRequiredService<IStringLocalizer<Book>>();

            var title = localizer[ReferenceKeys.BOOK_PROPHET_TITLE];
            Assert.IsFalse(title.ResourceNotFound);
            Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_PROPHET_TITLE], title.Value);

            var missing = localizer[ReferenceKeys.DEMO_MISSING_EVERYWHERE];
            Assert.IsTrue(missing.ResourceNotFound);
            Assert.Contains("MISSING", missing.Value);
        }
        finally {
            CultureInfo.CurrentUICulture = originalCulture;
        }

        Assert.AreEqual(originalCulture, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Verifies that localized validation formats messages from the canonical pipeline.
    /// </summary>
    [TestMethod]
    public void LocalizedValidation_FormatsMessagesFromTheCanonicalPipeline() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        using var services = BuildComposition(ReferenceProviderNames.JSON, temp.Path);
        var originalCulture = CultureInfo.CurrentUICulture;

        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");

            var missingBookRequest = new CreateOrderRequest { Quantity = 2 };
            var missingBookResults = new List<ValidationResult>();
            var missingBookValid = Validator.TryValidateObject(
                missingBookRequest,
                new ValidationContext(missingBookRequest, services, null),
                missingBookResults,
                validateAllProperties: true);
            Assert.IsFalse(missingBookValid);
            Assert.AreEqual(LocalizationContent.GetTemplates("ar")[ReferenceKeys.BOOK_REQUIRED_VALIDATION], missingBookResults.Single().ErrorMessage);

            var outOfRangeRequest = new CreateOrderRequest { BookId = "book-001", Quantity = 0 };
            var outOfRangeResults = new List<ValidationResult>();
            var outOfRangeValid = Validator.TryValidateObject(
                outOfRangeRequest,
                new ValidationContext(outOfRangeRequest, services, null),
                outOfRangeResults,
                validateAllProperties: true);
            Assert.IsFalse(outOfRangeValid);
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, LocalizationContent.GetTemplates("ar")[ReferenceKeys.QUANTITY_RANGE_VALIDATION], OrderLimits.MIN_QUANTITY, OrderLimits.MAX_QUANTITY),
                outOfRangeResults.Single().ErrorMessage);
        }
        finally {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    /// <summary>
    /// Verifies that register reference store can be is disabled.
    /// </summary>
    [TestMethod]
    public void RegisterReferenceStore_CanBeDisabled() {
        using var services = BuildComposition(ReferenceProviderNames.JSON, string.Empty, registerStore: false);

        Assert.ThrowsExactly<InvalidOperationException>(services.GetRequiredService<ILocalizationStore>);
    }

    /// <summary>
    /// Verifies that add reference localization provider rejects unknown provider.
    /// </summary>
    [TestMethod]
    public void AddReferenceLocalizationProvider_RejectsUnknownProvider() => _ = Assert.ThrowsExactly<ArgumentException>(() =>
            new ServiceCollection().AddReferenceLocalizationProvider(options => options.Provider = "Bogus"));

    /// <summary>
    /// Verifies that every translation provider requires an api key.
    /// </summary>
    [TestMethod]
    public void EveryTranslationProvider_RequiresAnApiKey() {
        var expectedKeys = new Dictionary<string, string>(StringComparer.Ordinal) {
            [ReferenceProviderNames.AZURE_TRANSLATION] = "ReferenceLocalization:AzureTranslation:ApiKey",
            [ReferenceProviderNames.GOOGLE_TRANSLATION] = "ReferenceLocalization:GoogleTranslation:ApiKey",
        };

        foreach(var (provider, key) in expectedKeys) {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddReferenceLocalizationProvider(options => options.Provider = provider));

            Assert.IsTrue(exception.Message.Contains(key, StringComparison.Ordinal), provider);
        }
    }

    /// <summary>
    /// Verifies that translation providers register exactly one provider when an api key is configured.
    /// </summary>
    [TestMethod]
    public void TranslationProviders_RegisterExactlyOneProvider_WhenAnApiKeyIsConfigured() {
        using var azure = BuildComposition(ReferenceProviderNames.AZURE_TRANSLATION, string.Empty, translationApiKey: "placeholder-key");
        using var google = BuildComposition(ReferenceProviderNames.GOOGLE_TRANSLATION, string.Empty, translationApiKey: "placeholder-key");

        var azureProviders = azure.GetServices<IInnerMessageProvider>().ToArray();
        Assert.HasCount(1, azureProviders);
        Assert.IsInstanceOfType<AzureTranslationProvider>(azureProviders[0]);

        var googleProviders = google.GetServices<IInnerMessageProvider>().ToArray();
        Assert.HasCount(1, googleProviders);
        Assert.IsInstanceOfType<GoogleTranslationProvider>(googleProviders[0]);
    }

    /// <summary>
    /// Verifies that translation providers configuration shape drives the production registration.
    /// </summary>
    [TestMethod]
    public void TranslationProviders_ConfigurationShape_DrivesTheProductionRegistration() {
        using var azure = BuildComposition(
            ReferenceProviderNames.AZURE_TRANSLATION,
            string.Empty,
            options => {
                options.AzureTranslation.ApiKey = "placeholder-key";
                options.AzureTranslation.Region = "global";
                options.AzureTranslation.FromLanguage = "en";
            });
        using var google = BuildComposition(
            ReferenceProviderNames.GOOGLE_TRANSLATION,
            string.Empty,
            options => {
                options.GoogleTranslation.ApiKey = "placeholder-key";
                options.GoogleTranslation.Endpoint = "https://translation.googleapis.com/language/translate/v2";
            });

        var azureProvider = azure.GetRequiredService<IInnerMessageProvider>();
        var googleProvider = google.GetRequiredService<IInnerMessageProvider>();

        Assert.IsInstanceOfType<AzureTranslationProvider>(azureProvider);
        Assert.IsInstanceOfType<GoogleTranslationProvider>(googleProvider);

        // Azure participates in the capability model; Google honestly reports none.
        var azureCapabilities = azureProvider as IProviderCapabilities;
        Assert.IsNotNull(azureCapabilities);
        Assert.IsTrue(azureCapabilities.Supports(ProviderFeature.Caching));
        Assert.IsFalse(((IProviderCapabilities)googleProvider).Supports(ProviderFeature.Caching));
    }

    /// <summary>
    /// Verifies that translation providers resolve deterministically without an external service.
    /// </summary>
    [TestMethod]
    public async Task TranslationProviders_ResolveDeterministicallyWithoutAnExternalServiceAsync() {
        await using var azure = BuildComposition(ReferenceProviderNames.AZURE_TRANSLATION, string.Empty, translationApiKey: "placeholder-key");
        await using var google = BuildComposition(ReferenceProviderNames.GOOGLE_TRANSLATION, string.Empty, translationApiKey: "placeholder-key");

        var azureProvider = azure.GetRequiredService<IInnerMessageProvider>();
        var googleProvider = google.GetRequiredService<IInnerMessageProvider>();

        // Invariant-culture lookups resolve to null without any HTTP work (production contract).
        Assert.IsNull(await azureProvider.GetMessageTemplateAsync(ReferenceKeys.CATALOG_HEADING, CultureInfo.InvariantCulture, CancellationToken.None), "Azure invariant culture must resolve to null without a call.");
        Assert.IsNull(await googleProvider.GetMessageTemplateAsync(ReferenceKeys.CATALOG_HEADING, CultureInfo.InvariantCulture, CancellationToken.None), "Google invariant culture must resolve to null without a call.");

        // A pre-cancelled request faults with OperationCanceledException before any HTTP work.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            azureProvider.GetMessageTemplateAsync(ReferenceKeys.CATALOG_HEADING, new CultureInfo("ar"), cancelled.Token).AsTask());
        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            googleProvider.GetMessageTemplateAsync(ReferenceKeys.CATALOG_HEADING, new CultureInfo("ar"), cancelled.Token).AsTask());
    }

    /// <summary>
    /// Verifies that reference localization options keeps json as the unchanged default selection.
    /// </summary>
    [TestMethod]
    public void ReferenceLocalizationOptions_KeepsJsonAsTheUnchangedDefaultSelection() {
        var options = new ReferenceLocalizationOptions();

        Assert.AreEqual(ReferenceProviderNames.JSON, options.Provider);
        Assert.DoesNotContain(options.Provider, ReferenceProviderNames.CredentialGatedProviders, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that every server provider requires AConnection string.
    /// </summary>
    [TestMethod]
    public void EveryServerProvider_RequiresAConnectionString() {
        var expectedKeys = new Dictionary<string, string>(StringComparer.Ordinal) {
            [ReferenceProviderNames.SQL_SERVER] = "ReferenceLocalization:SqlServer:ConnectionString",
            [ReferenceProviderNames.POSTGRE_SQL] = "ReferenceLocalization:PostgreSql:ConnectionString",
            [ReferenceProviderNames.MY_SQL] = "ReferenceLocalization:MySql:ConnectionString",
        };

        foreach(var (provider, key) in expectedKeys) {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
                new ServiceCollection().AddReferenceLocalizationProvider(options => options.Provider = provider));

            Assert.Contains(key, exception.Message, provider);
        }
    }

    /// <summary>
    /// Verifies that reference provider names partition every known provider without overlap.
    /// </summary>
    [TestMethod]
    public void ReferenceProviderNames_PartitionEveryKnownProviderWithoutOverlap() {
        var supported = ReferenceProviderNames.SupportedProviders;
        var gated = ReferenceProviderNames.InfrastructureGatedProviders;
        var credentialGated = ReferenceProviderNames.CredentialGatedProviders;
        var deferred = ReferenceProviderNames.DeferredProviders;

        Assert.AreSequenceEqual(
            _knownProviderNames, [.. supported, .. gated, .. credentialGated, .. deferred], Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);

        Assert.IsEmpty(supported.Intersect(gated, StringComparer.Ordinal), "Build-safe and infrastructure-gated names must not overlap.");
        Assert.IsEmpty(supported.Intersect(credentialGated, StringComparer.Ordinal), "Build-safe and credential-gated names must not overlap.");
        Assert.IsEmpty(supported.Intersect(deferred, StringComparer.Ordinal), "Build-safe and deferred names must not overlap.");
        Assert.IsEmpty(gated.Intersect(credentialGated, StringComparer.Ordinal), "Infrastructure-gated and credential-gated names must not overlap.");
        Assert.IsEmpty(gated.Intersect(deferred, StringComparer.Ordinal), "Infrastructure-gated and deferred names must not overlap.");
        Assert.IsEmpty(credentialGated.Intersect(deferred, StringComparer.Ordinal), "Credential-gated and deferred names must not overlap.");
    }

    /// <summary>
    /// Verifies that add reference localization provider rejects ASecond registration.
    /// </summary>
    [TestMethod]
    public void AddReferenceLocalizationProvider_RejectsASecondRegistration() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var services = new ServiceCollection();
        services.AddReferenceLocalizationProvider(options => {
            options.Provider = ReferenceProviderNames.JSON;
            options.ContentRootPath = temp.Path;
        });

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddReferenceLocalizationProvider(options => {
            options.Provider = ReferenceProviderNames.JSON;
            options.ContentRootPath = temp.Path;
        }));
    }

    /// <summary>
    /// Verifies that add reference localization provider rejects pre registered provider.
    /// </summary>
    [TestMethod]
    public void AddReferenceLocalizationProvider_RejectsPreRegisteredProvider() {
        using var temp = TestPaths.CreateLocalizationDirectory();
        var services = new ServiceCollection();
        services.AddMemoryLocalization(LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE));

        _ = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddReferenceLocalizationProvider(options => {
            options.Provider = ReferenceProviderNames.JSON;
            options.ContentRootPath = temp.Path;
        }));
    }

    /// <summary>
    /// Builds composition.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="contentRootPath">The content root path.</param>
    /// <param name="registerStore">The register store.</param>
    /// <param name="translationApiKey">The translation api key.</param>
    /// <returns>The composition result.</returns>
    private static Microsoft.Extensions.DependencyInjection.ServiceProvider BuildComposition(
        string provider,
        string contentRootPath,
        bool registerStore = true,
        string? translationApiKey = null) {
        var services = new ServiceCollection();
        services.AddReferenceLocalizationProvider(options => {
            options.Provider = provider;
            if(!string.IsNullOrEmpty(contentRootPath)) {
                options.ContentRootPath = contentRootPath;
                options.SqliteDataDirectory = Path.Combine(contentRootPath, "Data");
            }

            options.RegisterReferenceStore = registerStore;

            if(!string.IsNullOrEmpty(translationApiKey)) {
                options.AzureTranslation.ApiKey = translationApiKey;
                options.GoogleTranslation.ApiKey = translationApiKey;
            }
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds composition.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="contentRootPath">The content root path.</param>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <returns>The composition result.</returns>
    private static Microsoft.Extensions.DependencyInjection.ServiceProvider BuildComposition(
        string provider,
        string contentRootPath,
        Action<ReferenceLocalizationOptions> configure) {
        var services = new ServiceCollection();
        services.AddReferenceLocalizationProvider(options => {
            options.Provider = provider;
            if(!string.IsNullOrEmpty(contentRootPath)) {
                options.ContentRootPath = contentRootPath;
                options.SqliteDataDirectory = Path.Combine(contentRootPath, "Data");
            }

            configure(options);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
