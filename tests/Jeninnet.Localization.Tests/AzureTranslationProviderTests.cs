//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the azure translation provider tests.
/// </summary>
[TestClass]
public class AzureTranslationProviderTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");

    /// <summary>
    /// Verifies that canonical lookup translates key to target culture.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_TranslatesKeyToTargetCultureAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.Query.Should().Contain("api-version=3.0");
            request.RequestUri.Query.Should().Contain("to=fr");
            request.RequestUri.Query.Should().Contain("from=en");
            request.Headers.GetValues("Ocp-Apim-Subscription-Key").First().Should().Be("test-api-key");
            request.Headers.GetValues("Ocp-Apim-Subscription-Region").First().Should().Be("test-region");

            var content = request.Content;
            Assert.IsNotNull(content);
            var body = await content.ReadAsStringAsync(cancellationToken);
            body.Should().Contain("\"Text\"");
            body.Should().Contain("Hello world");

            requestCount++;
            return TranslationResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, FromLanguage: "en", Region: "test-region");

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Bonjour le monde");
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

        Func<Task> act = async () => await provider.GetMessageTemplateAsync(null!, _french, CancellationToken.None);

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

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that canonical lookup regional culture is sent as target.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_RegionalCulture_IsSentAsTargetAsync() {
        var handler = new MockHttpMessageHandler((request, _) => {
            request.RequestUri!.Query.Should().Contain("to=fr-CA");
            Assert.IsFalse(request.RequestUri.Query.Contains("from=", StringComparison.Ordinal));
            return Task.FromResult(TranslationResponse(("Allô le monde", "fr-CA")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplateAsync("Hello world", CultureInfo.GetCultureInfo("fr-CA"), CancellationToken.None);

        result.Should().Be("Allô le monde");
    }

    /// <summary>
    /// Verifies that canonical lookup caches result second call served locally.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_CachesResult_SecondCallServedLocallyAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var first = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var second = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        first.Should().Be("Bonjour le monde");
        second.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that canonical lookup caching disabled each call hits http.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_CachingDisabled_EachCallHitsHttpAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);

        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that multi key lookup resolves batch in single request.
    /// </summary>
    [TestMethod]
    public async Task MultiKeyLookup_ResolvesBatchInSingleRequestAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            body.Select(static item => item.Text).Should().HaveCount(2);
            body.Select(static item => item.Text).Should().Contain("Hello");
            body.Select(static item => item.Text).Should().Contain("World");

            requestCount++;
            return TranslationResponse(("Bonjour", "fr"), ("Monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        result["Hello"].Should().Be("Bonjour");
        result["World"].Should().Be("Monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that multi key lookup cache miss only acquires remaining.
    /// </summary>
    [TestMethod]
    public async Task MultiKeyLookup_CacheMissOnly_AcquiresRemainingAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            requestCount++;
            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.GetMessageTemplatesAsync(["Hello"], _french, TestContext.CancellationToken);
        var result = await provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        result["Hello"].Should().Be("Hello-fr");
        result["World"].Should().Be("World-fr");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that multi key lookup invariant culture returns all null.
    /// </summary>
    [TestMethod]
    public async Task MultiKeyLookup_InvariantCulture_ReturnsAllNullAsync() {
        var handler = new MockHttpMessageHandler((_, _) => throw new InvalidOperationException("must not be called"));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GetMessageTemplatesAsync(["Hello", "World"], CultureInfo.InvariantCulture, TestContext.CancellationToken);

        result["Hello"].Should().BeNull();
        result["World"].Should().BeNull();
    }

    /// <summary>
    /// Verifies that preload populates cache subsequent lookup served locally.
    /// </summary>
    [TestMethod]
    public async Task Preload_PopulatesCache_SubsequentLookupServedLocallyAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour", "fr"), ("Monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.PreloadAsync(["Hello", "World"], _french, TestContext.CancellationToken);
        var result = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        result.Should().Be("Bonjour");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that preload caching disabled is no op.
    /// </summary>
    [TestMethod]
    public async Task Preload_CachingDisabled_IsNoOpAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CachingEnabled: false);

        await provider.PreloadAsync(["Hello"], _french, TestContext.CancellationToken);

        requestCount.Should().Be(0);
        provider.Supports(ProviderFeature.Preload).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that preload invariant culture is no op.
    /// </summary>
    [TestMethod]
    public async Task Preload_InvariantCulture_IsNoOpAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.PreloadAsync(["Hello"], CultureInfo.InvariantCulture, TestContext.CancellationToken);

        requestCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that refresh invalidates cache subsequent lookup refetches.
    /// </summary>
    [TestMethod]
    public async Task Refresh_InvalidatesCache_SubsequentLookupRefetchesAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        await provider.RefreshAsync(TestContext.CancellationToken);
        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        result.Should().Be("Bonjour le monde");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that refresh concurrent stale acquisition does not repopulate cache.
    /// </summary>
    [TestMethod]
    public async Task Refresh_ConcurrentStaleAcquisition_DoesNotRepopulateCacheAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (_, _) => {
            if(requestCount == 0) {
                requestCount++;
                firstStarted.SetResult();
                await gate.Task;
                return TranslationResponse(("old-fr", "fr"));
            }

            requestCount++;
            return TranslationResponse(("new-fr", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var staleLookup = provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        await firstStarted.Task;
        await provider.RefreshAsync(TestContext.CancellationToken);

        gate.SetResult();
        (await staleLookup).Should().Be("old-fr");

        var fresh = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        fresh.Should().Be("new-fr");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that refresh stale completion concurrent with cache write does not repopulate cache.
    /// </summary>
    [TestMethod]
    public async Task Refresh_StaleCompletionConcurrentWithCacheWrite_DoesNotRepopulateCacheAsync() {
        var requestCount = 0;
        var gateSlot = new[] { new SemaphoreSlim(0, 1) };
        var staleStartedSlot = new[] { new TaskCompletionSource() };
        var firstChunkSlot = new[] { 0 };
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            var keys = body.Select(static item => item.Text).ToArray();
            Interlocked.Increment(ref requestCount);
            if(keys.Length == 100 && Interlocked.Exchange(ref firstChunkSlot[0], 1) == 0) {
                staleStartedSlot[0].TrySetResult();
                await gateSlot[0].WaitAsync(cancellationToken);
            }

            return TranslationResponse([.. keys.Select(static key => (key + "-old", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        // The stale completion publishes one thousand entries after its generation re-check. A cache
        // filled to its bounded capacity makes every publication run the bounded-cache eviction scan,
        // lengthening the check-to-write window so a refresh with a swept delay can interleave within
        // it. The cache is re-primed for every attempt to keep that amplification.
        foreach(var refreshDelayMs in (int[])[20, 40, 80, 120, 180]) {
            gateSlot[0] = new SemaphoreSlim(0, 1);
            staleStartedSlot[0] = new TaskCompletionSource();
            firstChunkSlot[0] = 0;
            for(var index = 0; index < 4096; index++) {
                await provider.GetMessageTemplateAsync($"warm{index}", _french, CancellationToken.None);
            }

            var staleKeys = Enumerable.Range(0, 1000).Select(index => $"stale{refreshDelayMs}_{index}").ToArray();
            async Task<IReadOnlyDictionary<string, string?>> AcquireStaleBatchAsync(string[] keys) =>
                await provider.GetMessageTemplatesAsync(keys, _french, TestContext.CancellationToken);
            var staleBatch = AcquireStaleBatchAsync(staleKeys);
            await staleStartedSlot[0].Task;

            var before = Volatile.Read(ref requestCount);
            gateSlot[0].Release();
            var refresh = Task.Run(
                async () => {
                    await Task.Delay(refreshDelayMs, CancellationToken.None);
                    await provider.RefreshAsync(TestContext.CancellationToken);
                },
                CancellationToken.None);
            await Task.WhenAll(staleBatch, refresh);

            var probeResult = await provider.GetMessageTemplateAsync(staleKeys[^1], _french, CancellationToken.None);
            var actualRequests = Volatile.Read(ref requestCount);
            actualRequests.Should().Be(before + 10, $"delay={refreshDelayMs} probe={probeResult}");
        }
    }

    /// <summary>
    /// Verifies that configuration non positive cache expiration rejected when caching is enabled.
    /// </summary>
    [TestMethod]
    public void Configuration_NonPositiveCacheExpiration_RejectedWhenCachingEnabled() {
        using var client = new HttpClient(new StubHandler());

        var zero = () => CreateProvider(client, CacheExpiration: TimeSpan.Zero);
        zero.Should().Throw<InvalidOperationException>("[Architecture] CacheExpiration must be positive when caching is enabled")
            .Exception.Message.Should().Contain("CacheExpiration");

        var negative = () => CreateProvider(client, CacheExpiration: TimeSpan.FromSeconds(-1));
        negative.Should().Throw<InvalidOperationException>();

        var cachingDisabled = CreateProvider(client, CachingEnabled: false, CacheExpiration: TimeSpan.Zero);
        cachingDisabled.Supports(ProviderFeature.Caching).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that supports reflects effective configuration.
    /// </summary>
    [TestMethod]
    public void Supports_ReflectsEffectiveConfiguration() {
        using var client = new HttpClient(new StubHandler());
        var enabled = CreateProvider(client, CachingEnabled: true);
        enabled.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        enabled.Supports(ProviderFeature.EventsListening).Should().BeTrue();
        enabled.Supports(ProviderFeature.Preload).Should().BeTrue();
        enabled.Supports(ProviderFeature.Refresh).Should().BeTrue();
        enabled.Supports(ProviderFeature.Caching).Should().BeTrue();

        var disabled = CreateProvider(client, CachingEnabled: false);
        disabled.Supports(ProviderFeature.Caching).Should().BeFalse();
        disabled.Supports(ProviderFeature.Preload).Should().BeFalse();
        disabled.Supports(ProviderFeature.MultiKeys).Should().BeTrue();
        disabled.Supports(ProviderFeature.EventsListening).Should().BeTrue();
        disabled.Supports(ProviderFeature.Refresh).Should().BeTrue();
        enabled.Supports(ProviderFeature.None).Should().BeFalse();
        disabled.Supports(ProviderFeature.None).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that events translation acquired raised once per external acquisition.
    /// </summary>
    [TestMethod]
    public async Task Events_TranslationAcquired_RaisedOncePerExternalAcquisitionAsync() {
        var acquired = 0;
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(TranslationResponse(("Bonjour le monde", "fr"))));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        provider.TranslationAcquired += (_, _) => acquired++;

        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        acquired.Should().Be(1);
    }

    /// <summary>
    /// Verifies that events acquisition failed raised on http error.
    /// </summary>
    [TestMethod]
    public async Task Events_AcquisitionFailed_RaisedOnHttpErrorAsync() {
        var failed = 0;
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        provider.AcquisitionFailed += (_, _) => failed++;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None).AsTask());

        failed.Should().Be(1);
    }

    /// <summary>
    /// Verifies that events handler exception propagates to caller without acquisition is failed.
    /// </summary>
    [TestMethod]
    public async Task Events_HandlerException_PropagatesToCallerWithoutAcquisitionFailedAsync() {
        var failed = 0;
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(TranslationResponse(("Bonjour le monde", "fr"))));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        provider.TranslationAcquired += (_, _) => throw new InvalidOperationException("listener failure");
        provider.AcquisitionFailed += (_, _) => failed++;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None).AsTask());

        exception.Message.Should().Be("listener failure");
        failed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that refresh already cancelled token does not perform invalidation.
    /// </summary>
    [TestMethod]
    public async Task Refresh_AlreadyCancelledToken_DoesNotPerformInvalidationAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => provider.RefreshAsync(cts.Token);
        act.Should().Throw<OperationCanceledException>(
            "[Architecture] a canceled refresh token prevents the invalidation");

        var result = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        result.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that cancellation of batch lookup does not raise acquisition is failed.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_OfBatchLookup_DoesNotRaiseAcquisitionFailedAsync() {
        var failed = 0;
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(TranslationResponse(("Bonjour", "fr"))));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        provider.AcquisitionFailed += (_, _) => failed++;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GetMessageTemplatesAsync(["Hello"], _french, cts.Token).AsTask());

        failed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that coalescing batch joins in flight single key.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_BatchJoinsInFlightSingleKeyAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            bodies.Add([.. body.Select(static item => item.Text).Order()]);
            if(requestCount++ == 0) {
                firstStarted.SetResult();
                await gate.Task;
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var singleValueTask = provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        await firstStarted.Task;
        var batchValueTask = provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);

        gate.SetResult();
        var batch = await batchValueTask;

        (await singleValueTask).Should().Be("Hello-fr");
        batch["Hello"].Should().Be("Hello-fr");
        batch["World"].Should().Be("World-fr");
        requestCount.Should().Be(2);
        bodies[0].Should().BeEquivalentTo(["Hello"], "[Architecture] the batch must join the in-flight single-key acquisition, not re-request it");
        bodies[1].Should().BeEquivalentTo(["World"], "[Architecture] the batch must acquire only its own remaining key");
    }

    /// <summary>
    /// Verifies that coalescing single key joins in flight batch.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_SingleKeyJoinsInFlightBatchAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            bodies.Add([.. body.Select(static item => item.Text).Order()]);
            if(requestCount++ == 0) {
                firstStarted.SetResult();
                await gate.Task;
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var batchValueTask = provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);
        await firstStarted.Task;
        var singleValueTask = provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        gate.SetResult();
        var batch = await batchValueTask;

        (await singleValueTask).Should().Be("Hello-fr");
        batch["Hello"].Should().Be("Hello-fr");
        batch["World"].Should().Be("World-fr");
        requestCount.Should().Be(1);
        bodies[0].Should().BeEquivalentTo(["Hello", "World"], "[Architecture] the single-key lookup must join the in-flight batch without a second request");
    }

    /// <summary>
    /// Verifies that coalescing overlapping batches share per key acquisitions.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_OverlappingBatches_SharePerKeyAcquisitionsAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            bodies.Add([.. body.Select(static item => item.Text).Order()]);
            if(requestCount++ == 0) {
                firstStarted.SetResult();
                await gate.Task;
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var firstBatch = provider.GetMessageTemplatesAsync(["Hello", "World"], _french, TestContext.CancellationToken);
        await firstStarted.Task;
        var secondBatch = provider.GetMessageTemplatesAsync(["World", "Sun"], _french, TestContext.CancellationToken);

        gate.SetResult();
        var result1 = await firstBatch;
        var result2 = await secondBatch;

        result1["Hello"].Should().Be("Hello-fr");
        result1["World"].Should().Be("World-fr");
        result2["World"].Should().Be("World-fr");
        result2["Sun"].Should().Be("Sun-fr");
        requestCount.Should().Be(2);
        bodies[0].Should().BeEquivalentTo(["Hello", "World"], "[Architecture] the first batch must acquire both of its keys");
        bodies[1].Should().BeEquivalentTo(["Sun"], "[Architecture] the overlapping batch must acquire only its non-overlapping key");
    }

    /// <summary>
    /// Verifies that coalescing mixed batch serves cached joins in flight acquires rest.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_MixedBatch_ServesCachedJoinsInFlightAcquiresRestAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var bodies = new List<string[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            var texts = body.Select(static item => item.Text).Order().ToArray();
            bodies.Add(texts);
            requestCount++;
            if(texts.SequenceEqual(["World"])) {
                worldStarted.SetResult();
                await gate.Task;
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        var worldValueTask = provider.GetMessageTemplateAsync("World", _french, CancellationToken.None);
        await worldStarted.Task;
        var batchValueTask = provider.GetMessageTemplatesAsync(["Hello", "World", "Sun"], _french, TestContext.CancellationToken);

        gate.SetResult();
        var batch = await batchValueTask;

        (await worldValueTask).Should().Be("World-fr");
        batch["Hello"].Should().Be("Hello-fr");
        batch["World"].Should().Be("World-fr");
        batch["Sun"].Should().Be("Sun-fr");
        requestCount.Should().Be(3);
        bodies[0].Should().BeEquivalentTo(["Hello"], "[Architecture] the priming lookup must be the only acquisition of Hello");
        bodies[1].Should().BeEquivalentTo(["World"], "[Architecture] the batch must join the in-flight World acquisition, not re-request it");
        bodies[2].Should().BeEquivalentTo(["Sun"], "[Architecture] the batch must acquire only its uncached, un-in-flight key");
    }

    /// <summary>
    /// Verifies that coalescing refresh during batch acquisition does not repopulate cache.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_RefreshDuringBatchAcquisition_DoesNotRepopulateCacheAsync() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (_, _) => {
            if(requestCount == 0) {
                requestCount++;
                firstStarted.SetResult();
                await gate.Task;
                return TranslationResponse(("old-fr", "fr"));
            }

            requestCount++;
            return TranslationResponse(("new-fr", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var batchValueTask = provider.GetMessageTemplatesAsync(["Hello"], _french, TestContext.CancellationToken);
        await firstStarted.Task;
        await provider.RefreshAsync(TestContext.CancellationToken);

        gate.SetResult();
        var batch = await batchValueTask;
        batch["Hello"].Should().Be("old-fr");

        var fresh = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        fresh.Should().Be("new-fr");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that coalescing concurrent same key lookups single request.
    /// </summary>
    [TestMethod]
    public async Task Coalescing_ConcurrentSameKeyLookups_SingleRequestAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler(async (_, _) => {
            await Task.Delay(100, CancellationToken.None);
            requestCount++;
            return TranslationResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var firstValueTask = provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var secondValueTask = provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var first = await firstValueTask;
        var second = await secondValueTask;

        first.Should().Be("Bonjour le monde");
        second.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that non success response throws http request exception.
    /// </summary>
    [TestMethod]
    public async Task NonSuccessResponse_ThrowsHttpRequestExceptionAsync() {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None).AsTask());
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
    /// Verifies that cancellation is honored in batch lookup.
    /// </summary>
    [TestMethod]
    public async Task Cancellation_IsHonoredInBatchLookupAsync() {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(TranslationResponse(("Bonjour", "fr"))));

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GetMessageTemplatesAsync(["Hello"], _french, cts.Token).AsTask());
    }

    /// <summary>
    /// Verifies that multi key lookup mid operation cancellation propagates token to http and throws.
    /// </summary>
    [TestMethod]
    public async Task MultiKeyLookup_MidOperationCancellation_PropagatesTokenToHttpAndThrowsAsync() {
        var started = new TaskCompletionSource();
        CancellationToken observed = default;
        var handler = new MockHttpMessageHandler(async (_, cancellationToken) => {
            observed = cancellationToken;
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslationResponse(("unreachable", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        using var cancellation = new CancellationTokenSource();

        var lookup = provider.GetMessageTemplatesAsync(["Hello"], _french, cancellation.Token).AsTask();
        await started.Task;
        await cancellation.CancelAsync();

        async Task ActAsync() => await lookup;

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        observed.CanBeCanceled.Should().BeTrue("the caller token must reach the HTTP operation on the batch path");
        observed.IsCancellationRequested.Should().BeTrue("caller cancellation must reach the HTTP operation on the batch path");
    }

    /// <summary>
    /// Verifies that chunking many keys splits into batches of one is hundred.
    /// </summary>
    [TestMethod]
    public async Task Chunking_ManyKeys_SplitsIntoBatchesOfOneHundredAsync() {
        var requestCount = 0;
        var bodies = new List<TranslationRequestBody[]>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken: cancellationToken);
            Assert.IsNotNull(body);
            bodies.Add(body);
            requestCount++;
            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        var keys = Enumerable.Range(0, 150).Select(index => $"key{index}").ToArray();
        var result = await provider.GetMessageTemplatesAsync(keys, _french, TestContext.CancellationToken);

        requestCount.Should().Be(2);
        bodies[0].Should().HaveCount(100);
        bodies[1].Should().HaveCount(50);
        result["key0"].Should().Be("key0-fr");
        result["key149"].Should().Be("key149-fr");
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
    /// Verifies that canonical lookup owner cancellation does not cancel shared acquisition.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_OwnerCancellation_DoesNotCancelSharedAcquisitionAsync() {
        var requestCount = 0;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handler = new MockHttpMessageHandler(async (_, _) => {
            Interlocked.Increment(ref requestCount);
            started.TrySetResult();
            await release.Task;
            return TranslationResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        using var ownerCancellation = new CancellationTokenSource();
        async Task<string?> OwnerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, ownerCancellation.Token);
        var ownerTask = OwnerAsync();
        await started.Task;
        async Task<string?> JoinerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var joinerTask = JoinerAsync();
        await ownerCancellation.CancelAsync();

        async Task AwaitOwnerAsync() => await ownerTask;

        await ((Func<Task>)AwaitOwnerAsync).Should().ThrowAsync<OperationCanceledException>("the owner's cancellation cancels only the owner's wait");
        requestCount.Should().Be(1, "the shared acquisition must not be cancelled or duplicated by owner cancellation");

        release.TrySetResult();
        var joinerResult = await joinerTask;

        joinerResult.Should().Be("Bonjour le monde", "the detached shared acquisition continues to terminal completion for remaining callers");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that canonical lookup joiner cancellation does not cancel shared work.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_JoinerCancellation_DoesNotCancelSharedWorkAsync() {
        var requestCount = 0;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handler = new MockHttpMessageHandler(async (_, _) => {
            Interlocked.Increment(ref requestCount);
            started.TrySetResult();
            await release.Task;
            return TranslationResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        async Task<string?> OwnerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);
        var ownerTask = OwnerAsync();
        await started.Task;

        using var joinerCancellation = new CancellationTokenSource();
        async Task<string?> JoinerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, joinerCancellation.Token);
        var joinerTask = JoinerAsync();
        await joinerCancellation.CancelAsync();

        async Task AwaitJoinerAsync() => await joinerTask;

        await ((Func<Task>)AwaitJoinerAsync).Should().ThrowAsync<OperationCanceledException>("a joiner's cancellation cancels only its own wait");
        requestCount.Should().Be(1, "the joiner's cancellation must neither start nor cancel shared work");

        release.TrySetResult();
        var ownerResult = await ownerTask;

        ownerResult.Should().Be("Bonjour le monde");
        requestCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that canonical lookup all callers cancel detached acquisition completes and populates cache.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_AllCallersCancel_DetachedAcquisitionCompletesAndPopulatesCacheAsync() {
        var requestCount = 0;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var acquired = new TaskCompletionSource();
        var handler = new MockHttpMessageHandler(async (_, _) => {
            Interlocked.Increment(ref requestCount);
            started.TrySetResult();
            await release.Task;
            return TranslationResponse(("Bonjour le monde", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        provider.TranslationAcquired += (_, _) => acquired.TrySetResult();

        using var ownerCancellation = new CancellationTokenSource();
        using var joinerCancellation = new CancellationTokenSource();
        async Task<string?> OwnerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, ownerCancellation.Token);
        async Task<string?> JoinerAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, joinerCancellation.Token);
        var ownerTask = OwnerAsync();
        await started.Task;
        var joinerTask = JoinerAsync();
        await ownerCancellation.CancelAsync();
        await joinerCancellation.CancelAsync();

        async Task AwaitOwnerAsync() => await ownerTask;
        async Task AwaitJoinerAsync() => await joinerTask;
        await ((Func<Task>)AwaitOwnerAsync).Should().ThrowAsync<OperationCanceledException>();
        await ((Func<Task>)AwaitJoinerAsync).Should().ThrowAsync<OperationCanceledException>();

        release.TrySetResult();
        await acquired.Task;

        var cached = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        cached.Should().Be("Bonjour le monde", "the detached acquisition completes to terminal success even when every caller cancelled");
        requestCount.Should().Be(1, "the completed detached result is served from the cache without a new request");
    }

    /// <summary>
    /// Verifies that canonical lookup failed acquisition is removed and retryable.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_FailedAcquisition_IsRemovedAndRetryableAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            //: the default resilience retries transient faults, so the script exhausts all
            // three default attempts before the terminal failure surfaces.
            return Task.FromResult(attempt <= 3
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        async Task FirstLookupAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        await ((Func<Task>)FirstLookupAsync).Should().ThrowAsync<HttpRequestException>("the acquisition failure propagates to the caller");

        var retried = await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        retried.Should().Be("Bonjour le monde", "a failed acquisition is removed at terminal completion and is retryable");
        requestCount.Should().Be(4);
    }

    /// <summary>
    /// Verifies that canonical lookup cache is bounded oldest entry is evicted.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_CacheIsBounded_OldestEntryEvictedAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(TranslationResponse(("translated", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);

        for(var index = 0; index < 4100; index++) {
            await provider.GetMessageTemplateAsync($"key{index}", _french, CancellationToken.None);
        }

        requestCount.Should().Be(4100, "each distinct key is acquired once while filling the cache");

        await provider.GetMessageTemplateAsync("key4099", _french, CancellationToken.None);
        requestCount.Should().Be(4100, "the most recent entries remain cached");

        await provider.GetMessageTemplateAsync("key0", _french, CancellationToken.None);
        requestCount.Should().Be(4101, "the bounded cache evicts the oldest entry instead of growing without bound");
    }

    /// <summary>
    /// Verifies that canonical lookup cache entry expires reacquires after ttl.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_CacheEntryExpires_ReacquiresAfterTtlAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(TranslationResponse(($"value-{attempt}", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CacheExpiration: TimeSpan.FromMilliseconds(200));

        var first = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);
        var withinTtl = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        withinTtl.Should().Be(first, "within the TTL the cached entry is served");
        requestCount.Should().Be(1);

        await Task.Delay(350, TestContext.CancellationToken);

        var afterExpiry = await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None);

        afterExpiry.Should().Be("value-2", "after TTL expiry the entry is no longer current and the value is reacquired");
        requestCount.Should().Be(2);
    }

    /// <summary>
    /// Verifies that canonical lookup missing translation is negative cached within ttl only.
    /// </summary>
    [TestMethod]
    public async Task CanonicalLookup_MissingTranslation_IsNegativeCachedWithinTtlOnlyAsync() {
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(NullTranslationResponse());
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, CacheExpiration: TimeSpan.FromMilliseconds(200));

        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None)).Should().BeNull();
        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None)).Should().BeNull();
        requestCount.Should().Be(1, "a missing result may be cached only within its TTL");

        await Task.Delay(350, TestContext.CancellationToken);

        (await provider.GetMessageTemplateAsync("Hello", _french, CancellationToken.None)).Should().BeNull();
        requestCount.Should().Be(2, "after expiry the missing result is reacquired: no permanent negative caching");
    }

    /// <summary>
    /// Performs the null translation response operation.
    /// </summary>
    /// <returns>The null translation response result.</returns>
    private static HttpResponseMessage NullTranslationResponse() => new(HttpStatusCode.OK) {
        Content = JsonContent.Create(new[] {
            new { translations = new[] { new { text = (string?)null, to = (string?)null } } },
        }),
    };

    /// <summary>
    /// Performs the ok json operation.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <returns>The ok json result.</returns>
    private static HttpResponseMessage OkJson(string json) => new(HttpStatusCode.OK) {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="FromLanguage">The from language.</param>
    /// <param name="Region">The region.</param>
    /// <param name="CachingEnabled">The caching enabled.</param>
    /// <param name="CacheExpiration">The cache expiration.</param>
    /// <returns>The provider result.</returns>
    private static AzureTranslationProvider CreateProvider(
        HttpClient client,
        string? FromLanguage = null,
        string? Region = null,
        bool CachingEnabled = true,
        TimeSpan? CacheExpiration = null) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
            FromLanguage = FromLanguage ?? string.Empty,
            Region = Region ?? string.Empty,
            CachingEnabled = CachingEnabled,
            CacheExpiration = CacheExpiration ?? AzureTranslationOptions.DefaultCacheExpiration,
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
