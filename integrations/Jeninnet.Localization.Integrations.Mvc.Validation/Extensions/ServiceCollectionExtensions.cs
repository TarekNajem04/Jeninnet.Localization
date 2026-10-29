//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Mvc.Validation.Extensions;

/// <summary>
/// Registers all localization services and configures validation and display metadata.
/// </summary>
public static class JeninnetLocalizationExtensions {
    /// <summary>
    /// Specifies the options for configure mvc.
    /// </summary>
    /// <param name="factory">The factory.</param>
    private sealed class ConfigureMvcOptions(IStringLocalizerFactory factory) : IConfigureOptions<MvcOptions> {
        /// <summary>
        /// Configures MVC options to include the localized validation and display metadata providers.
        /// </summary>
        /// <param name="options">The MVC options.</param>
        public void Configure(MvcOptions options) {
            options.ModelMetadataDetailsProviders.Add(new LocalizedValidationMetadataProvider(factory));
            options.ModelMetadataDetailsProviders.Add(new LocalizedDisplayMetadataProvider(factory));
        }
    }

    /// <summary>
    /// Adds MVC localized validation and display metadata services to the service collection.
    /// </summary>
    /// <remarks>
    /// Ensures Core's <see cref="CustomStringLocalizerFactory"/> is available as the
    /// <see cref="IStringLocalizerFactory"/> and ensures the standard <see cref="IStringLocalizer{T}"/>
    /// wrapper is available through DI (Dependency Injection). It also wires the localized validation and display metadata
    /// providers into <see cref="MvcOptions"/> (appended after the framework's DataAnnotations
    /// metadata provider so the localized values take precedence). This is the single documented
    /// <c>Integrations.Mvc.Validation → Core</c> dependency (§18 Rule 7).
    /// <para>
    /// Registration order matters: call this method <em>after</em> <c>AddControllers()</c>/
    /// <c>AddMvcCore()</c>. The metadata providers run in <see cref="MvcOptions.ModelMetadataDetailsProviders"/>
    /// registration order, and the localized providers must be appended last to take precedence over
    /// the framework's DataAnnotations metadata provider.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMvcLocalizedValidation(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        // Hook up IStringLocalizer
        services.Replace(ServiceDescriptor.Singleton<IStringLocalizerFactory, CustomStringLocalizerFactory>());
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));

        // For localization of DataAnnotations (e.g. RequiredAttribute) and display metadata
        services.ConfigureOptions<ConfigureMvcOptions>();

        return services;
    }

    /// <summary>
    /// Adds the HTML-safe message localizer bridge to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="HtmlMessageLocalizer"/> as the framework
    /// <see cref="IHtmlLocalizer"/> (and <see cref="HtmlMessageLocalizer{T}"/>
    /// as <see cref="IHtmlLocalizer{T}"/>), and <see cref="HtmlMessageLocalizerFactory"/>
    /// as the framework <see cref="IHtmlLocalizerFactory"/>, replacing the
    /// framework defaults so Razor views and factory-resolving components render
    /// Jeninnet templates with HTML-encoded arguments. This is an explicit opt-in:
    /// call it only when views should resolve through the Jeninnet pipeline. It
    /// requires an <see cref="Jeninnet.Localization.Abstractions.IMessageLocalizer"/>
    /// registration (Core <c>AddLocalization()</c> plus exactly one message
    /// provider); the bridge itself owns no lookup or fallback logic.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddHtmlMessageLocalizer(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Transient<IHtmlLocalizer, HtmlMessageLocalizer>());
        services.Replace(ServiceDescriptor.Transient(typeof(IHtmlLocalizer<>), typeof(HtmlMessageLocalizer<>)));
        services.Replace(ServiceDescriptor.Singleton<IHtmlLocalizerFactory, HtmlMessageLocalizerFactory>());

        return services;
    }

    /// <summary>
    /// Adds the view-scoped message localizer bridge to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="ViewMessageLocalizer"/> as the framework
    /// <see cref="IViewLocalizer"/>, replacing the framework default so Razor views
    /// resolve through the Jeninnet pipeline with view-path-scoped keys
    ///: a key resolves view-scoped first (full canonical culture
    /// fallback), then shared through the plain pipeline lookup (full canonical
    /// culture fallback). View-scoped entries are <c>msgctxt</c>-scoped keys in the
    /// configured provider; with no scoped entry the
    /// result is exactly the plain pipeline result. This is an explicit opt-in: it
    /// requires an <see cref="Jeninnet.Localization.Abstractions.IMessageLocalizer"/>
    /// registration (Core <c>AddLocalization()</c> plus exactly one message
    /// provider); the bridge itself owns no lookup or fallback logic and composes
    /// the  <see cref="HtmlMessageLocalizer"/> for HTML-safe output.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddViewMessageLocalizer(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Transient<IViewLocalizer, ViewMessageLocalizer>());

        return services;
    }
}
