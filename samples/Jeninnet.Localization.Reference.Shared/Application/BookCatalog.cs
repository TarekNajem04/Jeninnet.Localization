//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Application;

/// <summary>
/// The deterministic in-memory reference catalog.
/// </summary>
public static class BookCatalog {
    private static readonly Book[] _books = [
        new("book-001", ReferenceKeys.BOOK_PROPHET_TITLE, ReferenceKeys.BOOK_PROPHET_DESCRIPTION, "Kahlil Gibran", 19.99m),
        new("book-002", ReferenceKeys.BOOK_CAIRO_TRILOGY_TITLE, ReferenceKeys.BOOK_CAIRO_TRILOGY_DESCRIPTION, "Naguib Mahfouz", 24.50m),
        new("book-003", ReferenceKeys.BOOK_SEASON_OF_MIGRATION_TITLE, ReferenceKeys.BOOK_SEASON_OF_MIGRATION_DESCRIPTION, "Tayeb Salih", 14.75m),
        new("book-004", ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_TITLE, ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_DESCRIPTION, "Nizar Qabbani", 16.25m),
    ];

    /// <summary>
    /// Gets the ordered list of all catalog books.
    /// </summary>
    public static IReadOnlyList<Book> All { get; } = Array.AsReadOnly(_books);

    /// <summary>
    /// Attempts to retrieve a book by its stable identifier.
    /// </summary>
    /// <param name="id">The book identifier.</param>
    /// <param name="book">The matching book when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a book with the identifier exists in the catalog.</returns>
    public static bool TryGetBook(string id, [NotNullWhen(true)] out Book? book) {
        ArgumentNullException.ThrowIfNull(id);

        var match = _books.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if(match is not null) {
            book = match;
            return true;
        }

        book = null;
        return false;
    }
}
