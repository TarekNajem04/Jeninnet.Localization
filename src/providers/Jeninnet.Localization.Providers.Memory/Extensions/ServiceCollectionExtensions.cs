//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Memory.Extensions;

/// <summary>
/// Provides extension methods for registering Memory localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds Memory localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMemoryLocalization(
        this IServiceCollection services,
        MemoryLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Messages);

        services.AddSingleton<IInnerMessageProvider>(new MemoryMessageProvider(options));

        return services;
    }

    /// <summary>
    /// Adds Memory localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="messages">The dictionary of localization messages.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMemoryLocalization(
        this IServiceCollection services,
        IReadOnlyDictionary<string, string> messages
        ) => services.AddMemoryLocalization(
            new MemoryLocalizationOptions {
                Messages = messages
            }
        );

    /// <summary>
    /// Adds Memory localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMemoryLocalization(this IServiceCollection services, Action<MemoryLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new MemoryLocalizationOptions();
        configure(options);
        return services.AddMemoryLocalization(options);
    }
}
