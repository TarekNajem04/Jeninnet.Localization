//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Api.Controllers;

/// <summary>
/// Exposes the reference BookStore catalog and order operations through localized API endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase {
    private readonly IStringLocalizer<BooksController> _localizer;
    private readonly IMessageLocalizer _messageLocalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooksController"/> class.
    /// </summary>
    /// <param name="localizer">The localized string resolver.</param>
    /// <param name="messageLocalizer">The localized message resolver.</param>
    public BooksController(
        IStringLocalizer<BooksController> localizer,
        IMessageLocalizer messageLocalizer) {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(messageLocalizer);
        _localizer = localizer;
        _messageLocalizer = messageLocalizer;
    }

    /// <summary>
    /// Gets the full catalog with localized titles and descriptions.
    /// </summary>
    /// <returns>The catalog response.</returns>
    [HttpGet("catalog")]
    public IActionResult GetCatalog() {
        var books = BookCatalog.All.Select(book => new {
            book.Id,
            Title = _localizer[book.TitleKey].Value,
            Description = _localizer[book.DescriptionKey].Value,
            book.Author,
            book.Price
        }).ToArray();

        return Ok(new {
            Heading = _localizer[ReferenceKeys.CATALOG_HEADING].Value,
            Books = books
        });
    }

    /// <summary>
    /// Gets a single book with localized title and description.
    /// </summary>
    /// <param name="id">The book identifier.</param>
    /// <returns>The book response, or 404 if not found.</returns>
    [HttpGet("catalog/{id}")]
    public IActionResult GetBook(string id) {
        if(!BookCatalog.TryGetBook(id, out var book)) {
            return NotFound();
        }

        return Ok(new {
            book.Id,
            Title = _localizer[book.TitleKey].Value,
            Description = _localizer[book.DescriptionKey].Value,
            book.Author,
            book.Price
        });
    }

    /// <summary>
    /// Places an order and returns a localized confirmation or validation error.
    /// </summary>
    /// <param name="request">The order request.</param>
    /// <returns>A localized order result.</returns>
    [HttpPost("orders")]
    public async Task<IActionResult> PlaceOrderAsync([FromBody] CreateOrderRequest request) {
        if(!ModelState.IsValid) {
            return ValidationProblem(ModelState);
        }

        var result = OrderService.PlaceOrder(request);

        if(!result.Succeeded) {
            var errorParams = result.ErrorMessageArguments
                .Select((arg, idx) => new KeyValuePair<string, object?>(idx.ToString(CultureInfo.InvariantCulture), arg))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var errorMessage = await _messageLocalizer.LocalizeAsync(result.ErrorMessageKey!, errorParams);
            return BadRequest(new {
                Error = result.ErrorMessageKey,
                Message = errorMessage
            });
        }

        var orderMessage = await _messageLocalizer.LocalizeAsync(
            ReferenceKeys.ORDER_PLACED_MESSAGE,
            new Dictionary<string, object?> {
                ["0"] = result.Order!.Id,
                ["1"] = result.Order.Items.Sum(static i => i.Quantity),
                ["2"] = result.Order.Total
            });

        return Ok(new {
            OrderId = result.Order.Id,
            Message = orderMessage,
            Items = result.Order.Items.Select(i => new {
                i.BookId,
                Title = _localizer[i.TitleKey].Value,
                i.Author,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            })
        });
    }
}
