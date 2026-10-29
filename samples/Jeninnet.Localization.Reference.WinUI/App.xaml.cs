//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.WinUI;

/// <summary>
/// The WinUI 3 host application.
/// </summary>
public partial class App : Application {
    private Window? _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App() {
        InitializeComponent();
        Services = WinUiHostComposition.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the host service provider.
    /// </summary>
    public static ServiceProvider Services { get; private set; } = null!;

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args) {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }
}
