//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// A single deterministic book in the reference catalog.
/// </summary>
/// <param name="Id">The stable book identifier (e.g. <c>"book-001"</c>).</param>
/// <param name="TitleKey">The localization key for the localized book title.</param>
/// <param name="DescriptionKey">The localization key for the localized book description.</param>
/// <param name="Author">The culture-neutral author display name.</param>
/// <param name="Price">The unit price of the book.</param>
public sealed record Book(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string Author,
    decimal Price) {
    /// <inheritdoc/>
    public override string ToString() => Id;
}
