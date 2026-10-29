//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// The construction-time snapshot of the resilience options consumed by
/// <see cref="ResiliencePipeline"/>. The Provider copies scalar values out of the caller-owned
/// options and the pipeline owns this snapshot.
/// </summary>
/// <param name="RequestTimeout">The per-chunk request budget.</param>
/// <param name="MaxAttempts">The maximum number of attempts per chunk.</param>
/// <param name="BaseDelay">The initial retry delay.</param>
/// <param name="MaxDelay">The retry-delay ceiling.</param>
/// <param name="BreakerThreshold">The consecutive-failure count that opens the breaker.</param>
/// <param name="BreakerOpenDuration">How long the breaker stays open before a half-open probe.</param>
/// <param name="RateLimitPerSecond">The sustained token-bucket refill rate; non-positive disables rate limiting.</param>
/// <param name="RateLimitBurst">The token-bucket capacity.</param>
internal readonly record struct ResilienceSettings(
    TimeSpan RequestTimeout,
    int MaxAttempts,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    int BreakerThreshold,
    TimeSpan BreakerOpenDuration,
    int RateLimitPerSecond,
    int RateLimitBurst);
