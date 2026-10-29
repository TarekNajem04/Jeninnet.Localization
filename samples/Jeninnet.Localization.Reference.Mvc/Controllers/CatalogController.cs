//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Mvc.Controllers;

/// <summary>
/// Serves the localized book catalog pages.
/// </summary>
[Route("catalog")]
public sealed class CatalogController : Controller {
    /// <summary>
    /// Displays the localized catalog.
    /// </summary>
    /// <returns>The catalog view rendered with every known book.</returns>
    [HttpGet("")]
    public IActionResult Index() => View(BookCatalog.All);

    /// <summary>
    /// Displays the localized details of a single book.
    /// </summary>
    /// <param name="id">The book identifier.</param>
    /// <returns>The book details view, or a not-found result when the identifier is unknown.</returns>
    [HttpGet("{id}")]
    public IActionResult Details([FromRoute] string id) {
        if(!BookCatalog.TryGetBook(id, out var book)) {
            return NotFound();
        }

        return View(book);
    }
}
