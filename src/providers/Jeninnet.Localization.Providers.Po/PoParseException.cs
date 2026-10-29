//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// The exception thrown when a gettext PO (Portable Object) catalog cannot be parsed.
/// Malformed PO fails fast with file/line diagnostics and never produces partial tables.
/// </summary>
public sealed class PoParseException : Exception {
    /// <summary>
    /// Gets the path of the PO file that failed to parse.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number where parsing failed, or <c>0</c> when no single line applies.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoParseException"/> class.
    /// </summary>
    /// <param name="filePath">The path of the PO file that failed to parse.</param>
    /// <param name="lineNumber">The 1-based line number where parsing failed.</param>
    /// <param name="message">The diagnostic message.</param>
    public PoParseException(string filePath, int lineNumber, string message)
        : base($"{message} (file '{filePath}', line {lineNumber.ToString(CultureInfo.InvariantCulture)})") {
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoParseException"/> class.
    /// </summary>
    /// <param name="filePath">The path of the PO file that failed to parse.</param>
    /// <param name="message">The diagnostic message.</param>
    public PoParseException(string filePath, string message)
        : base($"{message} (file '{filePath}')") {
        FilePath = filePath;
        LineNumber = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PoParseException"/> class.
    /// </summary>
    public PoParseException() => FilePath = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoParseException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public PoParseException(string message)
        : base(message) => FilePath = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PoParseException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public PoParseException(string message, Exception? innerException)
        : base(message, innerException) => FilePath = string.Empty;
}
