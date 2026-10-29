//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Custom exception to indicate forbidden provider-specific types in the architecture tests.
/// </summary>
public class ForbiddenProviderTypeException : Exception {
    /// <inheritdoc/>
    public ForbiddenProviderTypeException(string message) : base(message) { }

    /// <inheritdoc/>
    public ForbiddenProviderTypeException() { }

    /// <inheritdoc/>
    public ForbiddenProviderTypeException(string? message, Exception? innerException) : base(message, innerException) { }
}
