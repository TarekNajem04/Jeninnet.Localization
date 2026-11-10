//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.DeepL;
using Jeninnet.Localization.Translation.DeepL.Extensions;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the DeepL translation provider tests. Every test drives the provider through a
/// fake <see cref="HttpMessageHandler"/>: no test requires a DeepL authentication key or
/// network access. A live-key check would belong to an environment-gated suite and is
/// intentionally absent.
/// </summary>
[TestClass]
public class DeepLTranslationProviderTests {
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that canonical lookup translates key to target culture.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_TranslatesKeyToTargetCultureAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/v2/translate");
            request.Headers.GetValues("Authorization").First().Should().Be("DeepL-Auth-Key test-api-key");

            var content = request.Content;
            Assert.IsNotNull(content);
            var body = await content.ReadAsStringAsync(cancellationToken);
            body.Should().Contain("\"target_lang\":\"DE\"");
            body.Should().Contain("Hello world");
            body.Should().NotContain("source_lang");

            requestCount++;
            return TranslationResponse("Hallo Welt");
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        result.Should().Be("Hallo Welt");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that canonical lookup invariant culture returns null without http.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_InvariantCulture_ReturnsNullWithoutHttpAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that canonical lookup null key throws.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullKey_ThrowsAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync(null!, _german, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that canonical lookup null culture throws.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullCulture_ThrowsAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null culture");
    }

    /// <summary>
    /// Verifies that canonical lookup null culture with pre cancelled token still throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullCulture_WithPreCancelledToken_StillThrowsArgumentNullExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", null!, cancellation.Token);

