//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.BlazorServer;

/// <summary>
/// The host-level composition entry point for the reference Blazor Server application.
/// </summary>
public static class ReferenceBlazorComposition {
    /// <summary>
    /// Registers the reference localization stack, the Blazor Server interactive components, and
    /// the circuit-scoped Jeninnet Blazor localization integration (cookie-persisted culture).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReferenceBlazor(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddReferenceLocalizationProvider(configure);
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddBlazorServerLocalization(options => {
            options.DefaultCulture = ReferenceCultures.DEFAULT_CULTURE;
            foreach(var culture in ReferenceCultures.SupportedCultures) {
                options.SupportedCultures.Add(culture);
            }
        });
        services.AddScoped<IBlazorCultureStore, BlazorCookieCultureStore>();

        return services;
    }
}
