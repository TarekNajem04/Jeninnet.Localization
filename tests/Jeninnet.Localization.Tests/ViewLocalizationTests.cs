//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for view/Razor localization: view-path scope resolution
///, resolution order (scoped → shared,
/// each through the canonical culture-fallback chain), fallback equivalence with the
/// plain pipeline, and  HTML-safe output through the view surface.
/// </summary>
[TestClass]
public class ViewLocalizationTests {
    private const string CATALOG_INDEX_PATH = "/Views/Catalog/Index.cshtml";
    private const string SCOPED_CONTEXT_SEPARATOR = "\u0004";

    /// <summary>
    /// Verifies that same key resolves differently per view where scoped entries exist.
    /// </summary>
    [TestMethod]
    public void SameKey_ResolvesDifferentlyPerView_WhereScopedEntriesExist() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Index view heading",
                ["Views.Catalog.Details" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Details view heading",
                ["heading"] = "Shared heading"
            }
        };
        var indexView = CreateViewLocalizer(new RecordingProvider(templates), CATALOG_INDEX_PATH);
        var detailsView = CreateViewLocalizer(new RecordingProvider(templates), "/Views/Catalog/Details.cshtml");
        var unscopedView = CreateViewLocalizer(new RecordingProvider(templates), "/Views/Home/About.cshtml");

        RunWithCurrentUiCulture("en", () => {
            indexView["heading"].Value.Should().Be("Index view heading");
            detailsView["heading"].Value.Should().Be("Details view heading");
            unscopedView["heading"].Value.Should().Be("Shared heading", "a view without scoped entries resolves identically to the shared pipeline");
        });
    }

    /// <summary>
    /// Verifies that indexer resolves the scoped entry first then the shared entry.
    /// </summary>
    [TestMethod]
    public void Indexer_ResolvesTheScopedEntryFirst_ThenTheSharedEntry() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Scoped heading",
                ["heading"] = "Shared heading"
            }
        };
        var (scoped, scopedProvider) = CreateViewLocalizerWithProvider(templates, CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("en", () => scoped["heading"].Value.Should().Be("Scoped heading"));

        scopedProvider.Requests.Should().HaveCount(1);
        scopedProvider.Requests[0].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "en"),
            "the scoped lookup is the gettext-canonical context entry");

        var (shared, sharedProvider) = CreateViewLocalizerWithProvider(templates, "/Views/Home/About.cshtml");

        RunWithCurrentUiCulture("en", () => shared["heading"].Value.Should().Be("Shared heading"));

        sharedProvider.Requests.Should().HaveCount(3,
            "the scoped probe walks the full canonical chain (exact 'en' plus the default-culture 'en' stage) before the shared lookup");
        sharedProvider.Requests[0].Key.Should().StartWith("Views.Home.About" + SCOPED_CONTEXT_SEPARATOR,
            "the scoped probe still runs first and misses");
        sharedProvider.Requests[^1].Should().Be(("heading", "en"), "the shared lookup is the plain pipeline key");
    }

    /// <summary>
    /// Verifies that contextualize derives the scope from the executing file path first.
    /// </summary>
    [TestMethod]
    public void Contextualize_DerivesTheScopeFromTheExecutingFilePathFirst() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Executing file scope"
            }
        };
        var provider = new RecordingProvider(templates);
        var localizer = CreateViewLocalizer(provider, "/Views/Shared/_Layout.cshtml", executingFilePath: CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("en", () => localizer["heading"].Value.Should().Be("Executing file scope",
            "the executing file (partials, theme-expanded locations) owns the scope, not the layout path"));

        provider.Requests[0].Key.Should().Be("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading");
    }

    /// <summary>
    /// Verifies that contextualize normalizes the view path to the scope context.
    /// </summary>
    [TestMethod]
    public void Contextualize_NormalizesTheViewPathToTheScopeContext() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Home.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Scoped heading"
            }
        };

        foreach(var path in new[] { "/Views/Home/Index.cshtml", "Views/Home/Index.cshtml", "\\Views\\Home\\Index.cshtml" }) {
            var provider = new RecordingProvider(templates);
            var localizer = CreateViewLocalizer(provider, path);

            RunWithCurrentUiCulture("en", () => localizer["heading"].Value.Should().Be("Scoped heading"));

            provider.Requests[0].Key.Should().Be("Views.Home.Index" + SCOPED_CONTEXT_SEPARATOR + "heading",
                $"'{path}' must normalize to the same resource-style scope");
        }
    }

    /// <summary>
    /// Verifies that scoped resolution uses the canonical culture fallback.
    /// </summary>
    [TestMethod]
    public void ScopedResolution_UsesTheCanonicalCultureFallback() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Scoped (ar)"
            }
        };
        var provider = new RecordingProvider(templates);
        var localizer = CreateViewLocalizer(provider, CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("ar-SY", () => localizer["heading"].Value.Should().Be("Scoped (ar)",
            "the scoped lookup walks the canonical culture chain (exact → parent → default) before the shared fallback"));

        provider.Requests.Should().HaveCount(2);
        provider.Requests[0].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar-SY"));
        provider.Requests[1].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar"));
    }

    /// <summary>
    /// Verifies that scoped entry in AParent culture wins over AShared entry in the exact culture.
    /// </summary>
    [TestMethod]
    public void ScopedEntryInAParentCulture_WinsOverASharedEntryInTheExactCulture() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["ar-SY"] = new() { ["heading"] = "Shared exact (ar-SY)" },
            ["ar"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Scoped parent (ar)"
            }
        };
        var provider = new RecordingProvider(templates);
        var localizer = CreateViewLocalizer(provider, CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("ar-SY", () => localizer["heading"].Value.Should().Be("Scoped parent (ar)",
            ": the scope dominates the culture dimension; the shared exact-culture entry is never reached"));

        provider.Requests.Should().HaveCount(2);
        provider.Requests[0].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar-SY"));
        provider.Requests[1].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar"));
    }

    /// <summary>
    /// Verifies that missing view scope falls back exactly like the pipeline fallback.
    /// </summary>
    [TestMethod]
    public void MissingViewScope_FallsBackExactlyLikeThePipelineFallback() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["heading"] = "Shared heading (en)" }
        };
        var viewProvider = new RecordingProvider(templates);
        var plainProvider = new RecordingProvider(templates);
        var viewLocalizer = CreateViewLocalizer(viewProvider, CATALOG_INDEX_PATH);
        var plainLocalizer = new DefaultMessageLocalizer(plainProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });
        var culture = new CultureInfo("ar-SY");

        var (View, Plain) = RunWithCurrentUiCulture("ar-SY", () => {
            var viewValue = viewLocalizer["heading"].Value;
            var plainValue = plainLocalizer.LocalizeAsync("heading", new Dictionary<string, object?>(), culture, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            return (View: viewValue, Plain: plainValue);
        });

        View.Should().Be(Plain);

        viewProvider.Requests.Should().HaveCount(6, "the scoped probe walks the full chain before the shared lookup repeats it");
        viewProvider.Requests[0].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar-SY"));
        viewProvider.Requests[1].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "ar"));
        viewProvider.Requests[2].Should().Be(("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading", "en"));

        var sharedTail = viewProvider.Requests.Skip(3).ToList();
        sharedTail.Should().HaveCount(plainProvider.Requests.Count);
        for(var index = 0; index < plainProvider.Requests.Count; index++) {
            sharedTail[index].Should().Be(plainProvider.Requests[index],
                "the shared step must issue exactly the pipeline's own request sequence");
        }
    }

    /// <summary>
    /// Verifies that missing key everywhere keeps the framework missing convention.
    /// </summary>
    [TestMethod]
    public void MissingKeyEverywhere_KeepsTheFrameworkMissingConvention() {
        var localizer = CreateViewLocalizer(new RecordingProvider([]), CATALOG_INDEX_PATH);

        var result = RunWithCurrentUiCulture("en", () => localizer["ghost"]);

        result.Name.Should().Be("ghost");
        result.Value.Should().Be("ghost");
        result.IsResourceNotFound.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that indexer encodes arguments through mf04 on scoped and shared paths.
    /// </summary>
    [TestMethod]
    public void Indexer_EncodesArgumentsThroughMf04_OnScopedAndSharedPaths() {
        const string payload = "<img src=x onerror=alert(1)>";
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "<b>Scoped {0}!</b>",
                ["greet"] = "<b>Shared {0}!</b>"
            }
        };
        var scopedView = CreateViewLocalizer(new RecordingProvider(templates), CATALOG_INDEX_PATH);
        var sharedView = CreateViewLocalizer(new RecordingProvider(templates), "/Views/Home/About.cshtml");

        RunWithCurrentUiCulture("en", () => {
            var scopedResult = scopedView["greet", payload];
            scopedResult.Value.Should().Be("<b>Scoped &lt;img src=x onerror=alert(1)&gt;!</b>");
            sharedView["greet", payload].Value.Should().Be("<b>Shared &lt;img src=x onerror=alert(1)&gt;!</b>");

            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            scopedResult.WriteTo(writer, HtmlEncoder.Default);
            writer.ToString().Should().Be("<b>Scoped &lt;img src=x onerror=alert(1)&gt;!</b>",
                "Razor writes the localized HTML raw: scoped template markup stays live while argument markup stays inert");
        });
    }

    /// <summary>
    /// Verifies that get string applies the view scope and the shared fallback.
    /// </summary>
    [TestMethod]
    public void GetString_AppliesTheViewScopeAndTheSharedFallback() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "Scoped heading",
                ["other"] = "Shared other"
            }
        };
        var localizer = CreateViewLocalizer(new RecordingProvider(templates), CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("en", () => {
            var scoped = localizer.GetString("heading");
            scoped.ResourceNotFound.Should().BeFalse();
            scoped.Value.Should().Be("Scoped heading");

            var shared = localizer.GetString("other");
            shared.ResourceNotFound.Should().BeFalse();
            shared.Value.Should().Be("Shared other");

            var missing = localizer.GetString("ghost");
            missing.ResourceNotFound.Should().BeTrue();
            missing.Value.Should().Be("ghost");
        });
    }

    /// <summary>
    /// Verifies that indexer captures the current UICulture per call.
    /// </summary>
    [TestMethod]
    public void Indexer_CapturesTheCurrentUICulturePerCall() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "Hello!" },
            ["ar"] = new() { ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "ar-hello" }
        };
        var localizer = CreateViewLocalizer(new RecordingProvider(templates), CATALOG_INDEX_PATH);

        RunWithCurrentUiCulture("en", () => localizer["greet"].Value.Should().Be("Hello!"));
        RunWithCurrentUiCulture("ar", () => localizer["greet"].Value.Should().Be("ar-hello"));
    }

    /// <summary>
    /// Verifies that with culture resolves in the fixed culture keeping the view scope.
    /// </summary>
    [TestMethod]
    public void WithCulture_ResolvesInTheFixedCulture_KeepingTheViewScope() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "Hello!" },
            ["ar"] = new() { ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "ar-hello" }
        };
        var localizer = CreateViewLocalizer(new RecordingProvider(templates), CATALOG_INDEX_PATH);
        var arabic = localizer.WithCulture(new CultureInfo("ar"));

        RunWithCurrentUiCulture("en", () => arabic["greet"].Value.Should().Be("ar-hello",
            "the fixed culture applies while the view scope is preserved"));
    }

    /// <summary>
    /// Verifies that without contextualize resolves shared only.
    /// </summary>
    [TestMethod]
    public void WithoutContextualize_ResolvesSharedOnly() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["heading"] = "Shared heading" }
        };
        var provider = new RecordingProvider(templates);
        var localizer = new ViewMessageLocalizer(new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));

        RunWithCurrentUiCulture("en", () => localizer["heading"].Value.Should().Be("Shared heading"));

        provider.Requests.Should().HaveCount(1);
        provider.Requests[0].Should().Be(("heading", "en"), "an uncontextualized bridge degrades to the shared pipeline with no scoped probe");
    }

    /// <summary>
    /// Verifies that contextualize with no usable view path resolves shared only.
    /// </summary>
    [TestMethod]
    public void Contextualize_WithNoUsableViewPath_ResolvesSharedOnly() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["heading"] = "Shared heading" }
        };
        var provider = new RecordingProvider(templates);
        var localizer = CreateViewLocalizer(provider, viewPath: string.Empty);

        RunWithCurrentUiCulture("en", () => localizer["heading"].Value.Should().Be("Shared heading"));

        provider.Requests.Should().HaveCount(1);
        provider.Requests[0].Should().Be(("heading", "en"));
    }

    /// <summary>
    /// Verifies that contextualize falls back to the view path when the executing path is empty.
    /// </summary>
    [TestMethod]
    public void Contextualize_FallsBackToTheViewPath_WhenTheExecutingPathIsEmpty() {
        var templates = new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading"] = "View path scope"
            }
        };
        var provider = new RecordingProvider(templates);
        var localizer = CreateViewLocalizer(provider, CATALOG_INDEX_PATH, executingFilePath: string.Empty);

        RunWithCurrentUiCulture("en", () => localizer["heading"].Value.Should().Be("View path scope"));

        provider.Requests[0].Key.Should().Be("Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "heading");
    }

    /// <summary>
    /// Verifies that bridge validates arguments.
    /// </summary>
    [TestMethod]
    public void Bridge_ValidatesArguments() {
        Action nullLocalizer = () => _ = new ViewMessageLocalizer(null!);
        Action nullViewContext = () => CreateViewLocalizer(new RecordingProvider([]), CATALOG_INDEX_PATH)
            .Contextualize(null!);

        nullLocalizer.Should().Throw<ArgumentNullException>();
        nullViewContext.Should().Throw<ArgumentNullException>();

        var localizer = CreateViewLocalizer(new RecordingProvider([]), CATALOG_INDEX_PATH);
        Action nullName = () => RunWithCurrentUiCulture("en", () => localizer[null!]);

        nullName.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that get all strings throws not is supported.
    /// </summary>
    [TestMethod]
    public void GetAllStrings_ThrowsNotSupported() {
        var localizer = CreateViewLocalizer(new RecordingProvider([]), CATALOG_INDEX_PATH);

        Action act = () => localizer.GetAllStrings(includeParentCultures: true);

        act.Should().Throw<NotSupportedException>("key enumeration is not part of the exact-match message pipeline");
    }

    /// <summary>
    /// Verifies that add view message localizer resolves the bridge from DI (Dependency Injection) and scopes per resolved instance.
    /// </summary>
    [TestMethod]
    public void AddViewMessageLocalizer_ResolvesTheBridgeFromDI_AndScopesPerResolvedInstance() {
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> {
            ["Views.Catalog.Index" + SCOPED_CONTEXT_SEPARATOR + "greet"] = "Scoped hello",
            ["greet"] = "Shared hello"
        });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddViewMessageLocalizer();

        using var provider = services.BuildServiceProvider();
        var indexLocalizer = provider.GetRequiredService<IViewLocalizer>();
        var unscopedLocalizer = provider.GetRequiredService<IViewLocalizer>();

        indexLocalizer.Should().BeOfType<ViewMessageLocalizer>();
        ((IViewContextAware)indexLocalizer).Contextualize(CreateViewContext(CATALOG_INDEX_PATH));
        ((IViewContextAware)unscopedLocalizer).Contextualize(CreateViewContext("/Views/Home/About.cshtml"));

        RunWithCurrentUiCulture("en", () => {
            indexLocalizer["greet"].Value.Should().Be("Scoped hello");
            unscopedLocalizer["greet"].Value.Should().Be("Shared hello");
        });
    }

    /// <summary>
    /// Verifies that add view message localizer replaces APre registered default.
    /// </summary>
    [TestMethod]
    public void AddViewMessageLocalizer_ReplacesAPreRegisteredDefault() {
        var services = new ServiceCollection();
        services.AddSingleton<IViewLocalizer>(_ => new PreRegisteredViewLocalizer());
        services.AddMemoryLocalization(new Dictionary<string, string> { ["greet"] = "Hello!" });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddViewMessageLocalizer();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IViewLocalizer>().Should().BeOfType<ViewMessageLocalizer>("the explicit opt-in takes over view localization");
        new PreRegisteredViewLocalizer().WithCulture(new CultureInfo("en")).Should().BeOfType<PreRegisteredViewLocalizer>();
    }

    /// <summary>
    /// Verifies that add view message localizer validates arguments.
    /// </summary>
    [TestMethod]
    public void AddViewMessageLocalizer_ValidatesArguments() {
        Action act = () => ((IServiceCollection)null!).AddViewMessageLocalizer();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Creates view localizer with provider.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <param name="viewPath">The view path.</param>
    /// <param name="executingFilePath">The executing file path.</param>
    /// <returns>The view localizer with provider result.</returns>
    private static (ViewMessageLocalizer Localizer, RecordingProvider Provider) CreateViewLocalizerWithProvider(
        Dictionary<string, Dictionary<string, string>> templates,
        string viewPath,
        string? executingFilePath = null) {
        var provider = new RecordingProvider(templates);
        return (CreateViewLocalizer(provider, viewPath, executingFilePath), provider);
    }

    /// <summary>
    /// Creates view localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="viewPath">The view path.</param>
    /// <param name="executingFilePath">The executing file path.</param>
    /// <returns>The view localizer result.</returns>
    private static ViewMessageLocalizer CreateViewLocalizer(RecordingProvider provider, string viewPath, string? executingFilePath = null) {
        var localizer = new ViewMessageLocalizer(new DefaultMessageLocalizer(
            provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        localizer.Contextualize(CreateViewContext(viewPath, executingFilePath));
        return localizer;
    }

    /// <summary>
    /// Creates view context.
    /// </summary>
    /// <param name="viewPath">The view path.</param>
    /// <param name="executingFilePath">The executing file path.</param>
    /// <returns>The view context result.</returns>
    private static ViewContext CreateViewContext(string viewPath, string? executingFilePath = null) =>
        new() {
            ExecutingFilePath = executingFilePath,
            View = new TestView(viewPath)
        };

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
    /// Runs with current ui culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="action">The action.</param>
    private static void RunWithCurrentUiCulture(string cultureName, Action action) =>
        RunWithCurrentUiCulture(cultureName, () => {
            action();
            return 0;
        });

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

    /// <summary>
    /// Represents a test view.
    /// </summary>
    /// <param name="path">The file path.</param>
    private sealed class TestView(string path) : IView {
        /// <summary>
        /// Gets the path.
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// Performs the render operation.
        /// </summary>
        /// <param name="context">The context.</param>
        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    /// <summary>
    /// Represents a pre registered view localizer.
    /// </summary>
    private sealed class PreRegisteredViewLocalizer : IViewLocalizer {
        /// <summary>
        /// Gets the value for the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        public LocalizedHtmlString this[string name] => new(name, name, true);
        /// <summary>
        /// Gets the localized string for the specified name and arguments.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="arguments">The arguments.</param>
        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, true);
        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The string result.</returns>
        public LocalizedString GetString(string name) => new(name, name, true);
        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The string result.</returns>
        public LocalizedString GetString(string name, params object[] arguments) => new(name, name, true);
        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="includeParentCultures">The include parent cultures.</param>
        /// <returns>The all strings sequence.</returns>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        /// <summary>
        /// Returns a copy with culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <returns>The with culture result.</returns>
        public PreRegisteredViewLocalizer WithCulture(CultureInfo culture) {
            _ = culture;
            return this;
        }
    }
}
