//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the request-localization bridge:
/// options building from store cultures (with the documented fallback and
/// fail-fast behavior), the served-culture provider composition
/// (route/query/cookie/header narrowed to the served set), and end-to-end
/// middleware behavior through the real <see cref="RequestLocalizationMiddleware"/>.
/// </summary>
[TestClass]
public class RequestLocalizationBridgeTests {
    private const string CONFIG_ERROR_MARKER = "LocalizationRequestCultureOptions.SupportedCultures";

    // ------------------------------------------------------------- options builder

    /// <summary>
    /// Verifies that registration populates cultures and default from store.
    /// </summary>
    [TestMethod]
    public void Registration_PopulatesCulturesAndDefaultFromStore() {
        var store = new FakeStore(["en", "ar", "ar-SY"]);

        var options = BuildOptions(store: store);

        SupportedNames(options).Should().BeEquivalentTo(
            ["en", "ar", "ar-SY"],
            "the store cultures populate both the content and the UI culture lists");
        SupportedUiNames(options).Should().BeEquivalentTo(["en", "ar", "ar-SY"], "the UI culture list mirrors the content culture list");
        options.DefaultRequestCulture.Culture.Name.Should().Be("en", "the pipeline default culture is the request default");
        store.DiscoveryCount.Should().Be(1, "the store is queried once at options construction");
    }

    /// <summary>
    /// Verifies that registration options are cached and the store is queried once.
    /// </summary>
    [TestMethod]
    public void Registration_OptionsAreCached_AndTheStoreIsQueriedOnce() {
        var store = new FakeStore(["en"]);
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(store);
        services.AddLocalizationRequestCultures();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        var second = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

        second.Should().BeSameAs(first, "the options factory caches the configured instance");
        store.DiscoveryCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that registration explicit supported cultures skip store discovery.
    /// </summary>
    [TestMethod]
    public void Registration_ExplicitSupportedCultures_SkipStoreDiscovery() {
        var store = new FakeStore(["en", "ar"]);

        var options = BuildOptions(
            configure: static o => {
                o.SupportedCultures.Add("en");
                o.SupportedCultures.Add("de");
            },
            store: store);

        SupportedNames(options).Should().BeEquivalentTo(["en", "de"], "explicit cultures win over store discovery");
        store.DiscoveryCount.Should().Be(0, "the store must not be queried when explicit cultures are configured");
    }

    /// <summary>
    /// Verifies that registration unions the default culture into the served set.
    /// </summary>
    [TestMethod]
    public void Registration_UnionsTheDefaultCultureIntoTheServedSet() {
        var store = new FakeStore(["ar", "ar-SY"]);

        var options = BuildOptions(store: store);

        SupportedNames(options).Should().BeEquivalentTo(
            ["ar", "ar-SY", "en"],
            "the default culture is always part of the served set (the middleware applies it regardless)");
    }

    /// <summary>
    /// Verifies that registration deduplicates store cultures ordinal ignore case and preserves order.
    /// </summary>
    [TestMethod]
    public void Registration_DeduplicatesStoreCulturesOrdinalIgnoreCase_AndPreservesOrder() {
        var store = new FakeStore(["ar-SY", "EN", "en", "ar"]);

        var options = BuildOptions(store: store);

        SupportedNames(options).Should().BeEquivalentTo(
            ["ar-SY", "en", "ar"],
            "store order is preserved and duplicates are removed ordinal-ignore-case");
    }

    /// <summary>
    /// Verifies that registration uses pipeline default culture when not overridden.
    /// </summary>
    [TestMethod]
    public void Registration_UsesPipelineDefaultCulture_WhenNotOverridden() {
        var options = BuildOptions(store: new FakeStore(["ar"]), pipelineDefaultCulture: "ar");

        options.DefaultRequestCulture.Culture.Name.Should().Be("ar", "the registered LocalizationOptions default culture is honored");
        SupportedNames(options).Should().Contain("ar");
    }

    /// <summary>
    /// Verifies that registration prefers explicit default culture over pipeline default.
    /// </summary>
    [TestMethod]
    public void Registration_PrefersExplicitDefaultCultureOverPipelineDefault() {
        var options = BuildOptions(
            configure: static o => o.DefaultCulture = "ar",
            store: new FakeStore(["en"]),
            pipelineDefaultCulture: "en");

        options.DefaultRequestCulture.Culture.Name.Should().Be("ar", "the explicit DefaultCulture wins");
        SupportedNames(options).Should().BeEquivalentTo(["en", "ar"], "the explicit default is unioned into the served set");
    }

    /// <summary>
    /// Verifies that registration falls back to english when no default is configured.
    /// </summary>
    [TestMethod]
    public void Registration_FallsBackToEnglish_WhenNoDefaultIsConfigured() {
        var options = BuildOptions(store: new FakeStore(["ar"]));

        options.DefaultRequestCulture.Culture.Name.Should().Be("en", "the documented fallback default is 'en'");
    }

    /// <summary>
    /// Verifies that registration falls back to default culture only when the store throws.
    /// </summary>
    [TestMethod]
    public void Registration_FallsBackToDefaultCultureOnly_WhenTheStoreThrows() {
        var store = new FakeStore(new InvalidOperationException("store unavailable"));

        var options = BuildOptions(store: store);

        SupportedNames(options).Should().BeEquivalentTo(
            ["en"],
            "an unreachable store degrades deterministically to the default culture alone (never an empty list)");
        options.DefaultRequestCulture.Culture.Name.Should().Be("en");
    }

    /// <summary>
    /// Verifies that registration falls back to default culture only when no store is registered.
    /// </summary>
    [TestMethod]
    public void Registration_FallsBackToDefaultCultureOnly_WhenNoStoreIsRegistered() {
        var options = BuildOptions(pipelineDefaultCulture: "ar");

        SupportedNames(options).Should().BeEquivalentTo(
            ["ar"],
            "without a store the served set is the default culture alone (documented fallback)");
        options.DefaultRequestCulture.Culture.Name.Should().Be("ar");
    }

    /// <summary>
    /// Verifies that registration fails fast when the store reports zero cultures.
    /// </summary>
    [TestMethod]
    public void Registration_FailsFast_WhenTheStoreReportsZeroCultures() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(new FakeStore([]));
        services.AddLocalizationRequestCultures();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

        exception.Message.Should().Contain("zero supported cultures", "the failure must name the condition");
        exception.Message.Should().Contain(CONFIG_ERROR_MARKER, "the failure must be actionable (point at the explicit override)");
    }

