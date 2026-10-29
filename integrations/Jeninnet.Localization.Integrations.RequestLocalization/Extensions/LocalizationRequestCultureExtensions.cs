//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.RequestLocalization.Extensions;

/// <summary>
/// Registers the request-localization bridge.
/// </summary>
public static class LocalizationRequestCultureExtensions {
    /// <summary>
    /// Adds the request-localization bridge: populates
    /// <see cref="RequestLocalizationOptions"/> from the registered
    /// <see cref="ILocalizationStore"/> (or the explicit overrides) and installs the
    /// served-culture provider composition (route → query string → cookie →
    /// <c>Accept-Language</c>). The host keeps calling the framework
    /// <c>app.UseRequestLocalization()</c> to add the middleware.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One registration call yields a working middleware configuration: served cultures and
    /// the default request culture resolve deterministically —
    /// explicit override, else store discovery with the default-culture union, else the
    /// default culture alone when no store is registered or the store is unreachable. A
    /// store that reports zero cultures fails fast with an actionable error. No ambient
    /// culture is mutated by the bridge.
    /// </para>
    /// <para>
    /// The bridge replaces <see cref="RequestLocalizationOptions.RequestCultureProviders"/>
    /// with the served-culture composition; a configure delegate registered after this call
    /// can adjust it further.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="CultureNotFoundException">Thrown when a configured explicit culture name is not recognized.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured route culture key is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the bridge is already registered in the service collection.</exception>
    public static IServiceCollection AddLocalizationRequestCultures(this IServiceCollection services, Action<LocalizationRequestCultureOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(LocalizationRequestCultureOptions))) {
            throw new InvalidOperationException(
                $"Request-culture localization is already registered in the service collection. Configure it through a single {nameof(AddLocalizationRequestCultures)} call.");
        }

        var options = new LocalizationRequestCultureOptions();
        configure?.Invoke(options);
        ValidateOptions(options);

        services.AddOptions();
        services.AddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RequestLocalizationOptions>, LocalizationRequestCultureOptionsSetup>());

        return services;
    }

    /// <summary>
    /// Validates options.
    /// </summary>
    /// <param name="options">The options instance.</param>
    private static void ValidateOptions(LocalizationRequestCultureOptions options) {
        if(!string.IsNullOrEmpty(options.DefaultCulture)) {
            _ = CultureInfo.GetCultureInfo(options.DefaultCulture);
        }

        foreach(var culture in options.SupportedCultures) {
            _ = CultureInfo.GetCultureInfo(culture);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.RouteCultureKey);
    }
}
