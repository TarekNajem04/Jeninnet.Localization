//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Blazor.Extensions;

/// <summary>
/// Registers the Blazor localization composition.
/// </summary>
public static class BlazorLocalizationExtensions {
    /// <summary>
    /// Adds the Blazor localization composition for the mode selected in
    /// <see cref="BlazorLocalizationOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BlazorLocalizationMode.Server"/> registers the circuit-scoped
    /// <see cref="BlazorCultureService"/> and the <see cref="BlazorCircuitCultureHandler"/>
    /// that seeds each circuit from its request culture. Call it after the host's provider
    /// and Core composition; the host wires persistence by registering an
    /// <see cref="IBlazorCultureStore"/> (cookie, local storage, or host-owned); without one
    /// the culture choice stays in process memory only.
    /// </para>
    /// <para>
    /// <see cref="BlazorLocalizationMode.Wasm"/> registers the singleton static-scope
    /// culture service. When <see cref="BlazorLocalizationOptions.RequireWasmCompatibleProviders"/>
    /// is set, every already-registered <see cref="IInnerMessageProvider"/> must declare
    /// <see cref="IWasmCompatibleMessageProvider"/> or registration fails with an
    /// actionable error — so call this method after the provider registration.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured culture options are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the composition is registered twice or a registered provider cannot be verified as WASM-compatible.</exception>
    public static IServiceCollection AddBlazorLocalization(this IServiceCollection services, Action<BlazorLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(BlazorLocalizationOptions))) {
            throw new InvalidOperationException(
                $"Blazor localization is already registered in the service collection. Configure it through a single {nameof(AddBlazorLocalization)} call.");
        }

        var options = new BlazorLocalizationOptions();
        configure?.Invoke(options);

        ValidateOptions(options);
        if(options.RequireWasmCompatibleProviders) {
            ValidateWasmCompatibleProviders(services);
        }

        services.AddSingleton(options);
        services.TryAddTransient<BlazorCultureJsInterop>();
        services.TryAddSingleton<IBlazorCultureStore, NullBlazorCultureStore>();

        if(options.Mode == BlazorLocalizationMode.Server) {
            services.AddHttpContextAccessor();
            services.TryAddScoped(CreateCultureService);
            services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, BlazorCircuitCultureHandler>());
        } else {
            services.TryAddSingleton(CreateCultureService);
        }

        return services;
    }

    /// <summary>
    /// Creates the culture service with the browser bridge only when the host actually
    /// provides <see cref="IJSRuntime"/>: hosts without one keep working
    /// and language detection stays a documented no-op.
    /// </summary>
    /// <param name="provider">The service provider the culture service is built from.</param>
    /// <returns>The culture service result.</returns>
    private static BlazorCultureService CreateCultureService(IServiceProvider provider) =>
        new(
            provider.GetRequiredService<BlazorLocalizationOptions>(),
            provider.GetRequiredService<IBlazorCultureStore>(),
            provider.GetService<IJSRuntime>() is null ? null : provider.GetRequiredService<BlazorCultureJsInterop>());

    /// <summary>
    /// Adds the Blazor Server composition: circuit-scoped culture service and circuit
    /// culture handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddBlazorServerLocalization(this IServiceCollection services, Action<BlazorLocalizationOptions>? configure = null) =>
        services.AddBlazorLocalization(options => {
            configure?.Invoke(options);
            options.Mode = BlazorLocalizationMode.Server;
        });

    /// <summary>
    /// Adds the Blazor WebAssembly composition: static-scope culture service plus the
    /// WASM-compatible provider guard.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a registered provider does not declare <see cref="IWasmCompatibleMessageProvider"/>.</exception>
    public static IServiceCollection AddBlazorWasmLocalization(this IServiceCollection services, Action<BlazorLocalizationOptions>? configure = null) =>
        services.AddBlazorLocalization(options => {
            configure?.Invoke(options);
            options.Mode = BlazorLocalizationMode.Wasm;
            options.RequireWasmCompatibleProviders = true;
        });

    /// <summary>
    /// Validates options.
    /// </summary>
    /// <param name="options">The options instance.</param>
    private static void ValidateOptions(BlazorLocalizationOptions options) {
        if(!Enum.IsDefined(options.Mode)) {
            throw new ArgumentException(
                $"{nameof(BlazorLocalizationOptions)}.{nameof(BlazorLocalizationOptions.Mode)} has an undefined value.",
                nameof(options));
        }

        if(!string.IsNullOrEmpty(options.DefaultCulture)) {
            _ = CultureInfo.GetCultureInfo(options.DefaultCulture);
        }

        foreach(var culture in options.SupportedCultures) {
            _ = CultureInfo.GetCultureInfo(culture);
        }

        ArgumentException.ThrowIfNullOrEmpty(options.CultureCookieName);
        ArgumentException.ThrowIfNullOrEmpty(options.CultureStorageKey);
        if(options.CultureCookieLifetime <= TimeSpan.Zero) {
            throw new ArgumentException(
                $"{nameof(BlazorLocalizationOptions)}.{nameof(BlazorLocalizationOptions.CultureCookieLifetime)} must be positive.",
                nameof(options));
        }
    }

    /// <summary>
    /// Validates wasm compatible providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void ValidateWasmCompatibleProviders(IServiceCollection services) {
        var offenders = new List<string>();
        foreach(var descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(IInnerMessageProvider))) {
            var implementationType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
            if(implementationType is null) {
                offenders.Add("a factory-registered provider (cannot be verified)");
                continue;
            }

            if(!typeof(IWasmCompatibleMessageProvider).IsAssignableFrom(implementationType)) {
                offenders.Add(implementationType.FullName ?? implementationType.Name);
            }
        }

        if(offenders.Count == 0) {
            return;
        }

        throw new InvalidOperationException(
            $"The Blazor WebAssembly composition requires providers to declare {nameof(IWasmCompatibleMessageProvider)} " +
            "(template resolution without file, database, or network I/O). " +
            $"Rejected registration(s): {string.Join(", ", offenders)}. " +
            "Register a WASM-compatible provider (for example Jeninnet.Localization.Providers.Memory) before " +
            $"{nameof(AddBlazorWasmLocalization)}, or use {nameof(AddBlazorServerLocalization)}/static scope without the guard " +
            "for hosts where file and database access is legitimate (desktop Blazor Hybrid).");
    }
}
