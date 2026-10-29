//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Consumer;

/// <summary>
/// The consumer scenario output: the localized strings resolved through the packed
/// packages and the translator CSV produced from the resolved pair.
/// </summary>
/// <param name="Thanks">The English thank-you line (Memory provider).</param>
/// <param name="Total">The English order-total line (Memory provider).</param>
/// <param name="ArabicThanks">The Arabic thank-you line (JSON provider).</param>
/// <param name="Csv">The CSV catalog hand-off for the English/Arabic pair.</param>
public sealed record CheckoutResult(string Thanks, string Total, string ArabicThanks, string Csv);
