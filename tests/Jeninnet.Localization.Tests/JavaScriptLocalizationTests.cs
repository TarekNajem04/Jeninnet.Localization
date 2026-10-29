//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the JavaScript resource endpoint: payload
/// shape and ordering, ETag revalidation (304), allow-list enforcement (disallowed
/// keys are never requested or leaked), JS-safe encoding, and static export
/// byte-identity with the endpoint payload.
/// </summary>
[TestClass]
public partial class JavaScriptLocalizationTests {
    /// <summary>
    /// Verifies that build async emits flat ordered json of the allow list.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_EmitsFlatOrderedJsonOfTheAllowListAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["second"] = "value-2", ["first"] = "value-1", ["offList"] = "never" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("second");
        options.AllowedKeys.Add("first");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var payload = await builder.BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(payload);
        payload.Culture.Should().Be("en");
        payload.Json.Should().Be("{\"second\":\"value-2\",\"first\":\"value-1\"}",
            "the payload is a flat object in allow-list declaration order");

        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, string>>(payload.Json);
        roundTripped.Should().BeEquivalentTo(new Dictionary<string, string> { ["second"] = "value-2", ["first"] = "value-1" });
    }

    /// <summary>
    /// Verifies that build async requests only allow listed keys and never leaks others.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_RequestsOnlyAllowListedKeys_AndNeverLeaksOthersAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["publicKey"] = "visible", ["secretKey"] = "secret", ["anotherSecret"] = "secret-2" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("publicKey");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var payload = await builder.BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(payload);
        payload.Json.Should().NotContain("secretKey");
        payload.Json.Should().NotContain("anotherSecret");
        provider.Requests.Should().HaveCount(1);
        provider.Requests[0].Should().Be(("publicKey", "en"),
            "the provider is asked for allow-listed keys only (never a store dump)");
    }

    /// <summary>
    /// Verifies that build async omits keys the provider does not serve for the culture.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_OmitsKeysTheProviderDoesNotServeForTheCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["present"] = "here" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("present");
        options.AllowedKeys.Add("absent");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var payload = await builder.BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(payload);
        payload.Json.Should().Be("{\"present\":\"here\"}");
    }

    /// <summary>
    /// Verifies that build async returns null for unrecognized or unsupported cultures.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ReturnsNullForUnrecognizedOrUnsupportedCulturesAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["key"] = "value" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("key");
        options.SupportedCultures.Add("en");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        (await builder.BuildAsync("en!x", TestContext.CancellationToken)).Should().BeNull("an unrecognized culture name has no payload");
        (await builder.BuildAsync("ar", TestContext.CancellationToken)).Should().BeNull("a culture outside SupportedCultures has no payload");
        provider.Requests.Should().BeEmpty("no provider lookup happens for unserved cultures");
    }

    /// <summary>
    /// Verifies that payload encodes templates safely for java script string contexts.
    /// </summary>
    [TestMethod]
    public async Task Payload_EncodesTemplatesSafelyForJavaScriptStringContextsAsync() {
        const string scriptTemplate = "<script>alert('x')</script>";
        const string separatorTemplate = "line\u2028separator";
        const string arabicTemplate = "الفهرس";
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                ["script"] = scriptTemplate,
                ["separator"] = separatorTemplate,
                ["arabic"] = arabicTemplate,
                ["quoted"] = "say \"hi\" \\ bye"
            }
        });
        var options = CreateOptions();
        foreach(var key in new[] { "script", "separator", "arabic", "quoted" }) {
            options.AllowedKeys.Add(key);
        }

        var payload = await new JavaScriptResourcePayloadBuilder(provider, options).BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(payload);
        payload.Json.Should().NotContain("<script>", "HTML-sensitive characters are escaped so the payload cannot break out of a script context");
        payload.Json.Should().Contain("\\u003Cscript\\u003E");
        payload.Json.Should().Contain("\\u0027", "single quotes are escaped by the JavaScript encoder");
        payload.Json.Should().Contain("\\u2028", "line/paragraph separators are escaped for JavaScript string contexts");
        payload.Json.Should().NotContain(arabicTemplate, "non-ASCII characters are escaped (JSON/JavaScript-safe payload)");
        payload.Json.Should().Contain("\\u");
        payload.Json.Should().Contain("\\u0022", "double quotes are escaped (as \\u0022 by the JavaScript encoder)");
        payload.Json.Should().Contain("\\\\", "backslashes are escaped");

        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, string>>(payload.Json)!;
        roundTripped["script"].Should().Be(scriptTemplate, "JSON.parse / JsonSerializer round-trips to the original template text");
        roundTripped["separator"].Should().Be(separatorTemplate);
        roundTripped["arabic"].Should().Be(arabicTemplate);
        roundTripped["quoted"].Should().Be("say \"hi\" \\ bye");
    }

    /// <summary>
    /// Verifies that build async uses the exact culture only without walking parents.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_UsesTheExactCultureOnly_WithoutWalkingParentsAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["heading"] = "heading in the parent culture" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("heading");
        options.SupportedCultures.Add("ar");
        options.SupportedCultures.Add("ar-SY");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var payload = await builder.BuildAsync("ar-SY", TestContext.CancellationToken);

        Assert.IsNotNull(payload);
        payload.Json.Should().Be("{}",
            "a key present only in the parent culture is not served — fallback stays owned by DefaultMessageLocalizer");
        provider.Requests.Should().HaveCount(1);
        provider.Requests[0].Should().Be(("heading", "ar-SY"),
            "the provider is asked for the exact culture only — the payload builder never walks parents");
    }

    /// <summary>
    /// Verifies that ETag is quoted content hash stable for equal content and changes with content.
    /// </summary>
    [TestMethod]
    public async Task ETag_IsQuotedContentHash_StableForEqualContent_AndChangesWithContentAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["key"] = "value" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("key");

        var first = await new JavaScriptResourcePayloadBuilder(provider, options).BuildAsync("en", TestContext.CancellationToken);
        var second = await new JavaScriptResourcePayloadBuilder(provider, options).BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        MyRegex().IsMatch(first.ETag).Should().BeTrue("the ETag is a quoted strong SHA-256 tag");
        second.ETag.Should().Be(first.ETag, "equal content always produces an equal content-hash ETag");

        provider.SetTemplate("en", "key", "changed");
        var changed = await new JavaScriptResourcePayloadBuilder(provider, options).BuildAsync("en", TestContext.CancellationToken);

        Assert.IsNotNull(changed);
        changed.ETag.Should().NotBe(first.ETag, "changed content produces a different ETag");
    }

    /// <summary>
    /// Verifies that export async skips unresolvable cultures.
    /// </summary>
    [TestMethod]
    public async Task ExportAsync_SkipsUnresolvableCulturesAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["key"] = "value" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("key");
        options.SupportedCultures.Add("en");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var exported = await builder.ExportAsync(["en", "en!x", "ar"], TestContext.CancellationToken);

        exported.Should().ContainSingle();
        exported[0].Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that export to directory writes bytes identical to the endpoint payload.
    /// </summary>
    [TestMethod]
    public async Task ExportToDirectory_WritesBytesIdenticalToTheEndpointPayloadAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["heading"] = "الفهرس" }
        });
        var options = CreateOptions();
        options.AllowedKeys.Add("heading");
        options.SupportedCultures.Add("ar");
        var builder = new JavaScriptResourcePayloadBuilder(provider, options);

        var directory = Path.Combine(Path.GetTempPath(), "jeninnet-fi03-" + Guid.NewGuid().ToString("N"));
        try {
            await builder.ExportToDirectoryAsync(directory, ["ar"], TestContext.CancellationToken);

            var filePath = Path.Combine(directory, "ar.json");
            File.Exists(filePath).Should().BeTrue("static export writes {culture}.json files");
            var fileBytes = await File.ReadAllBytesAsync(filePath, TestContext.CancellationToken);

            var payload = await builder.BuildAsync("ar", TestContext.CancellationToken);
            Assert.IsNotNull(payload);
            fileBytes.Should().BeEquivalentTo(payload.Utf8Json.ToArray(), "the export bytes are the payload bytes");

            var (app, endpoint) = BuildEndpointApp(provider);
            await using(app) {
                var (_, _, _, _, body) = await InvokeEndpointAsync(endpoint, app.Services, "ar");
                body.Should().BeEquivalentTo(fileBytes,
                    "the static export reproduces the endpoint payload byte-identically (acceptance criterion)");
            }
        }
        finally {
            if(Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that endpoint returns the allow listed set with ETag and cache control.
    /// </summary>
    [TestMethod]
    public async Task Endpoint_ReturnsTheAllowListedSet_WithETagAndCacheControlAsync() {
        var provider = CreateEndpointProvider();
        var (app, endpoint) = BuildEndpointApp(provider);
        await using(app) {
            var (StatusCode, ETag, CacheControl, ContentType, Body) = await InvokeEndpointAsync(endpoint, app.Services, "ar");

            endpoint.RoutePattern.RawText.Should().Be("/localization/v1/{culture}",
                "MapJavaScriptLocalization maps the configured versioned pattern");
            StatusCode.Should().Be(StatusCodes.Status200OK);
            ContentType.Should().Be("application/json; charset=utf-8");
            ETag.Should().NotBeNull();
            CacheControl.Should().Be("public, max-age=0, must-revalidate");

            var json = Encoding.UTF8.GetString(Body);
            json.Should().Contain("\"heading\"");
            json.Should().NotContain("secretKey", "disallowed keys never leak through the endpoint");
        }
    }

    /// <summary>
    /// Verifies that endpoint Returns304For matching if none match.
    /// </summary>
    [TestMethod]
    public async Task Endpoint_Returns304ForMatchingIfNoneMatchAsync() {
        var provider = CreateEndpointProvider();
        var (app, endpoint) = BuildEndpointApp(provider);
        await using(app) {
            var (_, servedETag, _, _, _) = await InvokeEndpointAsync(endpoint, app.Services, "ar");
            var eTag = servedETag!;

            var (StatusCode, ETag, CacheControl, _, Body) = await InvokeEndpointAsync(endpoint, app.Services, "ar", ifNoneMatch: eTag);
            StatusCode.Should().Be(StatusCodes.Status304NotModified);
            Body.Should().BeEmpty("a 304 response has no body");
            ETag.Should().Be(eTag, "the 304 response repeats the ETag");
            CacheControl.Should().Be("public, max-age=0, must-revalidate");

            (await InvokeEndpointAsync(endpoint, app.Services, "ar", ifNoneMatch: "W/" + eTag)).StatusCode
                .Should().Be(StatusCodes.Status304NotModified, "the RFC weak comparison accepts the weak form of the tag");
            (await InvokeEndpointAsync(endpoint, app.Services, "ar", ifNoneMatch: "*")).StatusCode
                .Should().Be(StatusCodes.Status304NotModified);
            (await InvokeEndpointAsync(endpoint, app.Services, "ar", ifNoneMatch: "\"other\"")).StatusCode
                .Should().Be(StatusCodes.Status200OK, "a non-matching tag returns the payload");
        }
    }

    /// <summary>
    /// Verifies that endpoint returns not found for unserved cultures.
    /// </summary>
    [TestMethod]
    public async Task Endpoint_ReturnsNotFoundForUnservedCulturesAsync() {
        var provider = CreateEndpointProvider();
        var (app, endpoint) = BuildEndpointApp(provider);
        await using(app) {
            (await InvokeEndpointAsync(endpoint, app.Services, "en")).StatusCode.Should().Be(StatusCodes.Status200OK);
            (await InvokeEndpointAsync(endpoint, app.Services, "pl")).StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "a culture outside SupportedCultures is not served");
            (await InvokeEndpointAsync(endpoint, app.Services, "en!x")).StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Verifies that add java script localization validates arguments and options.
    /// </summary>
    [TestMethod]
    public void AddJavaScriptLocalization_ValidatesArgumentsAndOptions() {
        Action nullServices = () => ((IServiceCollection)null!).AddJavaScriptLocalization();

        nullServices.Should().Throw<ArgumentNullException>();

        var noKeys = new ServiceCollection();
        var missingKeys = Assert.ThrowsExactly<ArgumentException>(() => noKeys.AddJavaScriptLocalization(static _ => { }));
        missingKeys.Message.Should().Contain("AllowedKeys");

        var noCultureParameter = new ServiceCollection();
        var missingParameter = Assert.ThrowsExactly<ArgumentException>(() => noCultureParameter.AddJavaScriptLocalization(static options => {
            options.AllowedKeys.Add("key");
            options.RoutePattern = "/localization/v1/all";
        }));
        missingParameter.Message.Should().Contain("{culture}");

        var duplicates = new ServiceCollection();
        var duplicateKeys = Assert.ThrowsExactly<ArgumentException>(() => duplicates.AddJavaScriptLocalization(static options => {
            options.AllowedKeys.Add("key");
            options.AllowedKeys.Add("key");
        }));
        duplicateKeys.Message.Should().Contain("duplicate");

        var badCulture = new ServiceCollection();
        Action culture = () => badCulture.AddJavaScriptLocalization(static options => {
            options.AllowedKeys.Add("key");
            options.SupportedCultures.Add("en!x");
        });

        culture.Should().Throw<CultureNotFoundException>();

        var twice = new ServiceCollection();
        twice.AddJavaScriptLocalization(static options => options.AllowedKeys.Add("key"));
        var second = Assert.ThrowsExactly<InvalidOperationException>(() => twice.AddJavaScriptLocalization(static options => options.AllowedKeys.Add("other")));
        second.Message.Should().Contain("already registered");
    }

    /// <summary>
    /// Verifies that add java script localization registers the options snapshot and builder.
    /// </summary>
    [TestMethod]
    public void AddJavaScriptLocalization_RegistersTheOptionsSnapshotAndBuilder() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new RecordingProvider([]));
        services.AddLocalization(static options => options.DefaultCulture = "en");
        services.AddJavaScriptLocalization(static options => options.AllowedKeys.Add("key"));

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<JavaScriptLocalizationOptions>();
        options.AllowedKeys.Should().ContainSingle();
        options.AllowedKeys[0].Should().Be("key");
        options.RoutePattern.Should().Be("/localization/v1/{culture}",
            "the endpoint version is the URL segment by default");
        provider.GetRequiredService<JavaScriptResourcePayloadBuilder>().Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that map java script localization requires registration.
    /// </summary>
    [TestMethod]
    public async Task MapJavaScriptLocalization_RequiresRegistrationAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        await using var app = builder.Build();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(app.MapJavaScriptLocalization);

        exception.Message.Should().Contain("AddJavaScriptLocalization");
    }

    // --------------------------------------------------------------------- helpers

    /// <summary>
    /// Creates options.
    /// </summary>
    /// <returns>The options result.</returns>
    private static JavaScriptLocalizationOptions CreateOptions() => new();

    /// <summary>
    /// Creates endpoint provider.
    /// </summary>
    /// <returns>The endpoint provider result.</returns>
    private static RecordingProvider CreateEndpointProvider() => new(new Dictionary<string, Dictionary<string, string>> {
        ["en"] = new() { ["heading"] = "Catalog", ["secretKey"] = "secret" },
        ["ar"] = new() { ["heading"] = "الفهرس", ["secretKey"] = "secret" }
    });

    /// <summary>
    /// Builds endpoint app.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <returns>The endpoint app result.</returns>
    private static (WebApplication App, RouteEndpoint Endpoint) BuildEndpointApp(
        RecordingProvider provider,
        Action<JavaScriptLocalizationOptions>? configure = null) {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IInnerMessageProvider>(provider);
        builder.Services.AddLocalization(static options => options.DefaultCulture = "en");
        builder.Services.AddJavaScriptLocalization(options => {
            options.SupportedCultures.Add("en");
            options.SupportedCultures.Add("ar");
            options.AllowedKeys.Add("heading");
            configure?.Invoke(options);
        });

        var app = builder.Build();
        app.MapJavaScriptLocalization();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();

        return (app, endpoint);
    }

    /// <summary>
    /// Invokes endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="services">The service collection.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="ifNoneMatch">The if none match.</param>
    /// <returns>A task whose result is invoke endpoint.</returns>
    private static async Task<(int StatusCode, string? ETag, string? CacheControl, string? ContentType, byte[] Body)> InvokeEndpointAsync(
        RouteEndpoint endpoint,
        IServiceProvider services,
        string culture,
        string? ifNoneMatch = null) {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.RouteValues["culture"] = culture;
        if(ifNoneMatch is not null) {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        await using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await endpoint.RequestDelegate!(httpContext);

        var headers = httpContext.Response.Headers;
        return (httpContext.Response.StatusCode,
            headers.ETag.Count == 0 ? null : headers.ETag.ToString(),
            headers.CacheControl.Count == 0 ? null : headers.CacheControl.ToString(),
            httpContext.Response.ContentType,
            body.ToArray());
    }

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class RecordingProvider(Dictionary<string, Dictionary<string, string>> templates) : IInnerMessageProvider {
        /// <summary>
        /// Gets the requests.
        /// </summary>
        public List<(string Key, string Culture)> Requests { get; } = [];

        /// <summary>
        /// Sets template.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="key">The message key.</param>
        /// <param name="template">The message template.</param>
        public void SetTemplate(string culture, string key, string template) {
            templates[culture][key] = template;
            Requests.Clear();
        }

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((key, culture.Name));
            return ValueTask.FromResult(
                templates.TryGetValue(culture.Name, out var byKey) && byKey.TryGetValue(key, out var template) ? template : null);
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Performs the my regex operation.
    /// </summary>
    /// <returns>The my regex result.</returns>
    [GeneratedRegex("^\"[0-9a-f]{64}\"$")]
    private static partial Regex MyRegex();
}
