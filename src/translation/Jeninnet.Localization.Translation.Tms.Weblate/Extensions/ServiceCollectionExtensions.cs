//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Weblate.Extensions;

/// <summary>
/// Provides extension methods for registering the Weblate TMS connector.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the Weblate TMS connector to the service collection as the single
    /// <see cref="ITmsConnector"/> and validates the provided configuration
    /// immediately. Registers an <c>IHttpClientFactory</c> and creates the connector
    /// as a singleton through that factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The action to configure <see cref="WeblateConnectorOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a configured option value is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="WeblateConnectorOptions"/> are incomplete.</exception>
    public static IServiceCollection AddWeblateTmsConnector(
        this IServiceCollection services,
        Action<WeblateConnectorOptions> configure
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new WeblateConnectorOptions();
        configure(options);
        options.Validate();

        services.Configure(configure);
        AddConnectorWithHttpClient(services);

        return services;
    }

    /// <summary>
    /// Adds the Weblate TMS connector to the service collection without configuring
    /// options. Options must be configured through the standard Options pipeline; the
    /// connector validates the resolved options at construction.
    /// Registers an <c>IHttpClientFactory</c> and creates the connector as a singleton
    /// through that factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddWeblateTmsConnector(
        this IServiceCollection services
    ) {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<WeblateConnectorOptions>();
        AddConnectorWithHttpClient(services);

        return services;
    }

    /// <summary>
    /// Adds connector with http client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void AddConnectorWithHttpClient(IServiceCollection services) {
        services.AddHttpClient();
        services.AddSingleton<ITmsConnector>(sp => new WeblateConnector(
            sp.GetRequiredService<IOptions<WeblateConnectorOptions>>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WeblateConnector))));
    }
}
