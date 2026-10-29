//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Provides the message templates and target culture associated with a successful Provider
/// acquisition, raised to event listeners.
/// </summary>
/// <param name="templates">The resolved message templates.</param>
/// <param name="culture">The target culture the templates were resolved for.</param>
public sealed class ProviderAcquisitionEventArgs(
    IReadOnlyDictionary<string, string?> templates,
    CultureInfo culture) : EventArgs {
    /// <summary>
    /// Gets the resolved message templates (keyed by message key).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Templates { get; } = templates ?? throw new ArgumentNullException(nameof(templates));

    /// <summary>
    /// Gets the target culture the templates were resolved for.
    /// </summary>
    public CultureInfo Culture { get; } = culture ?? throw new ArgumentNullException(nameof(culture));
}
