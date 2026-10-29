//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for registering the EF Core resource-set store.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the EF Core resource-set store to the service collection.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="SqlDbLocalizationStore"/> as the <see cref="ILocalizationStore"/>
    /// backed by a <see cref="LocalizationDataContext"/> using SQL Server.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null or empty.</exception>
    public static IServiceCollection AddEntityFrameworkLocalizationStore(
        this IServiceCollection services,
        string connectionString
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        services.AddDbContextFactory<LocalizationDataContext>(options => options.UseSqlServer(connectionString));
        services.AddSingleton<ILocalizationStore, SqlDbLocalizationStore>();

        return services;
    }

    /// <summary>
    /// Adds the EF Core resource-set store to the service collection, reading a named
    /// connection string from configuration.
    /// </summary>
    /// <remarks>
    /// This is the documented exception to the no-<see cref="IConfiguration"/> rule (the configuration exception):
    /// the EF store registration explicitly reads a named connection string.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="connectionStringName">The name of the connection string (default "LocalizationDb").</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the named connection string is missing or empty.</exception>
    public static IServiceCollection AddEntityFrameworkLocalizationStore(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "LocalizationDb"
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(connectionStringName);

        var connectionString = configuration.GetConnectionString(connectionStringName);
        if(string.IsNullOrEmpty(connectionString)) {
            throw new ArgumentException(
                $"No connection string named '{connectionStringName}' was found in configuration.",
                nameof(connectionStringName)
            );
        }

        return services.AddEntityFrameworkLocalizationStore(connectionString);
    }
}
