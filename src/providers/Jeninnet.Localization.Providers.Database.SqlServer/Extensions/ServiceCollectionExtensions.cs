//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.SqlServer.Extensions;

/// <summary>
/// Provides extension methods for registering the SQL Server localization provider.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds SQL Server localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqlServerLocalization(
        this IServiceCollection services,
        SqlServerLocalizationOptions options
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(options.TableName);

        services.AddSingleton<IInnerMessageProvider>(new SqlServerMessageProvider(options));
        return services;
    }

    /// <summary>
    /// Adds SQL Server localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="tableName">The name of the table containing validation messages.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqlServerLocalization(
        this IServiceCollection services,
        string connectionString,
        string tableName = "LocalizationMessages"
        ) => services.AddSqlServerLocalization(
            new SqlServerLocalizationOptions {
                ConnectionString = connectionString,
                TableName = tableName
            }
        );

    /// <summary>
    /// Adds SQL Server localization provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSqlServerLocalization(this IServiceCollection services, Action<SqlServerLocalizationOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerLocalizationOptions();
        configure(options);
        return services.AddSqlServerLocalization(options);
    }
}
