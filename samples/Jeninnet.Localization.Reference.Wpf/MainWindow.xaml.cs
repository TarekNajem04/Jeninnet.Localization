//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Wpf;

/// <summary>
/// The main reference BookStore window. Demonstrates lightweight MVVM consumption of the canonical
/// store-backed pipeline over a XAML view.
/// </summary>
public partial class MainWindow : Window {
    private readonly BookStoreViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The book store view model.</param>
    public MainWindow(BookStoreViewModel viewModel) {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        InitializeComponent();
        DataContext = _viewModel;
        viewModel.CurrentCulture = "en";
    }

    /// <summary>
    /// Handles the place order clicked notification.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void OnPlaceOrderClicked(object sender, RoutedEventArgs e) => _ = _viewModel.PlaceOrderAsync();
}
