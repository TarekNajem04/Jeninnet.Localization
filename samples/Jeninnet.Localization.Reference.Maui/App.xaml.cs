//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Maui;

/// <summary>
/// The MAUI host application.
/// </summary>
public partial class App : Application {
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="services">The host service provider.</param>
    public App(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<MainPage>());
}
