//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the Razor <c>&lt;loc&gt;</c> tag helper:
/// canonical-pipeline lookup, adversarial argument encoding, the fallback-body
/// freeze (authored Razor content, single encoding), output shape, the Razor
/// binding surface, culture behavior, and  parity (no duplicated
/// fallback/formatting logic).
/// </summary>
[TestClass]
public class LocalizeTagHelperTests {
    // ------------------------------------------------------------ pipeline lookup

    /// <summary>
    /// Verifies that process resolves the key through the canonical pipeline.
    /// </summary>
    [TestMethod]
    public async Task Process_ResolvesTheKeyThroughTheCanonicalPipelineAsync() {
        var (Helper, Provider, Context, Output) = CreateHarness("heading", arguments: null, childHtml: "Catalog");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be("<b>Catalog</b>", "the resolved template renders as trusted markup");
        Output.TagName.Should().BeNull("the <loc> element is removed from the output");
        Output.Attributes.Should().BeEmpty("the key/arguments attributes never leak into the HTML");
        Assert.AreSequenceEqual([("heading", "en")], Provider.Requests, "the key resolves through the canonical pipeline");
    }

    /// <summary>
    /// Verifies that process substitutes and encodes arguments.
    /// </summary>
    [TestMethod]
    public async Task Process_SubstitutesAndEncodesArgumentsAsync() {
        var (Helper, _, Context, Output) = CreateHarness("greet", ["<img src=x onerror=alert(1)>"], childHtml: "Hello!");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be(
            "Hello &lt;img src=x onerror=alert(1)&gt;!",
            "adversarial argument payloads render inert while the template stays trusted");
    }

    /// <summary>
    /// Verifies that process multiple arguments map positionally.
    /// </summary>
    [TestMethod]
    public async Task Process_MultipleArguments_MapPositionallyAsync() {
        var (Helper, _, Context, Output) = CreateHarness("pair", ["Lina", "<script>alert(1)</script>"], childHtml: "{0} and {1}");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be("Lina and &lt;script&gt;alert(1)&lt;/script&gt;");
    }

