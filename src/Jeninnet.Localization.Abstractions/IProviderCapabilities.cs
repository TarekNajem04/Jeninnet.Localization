//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Describes which optional features a message Provider supports.
/// A Provider typically also implements the capability interface matching each supported
/// <see cref="ProviderFeature"/> (e.g. <see cref="IMultiKeyMessageProvider"/> for
/// <see cref="ProviderFeature.MultiKeys"/>).
/// </summary>
public interface IProviderCapabilities {
    /// <summary>
    /// Returns a value indicating whether the Provider supports the given <paramref name="feature"/>.
    /// </summary>
    /// <param name="feature">The feature to query.</param>
    /// <returns><see langword="true"/> when the Provider supports <paramref name="feature"/>.</returns>
    bool Supports(ProviderFeature feature);
}
