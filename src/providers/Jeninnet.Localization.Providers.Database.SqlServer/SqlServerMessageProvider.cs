//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.SqlServer;

/// <summary>
/// Provides access to localized messages from Microsoft SQL Server database.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SqlServerMessageProvider"/> class with the specified options.
/// </remarks>
/// <param name="options">The options for the SqlServer message provider.</param>
public class SqlServerMessageProvider(SqlServerLocalizationOptions options) :
    DatabaseMessageProviderBase<SqlServerLocalizationOptions>(static connectionString => new SqlConnection(connectionString), options), IInnerMessageProvider {
    /// <inheritdoc />
    protected override string EscapeIdentifier(string identifier) => $"[{identifier}]";

}
