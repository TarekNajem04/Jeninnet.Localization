//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Avalonia;

/// <summary>
/// The Avalonia host window. It applies the selected culture to the window and refreshes the
/// displayed text from the localization pipeline.
/// </summary>
public partial class MainWindow : Window {
    private readonly HostCultureService _culture;
    private readonly HostSampleText _text;
    private bool _initializing;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="culture">The host culture service.</param>
    /// <param name="text">The sample text resolver.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> or <paramref name="text"/> is null.</exception>
    public MainWindow(HostCultureService culture, HostSampleText text) {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(text);

        _culture = culture;
        _text = text;
        InitializeComponent();

        CultureSelector.ItemsSource = HostSampleCultures.Supported;
        Opened += OnOpenedAsync;
    }

    private async void OnOpenedAsync(object? sender, EventArgs e) {
        _initializing = true;
        await _culture.InitializeAsync();
        CultureSelector.SelectedItem = _culture.Culture ?? HostSampleCultures.DEFAULT_CULTURE;
        _initializing = false;
        await RefreshAsync();
    }

    private async void OnCultureChangedAsync(object? sender, SelectionChangedEventArgs e) {
        if(_initializing || CultureSelector.SelectedItem is not string cultureName) {
            return;
        }

        await _culture.SetCultureAsync(cultureName);
        await RefreshAsync();
    }

    private async Task RefreshAsync() {
        CultureInfo.CurrentCulture = _culture.CultureInfo;
        CultureInfo.CurrentUICulture = _culture.CultureInfo;
        FlowDirection = _culture.ToFlowDirection();
        TitleText.Text = await _text.GetAsync(HostSampleKeys.APP_TITLE);
        WelcomeText.Text = await _text.GetAsync(HostSampleKeys.WELCOME);
        CultureLabel.Text = await _text.GetAsync(HostSampleKeys.CHOOSE_CULTURE);

        var directionKey = _culture.IsRightToLeft
            ? HostSampleKeys.RIGHT_TO_LEFT
            : HostSampleKeys.LEFT_TO_RIGHT;
        var currentCultureLabel = await _text.GetAsync(HostSampleKeys.CURRENT_CULTURE);
        var directionLabel = await _text.GetAsync(HostSampleKeys.DIRECTION);
        var directionValue = await _text.GetAsync(directionKey);
        StatusText.Text = $"{currentCultureLabel}: {_culture.CultureInfo.DisplayName}  |  {directionLabel}: {directionValue}";
    }
}
