//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the Blazor integration package:
/// circuit-scoped culture isolation (two concurrent circuits without cross-talk),
/// static WASM (WebAssembly) scope semantics, the WASM-compatible provider guard, the
/// host-wired culture stores, and the self-contained JS bridge.
/// </summary>
[TestClass]
public class BlazorLocalizationTests {
    private const string HEADING_KEY = "heading";

    // ---------------------------------------------------------------- registration

    /// <summary>
    /// Verifies that server registration resolves ACircuit scoped service and the circuit handler.
    /// </summary>
    [TestMethod]
    public void ServerRegistration_ResolvesACircuitScopedService_AndTheCircuitHandler() {
        var services = new ServiceCollection();
        services.AddBlazorServerLocalization(static options => options.SupportedCultures.Add("ar"));

        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var serviceA = scopeA.ServiceProvider.GetRequiredService<BlazorCultureService>();
        var serviceB = scopeB.ServiceProvider.GetRequiredService<BlazorCultureService>();

        serviceA.Should().NotBeSameAs(serviceB, "each circuit owns its culture service");
        scopeA.ServiceProvider.GetServices<CircuitHandler>().Should().ContainSingle(
            static handler => handler is BlazorCircuitCultureHandler,
            "the circuit culture handler is registered in the CircuitHandler enumerable");
        provider.GetService<IHttpContextAccessor>().Should().NotBeNull("the handler reads the connection's request culture feature");
    }

    /// <summary>
    /// Verifies that wasm registration resolves ASingleton static scope service.
    /// </summary>
    [TestMethod]
    public void WasmRegistration_ResolvesASingletonStaticScopeService() {
        var services = new ServiceCollection();
        services.AddBlazorWasmLocalization();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<BlazorCultureService>();
        var second = provider.GetRequiredService<BlazorCultureService>();

        second.Should().BeSameAs(first, "client-side hosts use one static culture scope");
        provider.GetServices<CircuitHandler>().Should().BeEmpty("there are no circuits in a client-side host");
    }

    /// <summary>
    /// Verifies that registration rejects ASecond composition.
    /// </summary>
    [TestMethod]
    public void Registration_RejectsASecondComposition() {
        var services = new ServiceCollection();
        services.AddBlazorServerLocalization();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddBlazorServerLocalization());

