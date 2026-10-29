//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Wpf;

/// <summary>
/// The localized catalog list item surfaced to the WPF view.
/// </summary>
/// <param name="Book">The culture-neutral catalog book.</param>
/// <param name="Title">The localized book title.</param>
public sealed record BookListItem(Book Book, string Title) {
    /// <summary>
    /// Gets the display text combining the localized title and the culture-neutral author.
    /// </summary>
    public string Display => $"{Title} — {Book.Author}";
}
