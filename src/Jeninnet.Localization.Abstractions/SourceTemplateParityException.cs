//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// The exception thrown when a translated template's placeholders do not match its
/// source template's. Thrown by <see cref="ISourceTemplateMessageProvider"/>
/// implementations so placeholder-dropping translations never silently serve.
/// </summary>
public sealed class SourceTemplateParityException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceTemplateParityException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public SourceTemplateParityException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceTemplateParityException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public SourceTemplateParityException(string message, Exception? innerException)
        : base(message, innerException) {
    }
}
