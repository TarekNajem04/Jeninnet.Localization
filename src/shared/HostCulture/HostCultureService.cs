// Shared host-culture source. Compiled into each desktop/mobile integration package.
#if JENINNET_HOSTS_AVALONIA
namespace Jeninnet.Localization.Integrations.Avalonia;
#elif JENINNET_HOSTS_MAUI
namespace Jeninnet.Localization.Integrations.Maui;
#elif JENINNET_HOSTS_WINUI
namespace Jeninnet.Localization.Integrations.WinUI;
#else
#error Compile HostCulture sources with a JENINNET_HOSTS_* constant.
#endif

/// <summary>
/// Owns the active culture for a desktop or mobile host. The host calls the service on its UI
/// thread; the service applies the culture to the current and default threads and raises
/// <see cref="CultureChanged"/> so views can re-render.
/// </summary>
public sealed class HostCultureService {
    private readonly HostCultureOptions _options;
    private readonly IHostCultureStore _store;
    private readonly object _sync = new();
    private CultureInfo? _culture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostCultureService"/> class.
    /// </summary>
    /// <param name="options">The host culture options.</param>
    /// <param name="store">The host-wired culture store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="store"/> is null.</exception>
    public HostCultureService(HostCultureOptions options, IHostCultureStore store) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);

        _options = options;
        _store = store;
    }

    /// <summary>
    /// Raised after a culture is applied through <see cref="SetCultureAsync"/> or
    /// <see cref="InitializeAsync"/>.
    /// </summary>
    public event EventHandler<HostCultureChangedEventArgs>? CultureChanged;

    /// <summary>
    /// Gets the applied culture name, or <see langword="null"/> when no culture has been applied yet.
    /// </summary>
    public string? Culture {
        get {
            lock(_sync) {
                return _culture?.Name;
            }
        }
    }

    /// <summary>
    /// Gets the applied culture; falls back to the configured default culture or the ambient UI culture.
    /// </summary>
    public CultureInfo CultureInfo {
        get {
            lock(_sync) {
                return _culture ?? ResolveFallbackCulture();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the active culture is written right-to-left.
    /// </summary>
    public bool IsRightToLeft => CultureInfo.TextInfo.IsRightToLeft;

    /// <summary>
    /// Restores the persisted culture, or applies <see cref="HostCultureOptions.DefaultCulture"/>
    /// when no persisted choice exists.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns><see langword="true"/> when a culture was applied; otherwise <see langword="false"/>.</returns>
    public async ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default) {
        var stored = await _store.ReadAsync(cancellationToken);
        if(TryResolveCulture(stored, out var storedCulture)) {
            return ApplyIfChanged(storedCulture);
        }

        if(TryResolveCulture(_options.DefaultCulture, out var defaultCulture)) {
            return ApplyIfChanged(defaultCulture);
        }

        return false;
    }

    /// <summary>
    /// Applies and persists a culture.
    /// </summary>
    /// <param name="cultureName">The culture name to apply.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the culture is persisted and applied.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultureName"/> is null, empty, or not supported.</exception>
    /// <exception cref="CultureNotFoundException">Thrown when <paramref name="cultureName"/> is not a recognized culture.</exception>
    public async ValueTask SetCultureAsync(string cultureName, CancellationToken cancellationToken = default) {
        var culture = ResolveCulture(cultureName);
        await _store.WriteAsync(culture.Name, cancellationToken);
        var previous = ApplyCulture(culture);
        RaiseCultureChanged(previous, culture);
    }

    /// <summary>
    /// Removes the persisted culture choice and applies the configured default culture when one exists.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the persisted value is removed.</returns>
    public async ValueTask ResetAsync(CancellationToken cancellationToken = default) {
        await _store.ClearAsync(cancellationToken);
        if(TryResolveCulture(_options.DefaultCulture, out var defaultCulture)) {
            ApplyIfChanged(defaultCulture);
        }
    }

    private bool ApplyIfChanged(CultureInfo culture) {
        lock(_sync) {
            if(string.Equals(_culture?.Name, culture.Name, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }

        var previous = ApplyCulture(culture);
        RaiseCultureChanged(previous, culture);
        return true;
    }

    private CultureInfo? ApplyCulture(CultureInfo culture) {
        CultureInfo? previous;
        lock(_sync) {
            previous = _culture;
            _culture = culture;
        }

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        return previous;
    }

    private void RaiseCultureChanged(CultureInfo? previous, CultureInfo culture) =>
        CultureChanged?.Invoke(this, new HostCultureChangedEventArgs(previous, culture));

    private CultureInfo ResolveCulture(string cultureName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        var culture = CultureInfo.GetCultureInfo(cultureName.Trim());
        if(_options.SupportedCultures.Count > 0 && !IsSupported(culture.Name)) {
            throw new ArgumentException(
                $"Culture '{culture.Name}' is not in the configured supported cultures.",
                nameof(cultureName));
        }

        return culture;
    }

    private bool TryResolveCulture(string? cultureName, out CultureInfo culture) {
        culture = CultureInfo.CurrentUICulture;
        if(string.IsNullOrWhiteSpace(cultureName)) {
            return false;
        }

        try {
            culture = CultureInfo.GetCultureInfo(cultureName.Trim());
        }
        catch(CultureNotFoundException) {
            return false;
        }

        return _options.SupportedCultures.Count == 0 || IsSupported(culture.Name);
    }

    private bool IsSupported(string cultureName) =>
        _options.SupportedCultures.Any(candidate =>
            string.Equals(candidate, cultureName, StringComparison.OrdinalIgnoreCase));

    private CultureInfo ResolveFallbackCulture() {
        if(TryResolveCulture(_options.DefaultCulture, out var fallback)) {
            return fallback;
        }

        return CultureInfo.CurrentUICulture;
    }
}
