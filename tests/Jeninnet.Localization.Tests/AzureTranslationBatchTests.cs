//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the azure translation batch tests.
/// </summary>
[TestClass]
public class AzureTranslationBatchTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that run completes backfill with complete ledger.
    /// </summary>
    [TestMethod]
    public async Task Run_CompletesBackfillWithCompleteLedgerAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(attempt <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TranslationResponse([.. Enumerable.Range(0, 100).Select(index => ($"key-{index:000}-fr", "fr"))]));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);
        var progress = new RecordingProgress();

        var keys = Enumerable.Range(0, 100).Select(index => $"key-{index:000}").ToArray();
        var result = await job.RunAsync(keys, [_french], progress, TestContext.CancellationToken);

        result.Entries.Should().HaveCount(100);
        result.TranslatedCount.Should().Be(100);
        result.CachedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        for(var index = 0; index < 100; index++) {
            result.Entries[index].Key.Should().Be($"key-{index:000}");
            result.Entries[index].Culture.Should().Be(_french);
            result.Entries[index].Status.Should().Be(TranslationBatchStatus.Translated);
            result.Entries[index].Template.Should().Be($"key-{index:000}-fr");
            result.Entries[index].FailureReason.Should().BeNull();
        }

        requestCount.Should().Be(3, "scripted faults converge inside the reused provider pipeline");
        progress.Reports.Should().ContainSingle();
        progress.Reports[0].ProcessedKeys.Should().Be(100);
        progress.Reports[0].TotalKeys.Should().Be(100);
        progress.Reports[0].Translated.Should().Be(100);
        progress.Reports[0].Culture.Should().Be(_french);
    }

    /// <summary>
    /// Verifies that run mixed ledger failures never abort batch.
    /// </summary>
    [TestMethod]
    public async Task Run_MixedLedger_FailuresNeverAbortBatchAsync() {
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            var texts = body.Select(static item => item.Text).ToArray();
            lock(bodies) {
                bodies.Add(texts);
            }

            Interlocked.Increment(ref requestCount);
            if(texts.Any(static text => text.Contains("poison", StringComparison.Ordinal))) {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return TranslationResponse([.. texts.Select(static text => (text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, static options => options.RetryBaseDelay = TimeSpan.FromMilliseconds(1));
        var job = new TranslationBatchJob(provider);
        var progress = new RecordingProgress();

        var keys = Enumerable.Range(0, 150).Select(index => index == 120 ? "poison" : $"ok-{index:000}").ToArray();
        var result = await job.RunAsync(keys, [_french], progress, TestContext.CancellationToken);

        result.Entries.Should().HaveCount(150, "the ledger is complete even with chunk failures");
        result.TranslatedCount.Should().Be(100);
        result.FailedCount.Should().Be(50);
        result.CachedCount.Should().Be(0);
        result.Entries[120].Status.Should().Be(TranslationBatchStatus.Failed);
        result.Entries[120].Template.Should().BeNull();
        result.Entries[120].FailureReason.Should().Contain("HttpRequestException");
        result.Entries[120].FailureReason.Should().Contain("503");
        result.Entries[0].Status.Should().Be(TranslationBatchStatus.Translated);
        result.Entries[0].Template.Should().Be("ok-000-fr");

        requestCount.Should().Be(4, "one clean chunk plus three retried attempts of the poisoned chunk");
        progress.Reports.Should().HaveCount(2);
        progress.Reports[0].ProcessedKeys.Should().Be(100);
        progress.Reports[0].Translated.Should().Be(100);
        progress.Reports[1].ProcessedKeys.Should().Be(150);
        progress.Reports[1].Translated.Should().Be(100);
        progress.Reports[1].Failed.Should().Be(50);
    }

    /// <summary>
    /// Verifies that run idempotent rerun translates nothing new.
    /// </summary>
    [TestMethod]
    public async Task Run_IdempotentRerun_TranslatesNothingNewAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            Interlocked.Increment(ref requestCount);
            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var keys = Enumerable.Range(0, 20).Select(index => $"rerun-{index}").ToArray();
        var first = await job.RunAsync(keys, [_french], null, TestContext.CancellationToken);
        first.TranslatedCount.Should().Be(20);
        var callsAfterFirst = Volatile.Read(ref requestCount);

        var second = await job.RunAsync(keys, [_french], null, TestContext.CancellationToken);

        second.Entries.Should().HaveCount(20);
        second.CachedCount.Should().Be(20, "completed keys are skipped via the provider cache");
        second.TranslatedCount.Should().Be(0);
        second.FailedCount.Should().Be(0);
        Volatile.Read(ref requestCount).Should().Be(callsAfterFirst, "an idempotent re-run translates nothing new");
    }

    /// <summary>
    /// Verifies that estimate matches real run billable calls.
    /// </summary>
    [TestMethod]
    public async Task Estimate_MatchesRealRunBillableCallsAsync() {
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            lock(bodies) {
                bodies.Add([.. body.Select(static item => item.Text)]);
            }

            Interlocked.Increment(ref requestCount);
            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        var memory = new InMemoryTranslationMemory();
        var keys = Enumerable.Range(0, 100).Select(index => $"est-{index:000}").ToArray();
        foreach(var key in keys.Take(30)) {
            memory.Add(key, _french, key + "-tm");
        }

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, options => options.TranslationMemory = memory);
        var job = new TranslationBatchJob(provider);

        var estimate = await job.EstimateAsync(keys, [_french], TestContext.CancellationToken);

        estimate.TotalKeys.Should().Be(100);
        estimate.FreeKeys.Should().Be(30, "translation-memory hits count as free");
        estimate.BillableKeys.Should().Be(70);
        estimate.BillableCalls.Should().Be(1);
        Volatile.Read(ref requestCount).Should().Be(0, "dry-run makes zero HTTP calls");

        var result = await job.RunAsync(keys, [_french], null, TestContext.CancellationToken);

        Volatile.Read(ref requestCount).Should().Be(estimate.BillableCalls,
            "dry-run estimates match the real run's billable-call count");
        result.CachedCount.Should().Be(30);
        result.TranslatedCount.Should().Be(70);
        bodies.Should().ContainSingle();
        bodies[0].Should().HaveCount(70, "remembered keys are excluded from the request");
    }

    /// <summary>
    /// Verifies that estimate counts cache hits as free.
    /// </summary>
    [TestMethod]
    public async Task Estimate_CountsCacheHitsAsFreeAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            Interlocked.Increment(ref requestCount);
            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var warm = Enumerable.Range(0, 20).Select(index => $"warm-{index}").ToArray();
        await job.RunAsync(warm, [_french], null, TestContext.CancellationToken);

        var keys = warm.Concat(Enumerable.Range(0, 10).Select(index => $"fresh-{index}")).ToArray();
        var before = Volatile.Read(ref requestCount);
        var estimate = await job.EstimateAsync(keys, [_french], TestContext.CancellationToken);

        estimate.TotalKeys.Should().Be(30);
        estimate.FreeKeys.Should().Be(20, "provider-cache hits count as free");
        estimate.BillableKeys.Should().Be(10);
        estimate.BillableCalls.Should().Be(1);
        Volatile.Read(ref requestCount).Should().Be(before, "dry-run makes zero HTTP calls");
    }

    /// <summary>
    /// Verifies that estimate chunk windows simulate provider chunking.
    /// </summary>
    [TestMethod]
    public async Task Estimate_ChunkWindowsSimulateProviderChunkingAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            Interlocked.Increment(ref requestCount);
            return TranslationResponse([.. body.Select(static item => (item.Text + "-t", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var keys = Enumerable.Range(0, 250).Select(index => $"bulk-{index:000}").ToArray();
        var estimate = await job.EstimateAsync(keys, [_french, _german], TestContext.CancellationToken);

        estimate.TotalKeys.Should().Be(500);
        estimate.FreeKeys.Should().Be(0);
        estimate.BillableKeys.Should().Be(500);
        estimate.BillableCalls.Should().Be(6, "100 + 100 + 50 per culture");

        var result = await job.RunAsync(keys, [_french, _german], null, TestContext.CancellationToken);

        Volatile.Read(ref requestCount).Should().Be(6);
        result.Entries.Should().HaveCount(500);
        result.TranslatedCount.Should().Be(500);
        result.Entries[0].Culture.Should().Be(_french);
        result.Entries[250].Culture.Should().Be(_german, "ledger groups cultures in requested order");
    }

    /// <summary>
    /// Verifies that run cancellation stops promptly with partial ledger.
    /// </summary>
    [TestMethod]
    public async Task Run_Cancellation_StopsPromptlyWithPartialLedgerAsync() {
        var requestCount = 0;
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            var attempt = Interlocked.Increment(ref requestCount);
            if(attempt == 1) {
                return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
            }

            secondStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslationResponse(("unreachable", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);
        using var cancellation = new CancellationTokenSource();

        var keys = Enumerable.Range(0, 350).Select(index => $"cancel-{index:000}").ToArray();
        var run = job.RunAsync(keys, [_french], null, cancellation.Token);
        await secondStarted.Task;
        await cancellation.CancelAsync();

        async Task AwaitRunAsync() => await run;

        var exception = await ((Func<Task>)AwaitRunAsync).Should().ThrowAsync<TranslationBatchCancelledException>(
            "cancellation stops the batch promptly");
        (exception.Exception as OperationCanceledException).Should().NotBeNull(
            "batch cancellation observes the standard cancellation contract");
        exception.Exception.PartialResult.Entries.Should().HaveCount(100,
            "the partial ledger holds exactly the completed chunk window");
        exception.Exception.PartialResult.TranslatedCount.Should().Be(100);
        Volatile.Read(ref requestCount).Should().Be(2, "no further chunk starts after cancellation");
    }

    /// <summary>
    /// Verifies that run pre cancelled token throws with empty partial ledger.
    /// </summary>
    [TestMethod]
    public async Task Run_PreCancelledToken_ThrowsWithEmptyPartialLedgerAsync() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        async Task ActAsync() => await job.RunAsync(["key"], [_french], null, cancellation.Token);

        var exception = await ((Func<Task>)ActAsync).Should().ThrowAsync<TranslationBatchCancelledException>();
        exception.Exception.PartialResult.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that batch job validation rejects bad input.
    /// </summary>
    [TestMethod]
    public void BatchJob_Validation_RejectsBadInput() {
        using var client = new HttpClient(new StubHandler());
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var nullProvider = () => new TranslationBatchJob(null!);
        nullProvider.Should().Throw<ArgumentNullException>();

        Action nullKeys = () => job.RunAsync(null!, [_french], null, CancellationToken.None);
        nullKeys.Should().Throw<ArgumentNullException>();

        Action nullCultures = () => job.RunAsync(["key"], null!, null, CancellationToken.None);
        nullCultures.Should().Throw<ArgumentNullException>();

        Action nullKeyEntry = () => job.RunAsync(["ok", null!], [_french], null, CancellationToken.None);
        nullKeyEntry.Should().Throw<ArgumentException>();

        Action nullCultureEntry = () => job.RunAsync(["ok"], [_french, null!], null, CancellationToken.None);
        nullCultureEntry.Should().Throw<ArgumentException>();

        Action invariantCulture = () => job.RunAsync(["ok"], [CultureInfo.InvariantCulture], null, CancellationToken.None);
        invariantCulture.Should().Throw<ArgumentException>();

        Action nullKeysEstimate = () => job.EstimateAsync(null!, [_french], CancellationToken.None);
        nullKeysEstimate.Should().Throw<ArgumentNullException>();

        Action invariantEstimate = () => job.EstimateAsync(["ok"], [CultureInfo.InvariantCulture], CancellationToken.None);
        invariantEstimate.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that run empty input returns empty result without work.
    /// </summary>
    [TestMethod]
    public async Task Run_EmptyInput_ReturnsEmptyResultWithoutWorkAsync() {
        var progress = new RecordingProgress();
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var emptyKeys = await job.RunAsync([], [_french], progress, TestContext.CancellationToken);
        emptyKeys.Entries.Should().BeEmpty();
        emptyKeys.TranslatedCount.Should().Be(0);

        var emptyCultures = await job.RunAsync(["key"], [], progress, TestContext.CancellationToken);
        emptyCultures.Entries.Should().BeEmpty();

        progress.Reports.Should().BeEmpty("no chunk window means no progress report");

        var estimate = await job.EstimateAsync([], [_french], TestContext.CancellationToken);
        estimate.TotalKeys.Should().Be(0);
        estimate.BillableCalls.Should().Be(0);
    }

    /// <summary>
    /// Verifies that run duplicate keys translated once.
    /// </summary>
    [TestMethod]
    public async Task Run_DuplicateKeys_TranslatedOnceAsync() {
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            lock(bodies) {
                bodies.Add([.. body.Select(static item => item.Text)]);
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var job = new TranslationBatchJob(provider);

        var result = await job.RunAsync(["a", "b", "a", "c", "b"], [_french], null, TestContext.CancellationToken);

        result.Entries.Should().HaveCount(3);
        result.Entries.Select(static entry => entry.Key).Should().BeEquivalentTo("a", "b", "c");
        bodies.Should().ContainSingle();
        bodies[0].Should().BeEquivalentTo("a", "b", "c");
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <returns>The provider result.</returns>
    private static AzureTranslationProvider CreateProvider(
        HttpClient client,
        Action<AzureTranslationOptions>? configure = null) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
        };
        configure?.Invoke(options);

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
    /// Test double for progress.
    /// </summary>
    private sealed class RecordingProgress : IProgress<TranslationBatchProgress> {
        /// <summary>
        /// Gets the reports.
        /// </summary>
        public List<TranslationBatchProgress> Reports { get; } = [];

        /// <summary>
        /// Performs the report operation.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Report(TranslationBatchProgress value) {
            ArgumentNullException.ThrowIfNull(value);
            lock(Reports) {
                Reports.Add(value);
            }
        }
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
