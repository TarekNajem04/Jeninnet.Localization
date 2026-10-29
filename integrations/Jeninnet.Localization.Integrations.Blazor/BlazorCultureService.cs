//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Owns the active culture for a Blazor host: circuit-scoped
/// (<see cref="BlazorLocalizationMode.Server"/>) or static
/// (<see cref="BlazorLocalizationMode.Wasm"/>).
/// </summary>
/// <remarks>
/// <para>
/// In <see cref="BlazorLocalizationMode.Server"/> mode the service is registered scoped
/// (one instance per circuit) and is the circuit's single culture authority. Ambient
/// culture is never mutated — concurrent circuits cannot bleed culture into each other.
/// The circuit's culture is seeded by <see cref="BlazorCircuitCultureHandler"/> from the
/// request culture at circuit open; switching persists through <see cref="IBlazorCultureStore"/>
/// and reloads the page so the request pipeline applies the new culture to the
/// server-rendered content.
/// </para>
/// <para>
/// In <see cref="BlazorLocalizationMode.Wasm"/> mode the service is registered singleton
/// (single-user process) and applying a culture sets
/// <see cref="CultureInfo.DefaultThreadCurrentCulture"/>,
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>,
/// <see cref="CultureInfo.CurrentCulture"/>, and <see cref="CultureInfo.CurrentUICulture"/>,
/// then raises <see cref="CultureChanged"/> so components re-render.
/// </para>
/// </remarks>
public sealed class BlazorCultureService {
    private readonly BlazorLocalizationOptions _options;
    private readonly IBlazorCultureStore _store;
    private readonly BlazorCultureJsInterop? _jsInterop;
    private CultureInfo? _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazorCultureService"/> class.
    /// </summary>
    /// <param name="options">The integration options.</param>
    /// <param name="store">The host-wired culture store (a no-op store when the host registered none).</param>
    /// <param name="jsInterop">The browser bridge used for language detection, or <see langword="null"/> when detection is not available.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="store"/> is null.</exception>
    public BlazorCultureService(BlazorLocalizationOptions options, IBlazorCultureStore store, BlazorCultureJsInterop? jsInterop = null) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);

        _options = options;
        _store = store;
        _jsInterop = jsInterop;
    }

    /// <summary>
    /// Raised after a culture is applied through <see cref="SetCultureAsync"/> or
    /// <see cref="InitializeAsync"/>; components subscribe to re-render.
    /// </summary>
    public event EventHandler<BlazorCultureChangedEventArgs>? CultureChanged;

    /// <summary>
    /// Gets the applied culture name, or <see langword="null"/> when no culture has been
    /// applied yet (request/ambient culture applies instead).
    /// </summary>
    public string? Culture => _culture?.Name;

    /// <summary>
    /// Gets the applied culture; falls back to <see cref="BlazorLocalizationOptions.DefaultCulture"/>
    /// or the ambient UI culture when none was applied.
    /// </summary>
    public CultureInfo CultureInfo => _culture ?? ResolveFallbackCulture();

    /// <summary>
    /// Applies a culture, persists it through the wired store, and raises
    /// <see cref="CultureChanged"/>.
    /// </summary>
    /// <param name="cultureName">The culture name to apply.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the culture is applied and persisted.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultureName"/> is null or empty, or not in <see cref="BlazorLocalizationOptions.SupportedCultures"/> when that list is non-empty.</exception>
    /// <exception cref="CultureNotFoundException">Thrown when <paramref name="cultureName"/> is not a recognized culture.</exception>
    public async Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) {
        var culture = ResolveCulture(cultureName);
        ApplyCulture(culture);
        await _store.WriteAsync(culture.Name, cancellationToken).ConfigureAwait(false);
        OnCultureChanged(culture);
    }

    /// <summary>
    /// Restores the culture persisted in the wired store, when it differs from the applied
    /// culture; when no stored choice applies and <see cref="BlazorLocalizationOptions.DetectBrowserLanguage"/>
    /// is enabled (static <see cref="BlazorLocalizationMode.Wasm"/> scope only), falls back
    /// to the browser language: <c>navigator.languages</c> (then
    /// <c>navigator.language</c>) mapped to <see cref="BlazorLocalizationOptions.SupportedCultures"/>
    /// — exact tag first, then the browser tag's parent chain. Detection is read-only
    /// (never persisted) and best-effort: interop unavailability means no detection,
    /// never an exception. Hosts call this once after the first render (JS interop is
    /// not available before then in client-side hosts).
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns><see langword="true"/> when a stored or detected culture was applied; otherwise <see langword="false"/>.</returns>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default) {
        var stored = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if(!string.IsNullOrEmpty(stored)) {
            if(string.Equals(stored, _culture?.Name, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if(TryResolveCulture(stored, out var storedCulture)) {
                ApplyCulture(storedCulture);
                OnCultureChanged(storedCulture);
                return true;
            }
        }

        var detected = await DetectBrowserCultureAsync(cancellationToken).ConfigureAwait(false);
        if(detected is null || string.Equals(detected.Name, _culture?.Name, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        ApplyCulture(detected);
        OnCultureChanged(detected);
        return true;
    }

    /// <summary>
    /// Seeds the circuit culture from the request culture feature. Lenient by design: an
    /// unsupported or unrecognized request culture is ignored (the middleware already
    /// resolved it against the supported set), so circuit bootstrap can never fail a
    /// connection over a culture name.
    /// </summary>
    /// <param name="requestCultureFeature">The request culture feature, or <see langword="null"/>.</param>
    internal void ApplyRequestCulture(IRequestCultureFeature? requestCultureFeature) {
        var name = requestCultureFeature?.RequestCulture.UICulture.Name;
        if(string.IsNullOrEmpty(name) || !TryResolveCulture(name, out var culture)) {
            return;
        }

        ApplyCulture(culture);
    }

    /// <summary>
    /// Applies culture.
    /// </summary>
    /// <param name="culture">The culture.</param>
    private void ApplyCulture(CultureInfo culture) {
        _culture = culture;
        if(_options.Mode == BlazorLocalizationMode.Wasm) {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }

    /// <summary>
    /// Detects the culture from the browser language: static scope only,
    /// opt-in, read-only, and best-effort (interop unavailability or an empty list means
    /// no detection; cancellation still propagates).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detected culture, or <see langword="null"/> when detection does not apply or yields no match.</returns>
    private async Task<CultureInfo?> DetectBrowserCultureAsync(CancellationToken cancellationToken) {
        if(!_options.DetectBrowserLanguage
            || _options.Mode != BlazorLocalizationMode.Wasm
            || _jsInterop is null) {
            return null;
        }

        string[]? languages;
        try {
            languages = await _jsInterop.ReadBrowserLanguagesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception) {
            return null;
        }

        return languages is null || languages.Length == 0 ? null : DetectCulture(languages);
    }

    /// <summary>
    /// Maps browser language preferences to a configured culture: for each preference in
    /// browser order, an exact tag match against <see cref="BlazorLocalizationOptions.SupportedCultures"/>
    /// first, then the preference's parent chain. With an empty supported list the first
    /// preference that resolves to a recognized culture wins. No match returns null.
    /// </summary>
    /// <param name="browserLanguages">The browser's ordered language preferences.</param>
    /// <returns>The matched culture, or <see langword="null"/> when no preference maps to a supported culture.</returns>
    private CultureInfo? DetectCulture(string[] browserLanguages) {
        for(var index = 0; index < browserLanguages.Length; index++) {
            var language = browserLanguages[index];
            if(string.IsNullOrWhiteSpace(language)) {
                continue;
            }

            if(_options.SupportedCultures.Count == 0) {
                if(TryResolveCulture(language, out var resolved)) {
                    return resolved;
                }

                continue;
            }

            var match = FindSupportedMatch(language);
            if(match is not null && TryResolveCulture(match, out var matched)) {
                return matched;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds supported match.
    /// </summary>
    /// <param name="browserLanguage">The browser language.</param>
    /// <returns>The supported match result.</returns>
    private string? FindSupportedMatch(string browserLanguage) {
        for(var index = 0; index < _options.SupportedCultures.Count; index++) {
            if(string.Equals(_options.SupportedCultures[index], browserLanguage, StringComparison.OrdinalIgnoreCase)) {
                return _options.SupportedCultures[index];
            }
        }

        if(!TryGetCulture(browserLanguage, out var browser)) {
            return null;
        }

        var parent = browser.Parent;
        while(parent is not null && !string.IsNullOrEmpty(parent.Name) && !parent.Equals(CultureInfo.InvariantCulture)) {
            for(var index = 0; index < _options.SupportedCultures.Count; index++) {
                if(string.Equals(_options.SupportedCultures[index], parent.Name, StringComparison.OrdinalIgnoreCase)) {
                    return _options.SupportedCultures[index];
                }
            }

            parent = parent.Parent;
        }

        return null;
    }

    /// <summary>
    /// Attempts to get culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private static bool TryGetCulture(string cultureName, [NotNullWhen(true)] out CultureInfo? culture) {
        culture = null;
        try {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch(ArgumentException) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Handles the culture changed notification.
    /// </summary>
    /// <param name="culture">The culture.</param>
    private void OnCultureChanged(CultureInfo culture) =>
        CultureChanged?.Invoke(this, new BlazorCultureChangedEventArgs(culture.Name, culture));

    /// <summary>
    /// Resolves fallback culture.
    /// </summary>
    /// <returns>The fallback culture result.</returns>
    private CultureInfo ResolveFallbackCulture() =>
        string.IsNullOrEmpty(_options.DefaultCulture)
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(_options.DefaultCulture);

    /// <summary>
    /// Resolves culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <returns>The culture result.</returns>
    private CultureInfo ResolveCulture(string cultureName) {
        ArgumentException.ThrowIfNullOrEmpty(cultureName);
        if(!IsSupported(cultureName)) {
            throw new ArgumentException(
                $"Culture '{cultureName}' is not in {nameof(BlazorLocalizationOptions)}.{nameof(BlazorLocalizationOptions.SupportedCultures)}.",
                nameof(cultureName));
        }

        return CultureInfo.GetCultureInfo(cultureName);
    }

    /// <summary>
    /// Attempts to resolve culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private bool TryResolveCulture(string cultureName, [NotNullWhen(true)] out CultureInfo? culture) {
        culture = null;
        if(!IsSupported(cultureName)) {
            return false;
        }

        try {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch(CultureNotFoundException) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the value is supported.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private bool IsSupported(string cultureName) {
        if(_options.SupportedCultures.Count == 0) {
            return true;
        }

        for(var index = 0; index < _options.SupportedCultures.Count; index++) {
            if(string.Equals(_options.SupportedCultures[index], cultureName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// The no-op culture store used when the host registered none: the culture choice stays in
/// process memory only.
/// </summary>
internal sealed class NullBlazorCultureStore : IBlazorCultureStore {
    /// <inheritdoc/>
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc/>
    public ValueTask WriteAsync(string culture, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
