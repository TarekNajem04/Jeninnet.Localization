//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// The exception thrown when a TMS connector cannot pull or push. Vendor HTTP
/// failures surface here with the status code and a body snippet; authentication
/// material is never included in the message.
/// </summary>
public sealed class TmsConnectorException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="TmsConnectorException"/> class.
    /// </summary>
    public TmsConnectorException() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TmsConnectorException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public TmsConnectorException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TmsConnectorException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public TmsConnectorException(string message, Exception? innerException)
        : base(message, innerException) {
    }
}
