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
/// Registration helpers shared by the desktop and mobile host integrations.
/// </summary>
internal static class HostCultureServiceCollectionExtensions {
    /// <summary>
    /// Registers the host culture service and a no-op culture store. A host package should
    /// register its platform store before calling this method.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional options delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddHostCulture(
        this IServiceCollection services,
        Action<HostCultureOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        if(configure is not null) {
            var options = new HostCultureOptions();
            configure(options);
            services.TryAddSingleton(options);
        }

        services.TryAddSingleton<HostCultureOptions>(_ => new HostCultureOptions());
        services.TryAddSingleton<IHostCultureStore>(_ => new NoOpHostCultureStore());
        services.TryAddSingleton<HostCultureService>();
        return services;
    }

    private sealed class NoOpHostCultureStore : IHostCultureStore {
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<string?>(null);

        public ValueTask WriteAsync(string cultureName, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask ClearAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
