//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core.Extensions;

/// <summary>
/// Registers all localization services.
/// </summary>
public static class JeninnetLocalizationExtensions {
    /// <summary>
    /// Adds the default localization services to the service collection.
    /// </summary>
    /// <remarks>
    /// A default <see cref="Jeninnet.Localization.Abstractions.Options.LocalizationOptions"/> instance is
    /// registered using <c>TryAddSingleton</c>, so an explicitly pre-registered options instance wins.
    /// Use the configuring overload to declare the localization options explicitly. The same call
    /// registers the store-backed <see cref="IStringLocalizerFactory"/> and the standard
    /// <see cref="IStringLocalizer{T}"/> wrapper; an <see cref="ILocalizationStore"/> must be registered
    /// before either Microsoft abstraction is resolved.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddLocalization(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new Jeninnet.Localization.Abstractions.Options.LocalizationOptions());
        return services.AddLocalizationCore();
    }

    /// <summary>
    /// Adds the default localization services to the service collection and configures the
    /// localization options using the specified <paramref name="configure"/> delegate.
    /// </summary>
    /// <remarks>
    /// The configured options are registered as a singleton and are the sole configuration surface
    /// for this call. If a <see cref="Jeninnet.Localization.Abstractions.Options.LocalizationOptions"/>
    /// instance is already registered in the service collection, an
    /// <see cref="InvalidOperationException"/> is thrown instead of silently creating a second,
    /// conflicting configuration path. This call also registers the store-backed
    /// <see cref="IStringLocalizerFactory"/> and the standard <see cref="IStringLocalizer{T}"/> wrapper;
    /// an <see cref="ILocalizationStore"/> must be registered before either Microsoft abstraction is resolved.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The delegate that configures the localization options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a <see cref="Jeninnet.Localization.Abstractions.Options.LocalizationOptions"/> instance is already registered in the service collection.</exception>
    /// <exception cref="System.Globalization.CultureNotFoundException">Thrown when the configured <see cref="Jeninnet.Localization.Abstractions.Options.LocalizationOptions.DefaultCulture"/> is not a recognized culture name.</exception>
    public static IServiceCollection AddLocalization(this IServiceCollection services, Action<Jeninnet.Localization.Abstractions.Options.LocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(Jeninnet.Localization.Abstractions.Options.LocalizationOptions))) {
            throw new InvalidOperationException(
                "LocalizationOptions is already registered in the service collection. Configure localization through AddLocalization(Action<LocalizationOptions>) alone, or remove the existing LocalizationOptions registration.");
        }

        var options = new Jeninnet.Localization.Abstractions.Options.LocalizationOptions();
        configure(options);

        if(!string.IsNullOrEmpty(options.DefaultCulture)) {
            _ = CultureInfo.GetCultureInfo(options.DefaultCulture);
        }

        services.AddSingleton(options);
        return services.AddLocalizationCore();
    }

    /// <summary>
    /// Adds localization core.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The localization core result.</returns>
    private static IServiceCollection AddLocalizationCore(this IServiceCollection services) {
        // Keep the Microsoft localization abstractions on the same store-backed bridge. The
        // framework generic wrapper delegates identity and lookup to this factory.
        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, CustomStringLocalizerFactory>());
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.TryAddSingleton<IMessageLocalizer, DefaultMessageLocalizer>();
        services.TryAddSingleton<IMessageFormatter, DefaultMessageFormatter>();
        services.TryAddSingleton<IMessageProvider, LocalizationProviderSelector>();
        return services;
    }
}
