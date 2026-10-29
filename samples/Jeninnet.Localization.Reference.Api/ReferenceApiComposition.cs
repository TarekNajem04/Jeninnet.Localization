//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Api;

/// <summary>
/// The host-level composition entry point for the reference API application.
/// </summary>
public static class ReferenceApiComposition {
    /// <summary>
    /// Registers the reference API localization stack, MVC controllers, and the shared BookStore domain services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReferenceApi(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddReferenceLocalizationProvider(configure);
        services.AddControllers();

        return services;
    }
}
