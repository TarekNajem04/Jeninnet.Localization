//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Adversarial contract tests for HTML-safe localization: argument
/// encoding in <see cref="HtmlSafeMessageFormatter"/>, the
/// <see cref="HtmlMessageLocalizer"/> bridge over the canonical pipeline
/// (same provider graph, no duplicate fallback), and the already-encoded
/// result guarantee Razor relies on.
/// </summary>
[TestClass]
public class HtmlSafeLocalizationTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Performs the parameters operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The parameters result.</returns>
    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] entries) =>
        entries.ToDictionary(static entry => entry.Name, static entry => entry.Value);

    /// <summary>
    /// Verifies that format renders argument markup inert while passing template markup through.
    /// </summary>
    [TestMethod]
    public void Format_RendersArgumentMarkupInertWhilePassingTemplateMarkupThrough() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("<b>{name}</b>", Parameters(("name", "<img src=x onerror=alert(1)>")));

        result.Should().Be("<b>&lt;img src=x onerror=alert(1)&gt;</b>");
    }

    /// <summary>
    /// Verifies that format neutralizes script payloads.
    /// </summary>
    [TestMethod]
    public void Format_NeutralizesScriptPayloads() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("Hello {name}!", Parameters(("name", "<script>alert('x')</script>")));

        result.Should().Be("Hello &lt;script&gt;alert(&#x27;x&#x27;)&lt;/script&gt;!");
    }

    /// <summary>
    /// Verifies that format encodes ampersands and quotes.
    /// </summary>
    [TestMethod]
    public void Format_EncodesAmpersandsAndQuotes() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("{value}", Parameters(("value", "a&b\"c'd")));

        result.Should().Be("a&amp;b&quot;c&#x27;d");
    }

    /// <summary>
    /// Verifies that format leaves trusted template markup is untouched.
    /// </summary>
    [TestMethod]
    public void Format_LeavesTrustedTemplateMarkupUntouched() {
        var formatter = new HtmlSafeMessageFormatter();
        const string template = "<a href=\"/items\">View {label}</a>";

        var result = formatter.Format(template, Parameters(("label", "all")));

        result.Should().Be("<a href=\"/items\">View all</a>");
    }

    /// <summary>
    /// Verifies that format formats null argument as empty.
    /// </summary>
    [TestMethod]
    public void Format_FormatsNullArgumentAsEmpty() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("Value: {value}.", Parameters(("value", null)));

        result.Should().Be("Value: .");
    }

    /// <summary>
    /// Verifies that format encodes non string values.
    /// </summary>
    [TestMethod]
    public void Format_EncodesNonStringValues() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("Count is {Count}.", Parameters(("Count", 42)));

        result.Should().Be("Count is 42.");
    }

    /// <summary>
    /// Verifies that format encodes already encoded input again.
    /// </summary>
    [TestMethod]
    public void Format_EncodesAlreadyEncodedInputAgain() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("{value}", Parameters(("value", "&lt;")));

        result.Should().Be("&amp;lt;", "arguments are untrusted by contract, so encoding applies exactly once to the raw value");
    }

    /// <summary>
    /// Verifies that format preserves inner formatter semantics.
    /// </summary>
    [TestMethod]
    public void Format_PreservesInnerFormatterSemantics() {
        var formatter = new HtmlSafeMessageFormatter();

        var result = formatter.Format("{known} {unknown}", Parameters(("known", "<b>x</b>")));

        result.Should().Be("&lt;b&gt;x&lt;/b&gt; {unknown}");
    }

    /// <summary>
    /// Verifies that format validates arguments.
    /// </summary>
    [TestMethod]
    public void Format_ValidatesArguments() {
        var formatter = new HtmlSafeMessageFormatter();

        Action nullTemplate = () => formatter.Format(null!, _noParameters);
        Action nullParameters = () => formatter.Format("x", null!);

        nullTemplate.Should().Throw<ArgumentNullException>();
        nullParameters.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that encode argument matches formatter behavior.
    /// </summary>
    [TestMethod]
    public void EncodeArgument_MatchesFormatterBehavior() {
        HtmlSafeMessageFormatter.EncodeArgument(null).Should().Be(string.Empty);
        HtmlSafeMessageFormatter.EncodeArgument(42).Should().Be(42);
        HtmlSafeMessageFormatter.EncodeArgument("<b>x</b>").Should().Be("&lt;b&gt;x&lt;/b&gt;");
        HtmlSafeMessageFormatter.EncodeArgument(4.5).Should().Be(4.5);
    }

    /// <summary>
    /// Verifies that format composes with the icu formatter.
    /// </summary>
    [TestMethod]
    public void Format_ComposesWithTheIcuFormatter() {
        var formatter = new HtmlSafeMessageFormatter(new IcuMessageFormatter());
        const string template = "{n, plural, one {# book} other {# books}} by {user}";

        var result = RunWithCurrentUiCulture("en", () => formatter.Format(template, Parameters(("n", 2), ("user", "<b>Lina</b>"))));

        result.Should().Be("2 books by &lt;b&gt;Lina&lt;/b&gt;");
    }

    /// <summary>
    /// Verifies that indexer renders argument markup inert.
    /// </summary>
    [TestMethod]
    public void Indexer_RendersArgumentMarkupInert() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "<b>Hello {0}!</b>" }
        });

        var result = RunWithCurrentUiCulture("en", () => localizer["greet", "<img src=x onerror=alert(1)>"]);

        result.Should().BeOfType<LocalizedHtmlString>();
        result.Name.Should().Be("greet");
        result.Value.Should().Be("<b>Hello &lt;img src=x onerror=alert(1)&gt;!</b>");
    }

    /// <summary>
    /// Verifies that indexer maps positional arguments.
    /// </summary>
    [TestMethod]
    public void Indexer_MapsPositionalArguments() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["pair"] = "{0} and {1}" }
        });

        var result = RunWithCurrentUiCulture("en", () => localizer["pair", "a", "b"]);

        result.Value.Should().Be("a and b");
    }

    /// <summary>
    /// Verifies that indexer returns missing marker when key misses.
    /// </summary>
    [TestMethod]
    public void Indexer_ReturnsMissingMarkerWhenKeyMisses() {
        var localizer = CreateBridge([]);

        var result = RunWithCurrentUiCulture("en", () => localizer["missing"]);

        result.Name.Should().Be("missing");
        result.Value.Should().Be("missing", "the framework missing convention carries the key");
    }

    /// <summary>
    /// Verifies that indexer tolerates explicit null arguments array.
    /// </summary>
    [TestMethod]
    public void Indexer_ToleratesExplicitNullArgumentsArray() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["plain"] = "Static." }
        });

        var result = RunWithCurrentUiCulture("en", () => localizer["plain", null!]);

        result.Value.Should().Be("Static.");
    }

    /// <summary>
    /// Verifies that get string encodes arguments.
    /// </summary>
    [TestMethod]
    public void GetString_EncodesArguments() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello {0}!" }
        });

        var result = RunWithCurrentUiCulture("en", () => localizer.GetString("greet", "<script>alert(1)</script>"));

        result.Value.Should().Be("Hello &lt;script&gt;alert(1)&lt;/script&gt;!");
        result.ResourceNotFound.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that get string returns missing marker.
    /// </summary>
    [TestMethod]
    public void GetString_ReturnsMissingMarker() {
        var localizer = CreateBridge([]);

        var result = RunWithCurrentUiCulture("en", () => localizer.GetString("missing"));

        result.ResourceNotFound.Should().BeTrue();
        result.Value.Should().Be("missing");
    }

    /// <summary>
    /// Verifies that get all strings throws not is supported.
    /// </summary>
    [TestMethod]
    public void GetAllStrings_ThrowsNotSupported() {
        var localizer = CreateBridge([]);

        Action act = () => localizer.GetAllStrings(includeParentCultures: true);

        act.Should().Throw<NotSupportedException>("key enumeration is not part of the exact-match message pipeline");
    }

    /// <summary>
    /// Verifies that bridge captures current UICulture per call.
    /// </summary>
    [TestMethod]
    public void Bridge_CapturesCurrentUICulturePerCall() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello!" },
            ["ar"] = new() { ["greet"] = "ar-hello" }
        });

        RunWithCurrentUiCulture("en", () => localizer["greet"].Value.Should().Be("Hello!"));
        RunWithCurrentUiCulture("ar", () => localizer["greet"].Value.Should().Be("ar-hello"));
    }

    /// <summary>
    /// Verifies that with culture resolves in the fixed culture.
    /// </summary>
    [TestMethod]
    public void WithCulture_ResolvesInTheFixedCulture() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello!" },
            ["ar"] = new() { ["greet"] = "ar-hello" }
        });
        var arabic = localizer.WithCulture(new CultureInfo("ar"));

        RunWithCurrentUiCulture("en", () => arabic["greet"].Value.Should().Be("ar-hello"));
    }

    /// <summary>
    /// Verifies that with culture validates null culture.
    /// </summary>
    [TestMethod]
    public void WithCulture_ValidatesNullCulture() {
        var localizer = CreateBridge([]);

        Action act = () => localizer.WithCulture(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that bridge resolves through the same provider graph as plain lookups.
    /// </summary>
    [TestMethod]
    public void Bridge_ResolvesThroughTheSameProviderGraphAsPlainLookups() {
        var bridgeProvider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello {0}!" }
        });
        var plainProvider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello {0}!" }
        });
        var bridge = new HtmlMessageLocalizer(new DefaultMessageLocalizer(
            bridgeProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));
        var plain = new DefaultMessageLocalizer(
            plainProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });
        var culture = new CultureInfo("fr-CA");

        var (Html, Plain) = RunWithCurrentUiCulture("fr-CA", () => {
            var html = bridge["greet", "Lina"];
            var plainValue = plain.LocalizeAsync("greet", new Dictionary<string, object?> { ["0"] = "Lina" }, culture, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            return (Html: html.Value, Plain: plainValue);
        });

        Html.Should().Be(Plain);
        bridgeProvider.Requests.Should().HaveCount(plainProvider.Requests.Count);
        for(var index = 0; index < plainProvider.Requests.Count; index++) {
            bridgeProvider.Requests[index].Should().Be(plainProvider.Requests[index]);
        }
    }

    /// <summary>
    /// Verifies that write to emits raw html without double encoding.
    /// </summary>
    [TestMethod]
    public void WriteTo_EmitsRawHtmlWithoutDoubleEncoding() {
        var localizer = CreateBridge(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "<b>Hello {0}!</b>" }
        });

        var result = RunWithCurrentUiCulture("en", () => localizer["greet", "<i>x</i>"]);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        result.WriteTo(writer, HtmlEncoder.Default);

        writer.ToString().Should().Be("<b>Hello &lt;i&gt;x&lt;/i&gt;!</b>", "Razor writes IHtmlContent raw: template markup live, argument markup inert");
    }

    /// <summary>
    /// Verifies that generic variant behaves like the non generic bridge.
    /// </summary>
    [TestMethod]
    public void GenericVariant_BehavesLikeTheNonGenericBridge() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello {0}!" }
        });
        var inner = new DefaultMessageLocalizer(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });
        HtmlMessageLocalizer<HtmlSafeLocalizationTests> localizer = new(inner);

        var result = RunWithCurrentUiCulture("en", () => localizer["greet", "<b>x</b>"]);

        result.Should().BeOfType<LocalizedHtmlString>();
        result.Value.Should().Be("Hello &lt;b&gt;x&lt;/b&gt;!");
    }

    /// <summary>
    /// Verifies that add html message localizer resolves bridges from DI (Dependency Injection).
    /// </summary>
    [TestMethod]
    public void AddHtmlMessageLocalizer_ResolvesBridgesFromDI() {
        using var provider = BuildServiceProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["greet"] = "Hello {0}!" }
        });

        var html = provider.GetRequiredService<IHtmlLocalizer>();
        var generic = provider.GetRequiredService<IHtmlLocalizer<HtmlSafeLocalizationTests>>();

        html.Should().BeOfType<HtmlMessageLocalizer>();
        generic.Should().BeOfType<HtmlMessageLocalizer<HtmlSafeLocalizationTests>>();
        RunWithCurrentUiCulture("en", () => html["greet", "<b>x</b>"].Value.Should().Be("Hello &lt;b&gt;x&lt;/b&gt;!"));
    }

    /// <summary>
    /// Verifies that add html message localizer replaces APre registered default.
    /// </summary>
    [TestMethod]
    public void AddHtmlMessageLocalizer_ReplacesAPreRegisteredDefault() {
        var services = new ServiceCollection();
        services.AddSingleton<IHtmlLocalizer>(_ => new PreRegisteredHtmlLocalizer());
        services.AddMemoryLocalization(new Dictionary<string, string> { ["greet"] = "Hello!" });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddHtmlMessageLocalizer();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHtmlLocalizer>().Should().BeOfType<HtmlMessageLocalizer>("the explicit opt-in takes over view localization");
        new PreRegisteredHtmlLocalizer().WithCulture(new CultureInfo("en")).Should().BeOfType<PreRegisteredHtmlLocalizer>();
    }

    /// <summary>
    /// Verifies that bridge validates arguments.
    /// </summary>
    [TestMethod]
    public void Bridge_ValidatesArguments() {
        Action nullLocalizer = () => _ = new HtmlMessageLocalizer(null!);
        Action nullGenericLocalizer = () => _ = new HtmlMessageLocalizer<HtmlSafeLocalizationTests>(null!);

        nullLocalizer.Should().Throw<ArgumentNullException>();
        nullGenericLocalizer.Should().Throw<ArgumentNullException>();

        var localizer = CreateBridge([]);
        Action nullName = () => RunWithCurrentUiCulture("en", () => localizer[null!]);

        nullName.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Creates bridge.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <returns>The bridge result.</returns>
    private static HtmlMessageLocalizer CreateBridge(Dictionary<string, Dictionary<string, string>> templates) =>
        new(new DefaultMessageLocalizer(
            new RecordingProvider(templates), new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));

    /// <summary>
    /// Builds service provider.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <returns>The service provider result.</returns>
    private static ServiceProvider BuildServiceProvider(Dictionary<string, Dictionary<string, string>> templates) {
        var services = new ServiceCollection();
        var messages = new Dictionary<string, string>();
        if(templates.TryGetValue("en", out var english)) {
            foreach(var pair in english) {
                messages[pair.Key] = pair.Value;
            }
        }

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
    /// Represents a pre registered html localizer.
    /// </summary>
    private sealed class PreRegisteredHtmlLocalizer : IHtmlLocalizer {
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
        public PreRegisteredHtmlLocalizer WithCulture(CultureInfo culture) {
            _ = culture;
            return this;
        }
    }
}