        await act.Should().ThrowAsync<ArgumentNullException>("argument validation retains precedence over cancellation at the provider boundary");
    }

    /// <summary>
    /// Verifies that canonical lookup pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Hello world", _german, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed before any source access and never becomes a missing-resource result");
    }

    /// <summary>
    /// Verifies that canonical lookup regional culture is uppercased as target.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_RegionalCulture_IsUppercasedAsTargetAsync() {
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            body.Should().Contain("\"target_lang\":\"FR-CA\"");
            return TranslationResponse("Allô le monde");
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        result.Should().Be("Allô le monde");
    }

    /// <summary>
    /// Verifies that canonical lookup from language is sent as source lang.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_FromLanguage_IsSentAsSourceLangAsync() {
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            body.Should().Contain("\"source_lang\":\"EN\"");
            return TranslationResponse("Hallo Welt");
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, FromLanguage: "EN");

        var result = await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        result.Should().Be("Hallo Welt");
    }

    /// <summary>
    /// Verifies that canonical lookup error response throws http request exception.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_ErrorResponse_ThrowsHttpRequestExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { ReasonPhrase = "Too Many Requests" }));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Contain("DeepL Translate returned 429");
    }

    /// <summary>
    /// Verifies that multi key batches up to fifty texts per request.
    /// </summary>
    [TestMethod]
    public async Task MultiKey_BatchesUpToFiftyTextsPerRequestAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var textCount = body.Split("\"Hello", StringSplitOptions.None).Length - 1;
            textCount.Should().BeLessThanOrEqualTo(50);
            requestCount++;
            return TranslationResponse([.. Enumerable.Range(0, textCount).Select(static _ => "Hallo")]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);

        var keys = Enumerable.Range(0, 51).Select(static index => $"Hello {index}").ToArray();
        var result = await provider.GetMessageTemplatesAsync(keys, _german, CancellationToken.None);

        result.Should().HaveCount(51);
        result.Values.All(static value => value == "Hallo").Should().BeTrue("every key in both chunks translates");
        requestCount.Should().Be(2, "51 keys chunk into two DeepL requests of at most 50 texts");
    }

    /// <summary>
    /// Verifies that caching second lookup serves from cache without http.
    /// </summary>
    [TestMethod]
    public async Task Caching_SecondLookup_ServesFromCacheWithoutHttpAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse("Hallo Welt"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that refresh clears cache.
    /// </summary>
    [TestMethod]
    public async Task Refresh_ClearsCacheAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse("Hallo Welt"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        await provider.RefreshAsync(CancellationToken.None);
        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that preload fills cache without lookup.
    /// </summary>
    [TestMethod]
    public async Task Preload_FillsCacheWithoutLookupAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse("Hallo Welt"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.PreloadAsync(["Hello world"], _german, CancellationToken.None);
        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that success raises translation acquired with payload.
    /// </summary>
    [TestMethod]
    public async Task Events_Success_RaisesTranslationAcquiredWithPayloadAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(TranslationResponse("Hallo Welt")));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        ProviderAcquisitionEventArgs? raised = null;
        provider.TranslationAcquired += (_, args) => raised = args;

        await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        raised.Should().NotBeNull();
        raised!.Templates["Hello world"].Should().Be("Hallo Welt");
        raised.Culture.Should().Be(_german);
    }

    /// <summary>
    /// Verifies that failure raises acquisition failed.
    /// </summary>
    [TestMethod]
    public async Task Events_Failure_RaisesAcquisitionFailedAsync() {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);
        ProviderAcquisitionFailedEventArgs? raised = null;
        provider.AcquisitionFailed += (_, args) => raised = args;

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        raised.Should().NotBeNull();
        raised!.Culture.Should().Be(_german);
    }

    /// <summary>
    /// Verifies that ctor null options throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullOptions_ThrowsArgumentNullException() {
        using var client = new HttpClient(new StubHandler());

        var act = () => new DeepLTranslationProvider(null!, client);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that supports reports multi key preload refresh caching and events.
    /// </summary>
    [TestMethod]
    public void Supports_ReportsMultiKeyPreloadRefreshCachingAndEvents() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);

        provider.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        provider.Supports(ProviderFeature.EventsListening).Should().BeTrue();
        provider.Supports(ProviderFeature.Preload).Should().BeTrue();
        provider.Supports(ProviderFeature.Refresh).Should().BeTrue();
        provider.Supports(ProviderFeature.Caching).Should().BeTrue();
        provider.Supports(ProviderFeature.None).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that supports reports no preload or caching when disabled.
    /// </summary>
    [TestMethod]
    public void Supports_CachingDisabled_ReportsNoPreloadOrCaching() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client, CachingEnabled: false);

        provider.Supports(ProviderFeature.Caching).Should().BeFalse();
        provider.Supports(ProviderFeature.Preload).Should().BeFalse();
        provider.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        provider.Supports(ProviderFeature.Refresh).Should().BeTrue();
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="FromLanguage">The source language.</param>
    /// <param name="CachingEnabled">Whether caching is enabled.</param>
    /// <param name="CacheExpiration">The cache expiration.</param>
    /// <returns>The provider result.</returns>
    private static DeepLTranslationProvider CreateProvider(
        HttpClient client,
        string? FromLanguage = null,
        bool CachingEnabled = true,
        TimeSpan? CacheExpiration = null) {
        var options = new DeepLTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.deepl.com",
            FromLanguage = FromLanguage ?? string.Empty,
            CachingEnabled = CachingEnabled,
            CacheExpiration = CacheExpiration ?? DeepLTranslationOptions.DefaultCacheExpiration,
        };

        return new DeepLTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Performs the translation response operation.
    /// </summary>
    /// <param name="texts">The translated texts.</param>
    /// <returns>The translation response result.</returns>
    private static HttpResponseMessage TranslationResponse(params string[] texts) {
        var payload = new {
            translations = texts.Select(static text => new { text, detected_source_language = "EN" }).ToArray(),
        };

        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(payload),
        };
    }

    /// <summary>
    /// Handles stub.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new InvalidOperationException("must not be called"));
    }

    /// <summary>
    /// Handles mock http message.
    /// </summary>
    /// <param name="sendAsync">The send async.</param>
    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _sendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
