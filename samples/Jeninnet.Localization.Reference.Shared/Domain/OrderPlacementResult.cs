//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// The deterministic result of an order-creation attempt.
/// </summary>
public sealed class OrderPlacementResult {
    /// <summary>
    /// Initializes a new instance of the OrderPlacementResult class.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="errorMessageKey">The error message key.</param>
    /// <param name="errorMessageArguments">The error message arguments.</param>
    private OrderPlacementResult(Order? order, string? errorMessageKey, IReadOnlyList<object?> errorMessageArguments) {
        Order = order;
        ErrorMessageKey = errorMessageKey;
        ErrorMessageArguments = errorMessageArguments;
        Succeeded = order is not null && string.IsNullOrEmpty(errorMessageKey);
    }

    /// <summary>
    /// Creates a successful result carrying the created order.
    /// </summary>
    /// <param name="order">The created order.</param>
    /// <returns>A successful result.</returns>
    public static OrderPlacementResult Successful(Order order) => new(order, errorMessageKey: null, []);

    /// <summary>
    /// Creates a failed result carrying a localized error-message key and its format arguments.
    /// </summary>
    /// <param name="errorMessageKey">The localization key of the error message.</param>
    /// <param name="errorMessageArguments">The optional composite-format arguments of the message.</param>
    /// <returns>A failed result.</returns>
    public static OrderPlacementResult Failed(string errorMessageKey, params object?[] errorMessageArguments)
        => new(order: null, errorMessageKey, errorMessageArguments);

    /// <summary>
    /// Gets a value indicating whether the order was created.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the created order, or <see langword="null"/> when the attempt failed.
    /// </summary>
    public Order? Order { get; }

    /// <summary>
    /// Gets the localization key of the error message, or <see langword="null"/> on success.
    /// </summary>
    public string? ErrorMessageKey { get; }

    /// <summary>
    /// Gets the composite-format arguments for the error message.
    /// </summary>
    public IReadOnlyList<object?> ErrorMessageArguments { get; }
}
