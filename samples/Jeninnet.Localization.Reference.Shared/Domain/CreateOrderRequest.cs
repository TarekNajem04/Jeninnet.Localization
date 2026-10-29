//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// The shared order-creation request model validated by hosts through the localized
/// DataAnnotations attributes provided by the library.
/// </summary>
public sealed class CreateOrderRequest {
    /// <summary>
    /// Gets or sets the selected book identifier.
    /// </summary>
    [Display(Name = ReferenceKeys.BOOK_ID_FIELD, Description = ReferenceKeys.BOOK_ID_DESCRIPTION)]
    [LocalizedRequired(ReferenceKeys.BOOK_REQUIRED_VALIDATION)]
    public string? BookId { get; set; }

    /// <summary>
    /// Gets or sets the ordered quantity.
    /// </summary>
    [Display(Name = ReferenceKeys.QUANTITY_FIELD, Prompt = ReferenceKeys.QUANTITY_PROMPT)]
    [LocalizedRange(OrderLimits.MIN_QUANTITY, OrderLimits.MAX_QUANTITY, ReferenceKeys.QUANTITY_RANGE_VALIDATION)]
    public int Quantity { get; set; }
}
