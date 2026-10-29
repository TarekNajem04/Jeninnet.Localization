//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
///  tests for <see cref="MarkdownMessageFormatter"/>: the documented
/// CommonMark subset vectors, placeholder preservation across constructs, the
/// adversarial argument suite (arguments stay inert in Markdown and HTML), the
/// subset-contract literal-rendering cases, determinism, and composition (
/// default inner, ICU (International Components for Unicode) inner, pre-encoded inner).
/// </summary>
[TestClass]
public class MarkdownMessageFormatterTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Performs the parameters operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The parameters result.</returns>
    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] entries) =>
        entries.ToDictionary(static entry => entry.Name, static entry => entry.Value);

    /// <summary>
    /// Performs the renderer operation.
    /// </summary>
    /// <returns>The er result.</returns>
    private static MarkdownMessageFormatter Renderer() => new(new DefaultMessageFormatter());

    /// <summary>
    /// Performs the encoded renderer operation.
    /// </summary>
    /// <returns>The encoded renderer result.</returns>
    private static MarkdownMessageFormatter EncodedRenderer() => new();

    /// <summary>
    /// Verifies that subset vectors render expected html.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow("Hello **world**", "<p>Hello <strong>world</strong></p>")]
    [DataRow("line one\nline two", "<p>line one\nline two</p>")]
    [DataRow("a\n\nb", "<p>a</p>\n<p>b</p>")]
    [DataRow("**bold**", "<p><strong>bold</strong></p>")]
    [DataRow("__bold__", "<p><strong>bold</strong></p>")]
    [DataRow("*em*", "<p><em>em</em></p>")]
    [DataRow("_em_", "<p><em>em</em></p>")]
    [DataRow("**bold *em* text**", "<p><strong>bold <em>em</em> text</strong></p>")]
    [DataRow("foo*bar*baz", "<p>foo<em>bar</em>baz</p>")]
    [DataRow("`code`", "<p><code>code</code></p>")]
    [DataRow("a\r\nb", "<p>a\nb</p>")]
    public void SubsetVectors_RenderExpectedHtml(string template, string expected) {
        var result = Renderer().Format(template, _noParameters);

        result.Should().Be(expected);
    }

    /// <summary>
    /// Verifies that unsupported or unmatched constructs render literally.
    /// </summary>
    /// <param name="template">The message template.</param>
    [TestMethod]
    [DataRow("snake_case_name")]
    [DataRow("*open")]
    [DataRow("**open")]
    [DataRow("_open")]
    [DataRow("***x***")]
    [DataRow("a * b * c")]
    [DataRow("``x``")]
    [DataRow("```\ncode\n```")]
    [DataRow("# Title")]
    [DataRow("---")]
    [DataRow("  - item")]
    [DataRow("![alt](u)")]
    public void UnsupportedOrUnmatchedConstructs_RenderLiterally(string template) {
        var result = Renderer().Format(template, _noParameters);

        result.Should().Be($"<p>{template}</p>");
    }

    /// <summary>
    /// Verifies that excluded constructs encode html sensitive characters.
    /// </summary>
    [TestMethod]
    public void ExcludedConstructs_EncodeHtmlSensitiveCharacters() {
        var result = Renderer().Format("> quote & more", _noParameters);

        result.Should().Be("<p>&gt; quote &amp; more</p>");
    }

    /// <summary>
    /// Verifies that html entity syntax stays literal.
    /// </summary>
    [TestMethod]
    public void HtmlEntitySyntax_StaysLiteral() {
        var result = Renderer().Format("&amp;", _noParameters);

        result.Should().Be("<p>&amp;amp;</p>", "entities are not part of the subset and stay literal text");
    }

    /// <summary>
    /// Verifies that code span content is encoded.
    /// </summary>
    [TestMethod]
    public void CodeSpan_ContentIsEncoded() {
        var result = Renderer().Format("`<b>&`", _noParameters);

        result.Should().Be("<p><code>&lt;b&gt;&amp;</code></p>");
    }

    /// <summary>
    /// Verifies that links render with attribute encoded destination.
    /// </summary>
    [TestMethod]
    public void Links_RenderWithAttributeEncodedDestination() {
        var result = Renderer().Format("[label](https://example.com/a?x=1&y=2)", _noParameters);

        result.Should().Be("<p><a href=\"https://example.com/a?x=1&amp;y=2\">label</a></p>");
    }

    /// <summary>
    /// Verifies that link label is inline is processed.
    /// </summary>
    [TestMethod]
    public void LinkLabel_IsInlineProcessed() {
        var result = Renderer().Format("[*bold* link](u)", _noParameters);

        result.Should().Be("<p><a href=\"u\"><em>bold</em> link</a></p>");
    }

    /// <summary>
    /// Verifies that escapes render delimiters literally.
    /// </summary>
    [TestMethod]
    public void Escapes_RenderDelimitersLiterally() {
        var result = Renderer().Format("\\*not em\\* and \\[x\\](y)", _noParameters);

        result.Should().Be("<p>*not em* and [x](y)</p>");
    }

    /// <summary>
    /// Verifies that lists render expected html.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow("- one\n- two", "<ul>\n<li>one</li>\n<li>two</li>\n</ul>")]
    [DataRow("- a\n* b", "<ul>\n<li>a</li>\n<li>b</li>\n</ul>")]
    [DataRow("+ a", "<ul>\n<li>a</li>\n</ul>")]
    [DataRow("1. a\n2. b", "<ol>\n<li>a</li>\n<li>b</li>\n</ol>")]
    [DataRow("3. a\n4. b", "<ol start=\"3\">\n<li>a</li>\n<li>b</li>\n</ol>")]
    [DataRow("intro\n- item", "<p>intro</p>\n<ul>\n<li>item</li>\n</ul>")]
    [DataRow("- item\noutro", "<ul>\n<li>item</li>\n</ul>\n<p>outro</p>")]
    [DataRow("- one\n\n- two", "<ul>\n<li>one</li>\n</ul>\n<ul>\n<li>two</li>\n</ul>")]
    public void Lists_RenderExpectedHtml(string template, string expected) {
        var result = Renderer().Format(template, _noParameters);

        result.Should().Be(expected, "list blocks render per the documented subset contract");
    }

    /// <summary>
    /// Verifies that empty or blank templates render empty.
    /// </summary>
    /// <param name="template">The message template.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\n\n")]
    public void EmptyOrBlankTemplates_RenderEmpty(string template) {
        var result = Renderer().Format(template, _noParameters);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that placeholders survive rendering.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow("**{name}**", "<p><strong>{name}</strong></p>")]
    [DataRow("`{value}`", "<p><code>{value}</code></p>")]
    [DataRow("[{name}]({url})", "<p><a href=\"{url}\">{name}</a></p>")]
    [DataRow("- {name}", "<ul>\n<li>{name}</li>\n</ul>")]
    [DataRow("{count:N0}", "<p>{count:N0}</p>")]
    public void Placeholders_SurviveRendering(string template, string expected) {
        var result = Renderer().Format(template, _noParameters);

        result.Should().Be(expected, "the inner formatter owns substitution and placeholders are literal text in the subset");
    }

    /// <summary>
    /// Verifies that default ctor encodes arguments into the rendered html.
    /// </summary>
    [TestMethod]
    public void DefaultCtor_EncodesArgumentsIntoTheRenderedHtml() {
        var result = EncodedRenderer().Format("**{name}**", Parameters(("name", "<script>alert('1')</script>")));

        result.Should().Be("<p><strong>&lt;script&gt;alert(&#x27;1&#x27;)&lt;/script&gt;</strong></p>");
    }

    /// <summary>
    /// Verifies that argument markdown syntax stays literal.
    /// </summary>
    /// <param name="payload">The payload.</param>
    [TestMethod]
    [DataRow("**bold**")]
    [DataRow("[x](y)")]
    [DataRow("`code`")]
    [DataRow("*em*")]
    public void ArgumentMarkdownSyntax_StaysLiteral(string payload) {
        var result = EncodedRenderer().Format("{value}", Parameters(("value", payload)));

        result.Should().Be($"<p>{payload}</p>", "arguments are inserted after rendering and are never parsed as Markdown");
    }

    /// <summary>
    /// Verifies that argument in link destination cannot break the attribute.
    /// </summary>
    [TestMethod]
    public void ArgumentInLinkDestination_CannotBreakTheAttribute() {
        var result = EncodedRenderer().Format("[x]({url})", Parameters(("url", "a\" onmouseover=\"alert(1)")));

        result.Should().Be("<p><a href=\"a&quot; onmouseover=&quot;alert(1)\">x</a></p>");
    }

    /// <summary>
    /// Verifies that argument braces are not is rescanned.
    /// </summary>
    [TestMethod]
    public void ArgumentBraces_AreNotRescanned() {
        var result = EncodedRenderer().Format("{a} {b}", Parameters(("a", "{b}"), ("b", "X")));

        result.Should().Be("<p>{b} X</p>", "substitution is single-pass over the rendered template");
    }

    /// <summary>
    /// Verifies that already encoded argument is encoded exactly once.
    /// </summary>
    [TestMethod]
    public void AlreadyEncodedArgument_IsEncodedExactlyOnce() {
        var result = EncodedRenderer().Format("{value}", Parameters(("value", "&lt;")));

        result.Should().Be("<p>&amp;lt;</p>");
    }

    /// <summary>
    /// Verifies that null argument renders empty.
    /// </summary>
    [TestMethod]
    public void NullArgument_RendersEmpty() {
        var result = EncodedRenderer().Format("{value}", Parameters(("value", null)));

        result.Should().Be("<p></p>");
    }

    /// <summary>
    /// Verifies that numeric argument renders invariant content.
    /// </summary>
    [TestMethod]
    public void NumericArgument_RendersInvariantContent() {
        var result = EncodedRenderer().Format("{count}", Parameters(("count", 42)));

        result.Should().Be("<p>42</p>");
    }

    /// <summary>
    /// Verifies that raw html is inert by default.
    /// </summary>
    [TestMethod]
    public void RawHtml_IsInertByDefault() {
        var result = Renderer().Format("<b>x</b> <img src=x onerror=alert(1)>", _noParameters);

        result.Should().Be("<p>&lt;b&gt;x&lt;/b&gt; &lt;img src=x onerror=alert(1)&gt;</p>");
    }

    /// <summary>
    /// Verifies that raw html passes through when explicitly opted in.
    /// </summary>
    [TestMethod]
    public void RawHtml_PassesThroughWhenExplicitlyOptedIn() {
        var formatter = new MarkdownMessageFormatter(new DefaultMessageFormatter(), allowRawHtml: true);

        var result = formatter.Format("<b>x</b>", _noParameters);

        result.Should().Be("<p><b>x</b></p>");
    }

    /// <summary>
    /// Verifies that composes with icu inner formatter.
    /// </summary>
    [TestMethod]
    public void ComposesWithIcuInnerFormatter() {
        var formatter = new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter()));
        const string template = "**{gender, select, male {He} other {They}} said**";

        var result = formatter.Format(template, Parameters(("gender", "male")));

        result.Should().Be("<p><strong>He said</strong></p>", "ICU selection stays with the inner formatter while Markdown rendering precedes substitution");
    }

    /// <summary>
    /// Verifies that composes with pre encoded arguments single encoding.
    /// </summary>
    [TestMethod]
    public void ComposesWithPreEncodedArguments_SingleEncoding() {
        var formatter = new MarkdownMessageFormatter(new DefaultMessageFormatter());
        var preEncoded = HtmlSafeMessageFormatter.EncodeArgument("<b>");

        var result = formatter.Format("**{value}**", Parameters(("value", preEncoded)));

        result.Should().Be("<p><strong>&lt;b&gt;</strong></p>", "the MVC bridge pre-encodes arguments; the default inner must not re-encode them");
    }

    /// <summary>
    /// Verifies that rendering is deterministic.
    /// </summary>
    [TestMethod]
    public void Rendering_IsDeterministic() {
        const string template = "**{name}** — *em* `code` [link](u)\n\n- one\n- {two}";

        var first = EncodedRenderer().Format(template, Parameters(("name", "<x>"), ("two", "2")));
        var second = EncodedRenderer().Format(template, Parameters(("name", "<x>"), ("two", "2")));

        second.Should().Be(first);
    }

    /// <summary>
    /// Verifies that format validates arguments.
    /// </summary>
    [TestMethod]
    public void Format_ValidatesArguments() {
        var formatter = EncodedRenderer();

        Action nullTemplate = () => formatter.Format(null!, _noParameters);
        Action nullParameters = () => formatter.Format("x", null!);

        nullTemplate.Should().Throw<ArgumentNullException>();
        nullParameters.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async formats markdown templates through the pipeline.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_FormatsMarkdownTemplatesThroughThePipelineAsync() {
        const string template = "**Welcome, {name}!**";
        var provider = new RecordingProvider(new Dictionary<string, string> { ["en"] = template });
        var localizer = new DefaultMessageLocalizer(provider, new MarkdownMessageFormatter(), new LocalizationOptions { DefaultCulture = "en" });

        var result = await localizer.LocalizeAsync("key", Parameters(("name", "<Admin>")), new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("<p><strong>Welcome, &lt;Admin&gt;!</strong></p>");
    }

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class RecordingProvider(Dictionary<string, string> templates) : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            _ = cancellationToken;
            return ValueTask.FromResult(templates.TryGetValue(culture.Name, out var template) ? template : null);
        }
    }
}