    /// <summary>
    /// Verifies that registration fails fast when the store reports an invalid culture name.
    /// </summary>
    [TestMethod]
    public void Registration_FailsFast_WhenTheStoreReportsAnInvalidCultureName() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(new FakeStore(["en", "not!a!culture"]));
        services.AddLocalizationRequestCultures();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

        exception.Message.Should().Contain("not!a!culture", "the failure must name the offending value");
    }

    /// <summary>
    /// Verifies that registration validates arguments and options.
    /// </summary>
    [TestMethod]
    public void Registration_ValidatesArgumentsAndOptions() {
        Action nullServices = () => ((IServiceCollection)null!).AddLocalizationRequestCultures();

        nullServices.Should().Throw<ArgumentNullException>();

        var invalidExplicitCulture = new ServiceCollection();
        Action explicitCulture = () => invalidExplicitCulture.AddLocalizationRequestCultures(static o => o.SupportedCultures.Add("en!x"));

        explicitCulture.Should().Throw<CultureNotFoundException>("explicit culture misconfiguration fails at registration, not per request");

        var invalidDefault = new ServiceCollection();
        Action invalidDefaultCulture = () => invalidDefault.AddLocalizationRequestCultures(static o => o.DefaultCulture = "en!x");

        invalidDefaultCulture.Should().Throw<CultureNotFoundException>();

        var invalidRouteKey = new ServiceCollection();
        Action routeKey = () => invalidRouteKey.AddLocalizationRequestCultures(static o => o.RouteCultureKey = " ");

        routeKey.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that registration rejects ASecond composition.
    /// </summary>
    [TestMethod]
    public void Registration_RejectsASecondComposition() {
        var services = new ServiceCollection();
        services.AddLocalizationRequestCultures();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddLocalizationRequestCultures());

        exception.Message.Should().Contain("already registered");
    }

    /// <summary>
    /// Verifies that registration installs the served culture provider composition.
    /// </summary>
    [TestMethod]
    public void Registration_InstallsTheServedCultureProviderComposition() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        options.RequestCultureProviders.Should().HaveCount(4, "route + query string + cookie + Accept-Language");
        options.RequestCultureProviders.Should().AllBeOfType<ServedCultureRequestCultureProvider>(
            "every source is narrowed to the served set");
    }

    /// <summary>
    /// Verifies that configure does not mutate ambient culture.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public void Configure_DoesNotMutateAmbientCulture() {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        _ = BuildOptions(store: new FakeStore(["en", "ar"]));
        _ = BuildOptions(configure: static o => o.DefaultCulture = "ar");

        CultureInfo.CurrentCulture.Should().BeSameAs(culture, "the bridge must not mutate ambient culture (cross-talk elimination)");
        CultureInfo.CurrentUICulture.Should().BeSameAs(uiCulture);
        CultureInfo.DefaultThreadCurrentCulture.Should().BeSameAs(defaultCulture);
        CultureInfo.DefaultThreadCurrentUICulture.Should().BeSameAs(defaultUiCulture);
    }

    // ------------------------------------------------------- provider composition

    /// <summary>
    /// Verifies that middleware query resolves served cultures and unserved falls back to default.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_QueryResolvesServedCultures_AndUnservedFallsBackToDefaultAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar", "ar-SY"]));

        var served = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=ar"));
        served.Should().Be(("ar", "ar"), "a served query culture is selected");

        var unserved = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=fr"));
        unserved.Should().Be(("en", "en"), "an unserved request falls back deterministically to the default culture");
    }

    /// <summary>
    /// Verifies that middleware cookie resolves served cultures and unserved falls back to default.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_CookieResolvesServedCultures_AndUnservedFallsBackToDefaultAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar", "ar-SY"]));

        var served = await InvokeAsync(options, static context => SetCultureCookie(context, "ar-SY"));
        served.Should().Be(("ar-SY", "ar-SY"), "the canonical culture cookie is read by the framework provider");

        var unserved = await InvokeAsync(options, static context => SetCultureCookie(context, "fr"));
        unserved.Should().Be(("en", "en"));
    }

    /// <summary>
    /// Verifies that middleware accept language resolves served cultures and unserved falls back to default.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_AcceptLanguageResolvesServedCultures_AndUnservedFallsBackToDefaultAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar", "ar-SY"]));

        var served = await InvokeAsync(options, static context => context.Request.Headers.AcceptLanguage = "ar-SY,ar;q=0.8");
        served.Should().Be(("ar-SY", "ar-SY"), "the highest-quality served header value wins");

        var unserved = await InvokeAsync(options, static context => context.Request.Headers.AcceptLanguage = "fr-FR,fr;q=0.8");
        unserved.Should().Be(("en", "en"));
    }

    /// <summary>
    /// Verifies that middleware header skips unserved candidates and selects the next served one.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_HeaderSkipsUnservedCandidates_AndSelectsTheNextServedOneAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => context.Request.Headers.AcceptLanguage = "fr-FR,ar;q=0.8");

        result.Should().Be(("ar", "ar"), "the first served candidate is selected when an earlier candidate is unserved");
    }

    /// <summary>
    /// Verifies that middleware route resolves served cultures and unserved falls back to default.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_RouteResolvesServedCultures_AndUnservedFallsBackToDefaultAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar", "ar-SY"]));

        var served = await InvokeAsync(options, static context => context.Request.RouteValues["culture"] = "ar");
        served.Should().Be(("ar", "ar"), "the route source is supplied by the bridge (the framework has no route provider)");

        var unserved = await InvokeAsync(options, static context => context.Request.RouteValues["culture"] = "fr");
        unserved.Should().Be(("en", "en"));
    }

    /// <summary>
    /// Verifies that middleware route wins over query string.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_RouteWinsOverQueryStringAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => {
            context.Request.RouteValues["culture"] = "ar";
            context.Request.QueryString = new QueryString("?culture=en");
        });

        result.Should().Be(("ar", "ar"), "the composition order is route → query string → cookie → header");
    }

    /// <summary>
    /// Verifies that middleware query string wins over cookie.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_QueryStringWinsOverCookieAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => {
            context.Request.QueryString = new QueryString("?culture=en");
            SetCultureCookie(context, "ar");
        });

        result.Should().Be(("en", "en"));
    }

    /// <summary>
    /// Verifies that middleware cookie wins over accept language.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_CookieWinsOverAcceptLanguageAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => {
            SetCultureCookie(context, "ar");
            context.Request.Headers.AcceptLanguage = "en";
        });

        result.Should().Be(("ar", "ar"));
    }

    /// <summary>
    /// Verifies that middleware resolves culture and ui culture independently.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_ResolvesCultureAndUiCultureIndependentlyAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar", "ar-SY"]));

        var result = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=ar&ui-culture=ar-SY"));

        result.Should().Be(("ar", "ar-SY"), "the framework query provider carries both sides and the bridge narrows each side");
    }

    /// <summary>
    /// Verifies that middleware unserved ui side falls back to the default ui culture.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_UnservedUiSideFallsBackToTheDefaultUiCultureAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=ar&ui-culture=fr"));

        result.Should().Be(("ar", "en"), "a served culture side with an unserved UI side keeps the served side and defaults the UI side");
    }

    /// <summary>
    /// Verifies that middleware parent culture resolves to the served parent when parent fallback is enabled.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_ParentCultureResolvesToTheServedParent_WhenParentFallbackIsEnabledAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));

        var result = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=ar-SY"));

        result.Should().Be(("ar", "ar"), "the framework-configured parent fallback narrows ar-SY to the served parent ar");
    }

    /// <summary>
    /// Verifies that middleware parent culture falls back to default when parent fallback is disabled.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_ParentCultureFallsBackToDefault_WhenParentFallbackIsDisabledAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]));
        options.FallBackToParentCultures = false;
        options.FallBackToParentUICultures = false;

        var result = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=ar-SY"));

        result.Should().Be(("en", "en"), "with the parent fallback disabled an unserved variant resolves to the default culture");
    }

    /// <summary>
    /// Verifies that middleware canonicalizes the served culture name.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_CanonicalizesTheServedCultureNameAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar-SY"]));

        var result = await InvokeAsync(options, static context => context.Request.QueryString = new QueryString("?culture=AR-sy"));

        result.Should().Be(("ar-SY", "ar-SY"), "matching is case-insensitive and the canonical served name is applied");
    }

    /// <summary>
    /// Verifies that middleware no candidate resolves applies the default culture.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task Middleware_NoCandidateResolves_AppliesTheDefaultCultureAsync() {
        var options = BuildOptions(store: new FakeStore(["en", "ar"]), pipelineDefaultCulture: "ar");

        var result = await InvokeAsync(options);

        result.Should().Be(("ar", "ar"), "no source → the pipeline default culture applies");
    }

    // ------------------------------------------------------- shipped provider units

    /// <summary>
    /// Verifies that route value provider validates arguments and reads the configured key.
    /// </summary>
    [TestMethod]
    public void RouteValueProvider_ValidatesArgumentsAndReadsTheConfiguredKey() {
        Action nullKey = () => _ = new RouteValueRequestCultureProvider(null!);
        Action emptyKey = () => _ = new RouteValueRequestCultureProvider(" ");
        nullKey.Should().Throw<ArgumentNullException>();
        emptyKey.Should().Throw<ArgumentException>();

        var provider = new RouteValueRequestCultureProvider("locale");
        var context = new DefaultHttpContext();
        context.Request.RouteValues["locale"] = "ar";

        var result = provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult()!;

        result.Should().NotBeNull();
        result.Cultures[0].ToString().Should().Be("ar");
        result.UICultures[0].ToString().Should().Be("ar");
    }

    /// <summary>
    /// Verifies that route value provider no route value yields no result.
    /// </summary>
    [TestMethod]
    public void RouteValueProvider_NoRouteValue_YieldsNoResult() {
        var provider = new RouteValueRequestCultureProvider();
        var context = new DefaultHttpContext();

        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult().Should().BeNull();
    }

    /// <summary>
    /// Verifies that served provider validates arguments and requires options.
    /// </summary>
    [TestMethod]
    public async Task ServedProvider_ValidatesArgumentsAndRequiresOptionsAsync() {
        Action nullInner = () => _ = new ServedCultureRequestCultureProvider(null!);
        nullInner.Should().Throw<ArgumentNullException>();

        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider());
        var context = new DefaultHttpContext();
        context.Request.RouteValues["culture"] = "ar";

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => provider.DetermineProviderCultureResult(context));

        exception.Message.Should().Contain("Options", "the provider must be assigned the RequestLocalizationOptions before use");
    }

    /// <summary>
    /// Verifies that served provider narrows the inner result to the served set.
    /// </summary>
    [TestMethod]
    public void ServedProvider_NarrowsTheInnerResultToTheServedSet() {
        var options = new RequestLocalizationOptions {
            SupportedCultures = [new CultureInfo("en"), new CultureInfo("ar")]
        };
        options.SupportedUICultures = options.SupportedCultures;
        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider()) { Options = options };
        var context = new DefaultHttpContext();

        context.Request.RouteValues["culture"] = "ar";
        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult()!.Cultures[0].ToString().Should().Be("ar");

        context.Request.RouteValues["culture"] = "fr";
        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult().Should().BeNull(
            "an unserved culture yields no result so the middleware applies the default");
    }

    /// <summary>
    /// Verifies that served provider empty served set yields no result.
    /// </summary>
    [TestMethod]
    public void ServedProvider_EmptyServedSet_YieldsNoResult() {
        var options = new RequestLocalizationOptions {
            SupportedCultures = [],
            SupportedUICultures = []
        };
        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider()) { Options = options };
        var context = new DefaultHttpContext();
        context.Request.RouteValues["culture"] = "ar";

        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult().Should().BeNull();
    }

    /// <summary>
    /// Verifies that served provider null supported list passes the result through.
    /// </summary>
    [TestMethod]
    public void ServedProvider_NullSupportedList_PassesTheResultThrough() {
        var options = new RequestLocalizationOptions {
            SupportedCultures = null,
            SupportedUICultures = null
        };
        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider()) { Options = options };
        var context = new DefaultHttpContext();
        context.Request.RouteValues["culture"] = "fr-CA";

        var result = provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult()!;

        result.Cultures[0].ToString().Should().Be("fr-CA", "a null supported list means no narrowing (framework semantics)");
    }

    /// <summary>
    /// Verifies that served provider invalid candidate name does not throw with parent fallback.
    /// </summary>
    [TestMethod]
    public void ServedProvider_InvalidCandidateName_DoesNotThrow_WithParentFallback() {
        var options = new RequestLocalizationOptions {
            SupportedCultures = [new CultureInfo("en")],
            SupportedUICultures = [new CultureInfo("en")]
        };
        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider()) { Options = options };
        var context = new DefaultHttpContext();
        context.Request.RouteValues["culture"] = "not!a!culture";

        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult().Should().BeNull(
            "an invalid culture name is not served; parent resolution must not throw");
    }

    /// <summary>
    /// Verifies that served provider inner yields no result yields no result.
    /// </summary>
    [TestMethod]
    public void ServedProvider_InnerYieldsNoResult_YieldsNoResult() {
        var options = new RequestLocalizationOptions {
            SupportedCultures = [new CultureInfo("en")],
            SupportedUICultures = [new CultureInfo("en")]
        };
        var provider = new ServedCultureRequestCultureProvider(new RouteValueRequestCultureProvider()) { Options = options };
        var context = new DefaultHttpContext();

        provider.DetermineProviderCultureResult(context).GetAwaiter().GetResult().Should().BeNull();
    }

    // ------------------------------------------------------------------ end to end

    /// <summary>
    /// Verifies that end to end one registration yields AWorking middleware configuration.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task EndToEnd_OneRegistrationYieldsAWorkingMiddlewareConfigurationAsync() {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizationStore>(new FakeStore(["en", "ar", "ar-SY"]));
        services.AddLocalizationRequestCultures();
        await using var provider = services.BuildServiceProvider();

        var middleware = new RequestLocalizationMiddleware(
            static _ => Task.CompletedTask,
            provider.GetRequiredService<IOptions<RequestLocalizationOptions>>(),
            NullLoggerFactory.Instance);

        var queryContext = new DefaultHttpContext();
        queryContext.Request.QueryString = new QueryString("?culture=ar");
        await middleware.Invoke(queryContext);
        queryContext.Features.Get<IRequestCultureFeature>()!.RequestCulture.Culture.Name.Should().Be(
            "ar",
            "the Blazor-sample-equivalent query selection still resolves from store cultures after the bridge registration");

        var cookieContext = new DefaultHttpContext();
        SetCultureCookie(cookieContext, "ar-SY");
        await middleware.Invoke(cookieContext);
        cookieContext.Features.Get<IRequestCultureFeature>()!.RequestCulture.Culture.Name.Should().Be(
            "ar-SY",
            "the Blazor cookie store's canonical cookie is still honored after the bridge registration");

        var defaultContext = new DefaultHttpContext();
        await middleware.Invoke(defaultContext);
        defaultContext.Features.Get<IRequestCultureFeature>()!.RequestCulture.Culture.Name.Should().Be(
            "en",
            "an unnamed request falls back to the configured default culture");
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Builds options.
    /// </summary>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <param name="store">The localization store.</param>
    /// <param name="pipelineDefaultCulture">The pipeline default culture.</param>
    /// <returns>The options result.</returns>
    private static RequestLocalizationOptions BuildOptions(
        Action<LocalizationRequestCultureOptions>? configure = null,
        ILocalizationStore? store = null,
        string? pipelineDefaultCulture = null) {
        var services = new ServiceCollection();
        if(store is not null) {
            services.AddSingleton(store);
        }

        if(pipelineDefaultCulture is not null) {
            services.AddSingleton(new LocalizationOptions { DefaultCulture = pipelineDefaultCulture });
        }

        services.AddLocalizationRequestCultures(configure);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    }

    /// <summary>
    /// Performs the invoke operation.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <param name="configureContext">The configure context.</param>
    /// <returns>A task whose result is invoke.</returns>
    private static async Task<(string Culture, string UICulture)> InvokeAsync(RequestLocalizationOptions options, Action<DefaultHttpContext>? configureContext = null) {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try {
            var context = new DefaultHttpContext();
            configureContext?.Invoke(context);

            var middleware = new RequestLocalizationMiddleware(
                static _ => Task.CompletedTask,
                Options.Create(options),
                NullLoggerFactory.Instance);
            await middleware.Invoke(context);

            var feature = context.Features.Get<IRequestCultureFeature>()!;
            return (feature.RequestCulture.Culture.Name, feature.RequestCulture.UICulture.Name);
        }
        finally {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    /// <summary>
    /// Sets culture cookie.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="culture">The culture.</param>
    private static void SetCultureCookie(DefaultHttpContext context, string culture) =>
        context.Request.Headers.Cookie = $"{CookieRequestCultureProvider.DefaultCookieName}={CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture))}";

    /// <summary>
    /// Performs the supported names operation.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <returns>The supported names sequence.</returns>
    private static IEnumerable<string> SupportedNames(RequestLocalizationOptions options) =>
        options.SupportedCultures!.Select(static culture => culture.Name);

    /// <summary>
    /// Performs the supported ui names operation.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <returns>The supported ui names sequence.</returns>
    private static IEnumerable<string> SupportedUiNames(RequestLocalizationOptions options) =>
        options.SupportedUICultures!.Select(static culture => culture.Name);

    /// <summary>
    /// Stores fake.
    /// </summary>
    private sealed class FakeStore : ILocalizationStore {
        private readonly Func<List<string>> _discover;

        /// <summary>
        /// Initializes a new instance of the FakeStore class.
        /// </summary>
        /// <param name="cultures">The cultures.</param>
        public FakeStore(IReadOnlyList<string> cultures) => _discover = () => [.. cultures];

        /// <summary>
        /// Initializes a new instance of the FakeStore class.
        /// </summary>
        /// <param name="exception">The exception.</param>
        public FakeStore(Exception exception) => _discover = () => throw exception;

        /// <summary>
        /// Gets or sets the discovery count.
        /// </summary>
        public int DiscoveryCount { get; private set; }

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoveryCount++;
            return Task.FromResult(_discover());
        }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult<ILocalizationResourceSet?>(null);

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ILocalizationResourceSet>());

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            localizationResourceSet = null;
            return false;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(LocalizedStringEntry.Missing(resourceSet, culture, name));

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>());

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            entry = null;
            return false;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
