//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Provides the culture and error associated with a failed Provider acquisition, raised to
/// event listeners.
/// </summary>
/// <param name="culture">The target culture whose acquisition failed.</param>
/// <param name="exception">The exception describing the failure.</param>
public sealed class ProviderAcquisitionFailedEventArgs(
    CultureInfo culture,
    Exception exception) : EventArgs {
    /// <summary>
    /// Gets the target culture whose acquisition failed.
    /// </summary>
    public CultureInfo Culture { get; } = culture ?? throw new ArgumentNullException(nameof(culture));

    /// <summary>
    /// Gets the exception describing the failure.
    /// </summary>
    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));
}
