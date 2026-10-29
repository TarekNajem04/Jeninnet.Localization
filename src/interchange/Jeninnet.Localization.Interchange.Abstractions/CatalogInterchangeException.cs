//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Abstractions;

/// <summary>
/// The failure of the shared catalog file codec: carries the
/// bare diagnostic message plus file/line facts. Format packages catch it at their
/// public boundary and wrap it into their own public interchange exception
/// (message/file/line preserved), so per-format exception contracts stay stable.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CatalogInterchangeException"/> class.
/// </remarks>
/// <param name="filePath">The path of the file that failed processing.</param>
/// <param name="lineNumber">The 1-based line number where processing failed.</param>
/// <param name="message">The bare diagnostic message (no file/line suffix).</param>
public sealed class CatalogInterchangeException(string filePath, int lineNumber, string message) : Exception(message) {
    /// <summary>
    /// Gets the path of the file that failed processing.
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Gets the 1-based line number where processing failed, or <c>0</c> when no single line applies.
    /// </summary>
    public int LineNumber { get; } = lineNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogInterchangeException"/> class
    /// without a line number.
    /// </summary>
    /// <param name="filePath">The path of the file that failed processing.</param>
    /// <param name="message">The bare diagnostic message (no file/line suffix).</param>
    public CatalogInterchangeException(string filePath, string message)
        : this(filePath, 0, message) {
    }
}
