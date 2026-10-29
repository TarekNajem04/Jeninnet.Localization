//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.WinForms;

/// <summary>
/// The main reference BookStore window. Demonstrates the canonical store-backed pipeline over a
/// classic WinForms UI, including runtime culture switching and localized DataAnnotations validation.
/// </summary>
public sealed class MainForm : Form {
    private readonly IServiceProvider _services;
    private readonly IMessageLocalizer _messageLocalizer;

    private readonly TableLayoutPanel _layout;
    private readonly Label _switchCultureLabel;
    private readonly ComboBox _cultureComboBox;
    private readonly Label _catalogHeadingLabel;
    private readonly ListBox _catalogListBox;
    private readonly Label _detailsLabel;
    private readonly Label _quantityLabel;
    private readonly NumericUpDown _quantityControl;
    private readonly Button _placeOrderButton;
    private readonly Label _messageLabel;

    private Book? _selectedBook;
    private Order? _lastOrder;
    private bool _showingOrderMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    /// <param name="services">The host service provider.</param>
    public MainForm(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _messageLocalizer = services.GetRequiredService<IMessageLocalizer>();

        _switchCultureLabel = new Label { AutoSize = true };
        _cultureComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 128 };
        _catalogHeadingLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        _catalogListBox = new ListBox { Dock = DockStyle.Fill };
        _detailsLabel = new Label { AutoSize = true, MaximumSize = new Size(640, 0) };
        _quantityLabel = new Label { AutoSize = true };
        _quantityControl = new NumericUpDown {
            Minimum = OrderLimits.MIN_QUANTITY,
            Maximum = OrderLimits.MAX_QUANTITY,
            Value = OrderLimits.MIN_QUANTITY
        };
        _placeOrderButton = new Button { AutoSize = true };
        _messageLabel = new Label { AutoSize = true, MaximumSize = new Size(640, 0) };

