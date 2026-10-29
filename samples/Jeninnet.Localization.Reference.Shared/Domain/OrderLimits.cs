//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Domain;

/// <summary>
/// The single source of truth for order quantity bounds, shared by the validation-model
/// attributes and the application service so the two validation layers cannot drift.
/// </summary>
public static class OrderLimits {
    /// <summary>
    /// The minimum acceptable order quantity.
    /// </summary>
    public const int MIN_QUANTITY = 1;

    /// <summary>
    /// The maximum acceptable order quantity.
    /// </summary>
    public const int MAX_QUANTITY = 99;
}
