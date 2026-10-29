//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.MySql;

/// <summary>
/// Provides access to localized messages from MySql database.
/// </summary>
/// <param name="options">The options for the MySql message provider.</param>
public class MySqlMessageProvider(MySqlLocalizationOptions options) :
    DatabaseMessageProviderBase<MySqlLocalizationOptions>(static connectionString => new MySqlConnection(connectionString), options), IInnerMessageProvider {
    /// <inheritdoc />
    protected override string EscapeIdentifier(string identifier) => $"`{identifier}`";
}
