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
/// Configuration shared by the desktop and mobile host integrations.
/// </summary>
public class HostCultureOptions {
    /// <summary>
    /// Gets or sets the culture applied when no persisted choice exists. Empty leaves the
    /// ambient culture unchanged.
    /// </summary>
    public string? DefaultCulture { get; set; }

    /// <summary>
    /// Gets the cultures the host accepts. Empty (the default) accepts every culture the
    /// framework recognizes.
    /// </summary>
    public IList<string> SupportedCultures { get; } = [];
}
