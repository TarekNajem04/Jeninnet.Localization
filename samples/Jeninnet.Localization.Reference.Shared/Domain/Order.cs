//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// An immutable reference order built from a validated order request.
/// </summary>
/// <param name="Id">The stable order identifier generated at creation time.</param>
/// <param name="Items">The ordered line items.</param>
public sealed record Order(string Id, IReadOnlyList<OrderItem> Items) {
    /// <summary>
    /// Gets the order total computed from the line totals.
    /// </summary>
    public decimal Total => Items.Sum(static item => item.LineTotal);
}
