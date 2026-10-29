//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Persists the selected culture in browser local storage via JavaScript (
/// decision 2); the client-side counterpart of <see cref="BlazorCookieCultureStore"/>
/// for WebAssembly and Blazor Hybrid / WebView2 hosts.
/// </summary>
public sealed class BlazorLocalStorageCultureStore : IBlazorCultureStore {
    private readonly BlazorCultureJsInterop _jsInterop;
    private readonly BlazorLocalizationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorLocalStorageCultureStore"/> class.
    /// </summary>
    /// <param name="jsInterop">The browser storage bridge.</param>
    /// <param name="options">The integration options (storage key).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsInterop"/> or <paramref name="options"/> is null.</exception>
    public BlazorLocalStorageCultureStore(BlazorCultureJsInterop jsInterop, BlazorLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(jsInterop);
        ArgumentNullException.ThrowIfNull(options);

        _jsInterop = jsInterop;
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        _jsInterop.ReadLocalStorageAsync(_options.CultureStorageKey, cancellationToken);

    /// <inheritdoc/>
    public ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(culture);

        return _jsInterop.WriteLocalStorageAsync(_options.CultureStorageKey, culture, cancellationToken);
    }
}
