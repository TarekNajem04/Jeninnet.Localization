//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the HTML-localizer factory bridge:
/// both BCL (Base Class Library) creation paths, behavioral parity with the DI-resolved
/// <see cref="IHtmlLocalizer{T}"/> bridge (PIPELINE trace + output, not
/// implementation coincidence), the recorded key-based mapping decision,
/// culture semantics, registration/replacement behavior, and input validation.
/// </summary>
[TestClass]
public class HtmlLocalizerFactoryTests {
    private const string GREET_TEMPLATE = "Hello {0}!";

    // ------------------------------------------------------------- registration

    /// <summary>
    /// Verifies that registration resolves the factory from di.
    /// </summary>
    [TestMethod]
    public void Registration_ResolvesTheFactoryFromDi() {
        using var provider = BuildProvider(new Dictionary<string, string> { ["greet"] = GREET_TEMPLATE });

        var factory = provider.GetRequiredService<IHtmlLocalizerFactory>();

        factory.Should().BeOfType<HtmlMessageLocalizerFactory>("the opt-in takes over the framework factory");
        var created = factory.Create(typeof(ICatalogResource));
        RunWithCurrentUiCulture("en", () => created["greet", "<b>x</b>"].Value.Should().Be("Hello &lt;b&gt;x&lt;/b&gt;!"));
    }

