//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// A single line item within a reference order.
/// </summary>
/// <param name="BookId">The identifier of the ordered book.</param>
/// <param name="TitleKey">The localization key for the book title at order time.</param>
/// <param name="Author">The author display name at order time.</param>
/// <param name="Quantity">The ordered quantity.</param>
/// <param name="UnitPrice">The per-unit price at order time.</param>
public sealed record OrderItem(
    string BookId,
    string TitleKey,
    string Author,
    int Quantity,
    decimal UnitPrice) {
    /// <summary>
    /// Gets the line total computed from the quantity and unit price.
    /// </summary>
    public decimal LineTotal => Quantity * UnitPrice;
}
