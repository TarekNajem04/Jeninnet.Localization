//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.BlazorHybrid;

/// <summary>
/// The host-level composition entry point for the reference Blazor Hybrid application.
/// </summary>
public static class ReferenceBlazorHybridComposition {
    /// <summary>
    /// Registers the reference localization stack consumed by the Blazor Hybrid root components
    /// plus the static-scope Jeninnet Blazor localization integration (local-storage-persisted
    /// culture). The provider may be selected through the <c>ReferenceLocalization__Provider</c>
    /// environment variable, which the optional <paramref name="configure"/> callback can still
    /// override.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReferenceBlazorHybrid(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWindowsFormsBlazorWebView();
        services.AddReferenceLocalizationProvider(options => {
            var provider = Environment.GetEnvironmentVariable("ReferenceLocalization__Provider");
            if(!string.IsNullOrWhiteSpace(provider)) {
                options.Provider = provider;
            }

            configure?.Invoke(options);
        });

        services.AddBlazorLocalization(options => {
            options.Mode = BlazorLocalizationMode.Wasm;
            options.DefaultCulture = ReferenceCultures.DEFAULT_CULTURE;
            foreach(var culture in ReferenceCultures.SupportedCultures) {
                options.SupportedCultures.Add(culture);
            }
        });

        // WebView2 supplies IJSRuntime from the BlazorWebView scope. The static singleton
        // registrations work in WebAssembly, but a Blazor Hybrid service must be resolved from
        // that scope or its JavaScript calls run outside the WebView context.
        services.AddScoped<BlazorCultureService>();
        services.AddScoped<IBlazorCultureStore, BlazorLocalStorageCultureStore>();

        return services;
    }

    /// <summary>
    /// Builds the Blazor Hybrid host service provider from a fresh service collection.
    /// </summary>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The built service provider.</returns>
    public static ServiceProvider BuildServiceProvider(Action<ReferenceLocalizationOptions>? configure = null) {
        var services = new ServiceCollection();
        services.AddReferenceBlazorHybrid(configure);
        return services.BuildServiceProvider();
    }
}
