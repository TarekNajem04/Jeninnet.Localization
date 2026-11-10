//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Translation.DeepL;

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Source-template translation tests: identity separation from key lookups, source-cache
/// behavior, placeholder-parity enforcement, and translation-memory/event integration
/// across the Azure, DeepL, and Google providers. Every test drives a fake
/// <see cref="HttpMessageHandler"/>: no test requires a vendor key or network access.
/// </summary>
[TestClass]
public class TranslationSourceTemplateTests {
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that azure source and key cache identities stay separate.
    /// </summary>
    [TestMethod]
    public async Task Azure_SourceAndKey_CacheIdentitiesStaySeparateAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            var body = await ReadAzureBodyAsync(request, cancellationToken);
            return AzureResponse([.. body.Select(static text => (text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client);
        var french = CultureInfo.GetCultureInfo("fr");

        var keyed = await provider.GetMessageTemplateAsync("Hello {name}", french, CancellationToken.None);
        var sourced = await provider.TranslateSourceAsync("Hello {name}", french, CancellationToken.None);

        keyed.Should().Be("Hello {name}-fr");
        sourced.Should().Be("Hello {name}-fr");
        calls.Should().Be(2, "the key lookup and the source lookup own independent cache entries");
    }

    /// <summary>
    /// Verifies that azure source cache hit serves without http.
    /// </summary>
    [TestMethod]
    public async Task Azure_SourceCache_HitServesWithoutHttpAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            var body = await ReadAzureBodyAsync(request, cancellationToken);
            return AzureResponse([.. body.Select(static text => (text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client);
        var french = CultureInfo.GetCultureInfo("fr");

        var first = await provider.TranslateSourceAsync("Hello {name}", french, CancellationToken.None);
        var second = await provider.TranslateSourceAsync("Hello {name}", french, CancellationToken.None);

        first.Should().Be(second);
        calls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that azure parity mismatch throws and is not cached.
    /// </summary>
    [TestMethod]
    public async Task Azure_ParityMismatch_ThrowsAndIsNotCachedAsync() {
        var calls = 0;
        var failures = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            calls++;
            return Task.FromResult(AzureResponse(("Bonjour", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client);
        provider.AcquisitionFailed += (_, _) => failures++;
        var french = CultureInfo.GetCultureInfo("fr");

        Func<Task> first = async () => await provider.TranslateSourceAsync("Hello {name}", french, CancellationToken.None);
        Func<Task> second = async () => await provider.TranslateSourceAsync("Hello {name}", french, CancellationToken.None);

        (await first.Should().ThrowAsync<SourceTemplateParityException>()).Which.Message.Should().Contain("name");
        (await second.Should().ThrowAsync<SourceTemplateParityException>()).Which.Message.Should().Contain("name");
        calls.Should().Be(2, "a parity failure is never cached");
        failures.Should().Be(2);
    }

    /// <summary>
    /// Verifies that azure refresh clears source cache.
    /// </summary>
    [TestMethod]
    public async Task Azure_Refresh_ClearsSourceCacheAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            var body = await ReadAzureBodyAsync(request, cancellationToken);
            return AzureResponse([.. body.Select(static text => (text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client);
        var french = CultureInfo.GetCultureInfo("fr");

        await provider.TranslateSourceAsync("Hello", french, CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);
        await provider.TranslateSourceAsync("Hello", french, CancellationToken.None);

        calls.Should().Be(2);
    }

    /// <summary>
    /// Verifies that azure translation memory hit short circuits.
    /// </summary>
    [TestMethod]
    public async Task Azure_TranslationMemory_HitShortCircuitsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello {name}", _german, "Hallo {name}");

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client, memory);

        var result = await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        result.Should().Be("Hallo {name}");
    }

    /// <summary>
    /// Verifies that azure events report external source acquisitions.
    /// </summary>
    [TestMethod]
    public async Task Azure_Events_ReportExternalSourceAcquisitionsAsync() {
        var acquired = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await ReadAzureBodyAsync(request, cancellationToken);
            return AzureResponse([.. body.Select(static text => (text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = AzureProvider(client);
        provider.TranslationAcquired += (_, _) => acquired++;
        var french = CultureInfo.GetCultureInfo("fr");

        await provider.TranslateSourceAsync("Hello", french, CancellationToken.None);
        await provider.TranslateSourceAsync("Hello", french, CancellationToken.None);

        acquired.Should().Be(1, "the cached second lookup raises no event");
    }

    /// <summary>
    /// Verifies that azure invariant culture null without http.
    /// </summary>
    [TestMethod]
    public async Task Azure_InvariantCulture_NullWithoutHttpAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = AzureProvider(client);

        (await provider.TranslateSourceAsync("Hello", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that azure source supports feature.
    /// </summary>
    [TestMethod]
    public void Azure_Source_SupportsFeature() {
        using var client = new HttpClient(new StubHandler());
        var provider = AzureProvider(client);

        provider.Supports(ProviderFeature.SourceTemplates).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that deepl source and key cache identities stay separate.
    /// </summary>
    [TestMethod]
    public async Task DeepL_SourceAndKey_CacheIdentitiesStaySeparateAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var count = JsonDocument.Parse(body).RootElement.GetProperty("text").GetArrayLength();
            return DeepLResponse(Enumerable.Range(0, count).Select(static _ => "Hallo {name}").ToArray());
        });

        using var client = new HttpClient(handler);
        var provider = DeepLProvider(client);

        var keyed = await provider.GetMessageTemplateAsync("Hello {name}", _german, CancellationToken.None);
        var sourced = await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        keyed.Should().Be("Hallo {name}");
        sourced.Should().Be("Hallo {name}");
        calls.Should().Be(2, "the key lookup and the source lookup own independent cache entries");
    }

    /// <summary>
    /// Verifies that deepl parity mismatch throws and is not cached.
    /// </summary>
    [TestMethod]
    public async Task DeepL_ParityMismatch_ThrowsAndIsNotCachedAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            calls++;
            return Task.FromResult(DeepLResponse("Hallo"));
        });

        using var client = new HttpClient(handler);
        var provider = DeepLProvider(client);

        Func<Task> first = async () => await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);
        Func<Task> second = async () => await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        (await first.Should().ThrowAsync<SourceTemplateParityException>()).Which.Message.Should().Contain("name");
        (await second.Should().ThrowAsync<SourceTemplateParityException>()).Which.Message.Should().Contain("name");
        calls.Should().Be(2, "a parity failure is never cached");
    }

    /// <summary>
    /// Verifies that deepl icu parity passes.
    /// </summary>
    [TestMethod]
    public async Task DeepL_IcuParity_PassesAsync() {
        const string source = "{count, plural, one{# file} other{# files}}";
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(DeepLResponse(source + "-de")));

        using var client = new HttpClient(handler);
        var provider = DeepLProvider(client);

        var result = await provider.TranslateSourceAsync(source, _german, CancellationToken.None);

        result.Should().Be(source + "-de");
    }

    /// <summary>
    /// Verifies that deepl translation memory hit short circuits.
    /// </summary>
    [TestMethod]
    public async Task DeepL_TranslationMemory_HitShortCircuitsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));
        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello {name}", _german, "Hallo {name}");

        using var client = new HttpClient(handler);
        var provider = DeepLProvider(client, memory);

        var result = await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        result.Should().Be("Hallo {name}");
    }

    /// <summary>
    /// Verifies that deepl source supports feature.
    /// </summary>
    [TestMethod]
    public void DeepL_Source_SupportsFeature() {
        using var client = new HttpClient(new StubHandler());
        var provider = DeepLProvider(client);

        provider.Supports(ProviderFeature.SourceTemplates).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that google translates source with parity check.
    /// </summary>
    [TestMethod]
    public async Task Google_TranslatesSource_WithParityCheckAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var text = body.RootElement.GetProperty("q").GetString()!;
            return GoogleResponse(text + "-de");
        });

        using var client = new HttpClient(handler);
        var provider = GoogleProvider(client);

        var result = await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        result.Should().Be("Hello {name}-de");
        calls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that google parity mismatch throws.
    /// </summary>
    [TestMethod]
    public async Task Google_ParityMismatch_ThrowsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(GoogleResponse("Bonjour")));

        using var client = new HttpClient(handler);
        var provider = GoogleProvider(client);

        Func<Task> act = async () => await provider.TranslateSourceAsync("Hello {name}", _german, CancellationToken.None);

        (await act.Should().ThrowAsync<SourceTemplateParityException>()).Which.Message.Should().Contain("name");
    }

