//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.PostgreSQL;

/// <summary>
/// Provides access to localized messages from PostgreSql database.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PostgreSqlMessageProvider"/> class with the specified options.
/// </remarks>
/// <param name="options">The options for the PostgreSql message provider.</param>
public class PostgreSqlMessageProvider(PostgreSqlLocalizationOptions options) :
    DatabaseMessageProviderBase<PostgreSqlLocalizationOptions>(static connectionString => new NpgsqlConnection(connectionString), options), IInnerMessageProvider {
    /// <inheritdoc />
    protected override string EscapeIdentifier(string identifier) => $"\"{identifier}\"";
}
