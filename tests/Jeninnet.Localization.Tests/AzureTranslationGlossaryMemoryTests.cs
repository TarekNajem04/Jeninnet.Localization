//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the azure translation glossary memory tests.
/// </summary>
[TestClass]
public class AzureTranslationGlossaryMemoryTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that glossary forced term survives mt paraphrase.
    /// </summary>
    [TestMethod]
    public async Task Glossary_ForcedTerm_SurvivesMtParaphraseAsync() {
        string[]? requestTexts = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            requestCount++;
            return TranslationResponse(("Salut [[g0]] monde", "fr"));
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
            return TranslationResponse([.. texts.Select(static text => (text + "-fr", "fr"))]);
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
    /// Verifies that glossary other culture leaves source text is unchanged.
    /// </summary>
    [TestMethod]
    public async Task Glossary_OtherCulture_LeavesSourceTextUnchangedAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse(("Hallo Welt", "de"));
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
    /// Verifies that glossary multiple matches are restored by token.
    /// </summary>
    [TestMethod]
    public async Task Glossary_MultipleMatches_AreRestoredByTokenAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse(("Le [[g1]] [[g0]]", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");
        glossary.Register("world", _french, "Monde");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        requestTexts.Should().BeEquivalentTo("[[g0]] [[g1]]");
        result.Should().Be("Le Monde Bonjour", "each placeholder token restores its own forced target wherever the translator placed it");
    }

    /// <summary>
    /// Verifies that glossary source containing token like text generates collision free token.
    /// </summary>
    [TestMethod]
    public async Task Glossary_SourceContainingTokenLikeText_GeneratesCollisionFreeTokenAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse(("[[g0]] [[g1]]", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplateAsync("[[g0]] Hello", _french, CancellationToken.None);

        requestTexts.Should().BeEquivalentTo(["[[g0]] [[g1]]"], "the generated token must not collide with literal source text");
        result.Should().Be("[[g0]] Bonjour", "literal token-like source text must pass through untouched");
    }

    /// <summary>
    /// Verifies that glossary rejected conflicting term is never is applied.
    /// </summary>
    [TestMethod]
    public async Task Glossary_RejectedConflictingTerm_IsNeverAppliedAsync() {
        string[]? requestTexts = null;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse(("[[g0]] City-fr", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("New York", _french, "NewYorkFR");
        var rejected = glossary.Register("York", _french, "YorkFR");
        rejected.Registered.Should().BeFalse();

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplateAsync("New York City", _french, CancellationToken.None);

        requestTexts.Should().BeEquivalentTo(["[[g0]] City"], "only the accepted term is projected");
        result.Should().Be("NewYorkFR City-fr");
    }

    /// <summary>
    /// Verifies that glossary result is cached subsequent lookup served locally.
    /// </summary>
    [TestMethod]
    public async Task Glossary_ResultIsCached_SubsequentLookupServedLocallyAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Salut [[g0]] monde", "fr")));
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var first = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var second = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        first.Should().Be("Salut Bonjour monde");
        second.Should().Be("Salut Bonjour monde");
        requestCount.Should().Be(1, "the cache write-back stores the glossary-restored result");
    }

    /// <summary>
    /// Verifies that glossary batch lookup projects each key.
    /// </summary>
    [TestMethod]
    public async Task Glossary_BatchLookup_ProjectsEachKeyAsync() {
        string[]? requestTexts = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            requestCount++;
            return TranslationResponse(("[[g0]] [[g1]]-fr", "fr"), ("[[g0]]-fr", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");
        glossary.Register("World", _french, "Monde");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        var result = await provider.GetMessageTemplatesAsync(["Hello world", "World"], _french, TestContext.CancellationToken);

        requestTexts.Should().BeEquivalentTo("[[g0]] [[g1]]", "[[g0]]");
        result["Hello world"].Should().Be("Bonjour Monde-fr");
        result["World"].Should().Be("Monde-fr");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that translation memory hit performs zero api calls.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_Hit_PerformsZeroApiCallsAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Bonjour le monde");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Bonjour le monde");
    }

    /// <summary>
    /// Verifies that translation memory miss falls back to mt and does not write back.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_Miss_FallsBackToMtAndDoesNotWriteBackAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour le monde", "fr")));
        });

        var memory = new InMemoryTranslationMemory();

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
        memory.TryLookup("Hello world", _french, out _).Should().BeFalse(
            "machine translations are never written back into the approved translation memory");
    }

    /// <summary>
    /// Verifies that translation memory hit is cached and refresh observes updated entry.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_Hit_IsCachedAndRefreshObservesUpdatedEntryAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "v1");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);

        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None)).Should().Be("v1");

        memory.Add("Hello", _french, "v2");
        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None))
            .Should().Be("v1", "the TTL cache write-back shadows the updated memory entry until refresh");

        await provider.RefreshAsync(TestContext.CancellationToken);

        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None)).Should().Be("v2");
    }

    /// <summary>
    /// Verifies that translation memory mixed batch acquires only missed keys and reports external payload.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_MixedBatch_AcquiresOnlyMissedKeysAndReportsExternalPayloadAsync() {
        string[]? requestTexts = null;
        var events = new List<ProviderAcquisitionEventArgs>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            return TranslationResponse(("Monde", "fr"));
        });

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);
        provider.TranslationAcquired += (_, e) => events.Add(e);

        var result = await provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        requestTexts.Should().BeEquivalentTo(["World"], "a memory hit must be excluded from the external batch");
        result["Hello"].Should().Be("Bonjour");
        result["World"].Should().Be("Monde");
        events.Should().ContainSingle();
        events[0].Templates.ContainsKey("World").Should().BeTrue();
        events[0].Templates.ContainsKey("Hello").Should().BeFalse(
            "the acquisition event payload covers only externally acquired keys");
    }

    /// <summary>
    /// Verifies that translation memory all keys hit raises no acquisition event.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_AllKeysHit_RaisesNoAcquisitionEventAsync() {
        var events = 0;
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "Bonjour");
        memory.Add("World", _french, "Monde");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);
        provider.TranslationAcquired += (_, _) => events++;

        var result = await provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        result["Hello"].Should().Be("Bonjour");
        result["World"].Should().Be("Monde");
        events.Should().Be(0, "an acquisition served fully by the translation memory performs no external acquisition");
    }

    /// <summary>
    /// Verifies that translation memory degenerate empty key does not fault the lookup path.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_DegenerateEmptyKey_DoesNotFaultTheLookupPathAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("empty-fr", "fr")));
        });

        var memory = new InMemoryTranslationMemory();

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);

        var empty = await provider.GetMessageTemplateAsync(string.Empty, _french, CancellationToken.None);
        var whitespace = await provider.GetMessageTemplateAsync(" ", _french, CancellationToken.None);

        empty.Should().Be("empty-fr", "the translation memory must not fault the canonical lookup path for degenerate keys");
        whitespace.Should().Be("empty-fr");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that translation memory hit takes precedence over glossary.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_HitTakesPrecedenceOverGlossaryAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Salut le monde");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary, memory);

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Salut le monde", "the translation-memory lookup precedes glossary forcing and machine translation");
    }

    /// <summary>
    /// Verifies that translation memory with caching disabled still short circuits mt.
    /// </summary>
    [TestMethod]
    public async Task TranslationMemory_WithCachingDisabled_StillShortCircuitsMtAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory, cachingEnabled: false);

        var result = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        result.Should().Be("Bonjour");
        provider.Supports(ProviderFeature.Caching).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that preload consults memory and glossary then caches results.
    /// </summary>
    [TestMethod]
    public async Task Preload_ConsultsMemoryAndGlossaryThenCachesResultsAsync() {
        string[]? requestTexts = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            requestTexts = await ReadRequestTextsAsync(request, cancellationToken);
            requestCount++;
            return TranslationResponse(("[[g0]]-fr", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("World", _french, "Monde");

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary, memory);

        await provider.PreloadAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        requestTexts.Should().BeEquivalentTo(["[[g0]]"], "preload resolves the memory hit locally and translates only the miss");
        var fromCache = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        var forced = await provider.GetMessageTemplateAsync("World", _french, CancellationToken.None);
        fromCache.Should().Be("Bonjour");
        forced.Should().Be("Monde-fr");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that options glossary snapshot at construction ignores later registration.
    /// </summary>
    [TestMethod]
    public async Task Options_GlossarySnapshotAtConstruction_IgnoresLaterRegistrationAsync() {
        var allTexts = new List<string>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var texts = await ReadRequestTextsAsync(request, cancellationToken);
            allTexts.AddRange(texts);
            return TranslationResponse([.. texts.Select(static text => (text + "-fr", "fr"))]);
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);

        glossary.Register("World", _french, "Monde");

        var laterTerm = await provider.GetMessageTemplateAsync("World", _french, CancellationToken.None);
        var constructionTerm = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        laterTerm.Should().Be("World-fr", "options snapshots stay immutable: post-construction registration does not apply");
        constructionTerm.Should().Be("Bonjour-fr", "the construction-time snapshot stays in effect");
        allTexts.Should().BeEquivalentTo("World", "[[g0]]");
    }

    /// <summary>
    /// Verifies that options instance mutation after construction does not change behavior.
    /// </summary>
    [TestMethod]
    public async Task Options_InstanceMutationAfterConstruction_DoesNotChangeBehaviorAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello world", _french, "Salut le monde");

        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Glossary = glossary,
            TranslationMemory = memory,
        };

        using var client = new HttpClient(handler);
        var provider = new AzureTranslationProvider(Options.Create(options), client);

        options.Glossary = new MessageGlossary();
        options.TranslationMemory = new InMemoryTranslationMemory();

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Salut le monde", "mutating the caller-owned options instance does not change the constructed Provider");
    }

    /// <summary>
    /// Verifies that cancellation canonical lookup pre cancelled token throws even with memory hit.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_CanonicalLookup_PreCancelledTokenThrowsEvenWithMemoryHitAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        var memory = new InMemoryTranslationMemory();
        memory.Add("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, translationMemory: memory);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Hello", _french, cancellation.Token);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>(
            "a pre-cancelled canonical lookup is observed before the translation-memory shortcut");
    }

    /// <summary>
    /// Verifies that cancellation batch lookup with glossary propagates token to http and throws.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_BatchLookupWithGlossary_PropagatesTokenToHttpAndThrowsAsync() {
        var started = new TaskCompletionSource();
        CancellationToken observed = default;
        var handler = new MockHttpMessageHandler(async (_, cancellationToken) => {
            observed = cancellationToken;
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslationResponse(("unreachable", "fr"));
        });

        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, glossary: glossary);
        using var cancellation = new CancellationTokenSource();

        var lookup = provider.GetMessageTemplatesAsync(["Hello world"], _french, cancellation.Token).AsTask();
        await started.Task;
        await cancellation.CancelAsync();

        async Task ActAsync() => await lookup;

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        observed.CanBeCanceled.Should().BeTrue("the caller token must reach the HTTP operation through the glossary projection");
        observed.IsCancellationRequested.Should().BeTrue("caller cancellation must reach the HTTP operation through the glossary projection");
    }

    /// <summary>
    /// Reads request texts.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is request texts.</returns>
    private static async Task<string[]> ReadRequestTextsAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
        Assert.IsNotNull(body);
        return [.. body.Select(static item => item.Text)];
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="glossary">The glossary.</param>
    /// <param name="translationMemory">The translation memory.</param>
    /// <param name="cachingEnabled">The caching enabled.</param>
    /// <returns>The provider result.</returns>
    private static AzureTranslationProvider CreateProvider(
        HttpClient client,
        MessageGlossary? glossary = null,
        ITranslationMemory? translationMemory = null,
        bool cachingEnabled = true) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
            FromLanguage = string.Empty,
            Region = string.Empty,
            CachingEnabled = cachingEnabled,
            CacheExpiration = AzureTranslationOptions.DefaultCacheExpiration,
            Glossary = glossary,
            TranslationMemory = translationMemory,
        };

        return new AzureTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Performs the translation response operation.
    /// </summary>
    /// <param name="translations">The translations.</param>
    /// <returns>The translation response result.</returns>
    private static HttpResponseMessage TranslationResponse(params (string Text, string To)[] translations) {
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
    /// Represents a translation request body.
    /// </summary>
    private sealed class TranslationRequestBody {
        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
