//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Application;

/// <summary>
/// Creates reference orders from validated order requests.
/// </summary>
public static class OrderService {
    private static int _orderNumber;

    /// <summary>
    /// Creates an order from a valid request.
    /// </summary>
    /// <param name="request">The validated order request.</param>
    /// <returns>A result carrying the created order or a deterministic error key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public static OrderPlacementResult PlaceOrder(CreateOrderRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        if(!BookCatalog.TryGetBook(request.BookId ?? string.Empty, out var book)) {
            return OrderPlacementResult.Failed(ReferenceKeys.BOOK_NOT_FOUND_VALIDATION, request.BookId);
        }

        if(request.Quantity is < OrderLimits.MIN_QUANTITY or > OrderLimits.MAX_QUANTITY) {
            return OrderPlacementResult.Failed(ReferenceKeys.QUANTITY_RANGE_VALIDATION, OrderLimits.MIN_QUANTITY, OrderLimits.MAX_QUANTITY);
        }

        var order = new Order(
            CreateOrderId(),
            [new OrderItem(book.Id, book.TitleKey, book.Author, request.Quantity, book.Price)]);

        return OrderPlacementResult.Successful(order);
    }

    /// <summary>
    /// Generates the next deterministic order identifier.
    /// </summary>
    /// <returns>A zero-padded, monotonically increasing order identifier.</returns>
    private static string CreateOrderId() {
        var number = Interlocked.Increment(ref _orderNumber);
        return $"ORD-{number:D4}";
    }
}
