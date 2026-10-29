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
/// Persists the user's culture choice for one host process.
/// </summary>
public interface IHostCultureStore {
    /// <summary>
    /// Reads the persisted culture name.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The persisted culture name, or <see langword="null"/> when nothing is stored.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the culture name.
    /// </summary>
    /// <param name="cultureName">The culture name to persist.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the value is persisted.</returns>
    ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the persisted culture choice, if one exists.
    /// </summary>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when the value is removed.</returns>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
