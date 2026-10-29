//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the integration composition tests.
/// </summary>
[TestClass]
public class IntegrationCompositionTests {
    /// <summary>
    /// Verifies that add localization composes parameterless pipeline end to end.
    /// </summary>
    [TestMethod]
    public async Task AddLocalization_ComposesParameterlessPipeline_EndToEndAsync() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> { ["Greeting"] = "Hello, {Name}!" });
        CoreJeninnetLocalizationExtensions.AddLocalization(services);

        await using var provider = services.BuildServiceProvider();

        var localizer = provider.GetRequiredService<IMessageLocalizer>();
        var formatter = provider.GetRequiredService<IMessageFormatter>();
        var messageProvider = provider.GetRequiredService<IMessageProvider>();
        var options = provider.GetRequiredService<LocalizationOptions>();

        var result = await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?> { ["Name"] = "John" }, new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Hello, John!");
        localizer.Should().BeOfType<DefaultMessageLocalizer>();
        formatter.Should().BeOfType<DefaultMessageFormatter>();
        messageProvider.Should().BeOfType<LocalizationProviderSelector>();
        options.DefaultCulture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that add localization configure shares singleton identity and captures option snapshot at construction.
    /// </summary>
    [TestMethod]
    public async Task AddLocalization_Configure_SharesSingletonIdentityAndCapturesOptionSnapshotAtConstructionAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new CultureSensitiveProvider(new Dictionary<string, string> { ["en"] = "Expected" }));
        services.AddLocalization(static options => options.DefaultCulture = "en");

        await using var provider = services.BuildServiceProvider();

        var firstLocalizer = provider.GetRequiredService<IMessageLocalizer>();
        var secondLocalizer = provider.GetRequiredService<IMessageLocalizer>();
        var options = provider.GetRequiredService<LocalizationOptions>();

        ReferenceEquals(firstLocalizer, secondLocalizer).Should().BeTrue();
        ReferenceEquals(provider.GetRequiredService<LocalizationOptions>(), options).Should().BeTrue();

        var before = await firstLocalizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None);
        before.Should().Be("Expected", "the configured default culture must feed the fallback through the container");

        options.DefaultCulture = "de-DE";

        var after = await firstLocalizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None);
        after.Should().Be("Expected", "mutation of the resolved options object must not change the localizer's captured behavior");
    }

    /// <summary>
    /// Verifies that culture aware provider through composed localizer exact then iso then default fallback.
    /// </summary>
    [TestMethod]
    public async Task CultureAwareProvider_ThroughComposedLocalizer_ExactThenIsoThenDefaultFallbackAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> {
                ["fr-CA"] = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = new Dictionary<string, string> { ["Greeting"] = "Bonjour CA" } }),
                ["fr"] = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = new Dictionary<string, string> { ["Greeting"] = "Bonjour" } }),
                ["en"] = new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = new Dictionary<string, string> { ["Greeting"] = "Hello" } })
            }));
        services.AddLocalization(static options => options.DefaultCulture = "en");

        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        var exact = await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("fr-CA"), CancellationToken.None);
        exact.Should().Be("Bonjour CA");

        var iso = await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("fr-CH"), CancellationToken.None);
        iso.Should().Be("Bonjour");

        var fallback = await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("de-CH"), CancellationToken.None);
        fallback.Should().Be("Hello", "default-culture fallback is owned by the localizer and must reach the exact-match provider for the default culture");
    }

    /// <summary>
    /// Verifies that cached store through factory reuses one cached layer across resolutions.
    /// </summary>
    [TestMethod]
    public void CachedStore_ThroughFactory_ReusesOneCachedLayerAcrossResolutions() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleResource", "en-US", "Welcome", "Hello");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IStringLocalizerFactory>();
        var first = factory.Create(typeof(SampleResource));
        var second = factory.Create(typeof(SampleResource));

        first["Welcome"].Value.Should().Be("Hello");
        second["Welcome"].Value.Should().Be("Hello");
        store.TotalInnerReadCount.Should().Be(1, "the cache layer must be shared across localizers created from the same pipeline");

        store.SetEntry("SampleResource", "en-US", "Welcome", "Mutated");

        var third = factory.Create(typeof(SampleResource));
        third["Welcome"].Value.Should().Be("Hello", "the same cached resource-set identity must remain in use after the initial load");
        store.TotalInnerReadCount.Should().Be(1);
    });

    /// <summary>
    /// Verifies that cached store through factory separates cultures and hides missing from enumeration.
    /// </summary>
    [TestMethod]
    public void CachedStore_ThroughFactory_SeparatesCulturesAndHidesMissingFromEnumeration() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleResource", "en-US", "Welcome", "Hello");
        store.SetEntry("SampleResource", "nl-NL", "Welcome", "Hallo");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        var english = factory.Create(typeof(SampleResource));
        english["Welcome"].Value.Should().Be("Hello");

        var missing = english["Absent"];
        missing.ResourceNotFound.Should().BeTrue();
        missing.Value.Should().Be("[MISSING:Absent]");

        var enumeration = english.GetAllStrings(includeParentCultures: false).Select(static s => s.Name).ToList();
        enumeration.Should().Contain("Welcome");
        enumeration.Should().NotContain("Absent");

        RunWithCurrentUiCulture("nl-NL", () => {
            var dutch = factory.Create(typeof(SampleResource));
            dutch["Welcome"].Value.Should().Be("Hallo");
        });

        english["Welcome"].Value.Should().Be("Hello", "the English resource set must never observe the Dutch value");
    });

    /// <summary>
    /// Verifies that cached store reload through container refreshes composed pipeline.
    /// </summary>
    [TestMethod]
    public async Task CachedStore_ReloadThroughContainer_RefreshesComposedPipelineAsync() {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleResource", "en-US", "Welcome", "before");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        await using var provider = services.BuildServiceProvider();

        await RunWithCurrentUiCultureAsync("en-US", async () => {
            var factory = provider.GetRequiredService<IStringLocalizerFactory>();
            var localizer = factory.Create(typeof(SampleResource));

            localizer["Welcome"].Value.Should().Be("before");
            store.SetEntry("SampleResource", "en-US", "Welcome", "after");

            var localizationStore = provider.GetRequiredService<ILocalizationStore>();
            await localizationStore.ReloadAsync(TestContext?.CancellationToken ?? CancellationToken.None);

            var reloaded = factory.Create(typeof(SampleResource));
            reloaded["Welcome"].Value.Should().Be("after");
        });

        store.ReloadCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that cached store failed reload through container propagates and keeps last known good.
    /// </summary>
    [TestMethod]
    public async Task CachedStore_FailedReloadThroughContainer_PropagatesAndKeepsLastKnownGoodAsync() {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleResource", "en-US", "Welcome", "before");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        await using var provider = services.BuildServiceProvider();

        await RunWithCurrentUiCultureAsync("en-US", async () => {
            var factory = provider.GetRequiredService<IStringLocalizerFactory>();
            factory.Create(typeof(SampleResource))["Welcome"].Value.Should().Be("before");

            store.ThrowOnReload = true;
            await ((Func<Task>)(() => provider.GetRequiredService<ILocalizationStore>().ReloadAsync(TestContext?.CancellationToken ?? CancellationToken.None))).Should().ThrowAsync<InvalidOperationException>();
            store.ThrowOnReload = false;

            store.SetEntry("SampleResource", "en-US", "Welcome", "mutated");
            factory.Create(typeof(SampleResource))["Welcome"].Value.Should().Be("before", "the last-known-good cache must survive the failed reload");
            store.TotalInnerReadCount.Should().Be(1);
        });
    }

    /// <summary>
    /// Verifies that cached store reload passes cancellation token to inner boundary.
    /// </summary>
    [TestMethod]
    public async Task CachedStore_Reload_PassesCancellationTokenToInnerBoundaryAsync() {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleResource", "en-US", "Welcome", "Hello");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        await using var provider = services.BuildServiceProvider();

        var localizationStore = provider.GetRequiredService<ILocalizationStore>();

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var act = async () => await localizationStore.ReloadAsync(cancellationSource.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        store.ReloadCallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that provider failure through composed localizer propagates to caller.
    /// </summary>
    [TestMethod]
    public async Task ProviderFailure_ThroughComposedLocalizer_PropagatesToCallerAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new ThrowingInnerProvider());
        CoreJeninnetLocalizationExtensions.AddLocalization(services);

        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        Func<Task> act = async () => await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that localized required attribute through composed pipeline uses localized message not hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughComposedPipeline_UsesLocalizedMessageNotHardcodedEnglish() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleModel", "en-US", "Required.Message", "Requis!");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute("Required.Message");
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new SampleModel(), provider, null);

        var result = attribute.GetValidationResult(null, validationContext);

        result.Should().NotBeNull();
        var errorMessage = result!.ErrorMessage;
        errorMessage.Should().Be("Requis!");
    });

    /// <summary>
    /// Verifies that localized required attribute through composed pipeline missing key uses missing marker not hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughComposedPipeline_MissingKeyUsesMissingMarkerNotHardcodedEnglish() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute("Unknown.Key");
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new SampleModel(), provider, null);

        var result = attribute.GetValidationResult(null, validationContext);

        result.Should().NotBeNull();
        var errorMessage = result!.ErrorMessage;
        errorMessage.Should().Be("[MISSING:Unknown.Key]");
    });

    /// <summary>
    /// Verifies that localized required attribute without localization services throws instead of hardcoded english.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_WithoutLocalizationServices_ThrowsInsteadOfHardcodedEnglish() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute("Required.Message");
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new SampleModel(), provider, null);

        var act = () => attribute.GetValidationResult(null, validationContext);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<SampleModel>");
    }

    /// <summary>
    /// Verifies that localized required attribute respects each validation current culture.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_RespectsEachValidationCurrentCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("SampleModel", "en-US", "Required.Message", "Required!");
        store.SetEntry("SampleModel", "fr", "Required.Message", "Requis!");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var attribute = new Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute("Required.Message");
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new SampleModel(), provider, null);

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var result = attribute.GetValidationResult(null, validationContext);
                Assert.IsNotNull(result);
                message = result.ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Required!");
        ValidateUnder("fr").Should().Be("Requis!");
    }

    /// <summary>
    /// Verifies that localized required attribute valid value returns success without services.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ValidValue_ReturnsSuccessWithoutServices() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var attribute = new Jeninnet.Localization.Integrations.DataAnnotations.LocalizedRequiredAttribute("Required.Message");
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(new SampleModel(), provider, null);

        var result = attribute.GetValidationResult("value", validationContext);

        result.Should().Be(System.ComponentModel.DataAnnotations.ValidationResult.Success);
    }

    /// <summary>
    /// Verifies that localized required attribute through framework validator valid value succeeds without services.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughFrameworkValidator_ValidValueSucceedsWithoutServices() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new ValidationModel { Name = "ok" }, provider, null);

        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeTrue(
            "a valid value must pass DataAnnotations validation without resolving localization services");
        results.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that localized required attribute through framework validator uses localized message per culture.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughFrameworkValidator_UsesLocalizedMessagePerCulture() {
        var store = new FakeLocalizationStore();
        store.SetEntry("ValidationModel", "en-US", "Required.Message", "Required!");
        store.SetEntry("ValidationModel", "fr", "Required.Message", "Requis!");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        string? ValidateUnder(string cultureName) {
            string? message = null;
            RunWithCurrentUiCulture(cultureName, () => {
                var results = new List<ValidationResult>();
                var context = new ValidationContext(new ValidationModel { Name = null }, provider, null);
                Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse(
                    "a missing required value must fail DataAnnotations validation");
                results.Should().ContainSingle(static _ => true, "the required-validation failure must produce exactly one validation result");
                message = results[0].ErrorMessage;
            });
            return message;
        }

        ValidateUnder("en-US").Should().Be("Required!");
        ValidateUnder("fr").Should().Be("Requis!");
    }

    /// <summary>
    /// Verifies that localized required attribute through framework validator missing resource uses missing marker.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughFrameworkValidator_MissingResourceUsesMissingMarker() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();

        var services = new ServiceCollection();
        using var cache = RegisterCachedStore(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new UnknownKeyModel(), provider, null);

        Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().ContainSingle(static _ => true);
        results[0].ErrorMessage.Should().Be("[MISSING:Unknown.Key]",
            "a missing localization resource must surface the canonical missing-key marker, never a hard-coded message");
    });

    /// <summary>
    /// Verifies that localized required attribute through framework validator missing localizer service throws configuration error.
    /// </summary>
    [TestMethod]
    public void LocalizedRequiredAttribute_ThroughFrameworkValidator_MissingLocalizerService_ThrowsConfigurationError() {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(new ValidationModel(), provider, null);

        var act = () => Validator.TryValidateObject(context.ObjectInstance, context, results, validateAllProperties: true);

        act.Should().Throw<InvalidOperationException>()
            .Exception.Message.Should().Contain("IStringLocalizer<ValidationModel>");
    }

    /// <summary>
    /// Verifies that custom string localizer factory bridge heals when store data becomes available.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerFactory_BridgeHealsWhenStoreDataBecomesAvailable() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        var factory = new CustomStringLocalizerFactory(store);
        var localizer = factory.Create(typeof(SampleModel));

        localizer["Required.Message"].ResourceNotFound.Should().BeTrue("an initial store miss surfaces the canonical missing state");

        store.SetEntry("SampleModel", "en-US", "Required.Message", "Requis!");

        var healed = localizer["Required.Message"];
        healed.ResourceNotFound.Should().BeFalse("the store-backed bridge re-resolves per lookup");
        healed.Value.Should().Be("Requis!");
    });

    /// <summary>
    /// Verifies that add localization composes microsoft bridge through dependency injection.
    /// </summary>
    [TestMethod]
    public void AddLocalization_ComposesMicrosoftBridgeThroughDependencyInjection() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Welcome", "Hello");

        var services = new ServiceCollection();
        using var cache = RegisterCachedStoreWithoutMvc(store, services);
        Microsoft.Extensions.DependencyInjection.LocalizationServiceCollectionExtensions.AddLocalization(services);
        CoreJeninnetLocalizationExtensions.AddLocalization(services);

        using var provider = services.BuildServiceProvider();
        var firstFactory = provider.GetRequiredService<IStringLocalizerFactory>();
        var secondFactory = provider.GetRequiredService<IStringLocalizerFactory>();
        var firstTypedLocalizer = provider.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<SampleResource>>();
        var secondTypedLocalizer = provider.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<SampleResource>>();

        firstFactory.Should().BeOfType<CustomStringLocalizerFactory>();
        ReferenceEquals(firstFactory, secondFactory).Should().BeTrue("the bridge factory is process-scoped");
        firstTypedLocalizer.Should().BeOfType<Microsoft.Extensions.Localization.StringLocalizer<SampleResource>>();
        ReferenceEquals(firstTypedLocalizer, secondTypedLocalizer).Should().BeFalse("the standard generic wrapper is transient");
        firstTypedLocalizer["Welcome"].Value.Should().Be("Hello");
    });

    /// <summary>
    /// Verifies that custom string localizer factory maps resource identity and validates arguments.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerFactory_MapsResourceIdentityAndValidatesArguments() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Welcome", "By type");
        store.SetEntry("NamedResource", "en-US", "Welcome", "By base name");

        var factory = new CustomStringLocalizerFactory(store);
        factory.Create(typeof(SampleResource))["Welcome"].Value.Should().Be("By type");
        factory.Create("NamedResource", "ignored-location")["Welcome"].Value.Should().Be("By base name");

        var nullType = () => factory.Create((Type)null!);
        var emptyBaseName = () => factory.Create(string.Empty, "ignored-location");
        var nullLocation = () => factory.Create("NamedResource", null!);

        nullType.Should().Throw<ArgumentNullException>();
        emptyBaseName.Should().Throw<ArgumentNullException>();
        nullLocation.Should().Throw<ArgumentNullException>();
    });

    /// <summary>
    /// Verifies that custom string localizer preserves microsoft metadata and case sensitive missing semantics.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizer_PreservesMicrosoftMetadataAndCaseSensitiveMissingSemantics() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Welcome", "Hello");

        var localizer = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));
        var hit = localizer["Welcome"];
        var missing = localizer["welcome"];

        hit.Name.Should().Be("Welcome");
        hit.Value.Should().Be("Hello");
        hit.ResourceNotFound.Should().BeFalse();
        hit.SearchedLocation.Should().BeNull();
        missing.Name.Should().Be("welcome");
        missing.Value.Should().Be("[MISSING:welcome]");
        missing.ResourceNotFound.Should().BeTrue();
        missing.SearchedLocation.Should().BeNull();
    });

    /// <summary>
    /// Verifies that custom string localizer factory captures culture at creation and uses exact culture only.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerFactory_CapturesCultureAtCreationAndUsesExactCultureOnly() {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Welcome", "Hello");
        store.SetEntry(nameof(SampleResource), "fr", "Welcome", "Bonjour");

        Microsoft.Extensions.Localization.IStringLocalizer? english = null;
        Microsoft.Extensions.Localization.IStringLocalizer? french = null;
        RunWithCurrentUiCulture("en-US", () => english = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource)));
        RunWithCurrentUiCulture("fr", () => {
            english!["Welcome"].Value.Should().Be("Hello", "a localizer intentionally retains its creation culture");
            french = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));
        });

        french!["Welcome"].Value.Should().Be("Bonjour");
    }

    /// <summary>
    /// Verifies that custom string localizer formats with current culture and propagates format errors.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizer_FormatsWithCurrentCultureAndPropagatesFormatErrors() => RunWithCurrentCulture("fr-FR", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "fr-FR", "Amount", "Value {0:0.00}");
        store.SetEntry(nameof(SampleResource), "fr-FR", "Plain", "Plain");
        store.SetEntry(nameof(SampleResource), "fr-FR", "Malformed", "Value {0");

        var localizer = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));

        localizer["Amount", 1234.5].Value.Should().Be("Value 1234,50");
        localizer["Plain", []].Value.Should().Be("Plain");
        localizer["Missing", []].Value.Should().Be("[MISSING:Missing]");

        var act = () => _ = localizer["Malformed", "value"];
        act.Should().Throw<FormatException>();
    });

    /// <summary>
    /// Verifies that custom string localizer get all strings does not invent parent culture traversal.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizer_GetAllStringsDoesNotInventParentCultureTraversal() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Exact", "Exact value");
        store.SetEntry(nameof(SampleResource), "en", "Parent", "Parent value");

        var localizer = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));
        var exactOnly = localizer.GetAllStrings(includeParentCultures: false).Select(static item => item.Name).ToList();
        var requestedWithParents = localizer.GetAllStrings(includeParentCultures: true).Select(static item => item.Name).ToList();

        exactOnly.Should().Contain("Exact");
        exactOnly.Should().NotContain("Parent");
        requestedWithParents.Should().Contain("Exact");
        requestedWithParents.Should().NotContain("Parent", "parent-culture traversal belongs to IMessageLocalizer, not this store bridge");
    });

    /// <summary>
    /// Verifies that custom string localizer factory invariant culture does not fall back to parent or default.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerFactory_InvariantCultureDoesNotFallBackToParentOrDefault() {
        var store = new FakeLocalizationStore();
        store.SetEntry(nameof(SampleResource), "en-US", "Welcome", "Hello");

        RunWithCulture(CultureInfo.InvariantCulture, () => {
            var localizer = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));

            localizer["Welcome"].Value.Should().Be("[MISSING:Welcome]");
        });
    }

    /// <summary>
    /// Verifies that custom string localizer generic does not permanently bind temporary store miss.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerGeneric_DoesNotPermanentlyBindTemporaryStoreMiss() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cachedStore = new CachedLocalizationStoreDecorator(store, cache);
        var localizer = new CustomStringLocalizer<SampleResource>(cachedStore);

        localizer["Late"].Value.Should().Be("[MISSING:Late]");
        store.SetEntry(nameof(SampleResource), "en-US", "Late", "Available later");

        localizer["Late"].Value.Should().Be("Available later");
    });

    /// <summary>
    /// Verifies that custom string localizer factory propagates store failure.
    /// </summary>
    [TestMethod]
    public void CustomStringLocalizerFactory_PropagatesStoreFailure() => RunWithCurrentUiCulture("en-US", () => {
        var store = new FakeLocalizationStore { ThrowOnTryGetResourceSet = true };
        var localizer = new CustomStringLocalizerFactory(store).Create(typeof(SampleResource));

        var act = () => _ = localizer["Welcome"];

        act.Should().Throw<InvalidOperationException>();
    });

    /// <summary>
    /// Registers cached store.
    /// </summary>
    /// <param name="store">The localization store.</param>
    /// <param name="services">The service collection.</param>
    /// <returns>The register cached store result.</returns>
    private static MemoryCache RegisterCachedStore(FakeLocalizationStore store, IServiceCollection services) {
        var cache = new MemoryCache(new MemoryCacheOptions());
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton<ILocalizationStore>(sp => new CachedLocalizationStoreDecorator(store, sp.GetRequiredService<IMemoryCache>()));
        services.AddMvcLocalizedValidation();
        return cache;
    }

    /// <summary>
    /// Registers cached store without mvc.
    /// </summary>
    /// <param name="store">The localization store.</param>
    /// <param name="services">The service collection.</param>
    /// <returns>The register cached store without mvc result.</returns>
    private static MemoryCache RegisterCachedStoreWithoutMvc(FakeLocalizationStore store, IServiceCollection services) {
        var cache = new MemoryCache(new MemoryCacheOptions());
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton<ILocalizationStore>(sp => new CachedLocalizationStoreDecorator(store, sp.GetRequiredService<IMemoryCache>()));
        return cache;
    }

    /// <summary>
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCurrentUiCulture(string cultureName, Action action) {
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            action();
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Runs with current culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCurrentCulture(string cultureName, Action action) => RunWithCulture(new CultureInfo(cultureName), action);

    /// <summary>
    /// Runs with culture.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCulture(CultureInfo culture, Action action) {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            action();
        }
        finally {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static async Task RunWithCurrentUiCultureAsync(string cultureName, Func<Task> action) {
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            await action().ConfigureAwait(false);
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

#pragma warning disable S2094 // Empty marker types exist only to drive resource-set naming via typeof and to serve as a validation model instance
    /// <summary>
    /// Represents a sample resource.
    /// </summary>
    private sealed class SampleResource;

    /// <summary>
    /// Represents a sample model.
    /// </summary>
    private sealed class SampleModel;

    /// <summary>
    /// Represents a validation model.
    /// </summary>
    private sealed class ValidationModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedRequired("Required.Message")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents an unknown key model.
    /// </summary>
    private sealed class UnknownKeyModel {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [LocalizedRequired("Unknown.Key")]
        public string? Name { get; set; }
    }
#pragma warning restore S2094

    /// <summary>
    /// Provides culture sensitive.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class CultureSensitiveProvider(IReadOnlyDictionary<string, string> templates) : IInnerMessageProvider {
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
    /// Provides throwing inner.
    /// </summary>
    private sealed class ThrowingInnerProvider : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Provider failure.");
    }

    /// <summary>
    /// Stores fake localization.
    /// </summary>
    private sealed class FakeLocalizationStore : ILocalizationStore {
        private readonly object _sync = new();
        private readonly Dictionary<(string Set, string Culture), Dictionary<string, string>> _data = [];
        private readonly Dictionary<(string Set, string Culture), FakeResourceSet> _sets = [];

        /// <summary>
        /// Gets or sets the reload call count.
        /// </summary>
        public int ReloadCallCount { get; private set; }

        /// <summary>
        /// Gets or sets the throw on reload.
        /// </summary>
        public bool ThrowOnReload { get; set; }

        /// <summary>
        /// Gets or sets the throw on try get resource set.
        /// </summary>
        public bool ThrowOnTryGetResourceSet { get; set; }

        /// <summary>
        /// Gets the total inner read count.
        /// </summary>
        public int TotalInnerReadCount {
            get {
                lock(_sync) {
                    return _sets.Values.Sum(static set => set.ReadCount);
                }
            }
        }

        /// <summary>
        /// Sets entry.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="key">The message key.</param>
        /// <param name="value">The value.</param>
        public void SetEntry(string resourceSet, string culture, string key, string value) {
            lock(_sync) {
                if(!_data.TryGetValue((resourceSet, culture), out var entries)) {
                    entries = [];
                    _data[(resourceSet, culture)] = entries;
                    _sets[(resourceSet, culture)] = new FakeResourceSet(resourceSet, culture, this);
                }

                entries[key] = value;
            }
        }

        /// <summary>
        /// Gets data.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The data result.</returns>
        public Dictionary<string, string>? GetData(string resourceSet, string culture) {
            lock(_sync) {
                return _data.TryGetValue((resourceSet, culture), out var entries) ? entries : null;
            }
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default)
            => Task.FromResult<ILocalizationResourceSet?>(GetSet(resourceSet, culture));

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) {
            List<ILocalizationResourceSet> matches;
            lock(_sync) {
                matches = [.. _sets.Where(set => set.Key.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)).Select(static set => (ILocalizationResourceSet)set.Value)];
            }

            return Task.FromResult(matches);
        }

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            if(ThrowOnTryGetResourceSet) {
                throw new InvalidOperationException("resource-set probe failed");
            }

            localizationResourceSet = GetSet(resourceSet, culture);
            return localizationResourceSet is not null;
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
            var set = GetSet(resourceSet, culture);
            return set is null ? Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name)) : set.GetStringAsync(name, cancellationToken);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var set = GetSet(resourceSet, culture);
            return set is null ? [] : await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var result = new Dictionary<string, LocalizedStringEntry>();

            foreach(var set in GetSetsForCulture(culture)) {
                foreach(var entry in await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false)) {
                    result[entry.Key] = entry.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            var set = GetSet(resourceSet, culture);

            if(set is not null && set.TryGetString(name, out entry)) {
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) {
            ReloadCallCount++;

            if(cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled(cancellationToken);
            }

            return ThrowOnReload ? Task.FromException(new InvalidOperationException("Reload failed.")) : Task.CompletedTask;
        }

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            List<string> cultures;
            lock(_sync) {
                cultures = [.. _sets.Keys.Select(static key => key.Culture).Distinct(StringComparer.OrdinalIgnoreCase)];
            }

            return Task.FromResult(cultures);
        }

        /// <summary>
        /// Gets set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The set result.</returns>
        private FakeResourceSet? GetSet(string resourceSet, string culture) {
            lock(_sync) {
                return _sets.TryGetValue((resourceSet, culture), out var set) ? set : null;
            }
        }

        /// <summary>
        /// Gets sets for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <returns>The sets for culture sequence.</returns>
        private List<FakeResourceSet> GetSetsForCulture(string culture) {
            lock(_sync) {
                return [.. _sets.Where(set => set.Key.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase)).Select(static set => set.Value)];
            }
        }
    }

    /// <summary>
    /// Test double for a resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="store">The localization store.</param>
    private sealed class FakeResourceSet(string resourceSet, string culture, FakeLocalizationStore store) : ILocalizationResourceSet {
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Gets or sets the read count.
        /// </summary>
        public int ReadCount { get; private set; }

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) {
            ReadCount++;
            var data = store.GetData(resourceSet, culture);

            return Task.FromResult(data is not null && data.TryGetValue(name, out var value)
                ? new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value)
                : LocalizedStringEntry.Missing(resourceSet, culture, name));
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) {
            var data = store.GetData(resourceSet, culture);
            var result = new Dictionary<string, LocalizedStringEntry>();

            if(data is not null) {
                foreach(var entry in data) {
                    result[entry.Key] = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, entry.Key, entry.Value);
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            ReadCount++;
            var data = store.GetData(resourceSet, culture);

            if(data is not null && data.TryGetValue(name, out var value)) {
                entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, value);
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
