//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Po;

/// <summary>
/// The exception thrown when PO (Portable Object) interchange content or a catalog file cannot be
/// processed. Malformed input fails fast with file/line diagnostics and never yields
/// partial results: nothing is applied when validation fails.
/// </summary>
public sealed class PoInterchangeException : Exception {
    /// <summary>
    /// Gets the path of the file that failed processing.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number where processing failed, or <c>0</c> when no single line applies.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoInterchangeException"/> class.
    /// </summary>
    /// <param name="filePath">The path of the file that failed processing.</param>
    /// <param name="lineNumber">The 1-based line number where processing failed.</param>
    /// <param name="message">The diagnostic message.</param>
    public PoInterchangeException(string filePath, int lineNumber, string message)
        : base($"{message} (file '{filePath}', line {lineNumber.ToString(CultureInfo.InvariantCulture)})") {
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoInterchangeException"/> class.
    /// </summary>
    /// <param name="filePath">The path of the file that failed processing.</param>
    /// <param name="message">The diagnostic message.</param>
    public PoInterchangeException(string filePath, string message)
        : base($"{message} (file '{filePath}')") {
        FilePath = filePath;
        LineNumber = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoInterchangeException"/> class.
    /// </summary>
    public PoInterchangeException() => FilePath = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoInterchangeException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public PoInterchangeException(string message)
        : base(message) => FilePath = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoInterchangeException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PoInterchangeException(string message, Exception? innerException)
        : base(message, innerException) => FilePath = string.Empty;
}
