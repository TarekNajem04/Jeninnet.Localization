//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Composite.Extensions;

/// <summary>
/// Provides extension methods for registering the opt-in ordered composite provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the composite provider as the single <see cref="IInnerMessageProvider"/>. The inner
    /// factories run once at singleton construction, in order; the composite resolves each key
    /// against them in that order with first-exact-wins semantics. Registering another provider
    /// alongside the composite still throws at the selector boundary.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="innerFactories">The inner provider factories in resolution order (at least one).</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/>, <paramref name="innerFactories"/>, or a factory is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="innerFactories"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown at resolution when a factory returns null.</exception>
    public static IServiceCollection AddCompositeLocalization(
        this IServiceCollection services,
        params Func<IServiceProvider, IInnerMessageProvider>[] innerFactories) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(innerFactories);

        if(innerFactories.Length == 0) {
            throw new ArgumentException("The composite provider requires at least one inner provider factory.", nameof(innerFactories));
        }

        foreach(var factory in innerFactories) {
            ArgumentNullException.ThrowIfNull(factory);
        }

        services.AddSingleton<IInnerMessageProvider>(sp => {
            var inners = new IInnerMessageProvider[innerFactories.Length];
            for(var index = 0; index < innerFactories.Length; index++) {
                inners[index] = innerFactories[index](sp) ?? throw new InvalidOperationException($"Inner provider factory at index {index} returned null.");
            }

            return new CompositeMessageProvider(inners);
        });

        return services;
    }
}
