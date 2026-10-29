//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.BlazorHybrid;

/// <summary>
/// The main reference BookStore window hosting an embedded WebView2 control. Demonstrates the
/// canonical store-backed pipeline over a Blazor Hybrid surface with in-app culture switching.
/// </summary>
public sealed class MainForm : Form {
    private readonly BlazorWebView _webView;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    /// <param name="services">The host service provider.</param>
    public MainForm(IServiceProvider services) {
        ArgumentNullException.ThrowIfNull(services);

        var localizer = services.GetRequiredService<IStringLocalizer<PageResources>>();
        Text = localizer[ReferenceKeys.APPLICATION_TITLE];
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1100, 780);

        _webView = new BlazorWebView {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html",
            Services = services
        };
        _webView.RootComponents.Add<App>("#app");

        Controls.Add(_webView);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if(disposing) {
            _webView.Dispose();
        }

        base.Dispose(disposing);
    }
}
