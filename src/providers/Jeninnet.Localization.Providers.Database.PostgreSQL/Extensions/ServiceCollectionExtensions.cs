//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.PostgreSQL.Extensions;

/// <summary>
/// Provides extension methods for registering PostgreSql localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds PostgreSql localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPostgreSqlLocalization(
        this IServiceCollection services,
        PostgreSqlLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(options.TableName);

        services.AddSingleton<IInnerMessageProvider>(new PostgreSqlMessageProvider(options));
        return services;
    }

    /// <summary>
    /// Adds PostgreSql localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="tableName">The name of the table containing validation messages.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPostgreSqlLocalization(
        this IServiceCollection services,
        string connectionString,
        string tableName = "LocalizationMessages"
        ) => services.AddPostgreSqlLocalization(
            new PostgreSqlLocalizationOptions {
                ConnectionString = connectionString,
                TableName = tableName
            }
        );

    /// <summary>
    /// Adds PostgreSql localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPostgreSqlLocalization(this IServiceCollection services, Action<PostgreSqlLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new PostgreSqlLocalizationOptions();
        configure(options);
        return services.AddPostgreSqlLocalization(options);
    }
}
