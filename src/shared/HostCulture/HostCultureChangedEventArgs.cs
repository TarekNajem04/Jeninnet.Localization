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
/// Describes a culture change applied by <see cref="HostCultureService"/>.
/// </summary>
public sealed class HostCultureChangedEventArgs : EventArgs {
    /// <summary>
    /// Initializes a new instance of the <see cref="HostCultureChangedEventArgs"/> class.
    /// </summary>
    /// <param name="oldCulture">The previously applied culture, or <see langword="null"/>.</param>
    /// <param name="newCulture">The newly applied culture.</param>
    public HostCultureChangedEventArgs(CultureInfo? oldCulture, CultureInfo newCulture) {
        ArgumentNullException.ThrowIfNull(newCulture);
        OldCulture = oldCulture;
        NewCulture = newCulture;
    }

    /// <summary>
    /// Gets the previously applied culture, or <see langword="null"/> when this was the first application.
    /// </summary>
    public CultureInfo? OldCulture { get; }

    /// <summary>
    /// Gets the newly applied culture.
    /// </summary>
    public CultureInfo NewCulture { get; }
}
