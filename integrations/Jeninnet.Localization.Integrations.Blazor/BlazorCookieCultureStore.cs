//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Persists the selected culture in the RequestLocalization cookie via browser
/// JavaScript.
/// </summary>
/// <remarks>
/// Values use the canonical <see cref="CookieRequestCultureProvider.MakeCookieValue"/> /
/// <see cref="CookieRequestCultureProvider.ParseCookieValue"/> encoding, so the standard
/// <see cref="CookieRequestCultureProvider"/> reads exactly what this store writes — that
/// is what keeps the Server culture switching (persist + reload) working across the
/// WebSocket upgrade.
/// </remarks>
public sealed class BlazorCookieCultureStore : IBlazorCultureStore {
    private readonly BlazorCultureJsInterop _jsInterop;
    private readonly BlazorLocalizationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorCookieCultureStore"/> class.
    /// </summary>
    /// <param name="jsInterop">The browser storage bridge.</param>
    /// <param name="options">The integration options (cookie name and lifetime).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsInterop"/> or <paramref name="options"/> is null.</exception>
    public BlazorCookieCultureStore(BlazorCultureJsInterop jsInterop, BlazorLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(jsInterop);
        ArgumentNullException.ThrowIfNull(options);

        _jsInterop = jsInterop;
        _options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) {
        var raw = await _jsInterop.ReadCookieAsync(_options.CultureCookieName, cancellationToken).ConfigureAwait(false);
        if(string.IsNullOrEmpty(raw)) {
            return null;
        }

        var parsed = CookieRequestCultureProvider.ParseCookieValue(raw);
        if(parsed is { UICultures.Count: > 0 }) {
            return parsed.UICultures[0].Value;
        }

        return parsed is { Cultures.Count: > 0 } ? parsed.Cultures[0].Value : null;
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(culture);

        var value = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
        return _jsInterop.WriteCookieAsync(_options.CultureCookieName, value, DateTimeOffset.UtcNow.Add(_options.CultureCookieLifetime), cancellationToken);
    }
}
