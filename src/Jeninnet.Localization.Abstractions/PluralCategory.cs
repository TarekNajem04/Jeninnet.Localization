//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// CLDR (Common Locale Data Repository) plural categories.
/// </summary>
/// <remarks>
/// The category set follows Unicode CLDR (<c>zero</c>, <c>one</c>, <c>two</c>,
/// <c>few</c>, <c>many</c>, <c>other</c>); <c>other</c> is always defined and is
/// the fallback for every language. See <see cref="PluralRules"/> for the covered
/// language table.
/// </remarks>
public enum PluralCategory {
    /// <summary>The 'zero' category.</summary>
    Zero = 0,
    /// <summary>The 'one' category.</summary>
    One = 1,
    /// <summary>The 'two' category.</summary>
    Two = 2,
    /// <summary>The 'few' category.</summary>
    Few = 3,
    /// <summary>The 'many' category.</summary>
    Many = 4,
    /// <summary>The 'other' category (always defined; the universal fallback).</summary>
    Other = 5
}