    /// <summary>
    /// Verifies that registration replaces APre registered framework factory.
    /// </summary>
    [TestMethod]
    public void Registration_ReplacesAPreRegisteredFrameworkFactory() {
        var services = new ServiceCollection();
        services.AddSingleton<IHtmlLocalizerFactory>(new FrameworkLikeFactory());
        services.AddMemoryLocalization(new Dictionary<string, string> { ["greet"] = GREET_TEMPLATE });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddHtmlMessageLocalizer();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHtmlLocalizerFactory>().Should().BeOfType<HtmlMessageLocalizerFactory>();
        provider.GetServices<IHtmlLocalizerFactory>().Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that registration does not duplicate the factory on repeated composition.
    /// </summary>
    [TestMethod]
    public void Registration_DoesNotDuplicateTheFactoryOnRepeatedComposition() {
        var services = new ServiceCollection();
        services.AddHtmlMessageLocalizer();
        services.AddHtmlMessageLocalizer();

        services.Count(static descriptor => descriptor.ServiceType == typeof(IHtmlLocalizerFactory)).Should().Be(
            1,
            "the opt-in replaces its own registration instead of stacking factories");
    }

    /// <summary>
    /// Verifies that html opt in does not touch the string factory mapping.
    /// </summary>
    [TestMethod]
    public void HtmlOptIn_DoesNotTouchTheStringFactoryMapping() {
        var services = new ServiceCollection();
        services.AddMvcLocalizedValidation();
        services.AddHtmlMessageLocalizer();

        services.First(static descriptor => descriptor.ServiceType == typeof(IStringLocalizerFactory)).ImplementationType.Should().Be(
            typeof(CustomStringLocalizerFactory),
            "the single type→resource-set mapping owner stays Core's string factory");
        services.First(static descriptor => descriptor.ServiceType == typeof(IHtmlLocalizerFactory)).ImplementationType.Should().Be(
            typeof(HtmlMessageLocalizerFactory));
    }

    // ---------------------------------------------------------- creation parity

    /// <summary>
    /// Verifies that create from type formats exactly like the generic di localizer.
    /// </summary>
    [TestMethod]
    public void CreateFromType_FormatsExactlyLikeTheGenericDiLocalizer() {
        var genericProvider = new RecordingProvider(EnglishTemplates());
        var generic = new HtmlMessageLocalizer<ICatalogResource>(new DefaultMessageLocalizer(
            genericProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        var factoryProvider = new RecordingProvider(EnglishTemplates());
        var factory = new HtmlMessageLocalizerFactory(new DefaultMessageLocalizer(
            factoryProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        var fromType = factory.Create(typeof(ICatalogResource));

        var (Generic, FromType) = RunWithCurrentUiCulture("fr-CA", () => (
            Generic: generic["greet", "<i>x</i>"].Value,
            FromType: fromType["greet", "<i>x</i>"].Value));

        FromType.Should().Be(Generic, "the type creation path formats exactly as the generic IHtmlLocalizer<T> bridge");
        Assert.AreSequenceEqual(
            genericProvider.Requests,
            factoryProvider.Requests,
            "both bridges walk the identical canonical fallback chain (fr-CA → fr → en default) in order");
    }

    /// <summary>
    /// Verifies that create from base name and location formats exactly like the generic di localizer.
    /// </summary>
    [TestMethod]
    public void CreateFromBaseNameAndLocation_FormatsExactlyLikeTheGenericDiLocalizer() {
        var genericProvider = new RecordingProvider(EnglishTemplates());
        var generic = new HtmlMessageLocalizer<ICatalogResource>(new DefaultMessageLocalizer(
            genericProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        var factoryProvider = new RecordingProvider(EnglishTemplates());
        var factory = new HtmlMessageLocalizerFactory(new DefaultMessageLocalizer(
            factoryProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        var fromBaseName = factory.Create("Views.Catalog.Index", "/Views/Catalog/Index.cshtml");

        var (Generic, FromBaseName) = RunWithCurrentUiCulture("en", () => (
            Generic: generic["greet", "<i>x</i>"].Value,
            FromBaseName: fromBaseName["greet", "<i>x</i>"].Value));

        FromBaseName.Should().Be(Generic, "the base-name/location creation path formats exactly as the generic bridge");
        Assert.AreSequenceEqual(genericProvider.Requests, factoryProvider.Requests);
    }

    /// <summary>
    /// Verifies that factory creation matches the di resolved localizers three way.
    /// </summary>
    [TestMethod]
    public void FactoryCreation_MatchesTheDiResolvedLocalizers_ThreeWay() {
        using var provider = BuildProvider(new Dictionary<string, string> { ["greet"] = GREET_TEMPLATE });

        var factory = provider.GetRequiredService<IHtmlLocalizerFactory>();
        var fromFactory = factory.Create(typeof(ICatalogResource));
        var generic = provider.GetRequiredService<IHtmlLocalizer<ICatalogResource>>();
        var nonGeneric = provider.GetRequiredService<IHtmlLocalizer>();

        RunWithCurrentUiCulture("en", () => {
            var expected = generic["greet", "<b>x</b>"].Value;
            fromFactory["greet", "<b>x</b>"].Value.Should().Be(expected);
            nonGeneric["greet", "<b>x</b>"].Value.Should().Be(expected);
        });
    }

    /// <summary>
    /// Verifies that factory every creation path resolves identically key based bridge.
    /// </summary>
    [TestMethod]
    public void Factory_EveryCreationPath_ResolvesIdentically_KeyBasedBridge() {
        var factory = new HtmlMessageLocalizerFactory(new DefaultMessageLocalizer(
            new RecordingProvider(EnglishTemplates()), new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));

        var fromFirstType = factory.Create(typeof(ICatalogResource));
        var fromSecondType = factory.Create(typeof(IOrdersResource));
        var fromBaseName = factory.Create("Any.Resource.Set", "/Any/Location.cshtml");

        var (FirstType, SecondType, BaseName) = RunWithCurrentUiCulture("en", () => (
            FirstType: fromFirstType["greet", "<i>x</i>"].Value,
            SecondType: fromSecondType["greet", "<i>x</i>"].Value,
            BaseName: fromBaseName["greet", "<i>x</i>"].Value));

        FirstType.Should().Be(SecondType, "the HTML bridge is key-based: the type does not scope the lookup");
        BaseName.Should().Be(FirstType, "the base name/location does not scope the lookup either");
    }

    // ------------------------------------------------------------  semantics

    /// <summary>
    /// Verifies that factory created localizer keeps the Mf04Semantics.
    /// </summary>
    [TestMethod]
    public void Factory_CreatedLocalizer_KeepsTheMf04Semantics() {
        var factory = CreateFactory(EnglishTemplates());
        var localizer = factory.Create(typeof(ICatalogResource));

        RunWithCurrentUiCulture("en", () => {
            var missing = localizer["missing"];
            missing.Name.Should().Be("missing");
            missing.Value.Should().Be("missing", "the framework missing convention carries the key");
            missing.IsResourceNotFound.Should().BeTrue();

            var getString = localizer.GetString("greet", "<script>alert(1)</script>");
            getString.Value.Should().Be("Hello &lt;script&gt;alert(1)&lt;/script&gt;!");
            getString.ResourceNotFound.Should().BeFalse();
        });

        Action enumerate = () => localizer.GetAllStrings(includeParentCultures: true);
        enumerate.Should().Throw<NotSupportedException>("key enumeration is not part of the exact-match message pipeline");
    }

    /// <summary>
    /// Verifies that factory captures the current ui culture per call.
    /// </summary>
    [TestMethod]
    public void Factory_CapturesTheCurrentUiCulturePerCall() {
        var factory = CreateFactory(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello!" },
            ["ar"] = new() { ["greet"] = "ar-hello" }
        });
        var localizer = factory.Create(typeof(ICatalogResource));

        RunWithCurrentUiCulture("en", () => localizer["greet"].Value.Should().Be("Hello!"));
        RunWithCurrentUiCulture("ar", () => localizer["greet"].Value.Should().Be("ar-hello"));
    }

    /// <summary>
    /// Verifies that factory created localizer supports with culture.
    /// </summary>
    [TestMethod]
    public void Factory_CreatedLocalizer_SupportsWithCulture() {
        var factory = CreateFactory(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello!" },
            ["ar"] = new() { ["greet"] = "ar-hello" }
        });
        var arabic = ((HtmlMessageLocalizer)factory.Create(typeof(ICatalogResource))).WithCulture(new CultureInfo("ar"));

        RunWithCurrentUiCulture("en", () => arabic["greet"].Value.Should().Be("ar-hello", "the fixed culture wins over the ambient UI culture"));
    }

    // ---------------------------------------------------------------- validation

    /// <summary>
    /// Verifies that factory validates arguments.
    /// </summary>
    [TestMethod]
    public void Factory_ValidatesArguments() {
        Action nullLocalizer = () => _ = new HtmlMessageLocalizerFactory(null!);
        nullLocalizer.Should().Throw<ArgumentNullException>();

        var factory = CreateFactory(EnglishTemplates());
        Action nullType = () => factory.Create((Type)null!);
        nullType.Should().Throw<ArgumentNullException>();

        Action nullBaseName = () => factory.Create(null!, "/Views/Index.cshtml");
        Action emptyBaseName = () => factory.Create(string.Empty, "/Views/Index.cshtml");
        Action nullLocation = () => factory.Create("Base", null!);

        nullBaseName.Should().Throw<ArgumentNullException>();
        emptyBaseName.Should().Throw<ArgumentNullException>("the input contract mirrors CustomStringLocalizerFactory.Create(string, string)");
        nullLocation.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Performs the english templates operation.
    /// </summary>
    /// <returns>The english templates result.</returns>
    private static Dictionary<string, Dictionary<string, string>> EnglishTemplates() => new() {
        ["en"] = new() { ["greet"] = GREET_TEMPLATE }
    };

    /// <summary>
    /// Creates factory.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <returns>The factory result.</returns>
    private static HtmlMessageLocalizerFactory CreateFactory(Dictionary<string, Dictionary<string, string>> templates) =>
        new(new DefaultMessageLocalizer(
            new RecordingProvider(templates), new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));

    /// <summary>
    /// Builds provider.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <returns>The provider result.</returns>
    private static ServiceProvider BuildProvider(Dictionary<string, string> messages) {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(messages);
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddHtmlMessageLocalizer();
        return services.BuildServiceProvider();
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
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    /// <typeparam name="T">The type of t.</typeparam>
    /// <returns>The with current ui culture result.</returns>
    private static T RunWithCurrentUiCulture<T>(string cultureName, Func<T> action) {
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return action();
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Defines the contract for a catalog resource.
    /// </summary>
    private interface ICatalogResource;

    /// <summary>
    /// Defines the contract for an orders resource.
    /// </summary>
    private interface IOrdersResource;

    /// <summary>
    /// Creates framework like.
    /// </summary>
    private sealed class FrameworkLikeFactory : IHtmlLocalizerFactory {
        /// <summary>
        /// Performs the create operation.
        /// </summary>
        /// <param name="resourceSource">The resource source.</param>
        /// <returns>The create result.</returns>
        public IHtmlLocalizer Create(Type resourceSource) => throw new NotSupportedException("the framework default must have been replaced");

        /// <summary>
        /// Performs the create operation.
        /// </summary>
        /// <param name="baseName">The base name.</param>
        /// <param name="location">The location.</param>
        /// <returns>The create result.</returns>
        public IHtmlLocalizer Create(string baseName, string location) => throw new NotSupportedException("the framework default must have been replaced");
    }

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class RecordingProvider(Dictionary<string, Dictionary<string, string>> templates) : IMessageProvider {
        /// <summary>
        /// Gets the requests.
        /// </summary>
        public List<(string Key, string Culture)> Requests { get; } = [];

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            Requests.Add((key, culture.Name));
            return ValueTask.FromResult(
                templates.TryGetValue(culture.Name, out var byKey) && byKey.TryGetValue(key, out var template) ? template : null);
        }
    }
}