    /// <summary>
    /// Verifies that google source has no cache.
    /// </summary>
    [TestMethod]
    public async Task Google_Source_HasNoCacheAsync() {
        var calls = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return GoogleResponse(body.RootElement.GetProperty("q").GetString()! + "-de");
        });

        using var client = new HttpClient(handler);
        var provider = GoogleProvider(client);

        await provider.TranslateSourceAsync("Hello", _german, CancellationToken.None);
        await provider.TranslateSourceAsync("Hello", _german, CancellationToken.None);

        calls.Should().Be(2, "Google stays cacheless; every source lookup calls the service");
    }

    /// <summary>
    /// Verifies that google invariant culture null without http.
    /// </summary>
    [TestMethod]
    public async Task Google_InvariantCulture_NullWithoutHttpAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = GoogleProvider(client);

        (await provider.TranslateSourceAsync("Hello", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that google source supports feature.
    /// </summary>
    [TestMethod]
    public void Google_Source_SupportsFeature() {
        using var client = new HttpClient(new StubHandler());
        var provider = GoogleProvider(client);

        provider.Supports(ProviderFeature.SourceTemplates).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that null arguments throw argument null exception.
    /// </summary>
    [TestMethod]
    public async Task NullArguments_ThrowArgumentNullExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = DeepLProvider(client);

        Func<Task> nullSource = async () => await provider.TranslateSourceAsync(null!, _german, CancellationToken.None);
        Func<Task> nullCulture = async () => await provider.TranslateSourceAsync("Hello", null!, CancellationToken.None);

        await nullSource.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = DeepLProvider(client);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.TranslateSourceAsync("Hello", _german, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Creates an Azure provider.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="memory">The translation memory.</param>
    /// <returns>The provider.</returns>
    private static AzureTranslationProvider AzureProvider(HttpClient client, ITranslationMemory? memory = null) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
            TranslationMemory = memory,
        };

        return new AzureTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Creates a DeepL provider.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="memory">The translation memory.</param>
    /// <returns>The provider.</returns>
    private static DeepLTranslationProvider DeepLProvider(HttpClient client, ITranslationMemory? memory = null) {
        var options = new DeepLTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.deepl.com",
            TranslationMemory = memory,
        };

        return new DeepLTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Creates a Google provider.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <returns>The provider.</returns>
    private static GoogleTranslationProvider GoogleProvider(HttpClient client) {
        var options = new GoogleTranslationOptions {
            ApiKey = "test-api-key",
        };

        return new GoogleTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Reads an Azure request body.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested texts.</returns>
    private static async Task<string[]> ReadAzureBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        return [.. body.RootElement.EnumerateArray().Select(static item => item.GetProperty("Text").GetString()!)];
    }

    /// <summary>
    /// Builds an Azure translation response.
    /// </summary>
    /// <param name="translations">The translations.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage AzureResponse(params (string Text, string To)[] translations) {
        var items = translations.Select(static pair => new {
            translations = new[] {
                new { text = pair.Text, to = pair.To },
            },
        });

        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(items),
        };
    }

    /// <summary>
    /// Builds a DeepL translation response.
    /// </summary>
    /// <param name="texts">The translated texts.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage DeepLResponse(params string[] texts) {
        var payload = new {
            translations = texts.Select(static text => new { text, detected_source_language = "EN" }).ToArray(),
        };

        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(payload),
        };
    }

    /// <summary>
    /// Builds a Google translation response.
    /// </summary>
    /// <param name="translatedText">The translated text.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage GoogleResponse(string translatedText) {
        var payload = new {
            data = new {
                translations = new[] {
                    new { translatedText },
                },
            },
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
