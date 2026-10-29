//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the google translation provider tests.
/// </summary>
[TestClass]
public class GoogleTranslationProviderTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");

    /// <summary>
    /// Verifies that canonical lookup translates key to target culture.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_TranslatesKeyToTargetCultureAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsoluteUri.Should().Contain(
                "https://translation.googleapis.com/language/translate/v2?key=test-api-key");
            request.RequestUri.Query.Should().Contain("key=test-api-key");

            var body = await request.Content!.ReadFromJsonAsync<TranslateRequest>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            body.Q.Should().Be("Hello world");
            body.Target.Should().Be("fr");
            body.Format.Should().Be("text");

            requestCount++;
            return TranslateResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that canonical lookup repeated lookup makes request each time.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_RepeatedLookup_MakesRequestEachTimeAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslateResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var first = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var second = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        first.Should().Be("Bonjour le monde");
        second.Should().Be("Bonjour le monde");
        requestCount.Should().Be(2, "Google performs no internal caching; every lookup re-acquires deterministically");
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
    /// Verifies that canonical lookup regional culture is sent as target.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_RegionalCulture_IsSentAsTargetAsync() {
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslateRequest>(cancellationToken: cancellationToken);
            body!.Target.Should().Be("fr-CA");
            return TranslateResponse(("Allô le monde", "fr-CA"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        result.Should().Be("Allô le monde");
    }

    /// <summary>
    /// Verifies that canonical lookup empty translations returns null.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_EmptyTranslations_ReturnsNullAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(OkJson("""{"data":{"translations":[]}}""")));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that canonical lookup missing translated text returns null.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_MissingTranslatedText_ReturnsNullAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(OkJson("""{"data":{"translations":[{}]}}""")));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that canonical lookup null payload returns null.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullPayload_ReturnsNullAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(OkJson("null")));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that canonical lookup malformed response throws json exception.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_MalformedResponse_ThrowsJsonExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(OkJson("not-json")));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    /// <summary>
    /// Verifies that canonical lookup remote error throws http request exception.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_RemoteError_ThrowsHttpRequestExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) {
                Content = new StringContent("""{"error":{"code":400,"message":"Invalid Value"}}"""),
            }));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Exception.Message.Should().Contain("400");
    }

    /// <summary>
    /// Verifies that canonical lookup source language is not sent service auto detects.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_SourceLanguageIsNotSent_ServiceAutoDetectsAsync() {
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslateRequest>(cancellationToken: cancellationToken);
            body!.Source.Should().BeNull();
            return TranslateResponse(("Bonjour", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        result.Should().Be("Bonjour");
    }

    /// <summary>
    /// Verifies that supports reports false for every feature.
    /// </summary>
    [TestMethod]
    public void Supports_ReportsFalseForEveryFeature() {
        var provider = CreateProvider(new HttpClient(new StubHandler()));

        foreach(var feature in Enum.GetValues<ProviderFeature>()) {
            provider.Supports(feature).Should().BeFalse(
                $"[Architecture] Google must honestly report unsupported feature {feature}");
        }
    }

    /// <summary>
    /// Verifies that canonical lookup null key throws.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullKey_ThrowsAsync() {
        var provider = CreateProvider(new HttpClient(new StubHandler()));

        Func<Task> act = async () => await provider.GetMessageTemplateAsync(null!, _french, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that canonical lookup null culture throws.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_NullCulture_ThrowsAsync() {
        var provider = CreateProvider(new HttpClient(new StubHandler()));

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Hello world", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that canonical lookup pre cancelled token does not call http.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_PreCancelledToken_DoesNotCallHttpAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>("cancellation is observed before any HTTP work");
    }

    /// <summary>
    /// Verifies that canonical lookup mid operation cancellation propagates token to http and throws.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_MidOperationCancellation_PropagatesTokenToHttpAndThrowsAsync() {
        var started = new TaskCompletionSource();
        CancellationToken observed = default;
        var handler = new MockHttpMessageHandler(async (_, cancellationToken) => {
            observed = cancellationToken;
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslateResponse(("unreachable", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        using var cancellation = new CancellationTokenSource();

        var lookup = provider.GetMessageTemplateAsync("Hello world", _french, cancellation.Token).AsTask();
        await started.Task;
        await cancellation.CancelAsync();

        async Task ActAsync() => await lookup;

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        observed.CanBeCanceled.Should().BeTrue("the caller token must reach the HTTP operation");
        observed.IsCancellationRequested.Should().BeTrue("the caller cancellation must reach the HTTP operation");
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <returns>The provider result.</returns>
    private static GoogleTranslationProvider CreateProvider(HttpClient client) {
        var options = new GoogleTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://translation.googleapis.com/language/translate/v2",
        };

        return new GoogleTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Performs the translate response operation.
    /// </summary>
    /// <param name="translations">The translations.</param>
    /// <returns>The translate response result.</returns>
    private static HttpResponseMessage TranslateResponse(params (string Text, string DetectedSourceLanguage)[] translations) {
        var items = new {
            data = new {
                translations = translations.Select(static pair => new {
                    translatedText = pair.Text,
                    detectedSourceLanguage = pair.DetectedSourceLanguage,
                }),
            },
        };

        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(items),
        };
    }

    /// <summary>
    /// Performs the ok json operation.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <returns>The ok json result.</returns>
    private static HttpResponseMessage OkJson(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

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
    /// Represents a translate request.
    /// </summary>
    private sealed class TranslateRequest {
        /// <summary>
        /// Gets or sets the q.
        /// </summary>
        [JsonPropertyName("q")]
        public string? Q { get; set; }

        /// <summary>
        /// Gets or sets the target.
        /// </summary>
        [JsonPropertyName("target")]
        public string? Target { get; set; }

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
