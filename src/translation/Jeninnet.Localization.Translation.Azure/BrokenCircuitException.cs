//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// The exception thrown when the Azure translation Provider's circuit breaker is open: the
/// Provider has observed <see cref="Options.AzureTranslationOptions.BreakerFailureThreshold"/>
/// consecutive transient failures and fails acquisitions fast — without an HTTP request — until
/// the breaker admits a half-open probe. A rejected acquisition raises
/// <see cref="IProviderEventSource.AcquisitionFailed"/> like any other acquisition failure.
/// </summary>
public sealed class BrokenCircuitException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="BrokenCircuitException"/> class.
    /// </summary>
    public BrokenCircuitException() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokenCircuitException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public BrokenCircuitException(string? message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokenCircuitException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The exception that caused the breaker to open.</param>
    public BrokenCircuitException(string? message, Exception? innerException)
        : base(message, innerException) {
    }
}
