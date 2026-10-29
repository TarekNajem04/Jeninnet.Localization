//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Wpf;

/// <summary>
/// The observable BookStore view model consumed by the WPF host. Demonstrates lightweight MVVM:
/// localized display properties, runtime culture switching, and localized DataAnnotations validation.
/// </summary>
public sealed class BookStoreViewModel : INotifyPropertyChanged {
    private readonly IServiceProvider _services;
    private readonly IMessageLocalizer _messageLocalizer;
    private IStringLocalizer<PageResources> _localizer;
    private Order? _lastOrder;
    private bool _showingOrderMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookStoreViewModel"/> class.
    /// </summary>
    /// <param name="services">The host service provider.</param>
    public BookStoreViewModel(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _messageLocalizer = services.GetRequiredService<IMessageLocalizer>();
        _localizer = services.GetRequiredService<IStringLocalizer<PageResources>>();

        ReloadBooks();
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the localized application title.</summary>
    public string Title => _localizer[ReferenceKeys.APPLICATION_TITLE];

    /// <summary>Gets the culture-switch action label.</summary>
    public string SwitchCultureLabel => _localizer[ReferenceKeys.SWITCH_CULTURE_ACTION];

    /// <summary>Gets the catalog heading.</summary>
    public string CatalogHeading => _localizer[ReferenceKeys.CATALOG_HEADING];

    /// <summary>Gets the book-selection field label.</summary>
    public string BookFieldLabel => _localizer[ReferenceKeys.BOOK_ID_FIELD];

    /// <summary>Gets the quantity field label.</summary>
    public string QuantityLabel => _localizer[ReferenceKeys.QUANTITY_FIELD];

    /// <summary>Gets the add-to-order action label.</summary>
    public string AddToOrderLabel => _localizer[ReferenceKeys.ADD_TO_ORDER_ACTION];

    /// <summary>Gets the supported culture codes.</summary>
    public IReadOnlyList<string> CultureOptions { get; } = ReferenceCultures.SupportedCultures;

    /// <summary>Gets the layout direction for the active culture.</summary>
    public System.Windows.FlowDirection FlowDirection =>
        new CultureInfo(CurrentCulture).TextInfo.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;

    /// <summary>Gets or sets the current culture code.</summary>
    public string CurrentCulture {
        get;
        set {
            if(!string.IsNullOrEmpty(value) && value != field) {
                field = value;
                ApplyCulture(value);
            }
        }
    } = CultureInfo.CurrentUICulture.Name;

    /// <summary>Gets the localized catalog items.</summary>
    public IReadOnlyList<BookListItem> Books { get; private set; } = [];

    /// <summary>Gets or sets the selected catalog item.</summary>
    public BookListItem? SelectedBook {
        get;
        set {
            field = value;
            Raise(nameof(SelectedBook));
            Raise(nameof(SelectedBookDetails));
        }
    }

    /// <summary>Gets or sets the requested order quantity.</summary>
    public int Quantity {
        get;
        set {
            field = value;
            Raise(nameof(Quantity));
        }
    } = OrderLimits.MIN_QUANTITY;

    /// <summary>Gets the localized details of the selected book.</summary>
    public string SelectedBookDetails {
        get {
            if(SelectedBook is null) {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}: {1}" + Environment.NewLine + "{2}: {3}" + Environment.NewLine + "{4}: {5:C}",
                _localizer[ReferenceKeys.FIELD_AUTHOR],
                SelectedBook.Book.Author,
                _localizer[ReferenceKeys.FIELD_DESCRIPTION],
                _localizer[SelectedBook.Book.DescriptionKey],
                _localizer[ReferenceKeys.FIELD_PRICE],
                SelectedBook.Book.Price);
        }
    }

    /// <summary>Gets the localized order feedback message.</summary>
    public string? Message {
        get;
        private set {
            field = value;
            Raise(nameof(Message));
        }
    }

    /// <summary>
    /// Validates and places the order, publishing the localized confirmation or error message.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PlaceOrderAsync() {
        var request = new CreateOrderRequest {
            BookId = SelectedBook?.Book.Id,
            Quantity = Quantity
        };

        var validationContext = new ValidationContext(request, _services, null);
        var validationResults = new List<ValidationResult>();
        if(!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true)) {
            _showingOrderMessage = false;
            Message = string.Join(
                Environment.NewLine,
                validationResults
                    .Select(static result => result.ErrorMessage)
                    .Where(static error => error is not null));
            return;
        }

        var result = OrderService.PlaceOrder(request);
        if(result.Succeeded && result.Order is not null) {
            _lastOrder = result.Order;
            _showingOrderMessage = true;
            Message = await _messageLocalizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                CreateOrderArguments(result.Order),
                CultureInfo.CurrentUICulture);
            Quantity = OrderLimits.MIN_QUANTITY;
            return;
        }

        if(result.ErrorMessageKey is not null) {
            _showingOrderMessage = false;
            var arguments = result.ErrorMessageArguments
                .Select((argument, index) => (Index: index.ToString(CultureInfo.InvariantCulture), Value: argument))
                .ToDictionary(static entry => entry.Index, static entry => entry.Value);
            Message = await _messageLocalizer.LocalizeAsync(result.ErrorMessageKey, arguments);
        }
    }

    /// <summary>
    /// Applies the selected culture, rebinds the page localizer, and refreshes the catalog and any visible order message.
    /// </summary>
    /// <param name="code">The culture code selected by the user.</param>
    private void ApplyCulture(string code) {
        var culture = new CultureInfo(code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _localizer = _services.GetRequiredService<IStringLocalizer<PageResources>>();

        var selectedBookId = SelectedBook?.Book.Id;
        ReloadBooks();
        if(selectedBookId is not null) {
            SelectedBook = Books.FirstOrDefault(book => book.Book.Id == selectedBookId);
        }

        if(_showingOrderMessage && _lastOrder is not null) {
            Message = _messageLocalizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                CreateOrderArguments(_lastOrder),
                CultureInfo.CurrentUICulture).AsTask().GetAwaiter().GetResult();
        }

        Raise(nameof(CurrentCulture));
        Raise(nameof(FlowDirection));
        Raise(nameof(Title));
        Raise(nameof(SwitchCultureLabel));
        Raise(nameof(CatalogHeading));
        Raise(nameof(BookFieldLabel));
        Raise(nameof(QuantityLabel));
        Raise(nameof(AddToOrderLabel));
        Raise(nameof(Books));
        Raise(nameof(SelectedBookDetails));
    }

    /// <summary>
    /// Creates order arguments.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The order arguments result.</returns>
    private static Dictionary<string, object?> CreateOrderArguments(Order order) => new() {
        ["0"] = order.Id,
        ["1"] = order.Items.Sum(static item => item.Quantity),
        ["2"] = order.Total
    };

    /// <summary>
    /// Performs the reload books operation.
    /// </summary>
    private void ReloadBooks() =>
        Books = [.. BookCatalog.All.Select(book => new BookListItem(book, _localizer[book.TitleKey].Value))];

    /// <summary>
    /// Performs the raise operation.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    private void Raise(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
