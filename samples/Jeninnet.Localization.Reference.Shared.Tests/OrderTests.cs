//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the order tests.
/// </summary>
[TestClass]
public sealed class OrderTests {
    /// <summary>
    /// Verifies that place order creates order with deterministic shape.
    /// </summary>
    [TestMethod]
    public void PlaceOrder_CreatesOrderWithDeterministicShape() {
        var request = new CreateOrderRequest { BookId = "book-001", Quantity = 2 };

        var result = OrderService.PlaceOrder(request);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Order);
        Assert.MatchesRegex("^ORD-\\d{4}$", result.Order.Id);
        Assert.HasCount(1, result.Order.Items);
        var item = result.Order.Items[0];
        Assert.AreEqual("book-001", item.BookId);
        Assert.AreEqual(ReferenceKeys.BOOK_PROPHET_TITLE, item.TitleKey);
        Assert.AreEqual("Kahlil Gibran", item.Author);
        Assert.AreEqual(2, item.Quantity);
        Assert.AreEqual(19.99m, item.UnitPrice);
        Assert.AreEqual(39.98m, item.LineTotal);
        Assert.AreEqual(39.98m, result.Order.Total);
    }

    /// <summary>
    /// Verifies that place order generates monotonically increasing ids.
    /// </summary>
    [TestMethod]
    public void PlaceOrder_GeneratesMonotonicallyIncreasingIds() {
        var first = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-002", Quantity = 1 });
        var second = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-002", Quantity = 1 });

        Assert.IsTrue(first.Succeeded && second.Succeeded);
        var firstNumber = int.Parse(first.Order!.Id["ORD-".Length..], CultureInfo.InvariantCulture);
        var secondNumber = int.Parse(second.Order!.Id["ORD-".Length..], CultureInfo.InvariantCulture);
        Assert.AreEqual(firstNumber + 1, secondNumber);
    }

    /// <summary>
    /// Verifies that place order returns book not found key for unknown book.
    /// </summary>
    [TestMethod]
    public void PlaceOrder_ReturnsBookNotFoundKeyForUnknownBook() {
        var result = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-999", Quantity = 1 });

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Order);
        Assert.AreEqual(ReferenceKeys.BOOK_NOT_FOUND_VALIDATION, result.ErrorMessageKey);
        Assert.AreSequenceEqual(["book-999"], result.ErrorMessageArguments.ToArray());
    }

    /// <summary>
    /// Verifies that place order returns quantity range key for out of bounds quantity.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(100)]
    public void PlaceOrder_ReturnsQuantityRangeKeyForOutOfBoundsQuantity(int quantity) {
        var result = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-001", Quantity = quantity });

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(ReferenceKeys.QUANTITY_RANGE_VALIDATION, result.ErrorMessageKey);
    }

    /// <summary>
    /// Verifies that place order accepts boundary quantities.
    /// </summary>
    /// <param name="quantity">The quantity.</param>
    [TestMethod]
    [DataRow(OrderLimits.MIN_QUANTITY)]
    [DataRow(OrderLimits.MAX_QUANTITY)]
    public void PlaceOrder_AcceptsBoundaryQuantities(int quantity) {
        var result = OrderService.PlaceOrder(new CreateOrderRequest { BookId = "book-003", Quantity = quantity });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(quantity, result.Order!.Items[0].Quantity);
    }

    /// <summary>
    /// Verifies that create order request applies localized validation attributes.
    /// </summary>
    [TestMethod]
    public void CreateOrderRequest_AppliesLocalizedValidationAttributes() {
        var bookIdProperty = typeof(CreateOrderRequest).GetProperty(nameof(CreateOrderRequest.BookId))!;
        Assert.IsNotNull(bookIdProperty.GetCustomAttribute<LocalizedRequiredAttribute>());

        var quantityProperty = typeof(CreateOrderRequest).GetProperty(nameof(CreateOrderRequest.Quantity))!;
        var range = quantityProperty.GetCustomAttribute<LocalizedRangeAttribute>();
        Assert.IsNotNull(range);
        Assert.AreEqual(ReferenceKeys.QUANTITY_RANGE_VALIDATION, range.LocalizationKey);
        Assert.AreEqual(OrderLimits.MIN_QUANTITY, (int)range.Minimum);
        Assert.AreEqual(OrderLimits.MAX_QUANTITY, range.Maximum);
    }
}
