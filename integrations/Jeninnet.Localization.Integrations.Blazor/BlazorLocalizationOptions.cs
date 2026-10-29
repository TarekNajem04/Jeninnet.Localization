//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Configuration for the Blazor localization integration (registered as a singleton by
/// the <c>AddBlazor*</c> composition entry points).
/// </summary>
public sealed class BlazorLocalizationOptions {
    /// <summary>
    /// Gets or sets the culture scope mode. Defaults to <see cref="BlazorLocalizationMode.Server"/>.
    /// </summary>
    public BlazorLocalizationMode Mode { get; set; } = BlazorLocalizationMode.Server;

    /// <summary>
    /// Gets or sets the culture applied when neither a request culture nor a stored culture
    /// exists. Optional; when unset the ambient culture is used.
    /// </summary>
    public string? DefaultCulture { get; set; }

    /// <summary>
    /// Gets the cultures the culture service accepts. Empty (the default) accepts every
    /// culture name the framework recognizes.
    /// </summary>
    public IList<string> SupportedCultures { get; } = [];

    /// <summary>
    /// Gets or sets the cookie name used by <see cref="BlazorCookieCultureStore"/>.
    /// Defaults to <see cref="CookieRequestCultureProvider.DefaultCookieName"/>, the name the
    /// standard <c>CookieRequestCultureProvider</c> reads.
    /// </summary>
    public string CultureCookieName { get; set; } = CookieRequestCultureProvider.DefaultCookieName;

    /// <summary>
    /// Gets or sets the lifetime of the persisted culture cookie. Defaults to 365 days.
    /// </summary>
    public TimeSpan CultureCookieLifetime { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Gets or sets the local-storage key used by <see cref="BlazorLocalStorageCultureStore"/>.
    /// Defaults to <c>jeninnet.localization.culture</c>.
    /// </summary>
    public string CultureStorageKey { get; set; } = "jeninnet.localization.culture";

    /// <summary>
    /// Gets or sets whether <see cref="BlazorCultureService.InitializeAsync"/> detects the
    /// culture from the browser language when no stored choice applies:
    /// <c>navigator.languages</c> (falling back to <c>navigator.language</c>) is mapped to
    /// <see cref="SupportedCultures"/> — exact tag first, then the browser tag's parent
    /// chain — and the first match is applied. Defaults to <see langword="false"/> (today's
    /// behavior: stored choice → configured default). Applies to the static
    /// <see cref="BlazorLocalizationMode.Wasm"/> scope only; server mode is seeded from the
    /// resolved request culture and ignores this option. Detection is read-only (never
    /// persisted) and best-effort (interop unavailability means no detection, never an
    /// exception).
    /// </summary>
    public bool DetectBrowserLanguage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every registered
    /// <see cref="IInnerMessageProvider"/> must declare
    /// <see cref="IWasmCompatibleMessageProvider"/>. Defaults to <see langword="false"/>;
    /// <c>AddBlazorWasmLocalization</c> sets it to <see langword="true"/> so file/DB
    /// providers are rejected with an actionable error at registration time.
    /// </summary>
    public bool RequireWasmCompatibleProviders { get; set; }
}
