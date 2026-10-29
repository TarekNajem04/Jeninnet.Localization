//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Provider-owned translation resilience: a hand-rolled, BCL-only policy pipeline with no
/// external dependencies. The pipeline order, outermost to innermost, is
/// <c>Timeout → Retry → Breaker → RateLimit → HTTP</c>: one total per-chunk budget bounds every
/// attempt, backoff delay, and rate-limiter wait; transient attempts are retried; the breaker
/// fails fast while open; the token bucket paces each HTTP attempt.
/// </summary>
/// <remarks>
/// <para>
/// Dependency decision: hand-rolled instead of Polly or
/// <c>Microsoft.Extensions.Http.Resilience</c>. A third-party policy library is unjustified for
/// four small policies with a fixed order and a fixed fault classification; the handler-chain
/// model is invisible to direct provider construction and cannot express per-provider snapshotted
/// options; plain C# state machines keep the  trim surface minimal; and a
/// <see cref="TimeProvider"/> seam keeps every policy deterministic under test without real
/// sleeps.
/// </para>
/// <para>
/// Only idempotent-safe transient faults are retried: network failures
/// (<see cref="HttpRequestException"/>), per-chunk timeouts, and HTTP 408/429/5xx. Translation
/// is a read-only operation, so resending a chunk is side-effect free. Client errors, malformed
/// responses, and caller cancellation are never retried and never count toward the breaker.
/// A timeout spends the chunk budget, so it is terminal and surfaces as
/// <see cref="OperationCanceledException"/> — it is never retried and never hangs.
/// </para>
/// <para>
/// Breaker and bucket state is per pipeline instance (one per Provider singleton) and shared by
/// every key, culture, and lookup path. A rejected acquisition throws
/// <see cref="BrokenCircuitException"/> without an HTTP request and is reported through the
/// existing <see cref="IProviderEventSource.AcquisitionFailed"/> event like any other failure.
/// </para>
/// </remarks>
internal sealed class ResiliencePipeline {
    private const double JITTER_RATIO = 0.25;

    private readonly TimeSpan _requestTimeout;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly int _breakerThreshold;
    private readonly TimeSpan _breakerOpenDuration;
    private readonly int _rateLimitPerSecond;
    private readonly int _rateLimitBurst;
    private readonly TimeProvider _timeProvider;
#if NET9_0_OR_GREATER

    private readonly Lock _sync = new();
#else

