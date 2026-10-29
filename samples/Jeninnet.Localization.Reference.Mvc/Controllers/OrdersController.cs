//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Mvc.Controllers;

/// <summary>
/// Places localized book orders captured through strongly-typed, display-metadata-aware views.
/// </summary>
[Route("orders")]
public sealed class OrdersController : Controller {
    private readonly IStringLocalizer<PageResources> _localizer;
    private readonly IMessageLocalizer _messageLocalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrdersController"/> class.
    /// </summary>
    /// <param name="localizer">The view-model localizer.</param>
    /// <param name="messageLocalizer">The message localizer.</param>
    public OrdersController(IStringLocalizer<PageResources> localizer, IMessageLocalizer messageLocalizer) {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(messageLocalizer);
        _localizer = localizer;
        _messageLocalizer = messageLocalizer;
    }

    /// <summary>
    /// Displays the localized order form.
    /// </summary>
    /// <returns>The order form view, including any confirmation for the last placed order.</returns>
    [HttpGet("create")]
    public async Task<IActionResult> CreateAsync() {
        ViewData["Books"] = CreateBookOptions();

        if(TryGetLastOrder(out var order)) {
            ViewData["OrderMessage"] = await _messageLocalizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                CreateOrderArguments(order),
                CultureInfo.CurrentUICulture);
        }

        return View(new CreateOrderRequest());
    }

    /// <summary>
    /// Places the order or re-renders the form with the localized embedded error message.
    /// </summary>
    /// <param name="request">The order request.</param>
    /// <returns>A redirect to the refreshed form after a successful order, or the form view with validation feedback.</returns>
    [HttpPost("create")]
    public async Task<IActionResult> CreateAsync([FromForm] CreateOrderRequest request) {
        if(!ModelState.IsValid) {
            ViewData["Books"] = CreateBookOptions();
            return View(request);
        }

        var result = OrderService.PlaceOrder(request);

        if(!result.Succeeded) {
            var errorParams = result.ErrorMessageArguments
                .Select((arg, index) => (Index: index.ToString(CultureInfo.InvariantCulture), Value: arg))
                .ToDictionary(entry => entry.Index, entry => entry.Value);

            var errorMessage = await _messageLocalizer.LocalizeAsync(result.ErrorMessageKey!, errorParams);
            ModelState.AddModelError(string.Empty, errorMessage ?? string.Empty);
            ViewData["Books"] = CreateBookOptions();
            return View(request);
        }

        var order = new LastOrder(
            result.Order!.Id,
            result.Order.Items.Sum(static item => item.Quantity),
            result.Order.Total);

        HttpContext.Session.SetString(ORDER_ID_KEY, order.Id);
        HttpContext.Session.SetString(ORDER_QUANTITY_KEY, order.Quantity.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString(ORDER_TOTAL_KEY, order.Total.ToString(CultureInfo.InvariantCulture));

        return RedirectToAction(nameof(CreateAsync), new { culture = CultureInfo.CurrentUICulture.Name });
    }

    /// <summary>
    /// Attempts to get last order.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private bool TryGetLastOrder(out LastOrder order) {
        order = default!;
        var id = HttpContext.Session.GetString(ORDER_ID_KEY);
        var quantityText = HttpContext.Session.GetString(ORDER_QUANTITY_KEY);
        var totalText = HttpContext.Session.GetString(ORDER_TOTAL_KEY);
        if(string.IsNullOrEmpty(id) ||
            !int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) ||
            !decimal.TryParse(totalText, NumberStyles.Number, CultureInfo.InvariantCulture, out var total)) {
            return false;
        }

        order = new LastOrder(id, quantity, total);
        return true;
    }

    /// <summary>
    /// Creates order arguments.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The order arguments result.</returns>
    private static Dictionary<string, object?> CreateOrderArguments(LastOrder order) => new() {
        ["0"] = order.Id,
        ["1"] = order.Quantity,
        ["2"] = order.Total
    };

    private const string ORDER_ID_KEY = "jeninnet.reference.last-order.id";
    private const string ORDER_QUANTITY_KEY = "jeninnet.reference.last-order.quantity";
    private const string ORDER_TOTAL_KEY = "jeninnet.reference.last-order.total";

    /// <summary>
    /// Represents a last order.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="Quantity">The quantity.</param>
    /// <param name="Total">The total.</param>
    private sealed record LastOrder(string Id, int Quantity, decimal Total);

    /// <summary>
    /// Creates book options.
    /// </summary>
    /// <returns>The book options sequence.</returns>
    private List<SelectListItem> CreateBookOptions() =>
        [.. BookCatalog.All.Select(book => new SelectListItem(_localizer[book.TitleKey].Value, book.Id))];
}
