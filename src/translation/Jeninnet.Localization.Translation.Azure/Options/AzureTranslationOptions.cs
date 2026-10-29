//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure.Options;

/// <summary>
/// Configuration options for the Azure Translator API.
/// </summary>
public class AzureTranslationOptions {
    private const string DEFAULT_ENDPOINT = "https://api.cognitive.microsofttranslator.com";

    /// <summary>
    /// The default time a resolved message stays in the Provider's in-memory cache.
    /// </summary>
    public static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the API key for Azure Translator.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public string ApiKey {
        get;
        set {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets the region associated with the API key. Optional; sent as the
    /// <c>Ocp-Apim-Subscription-Region</c> header only when non-blank.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base API endpoint for Azure Translator. Defaults to the global endpoint.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or not an absolute http/https URI.</exception>
    public string Endpoint {
        get;
        set {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if(!IsValidAbsoluteHttpUri(value)) {
                throw new ArgumentException(
                    "Endpoint must be a valid absolute URI using the http or https scheme.",
                    nameof(value));
            }

            field = value;
        }
    } = DEFAULT_ENDPOINT;

    /// <summary>
    /// Gets or sets the source language used to disambiguate the translation. Optional; when blank,
    /// the Azure Translator service auto-detects the source language.
    /// </summary>
    public string FromLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the Provider maintains an in-memory cache of
    /// resolved messages. Defaults to <see langword="true"/>.
    /// </summary>
    public bool CachingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the time that resolved messages remain in the in-memory cache. Defaults to
    /// <see cref="DefaultCacheExpiration"/>.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = DefaultCacheExpiration;

    /// <summary>
    /// Gets or sets the optional glossary of forced term translations consulted before machine
    /// translation. The terms are snapshotted when the Provider is constructed; later registration
    /// on the same <see cref="MessageGlossary"/> instance does not change the Provider's behavior.
    /// Defaults to <see langword="null"/> (no glossary).
    /// </summary>
    public MessageGlossary? Glossary { get; set; }

    /// <summary>
    /// Gets or sets the optional translation-memory store consulted before machine translation.
    /// An exact-match hit short-circuits the external translation call. The store reference is
    /// captured when the Provider is constructed; the store's content may still change (refresh the
    /// Provider to drop cached results that shadow updated entries). Defaults to
    /// <see langword="null"/> (no translation memory).
    /// </summary>
    public ITranslationMemory? TranslationMemory { get; set; }

    /// <summary>
    /// Gets or sets the total budget for one chunk acquisition, covering every retry attempt,
    /// backoff delay, and rate-limiter wait for that chunk. Expiry surfaces as
    /// <see cref="OperationCanceledException"/>, never as a hang or a missing-resource result.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of HTTP attempts for one chunk acquisition, including the
    /// initial attempt. Only idempotent-safe transient faults are retried (network failures,
    /// timeouts, HTTP 408/429/5xx); other failures propagate immediately. Defaults to 3.
    /// </summary>
    public int RetryMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts. The delay before attempt
    /// <c>n</c> grows exponentially (<c>base × 2^(n-1)</c>), is capped by
    /// <see cref="RetryMaxDelay"/>, and carries up to 25% random jitter. Defaults to
    /// 200 milliseconds.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the cap for a single retry backoff delay (before jitter). Defaults to
    /// 5 seconds.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of consecutive transient attempt failures after which the circuit
    /// breaker opens and further acquisitions fail fast with
    /// <see cref="BrokenCircuitException"/> until <see cref="BreakerOpenDuration"/> elapses.
    /// Only transient faults count (network failures, timeouts, HTTP 408/429/5xx); client errors
    /// and malformed responses never trip the breaker. Defaults to 5.
    /// </summary>
    public int BreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long the circuit breaker stays open before admitting a single half-open
    /// probe. A successful probe closes the breaker; a failed probe re-opens it for another
    /// <see cref="BreakerOpenDuration"/>. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan BreakerOpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the client-side token-bucket refill rate in HTTP requests per second.
    /// Values less than or equal to zero disable rate limiting. Disabled by default (safe default:
    /// preserves existing throughput; enable to protect a throttled subscription).
    /// </summary>
    public int RateLimitRequestsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets the token-bucket capacity: how many HTTP requests may proceed immediately in a
    /// burst before pacing applies. Only meaningful when
    /// <see cref="RateLimitRequestsPerSecond"/> is positive. Defaults to 1.
    /// </summary>
    public int RateLimitBurstSize { get; set; } = 1;

    /// <summary>
    /// Validates the complete options state and throws when required configuration is missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required setting is missing or invalid.</exception>
    internal void Validate() {
        if(string.IsNullOrWhiteSpace(ApiKey)) {
            throw new InvalidOperationException("Azure Translator ApiKey is not configured.");
        }

        if(!IsValidAbsoluteHttpUri(Endpoint)) {
            throw new InvalidOperationException("Azure Translator Endpoint is not configured or is not a valid absolute http/https URI.");
        }

        if(CachingEnabled && CacheExpiration <= TimeSpan.Zero) {
            throw new InvalidOperationException("Azure Translator CacheExpiration must be positive when CachingEnabled is true.");
        }

        if(RequestTimeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("Azure Translator RequestTimeout must be positive.");
        }

        if(RetryMaxAttempts < 1) {
            throw new InvalidOperationException("Azure Translator RetryMaxAttempts must be at least 1.");
        }

        if(RetryBaseDelay < TimeSpan.Zero) {
            throw new InvalidOperationException("Azure Translator RetryBaseDelay must not be negative.");
        }

        if(RetryMaxDelay < RetryBaseDelay) {
            throw new InvalidOperationException("Azure Translator RetryMaxDelay must not be less than RetryBaseDelay.");
        }

        if(BreakerFailureThreshold < 1) {
            throw new InvalidOperationException("Azure Translator BreakerFailureThreshold must be at least 1.");
        }

        if(BreakerOpenDuration <= TimeSpan.Zero) {
            throw new InvalidOperationException("Azure Translator BreakerOpenDuration must be positive.");
        }

        if(RateLimitRequestsPerSecond < 0) {
            throw new InvalidOperationException("Azure Translator RateLimitRequestsPerSecond must not be negative.");
        }

        if(RateLimitBurstSize < 1) {
            throw new InvalidOperationException("Azure Translator RateLimitBurstSize must be at least 1.");
        }
    }

    /// <summary>
    /// Determines whether the value is valid absolute http uri.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsValidAbsoluteHttpUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";
}
