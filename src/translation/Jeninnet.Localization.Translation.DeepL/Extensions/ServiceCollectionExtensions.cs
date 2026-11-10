//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.DeepL.Extensions;

/// <summary>
/// Provides extension methods for registering the DeepL translation localization Provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the DeepL translation localization Provider to the service collection as a canonical
    /// <see cref="IInnerMessageProvider"/> and validates the provided configuration immediately.
    /// Registers an <c>IHttpClientFactory</c> and creates the Provider as a singleton through that
    /// factory so the in-memory cache and in-flight coalescing state persist across resolutions.
    /// Core's exactly-one provider selector delegates to it when it is the single registered Provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The action to configure <see cref="DeepLTranslationOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a configured option value is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="DeepLTranslationOptions"/> are incomplete.</exception>
    public static IServiceCollection AddDeepLTranslationLocalization(
        this IServiceCollection services,
        Action<DeepLTranslationOptions> configure
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DeepLTranslationOptions();
        configure(options);
        options.Validate();

        services.Configure(configure);
        AddProviderWithHttpClient(services);

        return services;
    }

    /// <summary>
    /// Adds the DeepL translation localization Provider to the service collection without
    /// configuring options. Options must be configured through the standard Options pipeline; the
    /// Provider validates the resolved options at construction.
    /// Registers an <c>IHttpClientFactory</c> and creates the Provider as a singleton through that
    /// factory so the in-memory cache and in-flight coalescing state persist across resolutions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddDeepLTranslationLocalization(
        this IServiceCollection services
    ) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DeepLTranslationOptions>();
        AddProviderWithHttpClient(services);

        return services;
    }

    /// <summary>
    /// Adds provider with http client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void AddProviderWithHttpClient(IServiceCollection services) {
        services.AddHttpClient();
        services.AddSingleton<IInnerMessageProvider>(sp => new DeepLTranslationProvider(
            sp.GetRequiredService<IOptions<DeepLTranslationOptions>>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DeepLTranslationProvider))));
    }
}
