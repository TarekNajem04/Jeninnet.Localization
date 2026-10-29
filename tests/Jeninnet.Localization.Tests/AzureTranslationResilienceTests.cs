//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the azure translation resilience tests.
/// </summary>
[TestClass]
public class AzureTranslationResilienceTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");

    // ─── Retry ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that retry flaky transport converges without caller retries.
    /// </summary>
    [TestMethod]
    public async Task Retry_FlakyTransport_ConvergesWithoutCallerRetriesAsync() {
        var clock = new ManualTimeProvider();
        var hits = new List<DateTimeOffset>();
        var requestCount = 0;
        var failed = 0;
        var acquired = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            lock(hits) {
                hits.Add(clock.GetUtcNow());
            }

            return Task.FromResult(attempt <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock);
        provider.AcquisitionFailed += (_, _) => failed++;
        provider.TranslationAcquired += (_, _) => acquired++;

        var lookup = provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None).AsTask();
        await PumpUntilAsync(lookup, clock);

        (await lookup).Should().Be("Bonjour le monde");
        requestCount.Should().Be(3, "scripted 503s converge inside the provider without caller retries");
        acquired.Should().Be(1, "a converged acquisition raises exactly one event");
        failed.Should().Be(0);

        List<DateTimeOffset> ordered;
        lock(hits) {
            ordered = [.. hits];
        }

        var firstGap = ordered[1] - ordered[0];
        var secondGap = ordered[2] - ordered[1];
        (secondGap >= firstGap).Should().BeTrue(
            $"backoff grows between attempts (gaps {firstGap} then {secondGap})");
    }

    /// <summary>
    /// Verifies that retry non transient status fails fast without retry.
    /// </summary>
    [TestMethod]
    public async Task Retry_NonTransientStatus_FailsFastWithoutRetryAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock);

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<HttpRequestException>();
        requestCount.Should().Be(1, "client errors are not idempotent-safe retries: they fail fast");
    }

    /// <summary>
    /// Verifies that retry disabled surfaces first failure immediately.
    /// </summary>
    [TestMethod]
    public async Task Retry_Disabled_SurfacesFirstFailureImmediatelyAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => options.RetryMaxAttempts = 1);

        async Task ActAsync() => await provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None);

        await ((Func<Task>)ActAsync).Should().ThrowAsync<HttpRequestException>();
        requestCount.Should().Be(1, "a single attempt means no retry");
    }

    // ─── Circuit breaker ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that breaker sustained outage opens and recovers via half open probe.
    /// </summary>
    [TestMethod]
    public async Task Breaker_SustainedOutage_OpensAndRecoversViaHalfOpenProbeAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var failures = new List<Exception>();
        var handler = new MockHttpMessageHandler((_, _) => {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RetryMaxAttempts = 1;
            options.BreakerFailureThreshold = 2;
            options.BreakerOpenDuration = TimeSpan.FromSeconds(5);
        });
        provider.AcquisitionFailed += (_, e) => failures.Add(e.Exception);

        // Two consecutive transient failures trip the breaker (no retries configured).
        async Task FailAAsync() => await provider.GetMessageTemplateAsync("outage-a", _french, CancellationToken.None);
        async Task FailBAsync() => await provider.GetMessageTemplateAsync("outage-b", _french, CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(FailAAsync);
        await Assert.ThrowsAsync<HttpRequestException>(FailBAsync);
        requestCount.Should().Be(2);

        // While open, acquisitions fail fast without an HTTP request.
        async Task RejectedAsync() => await provider.GetMessageTemplateAsync("outage-c", _french, CancellationToken.None);
        await ((Func<Task>)RejectedAsync).Should().ThrowAsync<BrokenCircuitException>(
            "a sustained outage opens the breaker");
        requestCount.Should().Be(2, "open-breaker rejections perform zero HTTP requests");

        // After the open duration, exactly one half-open probe is admitted; its failure re-opens.
        clock.Advance(TimeSpan.FromSeconds(6));
        async Task ProbeAsync() => await provider.GetMessageTemplateAsync("outage-d", _french, CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(ProbeAsync);
        requestCount.Should().Be(3, "the half-open probe is counted as one HTTP request");

        async Task RejectedAgainAsync() => await provider.GetMessageTemplateAsync("outage-e", _french, CancellationToken.None);
        await ((Func<Task>)RejectedAgainAsync).Should().ThrowAsync<BrokenCircuitException>();
        requestCount.Should().Be(3);

        failures.Should().HaveCount(5);
        failures[2].Should().BeOfType<BrokenCircuitException>(
            "breaker state is observable through the existing failure event");
        failures[4].Should().BeOfType<BrokenCircuitException>();
    }

    /// <summary>
    /// Verifies that breaker successful probe closes and resumes normal flow.
    /// </summary>
    [TestMethod]
    public async Task Breaker_SuccessfulProbe_ClosesAndResumesNormalFlowAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(attempt <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TranslationResponse(("Bonjour le monde", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RetryMaxAttempts = 1;
            options.BreakerFailureThreshold = 2;
            options.BreakerOpenDuration = TimeSpan.FromSeconds(5);
        });

        async Task FailAAsync() => await provider.GetMessageTemplateAsync("recover-a", _french, CancellationToken.None);
        async Task FailBAsync() => await provider.GetMessageTemplateAsync("recover-b", _french, CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(FailAAsync);
        await Assert.ThrowsAsync<HttpRequestException>(FailBAsync);

        async Task RejectedAsync() => await provider.GetMessageTemplateAsync("recover-c", _french, CancellationToken.None);
        await ((Func<Task>)RejectedAsync).Should().ThrowAsync<BrokenCircuitException>();
        requestCount.Should().Be(2);

        clock.Advance(TimeSpan.FromSeconds(6));
        var probed = await provider.GetMessageTemplateAsync("recover-d", _french, CancellationToken.None);
        probed.Should().Be("Bonjour le monde", "a successful half-open probe closes the breaker");
        requestCount.Should().Be(3);

        var resumed = await provider.GetMessageTemplateAsync("recover-e", _french, CancellationToken.None);
        resumed.Should().Be("Bonjour le monde");
        requestCount.Should().Be(4, "normal flow resumes after the probe closes the breaker");
    }

    /// <summary>
    /// Verifies that breaker client errors do not trip breaker.
    /// </summary>
    [TestMethod]
    public async Task Breaker_ClientErrors_DoNotTripBreakerAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RetryMaxAttempts = 1;
            options.BreakerFailureThreshold = 2;
        });

        for(var index = 0; index < 3; index++) {
            async Task ActAsync() => await provider.GetMessageTemplateAsync($"client-{index}", _french, CancellationToken.None);
            await ((Func<Task>)ActAsync).Should().ThrowAsync<HttpRequestException>();
        }

        requestCount.Should().Be(3, "client errors fail fast every time and never trip the breaker");
    }

    /// <summary>
    /// Verifies that breaker success resets consecutive failure count.
    /// </summary>
    [TestMethod]
    public async Task Breaker_SuccessResetsConsecutiveFailureCountAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            return Task.FromResult(attempt is 1 or 3 or 4
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TranslationResponse(("ok", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RetryMaxAttempts = 1;
            options.BreakerFailureThreshold = 2;
        });

        async Task FailAAsync() => await provider.GetMessageTemplateAsync("reset-a", _french, CancellationToken.None);
        async Task FailCAsync() => await provider.GetMessageTemplateAsync("reset-c", _french, CancellationToken.None);
        async Task FailDAsync() => await provider.GetMessageTemplateAsync("reset-d", _french, CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(FailAAsync);
        (await provider.GetMessageTemplateAsync("reset-b", _french, CancellationToken.None)).Should().Be("ok");
        await Assert.ThrowsAsync<HttpRequestException>(FailCAsync);
        await Assert.ThrowsAsync<HttpRequestException>(FailDAsync);

        async Task RejectedAsync() => await provider.GetMessageTemplateAsync("reset-e", _french, CancellationToken.None);
        await ((Func<Task>)RejectedAsync).Should().ThrowAsync<BrokenCircuitException>(
            "only consecutive failures trip the breaker: fail, success, fail, fail");
        requestCount.Should().Be(4);
    }

    /// <summary>
    /// Verifies that breaker half open admits single probe.
    /// </summary>
    [TestMethod]
    public async Task Breaker_HalfOpenAdmitsSingleProbeAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockHttpMessageHandler(async (_, _) => {
            var attempt = Interlocked.Increment(ref requestCount);
            if(attempt == 2) {
                probeStarted.SetResult();
                await releaseProbe.Task;
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RetryMaxAttempts = 1;
            options.BreakerFailureThreshold = 1;
            options.BreakerOpenDuration = TimeSpan.FromSeconds(5);
        });

        async Task FailAAsync() => await provider.GetMessageTemplateAsync("probe-a", _french, CancellationToken.None);
        await Assert.ThrowsAsync<HttpRequestException>(FailAAsync);

        clock.Advance(TimeSpan.FromSeconds(6));
        var probe = provider.GetMessageTemplateAsync("probe-b", _french, CancellationToken.None).AsTask();
        await probeStarted.Task;

        async Task RejectedAsync() => await provider.GetMessageTemplateAsync("probe-c", _french, CancellationToken.None);
        await ((Func<Task>)RejectedAsync).Should().ThrowAsync<BrokenCircuitException>(
            "a second call during a half-open probe fails fast instead of joining it");
        requestCount.Should().Be(2);

        releaseProbe.SetResult();
        await Assert.ThrowsAsync<HttpRequestException>(() => probe);
        requestCount.Should().Be(2);
    }

    // ─── Rate limiting ──────────────────────────────────────────────────

    /// <summary>
    /// Verifies that rate limiter burst throughput is capped.
    /// </summary>
    [TestMethod]
    public async Task RateLimiter_BurstThroughputIsCappedAsync() {
        var clock = new ManualTimeProvider();
        var start = clock.GetUtcNow();
        var hits = new List<DateTimeOffset>();
        var handler = new MockHttpMessageHandler(async (request, cancellationToken) => {
            var body = await request.Content!.ReadFromJsonAsync<TranslationRequestBody[]>(cancellationToken);
            Assert.IsNotNull(body);
            lock(hits) {
                hits.Add(clock.GetUtcNow());
            }

            return TranslationResponse([.. body.Select(static item => (item.Text + "-fr", "fr"))]);
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => {
            options.RateLimitRequestsPerSecond = 2;
            options.RateLimitBurstSize = 1;
        });

        var keys = Enumerable.Range(0, 5).Select(index => $"burst-{index}").ToArray();
        var lookups = keys.Select(key => provider.GetMessageTemplateAsync(key, _french, CancellationToken.None).AsTask()).ToArray();
        await PumpAllUntilAsync(lookups, clock);

        List<DateTimeOffset> ordered;
        lock(hits) {
            ordered = [.. hits.Order()];
        }

        ordered.Should().HaveCount(5);
        ordered[0].Should().Be(start, "the first burst token is available immediately");
        for(var index = 1; index < ordered.Count; index++) {
            var gap = ordered[index] - ordered[index - 1];
            (gap >= TimeSpan.FromMilliseconds(400)).Should().BeTrue(
                $"token-bucket pacing caps throughput under burst (gap {index} was {gap})");
        }

        var span = ordered[^1] - ordered[0];
        (span >= TimeSpan.FromMilliseconds(1500)).Should().BeTrue(
            $"five acquisitions at two per second span at least 1.5 seconds (was {span})");

        foreach(var lookup in lookups) {
            (await lookup).Should().Contain("-fr");
        }
    }

    /// <summary>
    /// Verifies that rate limiter disabled by default preserves throughput.
    /// </summary>
    [TestMethod]
    public async Task RateLimiter_DisabledByDefault_PreservesThroughputAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((_, _) => {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(TranslationResponse(("Bonjour", "fr")));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock);

        var lookups = Enumerable.Range(0, 5)
            .Select(index => provider.GetMessageTemplateAsync($"free-{index}", _french, CancellationToken.None).AsTask())
            .ToArray();
        await Task.WhenAll(lookups).WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

        requestCount.Should().Be(5, "the safe default (disabled rate limiting) preserves existing throughput with zero clock advances");
    }

    // ─── Timeout ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that timeout stalled call surfaces cancellation without hang.
    /// </summary>
    [TestMethod]
    public async Task Timeout_StalledCall_SurfacesCancellationWithoutHangAsync() {
        var clock = new ManualTimeProvider();
        var requestCount = 0;
        var failed = new List<Exception>();
        var acquired = 0;
        var handler = new MockHttpMessageHandler(async (_, cancellationToken) => {
            Interlocked.Increment(ref requestCount);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslationResponse(("unreachable", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock, static options => options.RequestTimeout = TimeSpan.FromSeconds(5));
        provider.AcquisitionFailed += (_, e) => failed.Add(e.Exception);
        provider.TranslationAcquired += (_, _) => acquired++;

        var lookup = provider.GetMessageTemplateAsync("Hello world", _french, CancellationToken.None).AsTask();
        await PumpUntilAsync(lookup, clock, expectFault: true);

        await ((Func<Task>)(async () => await lookup)).Should().ThrowAsync<OperationCanceledException>(
            "timeouts always surface as cancellations, never hangs");
        requestCount.Should().Be(1, "a spent budget is terminal: timeouts are never retried");
        failed.Should().ContainSingle();
        failed[0].Should().BeOfType<TaskCanceledException>(
            "the timeout is observable through the existing failure event");
        acquired.Should().Be(0);
    }

    /// <summary>
    /// Verifies that timeout caller cancellation still wins and raises no event.
    /// </summary>
    [TestMethod]
    public async Task Timeout_CallerCancellation_StillWinsAndRaisesNoEventAsync() {
        var clock = new ManualTimeProvider();
        var failed = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new MockHttpMessageHandler(async (_, cancellationToken) => {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TranslationResponse(("unreachable", "fr"));
        });

        using var client = new HttpClient(handler);
        var provider = CreateProvider(client, clock);
        provider.AcquisitionFailed += (_, _) => failed++;
        using var cancellation = new CancellationTokenSource();

        var lookup = provider.GetMessageTemplateAsync("Hello world", _french, cancellation.Token).AsTask();
        await started.Task;
        await cancellation.CancelAsync();

        async Task ActAsync() => await lookup;

        await ((Func<Task>)ActAsync).Should().ThrowAsync<OperationCanceledException>();
        failed.Should().Be(0, "a genuine caller cancellation raises neither event, even with resilience enabled");
    }

    // ─── Options validation ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that resilience options invalid values rejected at construction.
    /// </summary>
    [TestMethod]
    public void ResilienceOptions_InvalidValues_RejectedAtConstruction() {
        using var client = new HttpClient(new StubHandler());

        var timeout = () => CreateProvider(client, TimeProvider.System, static options => options.RequestTimeout = TimeSpan.Zero);
        timeout.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RequestTimeout");

        var attempts = () => CreateProvider(client, TimeProvider.System, static options => options.RetryMaxAttempts = 0);
        attempts.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RetryMaxAttempts");

        var baseDelay = () => CreateProvider(client, TimeProvider.System, static options => options.RetryBaseDelay = TimeSpan.FromTicks(-1));
        baseDelay.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RetryBaseDelay");

        var maxDelay = () => CreateProvider(client, TimeProvider.System, static options => {
            options.RetryBaseDelay = TimeSpan.FromSeconds(2);
            options.RetryMaxDelay = TimeSpan.FromSeconds(1);
        });
        maxDelay.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RetryMaxDelay");

        var threshold = () => CreateProvider(client, TimeProvider.System, static options => options.BreakerFailureThreshold = 0);
        threshold.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("BreakerFailureThreshold");

        var openDuration = () => CreateProvider(client, TimeProvider.System, static options => options.BreakerOpenDuration = TimeSpan.Zero);
        openDuration.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("BreakerOpenDuration");

        var rate = () => CreateProvider(client, TimeProvider.System, static options => options.RateLimitRequestsPerSecond = -1);
        rate.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RateLimitRequestsPerSecond");

        var burst = () => CreateProvider(client, TimeProvider.System, static options => options.RateLimitBurstSize = 0);
        burst.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("RateLimitBurstSize");
    }

    /// <summary>
    /// Verifies that resilience options boundary values accepted at construction.
    /// </summary>
    [TestMethod]
    public void ResilienceOptions_BoundaryValues_AcceptedAtConstruction() {
        using var client = new HttpClient(new StubHandler());

        var provider = CreateProvider(client, TimeProvider.System, static options => {
            options.RetryMaxAttempts = 1;
            options.RetryBaseDelay = TimeSpan.Zero;
            options.RateLimitRequestsPerSecond = 0;
            options.RateLimitBurstSize = 1;
        });

        provider.Should().NotBeNull();
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="configure">The delegate that configures the options.</param>
    /// <returns>The provider result.</returns>
    private static AzureTranslationProvider CreateProvider(
        HttpClient client,
        TimeProvider timeProvider,
        Action<AzureTranslationOptions>? configure = null) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
        };
        configure?.Invoke(options);

        return new AzureTranslationProvider(Options.Create(options), client, timeProvider);
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
    /// Pumps the fake clock until the lookup task completes. All policy timing (timeouts,
    /// backoffs, breaker timing, token refills) runs on the fake clock; the one-millisecond real
    /// scheduling quantum per iteration only lets queued continuations run and carries no timing
    /// assertions. Fails the test (instead of hanging CI) when the pump does not converge.
    /// </summary>
    /// <typeparam name="T">The pumped task's result type.</typeparam>
    /// <param name="task">The task to pump.</param>
    /// <param name="clock">The fake clock driving the pumped operation.</param>
    /// <param name="expectFault">Whether the task is expected to fault instead of completing.</param>
    /// <returns>The task result, or <see langword="default"/> for an expected fault.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pump does not converge within the iteration budget.</exception>
    private static async Task<T> PumpUntilAsync<T>(Task<T> task, ManualTimeProvider clock, bool expectFault = false) {
        for(var index = 0; index < 1000 && !task.IsCompleted; index++) {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1, CancellationToken.None);
        }

        if(!task.IsCompleted) {
            throw new InvalidOperationException("The fake-clock pump did not converge; the acquisition is hanging.");
        }

        return expectFault ? default! : await task;
    }

    /// <summary>
    /// Performs the pump all until operation.
    /// </summary>
    /// <param name="tasks">The tasks.</param>
    /// <param name="clock">The clock.</param>
    private static async Task PumpAllUntilAsync(Task<string?>[] tasks, ManualTimeProvider clock) {
        for(var index = 0; index < 1000 && tasks.Any(static task => !task.IsCompleted); index++) {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1, CancellationToken.None);
        }

        if(tasks.Any(static task => !task.IsCompleted)) {
            throw new InvalidOperationException("The fake-clock pump did not converge; an acquisition is hanging.");
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
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
    /// A manually advanced <see cref="TimeProvider"/> backing deterministic resilience tests:
    /// timeouts, backoffs, breaker timing, and token refills all run on this clock, so no test
    /// performs real waiting.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider {
        private readonly object _sync = new();
        private DateTimeOffset _now = new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        private readonly List<Entry> _timers = [];

        /// <summary>
        /// Performs the advance operation.
        /// </summary>
        /// <param name="delta">The delta.</param>
        public void Advance(TimeSpan delta) {
            List<(TimerCallback Callback, object? State)> due;
            lock(_sync) {
                _now += delta;
                due = [];
                foreach(var entry in _timers) {
                    if(entry._disposed || entry._due > _now) {
                        continue;
                    }

                    if(entry._period == Timeout.InfiniteTimeSpan) {
                        entry._disposed = true;
                    } else {
                        entry._due += entry._period;
                    }

                    due.Add((entry._callback, entry._state));
                }

                _timers.RemoveAll(static entry => entry._disposed);
            }

            foreach(var (callback, state) in due) {
                callback(state);
            }
        }

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() {
            lock(_sync) {
                return _now;
            }
        }

        /// <inheritdoc/>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            ArgumentNullException.ThrowIfNull(callback);
            var entry = new Entry(this, callback, state, dueTime, period);
            lock(_sync) {
                _timers.Add(entry);
            }

            return entry;
        }

        /// <summary>
        /// Represents an entry.
        /// </summary>
        private sealed class Entry : ITimer {
            private readonly ManualTimeProvider _owner;

            /// <summary>
            /// All fields are guarded by the owner's lock; Advance reads them while holding it.
            /// </summary>
            internal readonly TimerCallback _callback;
            internal readonly object? _state;
            internal TimeSpan _period;
            internal DateTimeOffset _due;
            internal bool _disposed;

            /// <summary>
            /// Initializes a new instance of the Entry class.
            /// </summary>
            /// <param name="owner">The owner.</param>
            /// <param name="callback">The callback.</param>
            /// <param name="state">The state.</param>
            /// <param name="dueTime">The due time.</param>
            /// <param name="period">The period.</param>
            internal Entry(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period) {
                _owner = owner;
                _callback = callback;
                _state = state;
                _period = period;
                _due = NormalizeDue(dueTime, owner.GetUtcNow());
            }

            /// <summary>
            /// Performs the change operation.
            /// </summary>
            /// <param name="dueTime">The due time.</param>
            /// <param name="period">The period.</param>
            /// <returns>true when the condition holds; otherwise, false.</returns>
            public bool Change(TimeSpan dueTime, TimeSpan period) {
                lock(_owner._sync) {
                    if(_disposed) {
                        return false;
                    }

                    _period = period;
                    _due = NormalizeDue(dueTime, _owner._now);
                    return true;
                }
            }

            /// <summary>
            /// Releases the resources used by the current instance.
            /// </summary>
            public void Dispose() {
                lock(_owner._sync) {
                    _disposed = true;
                }
            }

            /// <summary>
            /// Performs the dispose operation.
            /// </summary>
            public ValueTask DisposeAsync() {
                Dispose();
                return ValueTask.CompletedTask;
            }

            /// <summary>
            /// Normalizes due.
            /// </summary>
            /// <param name="dueTime">The due time.</param>
            /// <param name="now">The now.</param>
            /// <returns>The normalize due result.</returns>
            private static DateTimeOffset NormalizeDue(TimeSpan dueTime, DateTimeOffset now) {
                if(dueTime == Timeout.InfiniteTimeSpan) {
                    return DateTimeOffset.MaxValue;
                }

                return dueTime < TimeSpan.Zero ? now : now + dueTime;
            }
        }
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
