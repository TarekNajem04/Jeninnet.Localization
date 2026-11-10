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
/// Contains the DeepL translation glossary memory tests. Translation-memory exact matches
/// short-circuit the paid call and glossary terms survive paraphrase through placeholder
/// protection, exactly as on the sibling translation providers.
/// </summary>
[TestClass]
public class DeepLTranslationGlossaryMemoryTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that glossary forced term survives MT paraphrase.
    /// </summary>
    [TestMethod]
    public async Task Glossary_ForcedTerm_SurvivesMtParaphraseAsync() {
        string[]? requestTexts = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            requestCount++;
            return TranslationResponse("Salut [[g0]] monde");
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Salut Bonjour monde", "the forced term must survive a paraphrase by the machine translator");
        requestTexts.Should().BeEquivalentTo("[[g0]] world");
        Assert.IsFalse(requestTexts![0].Contains("Hello", StringComparison.Ordinal),
            "the forced term must be protected before the text reaches the translator");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that glossary case and whole word rules control matching.
    /// </summary>
    [TestMethod]
    public async Task Glossary_CaseAndWholeWordRules_ControlMatchingAsync() {
        var allTexts = new List<string>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var texts = await ReadRequestTextsAsync(request, cancellationToken);
            allTexts.AddRange(texts);
            return TranslationResponse([.. texts.Select(static text => text + "-fr")]);
        });

        var glossary = new MessageGlossary();
        glossary.Register("cat", _french, "chat", caseSensitive: true, wholeWord: true);

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var matched = await provider.GetMessageTemplateAsync("The cat sat", _french, CancellationToken.None);
        var caseMiss = await provider.GetMessageTemplateAsync("The CAT sat", _french, CancellationToken.None);
        var wordMiss = await provider.GetMessageTemplateAsync("category", _french, CancellationToken.None);

        matched.Should().Be("The chat sat-fr");
        caseMiss.Should().Be("The CAT sat-fr", "a case-sensitive term does not match different casing");
        wordMiss.Should().Be("category-fr", "a whole-word term does not match inside a word");
        allTexts.Should().BeEquivalentTo("The [[g0]] sat", "The CAT sat", "category");
    }

    /// <summary>
    /// Verifies that glossary other culture leaves source text unchanged.
    /// </summary>
    [TestMethod]
    public async Task Glossary_OtherCulture_LeavesSourceTextUnchangedAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse("Hallo Welt");
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        result.Should().Be("Hallo Welt");
        requestTexts.Should().BeEquivalentTo(["Hello world"], "glossaries are per target culture");
    }

    /// <summary>
    /// Verifies that glossary snapshot at construction ignores later registration.
    /// </summary>
    [TestMethod]
    public async Task Glossary_SnapshotAtConstruction_IgnoresLaterRegistrationAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse("Hallo Welt");
        });

        var glossary = new MessageGlossary();
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);
        glossary.Register("Hello", _german, "Hallo-ignored");

        var result = await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        result.Should().Be("Hallo Welt");
        requestTexts.Should().BeEquivalentTo(["Hello world"], "terms registered after construction must not change the provider");
    }

    /// <summary>
    /// Verifies that memory exact match short circuits paid call.
    /// </summary>
    [TestMethod]
    public async Task Memory_ExactMatch_ShortCircuitsPaidCallAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _german, "Hallo Welt");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);
        ProviderAcquisitionEventArgs? raised = null;
        provider.TranslationAcquired += (_, args) => raised = args;

        var result = await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None);

        result.Should().Be("Hallo Welt");
        raised.Should().BeNull("a translation-memory hit raises no acquisition event");
    }

    /// <summary>
    /// Verifies that memory miss falls through to http and caches.
    /// </summary>
    [TestMethod]
    public async Task Memory_Miss_FallsThroughToHttpAndCachesAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse("Hallo Welt"));
        });

        var memory = new InMemoryTranslationMemory();
        memory.Add("Other", _german, "Andere");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);

        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        (await provider.GetMessageTemplateAsync("Hello world", _german, CancellationToken.None)).Should().Be("Hallo Welt");
        requestCount.Should().Be(1, "the fall-through result is cached");
    }

    /// <summary>
    /// Verifies that memory and glossary compose in documented order.
    /// </summary>
    [TestMethod]
    public async Task MemoryAndGlossary_ComposeInDocumentedOrderAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestCount++;
            var texts = await ReadRequestTextsAsync(request, cancellationToken);
            texts.Should().BeEquivalentTo("[[g0]] world");
            return TranslationResponse("Salut Bonjour monde");
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        var memory = new InMemoryTranslationMemory();
        memory.Add("Cached", _french, "En cache");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary, memory);

        (await provider.GetMessageTemplateAsync("Cached", _french, CancellationToken.None)).Should().Be("En cache");
        (await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None)).Should().Be("Salut Bonjour monde");
        requestCount.Should().Be(1, "only the glossary path reaches HTTP");
    }

    /// <summary>
    /// Reads request texts.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is request texts.</returns>
    private static async Task<string[]> ReadRequestTextsAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = await request.Content!.ReadFromJsonAsync<DeepLRequestBody>(cancellationToken: cancellationToken);
        Assert.IsNotNull(body);
        return body.Text;
    }

    /// <summary>
    /// Represents a DeepL request body.
    /// </summary>
    private sealed class DeepLRequestBody {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string[] Text { get; set; } = [];
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="glossary">The glossary.</param>
    /// <param name="translationMemory">The translation memory.</param>
    /// <param name="cachingEnabled">The caching enabled.</param>
    /// <returns>The provider result.</returns>
    private static DeepLTranslationProvider CreateProvider(
        HttpClient client,
        MessageGlossary? glossary = null,
        ITranslationMemory? translationMemory = null,
        bool cachingEnabled = true) {
        var options = new DeepLTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.deepl.com",
            FromLanguage = string.Empty,
            CachingEnabled = cachingEnabled,
            CacheExpiration = DeepLTranslationOptions.DefaultCacheExpiration,
            Glossary = glossary,
            TranslationMemory = translationMemory,
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
