//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.MySql.Extensions;

/// <summary>
/// Provides extension methods for registering MySQL localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds MySQL localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The options for the MySQL localization provider.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMySqlLocalization(
        this IServiceCollection services,
        MySqlLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(options.TableName);

        services.AddSingleton<IInnerMessageProvider>(new MySqlMessageProvider(options));
        return services;
    }

    /// <summary>
    /// Adds MySQL localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string for the MySQL database.</param>
    /// <param name="tableName">The name of the table containing validation messages.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMySqlLocalization(
        this IServiceCollection services,
        string connectionString,
        string tableName = "LocalizationMessages"
        ) => services.AddMySqlLocalization(
            new MySqlLocalizationOptions {
                ConnectionString = connectionString,
                TableName = tableName
            }
        );

    /// <summary>
    /// Adds MySQL localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMySqlLocalization(this IServiceCollection services, Action<MySqlLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new MySqlLocalizationOptions();
        configure(options);
        return services.AddMySqlLocalization(options);
    }
}
