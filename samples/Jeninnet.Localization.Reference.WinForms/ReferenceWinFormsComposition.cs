//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.WinForms;

/// <summary>
/// The host-level composition entry point for the reference WinForms application.
/// </summary>
public static class ReferenceWinFormsComposition {
    /// <summary>
    /// Registers the reference localization stack for the WinForms host. The provider may be
    /// selected through the <c>ReferenceLocalization__Provider</c> environment variable, which the
    /// optional <paramref name="configure"/> callback can still override.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReferenceWinForms(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddReferenceLocalizationProvider(options => {
            var provider = Environment.GetEnvironmentVariable("ReferenceLocalization__Provider");
            if(!string.IsNullOrWhiteSpace(provider)) {
                options.Provider = provider;
            }

            configure?.Invoke(options);
        });

        return services;
    }

    /// <summary>
    /// Builds the WinForms host service provider from a fresh service collection.
    /// </summary>
    /// <param name="configure">Optional provider configuration.</param>
    /// <returns>The built service provider.</returns>
    public static ServiceProvider BuildServiceProvider(Action<ReferenceLocalizationOptions>? configure = null) {
        var services = new ServiceCollection();
        services.AddReferenceWinForms(configure);
        return services.BuildServiceProvider();
    }
}