        exception.Message.Should().Contain("already registered");
    }

    /// <summary>
    /// Verifies that registration validates arguments and options.
    /// </summary>
    [TestMethod]
    public void Registration_ValidatesArgumentsAndOptions() {
        Action nullServices = () => ((IServiceCollection)null!).AddBlazorLocalization();

        nullServices.Should().Throw<ArgumentNullException>();

        var unknownCulture = new ServiceCollection();
        Action act = () => unknownCulture.AddBlazorServerLocalization(options => options.SupportedCultures.Add("en!x"));

        act.Should().Throw<CultureNotFoundException>();

        var badLifetime = new ServiceCollection();
        Action lifetime = () => badLifetime.AddBlazorServerLocalization(options => options.CultureCookieLifetime = TimeSpan.Zero);

        lifetime.Should().Throw<ArgumentException>();
    }

    // --------------------------------------------------------------- circuit scope

    /// <summary>
    /// Verifies that concurrent circuits open with their own request culture without cross talk.
    /// </summary>
    [TestMethod]
    public async Task ConcurrentCircuits_OpenWithTheirOwnRequestCulture_WithoutCrossTalkAsync() {
        await using var provider = BuildServerProvider();
        using var scopeArabic = provider.CreateScope();
        using var scopeEnglish = provider.CreateScope();

        var results = await Task.WhenAll(
            Task.Run(() => OpenAndLocalizeAsync(scopeArabic, "ar"), TestContext.CancellationToken),
            Task.Run(() => OpenAndLocalizeAsync(scopeEnglish, "en"), TestContext.CancellationToken));

        results[0].Should().Be(("ar", "مرحبا"), "the Arabic circuit resolves with its own culture");
        results[1].Should().Be(("en", "Hello"), "the English circuit resolves with its own culture");

        scopeArabic.ServiceProvider.GetRequiredService<BlazorCultureService>().Culture.Should().Be("ar",
            "the other circuit's activity must not touch this circuit's culture");
        scopeEnglish.ServiceProvider.GetRequiredService<BlazorCultureService>().Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that server culture operations never mutate the ambient culture.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task ServerCultureOperations_NeverMutateTheAmbientCultureAsync() {
        await using var provider = BuildServerProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BlazorCultureService>();

        var cultureBefore = CultureInfo.CurrentCulture;
        var uiCultureBefore = CultureInfo.CurrentUICulture;
        var defaultCultureBefore = CultureInfo.DefaultThreadCurrentCulture;

        await OpenCircuitAsync(scope, "ar");
        await service.SetCultureAsync("en", TestContext.CancellationToken);

        service.Culture.Should().Be("en");
        CultureInfo.CurrentCulture.Should().BeSameAs(cultureBefore, "Server mode must not mutate ambient culture (cross-talk elimination)");
        CultureInfo.CurrentUICulture.Should().BeSameAs(uiCultureBefore);
        CultureInfo.DefaultThreadCurrentCulture.Should().BeSameAs(defaultCultureBefore, "static thread defaults must never be assigned in Server mode");
    }

    /// <summary>
    /// Verifies that circuit culture is seeded only from supported request cultures.
    /// </summary>
    [TestMethod]
    public async Task CircuitCulture_IsSeededOnlyFromSupportedRequestCulturesAsync() {
        await using var provider = BuildServerProvider();
        using var scope = provider.CreateScope();

        await OpenCircuitAsync(scope, "zz-ZZ");

        scope.ServiceProvider.GetRequiredService<BlazorCultureService>().Culture.Should().BeNull(
            "an unrecognized request culture is ignored instead of failing the connection");
    }

    /// <summary>
    /// Verifies that handler validates arguments.
    /// </summary>
    [TestMethod]
    public void Handler_ValidatesArguments() {
        var service = new BlazorCultureService(new BlazorLocalizationOptions(), new RecordingCultureStore());

        Action nullService = () => _ = new BlazorCircuitCultureHandler(null!, new HttpContextAccessor());
        Action nullAccessor = () => _ = new BlazorCircuitCultureHandler(service, null!);

        nullService.Should().Throw<ArgumentNullException>();
        nullAccessor.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ WASM scope

    /// <summary>
    /// Verifies that wasm service applies the static ambient culture and raises the change event.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task WasmService_AppliesTheStaticAmbientCulture_AndRaisesTheChangeEventAsync() {
        var store = new RecordingCultureStore();
        var service = new BlazorCultureService(new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Wasm, DefaultCulture = "en" }, store);
        var events = new List<BlazorCultureChangedEventArgs>();
        service.CultureChanged += (_, args) => events.Add(args);

        var cultureBefore = CultureInfo.CurrentCulture;
        var uiCultureBefore = CultureInfo.CurrentUICulture;
        var defaultCultureBefore = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCultureBefore = CultureInfo.DefaultThreadCurrentUICulture;
        try {
            await service.SetCultureAsync("ar", TestContext.CancellationToken);

            CultureInfo.CurrentUICulture.Name.Should().Be("ar");
            CultureInfo.CurrentCulture.Name.Should().Be("ar");
            CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be("ar");
            CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be("ar");
            store.Writes.Should().ContainSingle();
            store.Writes[0].Should().Be("ar");
            events.Should().ContainSingle();
            events[0].Culture.Should().Be("ar");
            events[0].CultureInfo.Name.Should().Be("ar");
        }
        finally {
            CultureInfo.CurrentCulture = cultureBefore;
            CultureInfo.CurrentUICulture = uiCultureBefore;
            CultureInfo.DefaultThreadCurrentCulture = defaultCultureBefore;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUiCultureBefore;
        }
    }

    /// <summary>
    /// Verifies that culture info falls back to the configured default culture.
    /// </summary>
    [TestMethod]
    public void CultureInfo_FallsBackToTheConfiguredDefaultCulture() {
        var service = new BlazorCultureService(new BlazorLocalizationOptions { DefaultCulture = "ar" }, new RecordingCultureStore());

        service.Culture.Should().BeNull();
        service.CultureInfo.Name.Should().Be("ar", "the configured default applies until a culture is chosen");
    }

    /// <summary>
    /// Verifies that set culture async rejects unsupported and unrecognized cultures.
    /// </summary>
    [TestMethod]
    public async Task SetCultureAsync_RejectsUnsupportedAndUnrecognizedCulturesAsync() {
        var options = new BlazorLocalizationOptions();
        options.SupportedCultures.Add("en");
        var restricted = new BlazorCultureService(options, new RecordingCultureStore());
        var open = new BlazorCultureService(new BlazorLocalizationOptions(), new RecordingCultureStore());

        Func<Task> unsupported = () => restricted.SetCultureAsync("ar", TestContext.CancellationToken);
        Func<Task> unrecognized = () => open.SetCultureAsync("en!x", TestContext.CancellationToken);

        await unsupported.Should().ThrowAsync<ArgumentException>();
        await unrecognized.Should().ThrowAsync<CultureNotFoundException>();
        restricted.Culture.Should().BeNull("a rejected culture is never applied");
        open.Culture.Should().BeNull();
    }

    // ------------------------------------------------------------------- persistence

    /// <summary>
    /// Verifies that initialize async restores the stored culture and raises the change event.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_RestoresTheStoredCulture_AndRaisesTheChangeEventAsync() {
        var store = new RecordingCultureStore { Stored = "ar" };
        var service = new BlazorCultureService(new BlazorLocalizationOptions(), store);
        var raised = 0;
        service.CultureChanged += (_, _) => raised++;

        var restored = await service.InitializeAsync(TestContext.CancellationToken);

        restored.Should().BeTrue();
        service.Culture.Should().Be("ar");
        raised.Should().Be(1);

        (await service.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse("an already-applied culture is not re-raised");
        raised.Should().Be(1);
    }

    /// <summary>
    /// Verifies that initialize async ignores unsupported or unrecognized stored cultures.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_IgnoresUnsupportedOrUnrecognizedStoredCulturesAsync() {
        var unsupportedStore = new RecordingCultureStore { Stored = "fr" };
        var options = new BlazorLocalizationOptions();
        options.SupportedCultures.Add("en");
        var unsupported = new BlazorCultureService(options, unsupportedStore);

        (await unsupported.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse();
        unsupported.Culture.Should().BeNull();

        var unknown = new BlazorCultureService(new BlazorLocalizationOptions(), new RecordingCultureStore { Stored = "en!x" });

        (await unknown.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse();
        unknown.Culture.Should().BeNull();
    }

    // ---------------------------------------------------- browser language detection

    /// <summary>
    /// Verifies that initialize async detects browser language exact match and does not persist.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_DetectsBrowserLanguage_ExactMatch_AndDoesNotPersistAsync() {
        var (service, store, runtime) = CreateDetectingService("de-DE", "en");
        var languages = new[] { "de-DE", "en-US" };
        runtime.EnqueueResult(languages);
        var raised = 0;
        service.CultureChanged += (_, _) => raised++;

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeTrue();
        service.Culture.Should().Be("de-DE");
        raised.Should().Be(1);
        store.Writes.Should().BeEmpty("detection is read-only: the store keeps explicit user choices only");

        (await service.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse("an already-applied culture is not re-raised");
        raised.Should().Be(1);
    }

    /// <summary>
    /// Verifies that initialize async detects browser language by parent culture.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_DetectsBrowserLanguage_ByParentCultureAsync() {
        var (service, _, runtime) = CreateDetectingService("de");
        var languages = new[] { "de-DE" };
        runtime.EnqueueResult(languages);

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeTrue();
        service.Culture.Should().Be("de", "a de-DE browser preference matches the supported parent culture");
    }

    /// <summary>
    /// Verifies that initialize async detects browser language next preference when first is unserved.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_DetectsBrowserLanguage_NextPreferenceWhenFirstIsUnservedAsync() {
        var (service, _, runtime) = CreateDetectingService("de");
        var languages = new[] { "fr-FR", "de" };
        runtime.EnqueueResult(languages);

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeTrue();
        service.Culture.Should().Be("de");
    }

    /// <summary>
    /// Verifies that initialize async unserved browser language falls back to the configured default.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_UnservedBrowserLanguage_FallsBackToTheConfiguredDefaultAsync() {
        var options = new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Wasm, DetectBrowserLanguage = true, DefaultCulture = "en" };
        options.SupportedCultures.Add("de");
        var runtime = new RecordingJsRuntime();
        var languages = new[] { "fr-FR" };
        runtime.EnqueueResult(languages);
        var service = new BlazorCultureService(options, new RecordingCultureStore(), new BlazorCultureJsInterop(runtime));

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeFalse();
        service.Culture.Should().BeNull("no match leaves the culture unapplied");
        service.CultureInfo.Name.Should().Be("en", "the configured default applies deterministically");
    }

    /// <summary>
    /// Verifies that initialize async empty browser language list does not detect.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_EmptyBrowserLanguageList_DoesNotDetectAsync() {
        var (service, _, runtime) = CreateDetectingService("de");
        runtime.EnqueueResult(Array.Empty<string>());

        (await service.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse();
        service.Culture.Should().BeNull();
    }

    /// <summary>
    /// Verifies that initialize async empty supported cultures resolves the first recognized preference.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_EmptySupportedCultures_ResolvesTheFirstRecognizedPreferenceAsync() {
        var (service, _, runtime) = CreateDetectingService();
        var languages = new[] { "fr-FR", "de" };
        runtime.EnqueueResult(languages);

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeTrue();
        service.Culture.Should().Be("fr-FR", "with an empty supported list the first recognized browser preference is applied");
    }

    /// <summary>
    /// Verifies that initialize async stored choice wins over detection without js interop.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_StoredChoice_WinsOverDetection_WithoutJsInteropAsync() {
        var (service, store, runtime) = CreateDetectingService("de", "en");
        store.Stored = "en";
        var languages = new[] { "de" };
        runtime.EnqueueResult(languages);

        var restored = await service.InitializeAsync(TestContext.CancellationToken);

        restored.Should().BeTrue();
        service.Culture.Should().Be("en");
        runtime.Invocations.Should().BeEmpty("a stored choice short-circuits detection");
    }

    /// <summary>
    /// Verifies that initialize async unsupported stored culture still detects.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_UnsupportedStoredCulture_StillDetectsAsync() {
        var (service, store, runtime) = CreateDetectingService("de");
        store.Stored = "fr";
        var languages = new[] { "de" };
        runtime.EnqueueResult(languages);

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeTrue();
        service.Culture.Should().Be("de");
    }

    /// <summary>
    /// Verifies that initialize async detection disabled behavior is unchanged.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_DetectionDisabled_BehaviorIsUnchangedAsync() {
        var options = new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Wasm };
        options.SupportedCultures.Add("de");
        var runtime = new RecordingJsRuntime();
        var languages = new[] { "de" };
        runtime.EnqueueResult(languages);
        var service = new BlazorCultureService(options, new RecordingCultureStore(), new BlazorCultureJsInterop(runtime));

        (await service.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse();
        service.Culture.Should().BeNull();
        runtime.Invocations.Should().BeEmpty("detection is opt-in");
    }

    /// <summary>
    /// Verifies that initialize async server mode ignores detection.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_ServerMode_IgnoresDetectionAsync() {
        var options = new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Server, DetectBrowserLanguage = true };
        options.SupportedCultures.Add("de");
        var runtime = new RecordingJsRuntime();
        var languages = new[] { "de" };
        runtime.EnqueueResult(languages);
        var service = new BlazorCultureService(options, new RecordingCultureStore(), new BlazorCultureJsInterop(runtime));

        (await service.InitializeAsync(TestContext.CancellationToken)).Should().BeFalse("server mode is seeded from the resolved request culture");
        service.Culture.Should().BeNull();
        runtime.Invocations.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that initialize async js interop unavailable falls back without exception.
    /// </summary>
    [TestMethod]
    public async Task InitializeAsync_JsInteropUnavailable_FallsBackWithoutExceptionAsync() {
        var options = new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Wasm, DetectBrowserLanguage = true, DefaultCulture = "en" };
        options.SupportedCultures.Add("de");
        var service = new BlazorCultureService(options, new RecordingCultureStore(), new BlazorCultureJsInterop(new ThrowingJsRuntime()));

        var detected = await service.InitializeAsync(TestContext.CancellationToken);

        detected.Should().BeFalse("detection is best-effort and never fails initialization");
        service.CultureInfo.Name.Should().Be("en");
    }

    /// <summary>
    /// Verifies that read browser languages evaluates ASelf contained script.
    /// </summary>
    [TestMethod]
    public async Task ReadBrowserLanguages_EvaluatesASelfContainedScriptAsync() {
        var runtime = new RecordingJsRuntime();
        var browserLanguages = new[] { "de-DE", "en-US" };
        runtime.EnqueueResult(browserLanguages);
        var interop = new BlazorCultureJsInterop(runtime);

        var languages = await interop.ReadBrowserLanguagesAsync(TestContext.CancellationToken);

        languages.Should().Equal("de-DE", "en-US");
        runtime.Invocations.Should().ContainSingle();
        runtime.Invocations[0].Identifier.Should().Be("eval");
        var script = (string)runtime.Invocations[0].Args![0]!;
        script.Should().Contain("navigator.languages");
        script.Should().Contain("navigator.language");
    }

    /// <summary>
    /// Creates detecting service.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <returns>The detecting service result.</returns>
    private static (BlazorCultureService Service, RecordingCultureStore Store, RecordingJsRuntime Runtime) CreateDetectingService(params string[] supportedCultures) {
        var options = new BlazorLocalizationOptions { Mode = BlazorLocalizationMode.Wasm, DetectBrowserLanguage = true };
        foreach(var culture in supportedCultures) {
            options.SupportedCultures.Add(culture);
        }

        var store = new RecordingCultureStore();
        var runtime = new RecordingJsRuntime();
        var service = new BlazorCultureService(options, store, new BlazorCultureJsInterop(runtime));
        return (service, store, runtime);
    }

    /// <summary>
    /// Verifies that host wired store overrides the default no op store.
    /// </summary>
    [TestMethod]
    public async Task HostWiredStore_OverridesTheDefaultNoOpStoreAsync() {
        var store = new RecordingCultureStore();
        var services = new ServiceCollection();
        services.AddBlazorWasmLocalization();
        services.AddSingleton<IBlazorCultureStore>(store);

        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<BlazorCultureService>();

        await service.SetCultureAsync("ar", TestContext.CancellationToken);

        store.Writes.Should().ContainSingle();
        store.Writes[0].Should().Be("ar");
    }

    /// <summary>
    /// Verifies that set culture async propagates store failures after applying the culture without raising the event.
    /// </summary>
    [TestMethod]
    public async Task SetCultureAsync_PropagatesStoreFailures_AfterApplyingTheCulture_WithoutRaisingTheEventAsync() {
        var store = new ThrowingCultureStore();
        var service = new BlazorCultureService(new BlazorLocalizationOptions(), store);
        var raised = 0;
        service.CultureChanged += (_, _) => raised++;

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SetCultureAsync("ar", TestContext.CancellationToken));

        exception.Message.Should().Contain("store failed");
        service.Culture.Should().Be("ar", "the in-process culture is applied before persistence is attempted");
        raised.Should().Be(0, "the change event fires only after successful persistence");
    }

    // --------------------------------------------------------------- WASM guard

    /// <summary>
    /// Verifies that wasm guard rejects providers that do not declare wasm compatibility.
    /// </summary>
    [TestMethod]
    public void WasmGuard_RejectsProvidersThatDoNotDeclareWasmCompatibility() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new FileBackedProvider());

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddBlazorWasmLocalization());

        exception.Message.Should().Contain(nameof(FileBackedProvider));
        exception.Message.Should().Contain(nameof(IWasmCompatibleMessageProvider));
    }

    /// <summary>
    /// Verifies that wasm guard rejects unverifiable factory registrations.
    /// </summary>
    [TestMethod]
    public void WasmGuard_RejectsUnverifiableFactoryRegistrations() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(_ => new FileBackedProvider());

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddBlazorWasmLocalization());

        exception.Message.Should().Contain("factory-registered");
    }

    /// <summary>
    /// Verifies that wasm guard accepts providers that declare wasm compatibility.
    /// </summary>
    [TestMethod]
    public void WasmGuard_AcceptsProvidersThatDeclareWasmCompatibility() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> { [HEADING_KEY] = "Hello" });
        services.AddLocalization(static options => options.DefaultCulture = "en");

        services.AddBlazorWasmLocalization();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BlazorCultureService>().Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that wasm guard accepts any provider declaring the marker.
    /// </summary>
    [TestMethod]
    public async Task WasmGuard_AcceptsAnyProviderDeclaringTheMarkerAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new WasmSafeProvider());
        services.AddLocalization(static options => options.DefaultCulture = "en");

        services.AddBlazorWasmLocalization();

        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        (await localizer.LocalizeAsync(HEADING_KEY, new Dictionary<string, object?>(), new CultureInfo("en"), CancellationToken.None))
            .Should().Be("WASM-safe");
    }

    /// <summary>
    /// Verifies that server registration does not apply the wasm guard.
    /// </summary>
    [TestMethod]
    public void ServerRegistration_DoesNotApplyTheWasmGuard() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new FileBackedProvider());

        services.AddBlazorServerLocalization();

        services.Count.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Verifies that memory provider declares the wasm compatibility marker.
    /// </summary>
    [TestMethod]
    public void MemoryProvider_DeclaresTheWasmCompatibilityMarker() {
        var provider = new Jeninnet.Localization.Providers.Memory.MemoryMessageProvider(
            new Jeninnet.Localization.Providers.Memory.Options.MemoryLocalizationOptions { Messages = new Dictionary<string, string>() });

        Assert.IsInstanceOfType<IWasmCompatibleMessageProvider>(provider,
            "the in-memory provider resolves templates without any I/O");
    }

    // -------------------------------------------------------------------- JS bridge

    /// <summary>
    /// Verifies that write cookie evaluates ASelf contained script with json escaped arguments.
    /// </summary>
    [TestMethod]
    public async Task WriteCookie_EvaluatesASelfContainedScriptWithJsonEscapedArgumentsAsync() {
        var runtime = new RecordingJsRuntime();
        var interop = new BlazorCultureJsInterop(runtime);

        await interop.WriteCookieAsync(".AspNetCore.Culture", "c=ar|uic=ar", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), TestContext.CancellationToken);

        runtime.Invocations.Should().ContainSingle();
        var (Identifier, Args) = runtime.Invocations[0];
        Identifier.Should().Be("eval");
        var script = (string)Args![0]!;
        script.Should().Contain("document.cookie");
        script.Should().Contain(JsonSerializer.Serialize(".AspNetCore.Culture"));
        script.Should().Contain(JsonSerializer.Serialize("c=ar|uic=ar"));
        script.Should().Contain("expires");
    }

    /// <summary>
    /// Verifies that read cookie evaluates ASelf contained script.
    /// </summary>
    [TestMethod]
    public async Task ReadCookie_EvaluatesASelfContainedScriptAsync() {
        var runtime = new RecordingJsRuntime();
        runtime.EnqueueResult("c=ar|uic=ar");
        var interop = new BlazorCultureJsInterop(runtime);

        var value = await interop.ReadCookieAsync(".AspNetCore.Culture", TestContext.CancellationToken);

        value.Should().Be("c=ar|uic=ar");
        var script = (string)runtime.Invocations[0].Args![0]!;
        script.Should().Contain("document.cookie");
        script.Should().Contain(JsonSerializer.Serialize(".AspNetCore.Culture"));
    }

    /// <summary>
    /// Verifies that cookie store writes canonical cookie values and parses them back.
    /// </summary>
    [TestMethod]
    public async Task CookieStore_WritesCanonicalCookieValues_AndParsesThemBackAsync() {
        var runtime = new RecordingJsRuntime();
        var store = new BlazorCookieCultureStore(new BlazorCultureJsInterop(runtime), new BlazorLocalizationOptions());

        await store.WriteAsync("ar-SY", TestContext.CancellationToken);

        var expectedValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("ar-SY"));
        var writeScript = (string)runtime.Invocations[0].Args![0]!;
        writeScript.Should().Contain(JsonSerializer.Serialize(expectedValue),
            "the cookie value is exactly what the framework's CookieRequestCultureProvider reads");

        runtime.EnqueueResult(expectedValue);
        (await store.ReadAsync(TestContext.CancellationToken)).Should().Be("ar-SY");
    }

    /// <summary>
    /// Verifies that cookie store mirrors the framework parser which rejects values without the canonical shape.
    /// </summary>
    [TestMethod]
    public async Task CookieStore_MirrorsTheFrameworkParser_WhichRejectsValuesWithoutTheCanonicalShapeAsync() {
        var runtime = new RecordingJsRuntime();
        runtime.EnqueueResult("c=ar");
        var store = new BlazorCookieCultureStore(new BlazorCultureJsInterop(runtime), new BlazorLocalizationOptions());

        (await store.ReadAsync(TestContext.CancellationToken)).Should().BeNull(
            "CookieRequestCultureProvider.ParseCookieValue rejects values without the 'c=…|uic=…' shape, and the store mirrors the framework exactly");
    }

    /// <summary>
    /// Verifies that local storage store uses the configured key.
    /// </summary>
    [TestMethod]
    public async Task LocalStorageStore_UsesTheConfiguredKeyAsync() {
        var runtime = new RecordingJsRuntime();
        var options = new BlazorLocalizationOptions { CultureStorageKey = "test.culture" };
        var store = new BlazorLocalStorageCultureStore(new BlazorCultureJsInterop(runtime), options);

        await store.WriteAsync("ar", TestContext.CancellationToken);
        runtime.Invocations[0].Identifier.Should().Be("eval");
        ((string)runtime.Invocations[0].Args![0]!).Should().Contain(JsonSerializer.Serialize("test.culture"));

        runtime.EnqueueResult("en");
        (await store.ReadAsync(TestContext.CancellationToken)).Should().Be("en");
    }

    /// <summary>
    /// Verifies that js interop validates arguments.
    /// </summary>
    [TestMethod]
    public async Task JsInterop_ValidatesArgumentsAsync() {
        Action nullRuntime = () => _ = new BlazorCultureJsInterop(null!);
        nullRuntime.Should().Throw<ArgumentNullException>();

        var interop = new BlazorCultureJsInterop(new RecordingJsRuntime());
        Func<Task> nullName = async () => await interop.WriteCookieAsync(string.Empty, "value", cancellationToken: TestContext.CancellationToken);
        Func<Task> nullKey = async () => await interop.WriteLocalStorageAsync(null!, "value", TestContext.CancellationToken);

        await nullName.Should().ThrowAsync<ArgumentException>();
        await nullKey.Should().ThrowAsync<ArgumentNullException>();
    }

    // --------------------------------------------------------------------- helpers

    /// <summary>
    /// Builds server provider.
    /// </summary>
    /// <returns>The server provider result.</returns>
    private static ServiceProvider BuildServerProvider() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new CultureSensitiveProvider());
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddBlazorServerLocalization(static options => {
            options.SupportedCultures.Add("en");
            options.SupportedCultures.Add("ar");
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Opens and localize.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="requestCulture">The request culture.</param>
    /// <returns>A task whose result is open and localize.</returns>
    private static async Task<(string? CircuitCulture, string? Value)> OpenAndLocalizeAsync(IServiceScope scope, string requestCulture) {
        var service = scope.ServiceProvider.GetRequiredService<BlazorCultureService>();
        var localizer = scope.ServiceProvider.GetRequiredService<IMessageLocalizer>();

        await OpenCircuitAsync(scope, requestCulture);

        var value = await localizer.LocalizeAsync(HEADING_KEY, new Dictionary<string, object?>(), service.CultureInfo, CancellationToken.None);
        return (service.Culture, value);
    }

    /// <summary>
    /// Opens circuit.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <param name="requestCulture">The request culture.</param>
    private static Task OpenCircuitAsync(IServiceScope scope, string requestCulture) {
        var service = scope.ServiceProvider.GetRequiredService<BlazorCultureService>();
        var accessor = new HttpContextAccessor { HttpContext = CreateRequestContext(requestCulture) };
        var handler = new BlazorCircuitCultureHandler(service, accessor);

        return handler.OnCircuitOpenedAsync(null!, CancellationToken.None);
    }

    /// <summary>
    /// Creates request context.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <returns>The request context result.</returns>
    private static DefaultHttpContext CreateRequestContext(string cultureName) {
        var context = new DefaultHttpContext();
        context.Features.Set<IRequestCultureFeature>(new TestRequestCultureFeature(cultureName));
        return context;
    }

    /// <summary>
    /// Represents a test request culture feature.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    private sealed class TestRequestCultureFeature(string cultureName) : IRequestCultureFeature {
        /// <summary>
        /// Gets the request culture.
        /// </summary>
        public RequestCulture RequestCulture { get; } = new(cultureName);

        /// <summary>
        /// Gets the provider.
        /// </summary>
        public IRequestCultureProvider? Provider => null;
    }

    /// <summary>
    /// Stores recording culture.
    /// </summary>
    private sealed class RecordingCultureStore : IBlazorCultureStore {
        /// <summary>
        /// Gets or sets the stored.
        /// </summary>
        public string? Stored { get; set; }

        /// <summary>
        /// Gets the writes.
        /// </summary>
        public List<string> Writes { get; } = [];

        /// <summary>
        /// Performs the read operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is read.</returns>
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Stored);
        }

        /// <summary>
        /// Performs the write operation.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add(culture);
            Stored = culture;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stores throwing culture.
    /// </summary>
    private sealed class ThrowingCultureStore : IBlazorCultureStore {
        /// <summary>
        /// Performs the read operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is read.</returns>
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);

        /// <summary>
        /// Performs the write operation.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("culture store failed");
    }

    /// <summary>
    /// Test double for a js runtime.
    /// </summary>
    private sealed class RecordingJsRuntime : IJSRuntime {
        private readonly Queue<object?> _results = new();

        /// <summary>
        /// Gets the invocations.
        /// </summary>
        public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

        /// <summary>
        /// Performs the enqueue result operation.
        /// </summary>
        /// <param name="result">The result.</param>
        public void EnqueueResult(object? result) => _results.Enqueue(result);

        /// <summary>
        /// Performs the invoke operation.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <typeparam name="TValue">The type of TValue.</typeparam>
        /// <returns>A value task whose result is invoke.</returns>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        /// <summary>
        /// Performs the invoke operation.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <typeparam name="TValue">The type of TValue.</typeparam>
        /// <returns>A value task whose result is invoke.</returns>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add((identifier, args));

            var result = _results.Count > 0 ? _results.Dequeue() : null;
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }

    /// <summary>
    /// Represents a throwing js runtime.
    /// </summary>
    private sealed class ThrowingJsRuntime : IJSRuntime {
        /// <summary>
        /// Performs the invoke operation.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <typeparam name="TValue">The type of TValue.</typeparam>
        /// <returns>A value task whose result is invoke.</returns>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time");

        /// <summary>
        /// Performs the invoke operation.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <param name="args">The command-line arguments.</param>
        /// <typeparam name="TValue">The type of TValue.</typeparam>
        /// <returns>A value task whose result is invoke.</returns>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time");
    }

    /// <summary>
    /// Provides culture sensitive.
    /// </summary>
    private sealed class CultureSensitiveProvider : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            return ValueTask.FromResult<string?>(
                key == HEADING_KEY
                    ? culture.Name switch { "ar" => "مرحبا", "en" => "Hello", _ => null }
                    : null);
        }
    }

    /// <summary>
    /// Provides file backed.
    /// </summary>
    private sealed class FileBackedProvider : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(null);
    }

    /// <summary>
    /// Provides wasm safe.
    /// </summary>
    private sealed class WasmSafeProvider : IInnerMessageProvider, IWasmCompatibleMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(key == HEADING_KEY ? "WASM-safe" : null);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
