//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Mvc;

/// <summary>
/// The host-level composition entry point for the reference MVC application.
/// </summary>
public static class ReferenceMvcComposition {
    /// <summary>
    /// Registers the reference MVC localization stack, MVC controllers and views, and the shared
    /// display-metadata, validation, and view-scoped localization integration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReferenceMvc(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddReferenceLocalizationProvider(configure);
        services.AddControllersWithViews();
        services.AddMvcLocalizedValidation();
        services.AddViewMessageLocalizer();

        return services;
    }
}