        _layout = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 7
        };

        _cultureComboBox.Items.AddRange([.. ReferenceCultures.SupportedCultures]);

        // Build the layout and add the controls to the form.
        SuspendLayout();
        ClientSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        ResumeLayout(false);
        // End of layout building.

        _cultureComboBox.SelectedIndexChanged += OnCultureChanged;
        _catalogListBox.SelectedIndexChanged += OnCatalogSelectionChanged;
        _placeOrderButton.Click += OnPlaceOrderClicked;

        Controls.Add(_layout);

        // Set the initial culture to the current UI culture if supported, otherwise fallback to the default culture.
        _cultureComboBox.SelectedItem = ResolveInitialCulture();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if(disposing) {
            _layout.Dispose();
            _switchCultureLabel.Dispose();
            _cultureComboBox.Dispose();
            _catalogHeadingLabel.Dispose();
            _catalogListBox.Dispose();
            _detailsLabel.Dispose();
            _quantityLabel.Dispose();
            _quantityControl.Dispose();
            _placeOrderButton.Dispose();
            _messageLabel.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Resolves initial culture.
    /// </summary>
    /// <returns>The initial culture text.</returns>
    private static string ResolveInitialCulture() {
        var current = CultureInfo.CurrentUICulture.Name;
        return ReferenceCultures.SupportedCultures.Contains(current, StringComparer.Ordinal)
            ? current
            : ReferenceCultures.DEFAULT_CULTURE;
    }

    /// <summary>
    /// Builds the table layout and adds the localized catalog, order, and status controls to the form.
    /// </summary>
    private void BuildLayout() {
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _layout.Controls.Add(_switchCultureLabel, 0, 0);
        _layout.Controls.Add(_cultureComboBox, 1, 0);
        _layout.Controls.Add(_catalogHeadingLabel, 0, 1);
        _layout.SetColumnSpan(_catalogHeadingLabel, 2);
        _layout.Controls.Add(_catalogListBox, 0, 2);
        _layout.SetColumnSpan(_catalogListBox, 2);
        _layout.Controls.Add(_detailsLabel, 0, 3);
        _layout.SetColumnSpan(_detailsLabel, 2);
        _layout.Controls.Add(_quantityLabel, 0, 4);
        _layout.Controls.Add(_quantityControl, 1, 4);
        _layout.Controls.Add(_placeOrderButton, 0, 5);
        _layout.SetColumnSpan(_placeOrderButton, 2);
        _layout.Controls.Add(_messageLabel, 0, 6);
        _layout.SetColumnSpan(_messageLabel, 2);
    }

    /// <summary>
    /// Applies culture.
    /// </summary>
    /// <param name="code">The code.</param>
    private void ApplyCulture(string code) {
        CultureInfo.CurrentUICulture = new CultureInfo(code);
        RightToLeft = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;

        var localizer = GetLocalizer();
        Text = localizer[ReferenceKeys.APPLICATION_TITLE];
        _switchCultureLabel.Text = localizer[ReferenceKeys.SWITCH_CULTURE_ACTION];
        _catalogHeadingLabel.Text = localizer[ReferenceKeys.CATALOG_HEADING];
        _quantityLabel.Text = localizer[ReferenceKeys.QUANTITY_FIELD];
        _placeOrderButton.Text = localizer[ReferenceKeys.ADD_TO_ORDER_ACTION];

        var selectedBookId = _selectedBook?.Id;
        PopulateCatalog(localizer, selectedBookId);
        ReloadDetails(localizer);

        if(_showingOrderMessage && _lastOrder is not null) {
            _messageLabel.Text = _messageLocalizer.LocalizeAsync
                                    (
                                        ReferenceKeys.ORDER_PLACED_MESSAGE,
                                        CreateOrderArguments(_lastOrder),
                                        CultureInfo.CurrentUICulture
                                    ).AsTask()
                                     .GetAwaiter()
                                     .GetResult() ?? string.Empty;
        }
    }

    /// <summary>
    /// Performs the populate catalog operation.
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    /// <param name="selectedBookId">The selected book id.</param>
    private void PopulateCatalog(IStringLocalizer<PageResources> localizer, string? selectedBookId) {
        _catalogListBox.Items.Clear();

        foreach(var book in BookCatalog.All) {
            _catalogListBox.Items.Add(new CatalogItem(book, localizer[book.TitleKey].Value));
        }

        if(_catalogListBox.Items.Count > 0) {
            var selectedIndex = 0;
            if(selectedBookId is not null) {
                for(var index = 0; index < _catalogListBox.Items.Count; index++) {
                    if(_catalogListBox.Items[index] is CatalogItem item && item.Book.Id == selectedBookId) {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            _catalogListBox.SelectedIndex = selectedIndex;
        }
    }

    /// <summary>
    /// Performs the reload details operation.
    /// </summary>
    /// <param name="localizer">The localizer.</param>
    private void ReloadDetails(IStringLocalizer<PageResources> localizer) {
        if(_selectedBook is null) {
            _detailsLabel.Text = string.Empty;
            return;
        }

        _detailsLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0}: {1}" + Environment.NewLine + "{2}: {3}" + Environment.NewLine + "{4}: {5:C}",
            localizer[ReferenceKeys.FIELD_AUTHOR],
            _selectedBook.Author,
            localizer[ReferenceKeys.FIELD_DESCRIPTION],
            localizer[_selectedBook.DescriptionKey],
            localizer[ReferenceKeys.FIELD_PRICE],
            _selectedBook.Price);
    }

    /// <summary>
    /// Gets localizer.
    /// </summary>
    /// <returns>The localizer result.</returns>
    private IStringLocalizer<PageResources> GetLocalizer() =>
        _services.GetRequiredService<IStringLocalizer<PageResources>>();

    /// <summary>
    /// Handles the culture changed notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The command-line arguments.</param>
    private void OnCultureChanged(object? sender, EventArgs args) {
        if(_cultureComboBox.SelectedItem is string code && code != CultureInfo.CurrentUICulture.Name) {
            ApplyCulture(code);
        }
    }

    /// <summary>
    /// Handles the catalog selection changed notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The command-line arguments.</param>
    private void OnCatalogSelectionChanged(object? sender, EventArgs args) {
        if(_catalogListBox.SelectedItem is CatalogItem item) {
            _selectedBook = item.Book;
            ReloadDetails(GetLocalizer());
        }
    }

    /// <summary>
    /// Handles the place order clicked notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The command-line arguments.</param>
    private void OnPlaceOrderClicked(object? sender, EventArgs args) => _ = PlaceOrderAsync();

    /// <summary>
    /// Performs the place order operation.
    /// </summary>
    private async Task PlaceOrderAsync() {
        var request = new CreateOrderRequest {
            BookId = _selectedBook?.Id,
            Quantity = (int)_quantityControl.Value
        };

        var validationContext = new ValidationContext(request, _services, null);
        var validationResults = new List<ValidationResult>();
        if(!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true)) {
            _showingOrderMessage = false;
            _messageLabel.Text = string.Join(
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
            _messageLabel.Text = await _messageLocalizer.LocalizeAsync(
                ReferenceKeys.ORDER_PLACED_MESSAGE,
                CreateOrderArguments(result.Order),
                CultureInfo.CurrentUICulture) ?? string.Empty;
            _quantityControl.Value = OrderLimits.MIN_QUANTITY;
            return;
        }

        if(result.ErrorMessageKey is not null) {
            _showingOrderMessage = false;
            var arguments = result.ErrorMessageArguments
                .Select((argument, index) => (Key: index.ToString(CultureInfo.InvariantCulture), Value: argument))
                .ToDictionary(static entry => entry.Key, static entry => entry.Value);
            _messageLabel.Text = await _messageLocalizer.LocalizeAsync(result.ErrorMessageKey, arguments) ?? string.Empty;
        }
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
    /// Represents a catalog item.
    /// </summary>
    /// <param name="Book">The book.</param>
    /// <param name="Title">The title.</param>
    private sealed record CatalogItem(Book Book, string Title) {
        /// <inheritdoc/>
        public override string ToString() => $"{Title} — {Book.Author}";
    }
}