    private readonly object _sync = new();
#endif
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private bool _halfOpenProbeInFlight;
    private double _availableTokens;
    private DateTimeOffset _lastRefill;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResiliencePipeline"/> class.
    /// </summary>
    /// <param name="settings">The snapshotted resilience settings.</param>
    /// <param name="timeProvider">The clock backing timeouts, backoffs, breaker timing, and token refills.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timeProvider"/> is null.</exception>
    internal ResiliencePipeline(ResilienceSettings settings, TimeProvider timeProvider) {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _requestTimeout = settings.RequestTimeout;
        _maxAttempts = settings.MaxAttempts;
        _baseDelay = settings.BaseDelay;
        _maxDelay = settings.MaxDelay;
        _breakerThreshold = settings.BreakerThreshold;
        _breakerOpenDuration = settings.BreakerOpenDuration;
        _rateLimitPerSecond = settings.RateLimitPerSecond;
        _rateLimitBurst = settings.RateLimitBurst;
        _timeProvider = timeProvider;
        _availableTokens = settings.RateLimitBurst;
        _lastRefill = timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Executes one chunk acquisition through the resilience pipeline.
    /// </summary>
    /// <param name="sendAsync">The HTTP exchange for one attempt; receives the per-chunk budget token.</param>
    /// <param name="callerToken">The caller's token: cancellation here is never retried, never recorded, and never raised as a failure event.</param>
    /// <returns>The successful or terminally-failed response. Terminal client errors are returned for the caller to report; retryable faults are retried or thrown.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sendAsync"/> is null.</exception>
    /// <exception cref="BrokenCircuitException">Thrown when the breaker is open and the call is not the single half-open probe.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the caller cancels or the per-chunk budget expires.</exception>
    /// <exception cref="HttpRequestException">Thrown when a retryable status persists across every attempt.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the retry loop exits without producing a response.</exception>
    internal async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken callerToken) {
        ArgumentNullException.ThrowIfNull(sendAsync);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        await using var timeoutTimer = _timeProvider.CreateTimer(
            static state => ((CancellationTokenSource)state!).Cancel(),
            timeoutCts,
            _requestTimeout,
            Timeout.InfiniteTimeSpan);
        var timeoutToken = timeoutCts.Token;

        // A half-open probe is a single trial call: it never retries, so recovery probing cannot
        // amplify load on a still-struggling service.
        var admittedAsProbe = EnterBreaker();
        var maxAttempts = MaxAttemptsFor(admittedAsProbe);
        try {
            for(var attempt = 1; attempt <= maxAttempts; attempt++) {
                callerToken.ThrowIfCancellationRequested();

                await AcquireRateLimitTokenAsync(timeoutToken).ConfigureAwait(false);

                HttpResponseMessage response;
                try {
                    response = await sendAsync(timeoutToken).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(callerToken.IsCancellationRequested) {
                    throw;
                }
                catch(OperationCanceledException) {
                    // The per-chunk budget fired while the caller's token is still live: a
                    // transient timeout. The budget is spent, so the timeout is terminal — it is
                    // never retried — and surfaces as a cancellation, never a hang.
                    RecordBreakerFailure();
                    throw;
                }
                catch(HttpRequestException) {
                    RecordBreakerFailure();
                    if(attempt < maxAttempts) {
                        await DelayBeforeRetryAsync(attempt, timeoutToken).ConfigureAwait(false);
                        continue;
                    }

                    throw;
                }

                var classification = ClassifyResponse(response);
                if(classification.Return) {
                    return classification.Response!;
                }

                RecordBreakerFailure();
                if(attempt >= maxAttempts) {
                    throw classification.Failure!;
                }

                await DelayBeforeRetryAsync(attempt, timeoutToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("The resilience retry loop exited without producing a response.");
        }
        finally {
            AbandonProbeIfNeeded(admittedAsProbe);
        }
    }

    /// <summary>
    /// Gets the attempt budget: a half-open probe is a single trial call, every other call
    /// carries the configured maximum.
    /// </summary>
    /// <param name="admittedAsProbe">Whether this call was admitted as the half-open probe.</param>
    /// <returns>The attempt budget.</returns>
    private int MaxAttemptsFor(bool admittedAsProbe) => admittedAsProbe ? 1 : _maxAttempts;

    /// <summary>
    /// Abandons the half-open probe when this call was admitted as the probe.
    /// </summary>
    /// <param name="admittedAsProbe">Whether this call was admitted as the half-open probe.</param>
    private void AbandonProbeIfNeeded(bool admittedAsProbe) {
        if(admittedAsProbe) {
            AbandonBreakerProbe();
        }
    }

    /// <summary>
    /// Classifies one received response: success records the breaker success and returns the
    /// response, a terminal client error is returned for the caller to report, and a retryable
    /// status is disposed and yields its failure for the retry decision.
    /// </summary>
    /// <param name="response">The received response.</param>
    /// <returns>The classification driving the loop.</returns>
    private ResponseClassification ClassifyResponse(HttpResponseMessage response) {
        if(response.IsSuccessStatusCode) {
            RecordBreakerSuccess();
            return new ResponseClassification(response, null);
        }

        if(!IsRetryableStatus(response.StatusCode)) {
            return new ResponseClassification(response, null);
        }

        var failure = FailureFor(response);
        response.Dispose();
        return new ResponseClassification(null, failure);
    }

    /// <summary>
    /// The classification of one received response: either the response to return or the
    /// failure that drives the retry decision.
    /// </summary>
    /// <param name="Response">The response to return, or <see langword="null"/> when the status is retryable.</param>
    /// <param name="Failure">The retryable failure, or <see langword="null"/> when a response is returned.</param>
    private readonly record struct ResponseClassification(HttpResponseMessage? Response, HttpRequestException? Failure) {
        /// <summary>Gets a value indicating whether the response must be returned to the caller.</summary>
        internal bool Return => Response is not null;
    }

    /// <summary>
    /// Checks the breaker and admits the call, either normally (closed) or as the single
    /// half-open probe (open duration elapsed and no probe in flight).
    /// </summary>
    /// <returns><see langword="true"/> when admitted as the half-open probe; otherwise <see langword="false"/>.</returns>
    /// <exception cref="BrokenCircuitException">Thrown when the breaker is open.</exception>
    private bool EnterBreaker() {
        lock(_sync) {
            if(_consecutiveFailures < _breakerThreshold) {
                return false;
            }

            if(_timeProvider.GetUtcNow() - _openedAt >= _breakerOpenDuration) {
                if(_halfOpenProbeInFlight) {
                    throw new BrokenCircuitException(
                        "The Azure translation circuit breaker is half-open and its single probe is already in flight; failing fast without an HTTP request.");
                }

                _halfOpenProbeInFlight = true;
                return true;
            }

            throw new BrokenCircuitException(
                "The Azure translation circuit breaker is open after consecutive transient failures; failing fast without an HTTP request.");
        }
    }

    /// <summary>
    /// Records breaker failure.
    /// </summary>
    private void RecordBreakerFailure() {
        lock(_sync) {
            if(_halfOpenProbeInFlight) {
                // A failed probe re-opens the breaker for another full open duration. The
                // consecutive-failure count stays at or above the threshold.
                _halfOpenProbeInFlight = false;
                _openedAt = _timeProvider.GetUtcNow();
                return;
            }

            _consecutiveFailures++;
            if(_consecutiveFailures >= _breakerThreshold) {
                _openedAt = _timeProvider.GetUtcNow();
            }
        }
    }

    /// <summary>
    /// Records breaker success.
    /// </summary>
    private void RecordBreakerSuccess() {
        lock(_sync) {
            _consecutiveFailures = 0;
            _halfOpenProbeInFlight = false;
        }
    }

    /// <summary>
    /// Releases a half-open probe that never reached a recorded outcome (caller cancellation or
    /// an unexpected error), so a stuck probe can never wedge the breaker half-open forever.
    /// Harmless when no probe is in flight.
    /// </summary>
    private void AbandonBreakerProbe() {
        lock(_sync) {
            _halfOpenProbeInFlight = false;
        }
    }

    /// <summary>
    /// Acquires one token-bucket token, waiting (observing the per-chunk budget) when the bucket
    /// is empty. Disabled entirely when the configured rate is not positive.
    /// </summary>
    /// <param name="timeoutToken">The per-chunk budget token.</param>
    private async Task AcquireRateLimitTokenAsync(CancellationToken timeoutToken) {
        if(_rateLimitPerSecond <= 0) {
            return;
        }

        while(true) {
            TimeSpan wait;
            lock(_sync) {
                RefillBucket(_timeProvider.GetUtcNow());
                if(_availableTokens >= 1.0) {
                    _availableTokens--;
                    return;
                }

                wait = TimeSpan.FromSeconds((1.0 - _availableTokens) / _rateLimitPerSecond);
            }

            await Task.Delay(wait, _timeProvider, timeoutToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs the refill bucket operation.
    /// </summary>
    /// <param name="now">The now.</param>
    private void RefillBucket(DateTimeOffset now) {
        if(now <= _lastRefill) {
            _lastRefill = now;
            return;
        }

        _availableTokens = Math.Min(
            _rateLimitBurst,
            _availableTokens + ((now - _lastRefill).TotalSeconds * _rateLimitPerSecond));
        _lastRefill = now;
    }

    /// <summary>
    /// Performs the delay before retry operation.
    /// </summary>
    /// <param name="failedAttempt">The failed attempt.</param>
    /// <param name="timeoutToken">The timeout token.</param>
    private Task DelayBeforeRetryAsync(int failedAttempt, CancellationToken timeoutToken) =>
        Task.Delay(BackoffForAttempt(failedAttempt), _timeProvider, timeoutToken);

    /// <summary>
    /// Computes the backoff before the next attempt: exponential growth from the base delay,
    /// capped at the maximum delay, plus up to 25% random jitter.
    /// </summary>
    /// <param name="failedAttempt">The 1-based number of the attempt that just failed.</param>
    /// <returns>The backoff for attempt result.</returns>
    private TimeSpan BackoffForAttempt(int failedAttempt) {
        var delay = _baseDelay;
        for(var attempt = 1; attempt < failedAttempt; attempt++) {
            if(delay >= _maxDelay) {
                break;
            }

            delay = delay.Ticks > _maxDelay.Ticks / 2
                ? _maxDelay
                : TimeSpan.FromTicks(delay.Ticks * 2);
        }

        if(delay > _maxDelay) {
            delay = _maxDelay;
        }

        return delay + TimeSpan.FromTicks((long)(delay.Ticks * JITTER_RATIO * Random.Shared.NextDouble()));
    }

    /// <summary>
    /// Determines whether the value is retryable status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsRetryableStatus(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    /// <summary>
    /// Performs the failure for operation.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The failure for result.</returns>
    private static HttpRequestException FailureFor(HttpResponseMessage response) =>
        new($"Azure Translator returned {(int)response.StatusCode} {response.ReasonPhrase}.");
}
