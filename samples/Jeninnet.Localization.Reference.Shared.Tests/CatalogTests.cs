//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Tests;

/// <summary>
/// Contains the catalog tests.
/// </summary>
[TestClass]
public sealed class CatalogTests {
    private static readonly string[] _expectedBookIds =
        ["book-001", "book-002", "book-003", "book-004"];

    /// <summary>
    /// Verifies that all returns the four deterministic books in id order.
    /// </summary>
    [TestMethod]
    public void All_ReturnsTheFourDeterministicBooksInIdOrder() {
        var books = BookCatalog.All;

        Assert.HasCount(4, books);
        Assert.AreSequenceEqual(_expectedBookIds, [.. books.Select(static book => book.Id)]);
    }

    /// <summary>
    /// Verifies that all exposes expected prices.
    /// </summary>
    [TestMethod]
    public void All_ExposesExpectedPrices() {
        var prices = BookCatalog.All.ToDictionary(static book => book.Id, static book => book.Price);

        Assert.AreEqual(19.99m, prices["book-001"]);
        Assert.AreEqual(24.50m, prices["book-002"]);
        Assert.AreEqual(14.75m, prices["book-003"]);
        Assert.AreEqual(16.25m, prices["book-004"]);
    }

    /// <summary>
    /// Verifies that try get book returns matching book with localized keys.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="expectedTitleKey">The expected title key.</param>
    /// <param name="expectedAuthor">The expected author.</param>
    [TestMethod]
    [DataRow("book-001", ReferenceKeys.BOOK_PROPHET_TITLE, "Kahlil Gibran")]
    [DataRow("book-004", ReferenceKeys.BOOK_ARABIAN_LOVE_POEMS_TITLE, "Nizar Qabbani")]
    public void TryGetBook_ReturnsMatchingBookWithLocalizedKeys(string id, string expectedTitleKey, string expectedAuthor) {
        var found = BookCatalog.TryGetBook(id, out var book);

        Assert.IsTrue(found);
        Assert.IsNotNull(book);
        Assert.AreEqual(expectedTitleKey, book.TitleKey);
        Assert.AreEqual(expectedAuthor, book.Author);
    }

    /// <summary>
    /// Verifies that try get book returns false for unknown id.
    /// </summary>
    [TestMethod]
    public void TryGetBook_ReturnsFalseForUnknownId() {
        var found = BookCatalog.TryGetBook("book-999", out var book);

        Assert.IsFalse(found);
        Assert.IsNull(book);
    }
}
