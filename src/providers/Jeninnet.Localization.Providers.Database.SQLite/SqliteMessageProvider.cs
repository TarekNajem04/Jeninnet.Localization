//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.SQLite;

/// <summary>
/// Provides access to localized messages from Sqlite database.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SqliteMessageProvider"/> class with the specified options.
/// </remarks>
/// <param name="options">The options for the Sqlite message provider.</param>
public class SqliteMessageProvider(SqliteLocalizationOptions options) :
    DatabaseMessageProviderBase<SqliteLocalizationOptions>(static connectionString => new SqliteConnection(connectionString), options), IInnerMessageProvider {
    /// <inheritdoc />
    protected override string EscapeIdentifier(string identifier) => $"\"{identifier}\"";
}