    /// <summary>
    /// Verifies that process resolves through the same bridge as the html localizer.
    /// </summary>
    [TestMethod]
    public async Task Process_ResolvesThroughTheSameBridgeAsTheHtmlLocalizerAsync() {
        var (Helper, Provider, Context, Output) = CreateHarness("greet", ["<b>Lina</b>"], childHtml: "Hello!");
        var comparisonProvider = new RecordingProvider(Templates());
        var comparison = new HtmlMessageLocalizer(new DefaultMessageLocalizer(
            comparisonProvider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" }));

        var compared = await RunWithCurrentUiCultureAsync("fr-CA", async () => {
            await Helper.ProcessAsync(Context, Output);
            var html = comparison["greet", "<b>Lina</b>"];
            await using var writer = new StringWriter(CultureInfo.InvariantCulture);
            html.WriteTo(writer, HtmlEncoder.Default);
            return (TagHelper: Render(Output), Bridge: writer.ToString());
        });

        compared.TagHelper.Should().Be(compared.Bridge, "the tag helper resolves through the bridge (no duplicated logic)");
        Assert.AreSequenceEqual(
            comparisonProvider.Requests,
            Provider.Requests,
            "both walk the identical canonical fallback chain (fr-CA → fr → en default) in order");
    }

    /// <summary>
    /// Verifies that process uses the current ui culture with the canonical fallback chain.
    /// </summary>
    [TestMethod]
    public async Task Process_UsesTheCurrentUiCulture_WithTheCanonicalFallbackChainAsync() {
        var (Helper, Provider, Context, Output) = CreateHarness("greet", ["Lina"], childHtml: "Hello!");

        var content = await RunWithCurrentUiCultureAsync("fr-CA", async () => {
            await Helper.ProcessAsync(Context, Output);
            return Render(Output);
        });

        content.Should().Be("Hello Lina!", "the en default-culture template resolves through the canonical chain");
        Assert.AreSequenceEqual([("greet", "fr-CA"), ("greet", "fr"), ("greet", "en")], Provider.Requests);
    }

    // ------------------------------------------------------------- fallback freeze

    /// <summary>
    /// Verifies that process missing key renders the fallback body.
    /// </summary>
    [TestMethod]
    public async Task Process_MissingKey_RendersTheFallbackBodyAsync() {
        var (Helper, _, Context, Output) = CreateHarness("missing", arguments: null, childHtml: "Shopping Cart");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be("Shopping Cart", "the element body is the deterministic fallback on a pipeline miss");
        Output.TagName.Should().BeNull();
    }

    /// <summary>
    /// Verifies that process missing key keeps fallback markup and single encoding.
    /// </summary>
    [TestMethod]
    public async Task Process_MissingKey_KeepsFallbackMarkupAndSingleEncodingAsync() {
        var (Helper, _, Context, Output) = CreateHarness("missing", arguments: null, childHtml: "<em>Cart</em> &amp; more");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be(
            "<em>Cart</em> &amp; more",
            "the fallback is view-authored content: static markup passes through and Razor-encoded values stay single-encoded");
    }

    /// <summary>
    /// Verifies that process empty key renders the fallback body without querying the provider.
    /// </summary>
    [TestMethod]
    public async Task Process_EmptyKey_RendersTheFallbackBody_WithoutQueryingTheProviderAsync() {
        foreach(var key in new[] { null, "", "   " }) {
            var (Helper, Provider, Context, Output) = CreateHarness(key, arguments: ["Lina"], childHtml: "Shopping Cart");

            await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

            Render(Output).Should().Be("Shopping Cart", $"key '{key ?? "<null>"}' degrades to the body");
            Provider.Requests.Should().BeEmpty("an empty key never queries the pipeline");
        }
    }

    /// <summary>
    /// Verifies that process missing key and missing body renders nothing.
    /// </summary>
    [TestMethod]
    public async Task Process_MissingKeyAndMissingBody_RendersNothingAsync() {
        var (Helper, _, Context, Output) = CreateHarness("missing", arguments: null, childHtml: "");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Render(Output).Should().Be(string.Empty);
    }

    // ------------------------------------------------------------ binding surface

    /// <summary>
    /// Verifies that tag helper exposes the documented razor binding surface.
    /// </summary>
    [TestMethod]
    public void TagHelper_ExposesTheDocumentedRazorBindingSurface() {
        var target = typeof(LocalizeTagHelper).GetCustomAttribute<HtmlTargetElementAttribute>();

        target.Should().NotBeNull("the helper binds the <loc> element");
        target!.Tag.Should().Be("loc");

        typeof(LocalizeTagHelper).GetProperty(nameof(LocalizeTagHelper.Key))!
            .GetCustomAttribute<HtmlAttributeNameAttribute>()!.Name.Should().Be("key");
        typeof(LocalizeTagHelper).GetProperty(nameof(LocalizeTagHelper.Arguments))!
            .GetCustomAttribute<HtmlAttributeNameAttribute>()!.Name.Should().Be(
                "arguments",
                "the Razor expression binding shape (object[], the peer's localize-args)");
        typeof(LocalizeTagHelper).GetProperty(nameof(LocalizeTagHelper.Arguments))!.PropertyType.Should().Be(
            typeof(object[]),
            "a Razor expression such as arguments=\"@model.Args\" binds directly to the array");
    }

    /// <summary>
    /// Verifies that process removes the loc element and its attributes.
    /// </summary>
    [TestMethod]
    public async Task Process_RemovesTheLocElementAndItsAttributesAsync() {
        var (Helper, _, Context, Output) = CreateHarness("heading", ["Lina"], childHtml: "Catalog");

        await RunWithCurrentUiCultureAsync("en", () => Helper.ProcessAsync(Context, Output));

        Output.TagName.Should().BeNull();
        Output.Attributes.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- validation

    /// <summary>
    /// Verifies that process validates arguments.
    /// </summary>
    [TestMethod]
    public async Task Process_ValidatesArgumentsAsync() {
        Action nullLocalizer = () => _ = new LocalizeTagHelper(null!);
        nullLocalizer.Should().Throw<ArgumentNullException>();

        var (Helper, _, Context, Output) = CreateHarness("heading", arguments: null, childHtml: "Catalog");
        Func<Task> nullContext = () => Helper.ProcessAsync(null!, Output);
        Func<Task> nullOutput = () => Helper.ProcessAsync(Context, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await nullOutput.Should().ThrowAsync<ArgumentNullException>();
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Performs the templates operation.
    /// </summary>
    /// <returns>The templates result.</returns>
    private static Dictionary<string, Dictionary<string, string>> Templates() => new() {
        ["en"] = new() {
            ["heading"] = "<b>Catalog</b>",
            ["greet"] = "Hello {0}!",
            ["pair"] = "{0} and {1}"
        }
    };

    /// <summary>
    /// Creates harness.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="childHtml">The child html.</param>
    /// <returns>The harness result.</returns>
    private static (LocalizeTagHelper Helper, RecordingProvider Provider, TagHelperContext Context, TagHelperOutput Output) CreateHarness(
        string? key,
        object[]? arguments,
        string childHtml) {
        var provider = new RecordingProvider(Templates());
        var helper = new LocalizeTagHelper(new DefaultMessageLocalizer(
            provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" })) {
            Key = key,
            Arguments = arguments
        };

        var context = new TagHelperContext(
            [],
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString("N"));
        var attributes = new TagHelperAttributeList {
            new("key", key ?? string.Empty),
            new("arguments", (object?)arguments ?? string.Empty)
        };
        var output = new TagHelperOutput(
            "loc",
            attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(childHtml)));

        return (helper, provider, context, output);
    }

    /// <summary>
    /// Performs the render operation.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <returns>The render text.</returns>
    private static string Render(TagHelperOutput output) {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        output.Content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
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
            await action();
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
    /// <returns>A task whose result is with current ui culture.</returns>
    private static async Task<T> RunWithCurrentUiCultureAsync<T>(string cultureName, Func<Task<T>> action) {
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return await action();
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
}
