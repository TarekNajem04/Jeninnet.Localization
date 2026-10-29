//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.SQLite.Extensions;

/// <summary>
/// Provides extension methods for registering Sqlite localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds Sqlite localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqliteLocalization(
        this IServiceCollection services,
        SqliteLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(options.TableName);

        services.AddSingleton<IInnerMessageProvider>(new SqliteMessageProvider(options));
        return services;
    }

    /// <summary>
    /// Adds Sqlite localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="tableName">The name of the table containing validation messages.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqliteLocalization(
        this IServiceCollection services,
        string connectionString,
        string tableName = "LocalizationMessages"
        ) => services.AddSqliteLocalization(
            new SqliteLocalizationOptions {
                ConnectionString = connectionString,
                TableName = tableName
            }
        );

    /// <summary>
    /// Adds Sqlite localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqliteLocalization(this IServiceCollection services, Action<SqliteLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqliteLocalizationOptions();
        configure(options);
        return services.AddSqliteLocalization(options);
    }
}
