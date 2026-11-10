//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Caching.Extensions;

/// <summary>
/// Provides extension methods for registering message-provider cache decorators.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds an in-memory cached message provider to the service collection as the canonical
    /// <see cref="IInnerMessageProvider"/>. The decorator is registered as a singleton wrapping
    /// the provider produced by <paramref name="innerFactory"/>; both should live for the
    /// process lifetime. Core's exactly-one provider selector delegates to it when it is the
    /// single registered provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="innerFactory">The factory producing the inner message provider.</param>
    /// <param name="configure">An optional action to configure <see cref="MessageProviderCacheOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="innerFactory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    public static IServiceCollection AddCachedMessageProvider(
        this IServiceCollection services,
        Func<IServiceProvider, IInnerMessageProvider> innerFactory,
        Action<MessageProviderCacheOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(innerFactory);

        var options = new MessageProviderCacheOptions();
        configure?.Invoke(options);
        options.Validate();

        return services.AddSingleton<IInnerMessageProvider>(sp => new CachedMessageProvider(innerFactory(sp), Microsoft.Extensions.Options.Options.Create(options)));
    }

    /// <summary>
    /// Adds an in-memory cached message provider wrapping a concrete inner instance to the
    /// service collection as the canonical <see cref="IInnerMessageProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="configure">An optional action to configure <see cref="MessageProviderCacheOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="inner"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    public static IServiceCollection AddCachedMessageProvider(
        this IServiceCollection services,
        IInnerMessageProvider inner,
        Action<MessageProviderCacheOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(inner);

        var options = new MessageProviderCacheOptions();
        configure?.Invoke(options);
        options.Validate();

        return services.AddSingleton<IInnerMessageProvider>(_ => new CachedMessageProvider(inner, Microsoft.Extensions.Options.Options.Create(options)));
    }

    /// <summary>
    /// Adds a distributed-cached message provider to the service collection as the canonical
    /// <see cref="IInnerMessageProvider"/>. The <see cref="IDistributedCache"/> is resolved
    /// from the service provider. The decorator is registered as a singleton; the inner
    /// provider should live for the process lifetime. Core's exactly-one provider selector
    /// delegates to it when it is the single registered provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="innerFactory">The factory producing the inner message provider.</param>
    /// <param name="configure">An optional action to configure <see cref="MessageProviderCacheOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="innerFactory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    public static IServiceCollection AddDistributedCachedMessageProvider(
        this IServiceCollection services,
        Func<IServiceProvider, IInnerMessageProvider> innerFactory,
        Action<MessageProviderCacheOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(innerFactory);

        var options = new MessageProviderCacheOptions();
        configure?.Invoke(options);
        options.Validate();

        return services.AddSingleton<IInnerMessageProvider>(sp => new DistributedCachedMessageProvider(
            innerFactory(sp),
            sp.GetRequiredService<IDistributedCache>(),
            Microsoft.Extensions.Options.Options.Create(options)));
    }

    /// <summary>
    /// Adds a distributed-cached message provider wrapping a concrete inner instance to the
    /// service collection as the canonical <see cref="IInnerMessageProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="inner">The inner message provider to cache.</param>
    /// <param name="cache">The distributed cache backing the decorator.</param>
    /// <param name="configure">An optional action to configure <see cref="MessageProviderCacheOptions"/>.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/>, <paramref name="inner"/>, or <paramref name="cache"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the configured <see cref="MessageProviderCacheOptions"/> are invalid.</exception>
    public static IServiceCollection AddDistributedCachedMessageProvider(
        this IServiceCollection services,
        IInnerMessageProvider inner,
        IDistributedCache cache,
        Action<MessageProviderCacheOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(cache);

        var options = new MessageProviderCacheOptions();
        configure?.Invoke(options);
        options.Validate();

        return services.AddSingleton<IInnerMessageProvider>(_ => new DistributedCachedMessageProvider(inner, cache, Microsoft.Extensions.Options.Options.Create(options)));
    }
}
