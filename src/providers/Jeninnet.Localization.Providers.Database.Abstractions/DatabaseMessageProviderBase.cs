//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.Abstractions;

/// <summary>
/// Provides a base class for database-based message providers.
/// </summary>
/// <typeparam name="TOptions">The type of the database localization options.</typeparam>
/// <remarks>
///  lifecycle model: the connection string and table name are captured as immutable
/// construction snapshots; later mutation of the caller-owned options object cannot alter behavior.
/// </remarks>
public abstract class DatabaseMessageProviderBase<TOptions> : MessageProviderBase
    where TOptions : DatabaseLocalizationOptions {
    private readonly Func<string, DbConnection> _connectionFactory;
    private readonly string _connectionString;
    private readonly string _tableName;

    /// <inheritdoc />
    protected DatabaseMessageProviderBase(Func<string, DbConnection> connectionFactory, TOptions options) {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.ConnectionString);
        ArgumentException.ThrowIfNullOrEmpty(options.TableName);

        // Table-name validity is owned by DatabaseLocalizationOptions.TableName (single owner),
        // which accepts both simple and schema-qualified (dotted, up to 3 segments) identifiers.
        // The consumed option values are snapshotted here.
        _connectionFactory = connectionFactory;
        _connectionString = options.ConnectionString;
        _tableName = options.TableName;
    }

    /// <summary>
    /// Gets the message template for the specified key and culture from the database.
    /// </summary>
    /// <param name="key">The key for which to get the message template.</param>
    /// <param name="culture">The culture for which to get the message template.</param>
    /// <param name="cancellationToken">An optional token to cancel the database operation.</param>
    /// <returns>The message template, or null if not found.</returns>
    public override async ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();

        // Using the shared DbConnection pattern for all providers.
        await using var connection = _connectionFactory(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        // Solving the problem of quotes and field discrepancies between different systems:
        // identifiers fall under each vendor's escaping rules, while values stay parameters.
        var escapedTableName = EscapeQualifiedIdentifier(_tableName);
        var escapedKeyColumn = EscapeIdentifier("MessageKey");
        var escapedCultureColumn = EscapeIdentifier("Culture");
        var escapedTemplateColumn = EscapeIdentifier("Template");

        // Using the global parameter placeholder @ for all providers (supported by ADO.NET in general).
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
        command.CommandText = $"SELECT {escapedTemplateColumn} FROM {escapedTableName} WHERE {escapedKeyColumn} = @MessageKey AND {escapedCultureColumn} = @Culture";
#pragma warning restore S2077 // SQL queries should not be dynamically formatted

        // Adding parameters in a safe manner for all providers.
        var keyParam = command.CreateParameter();
        keyParam.ParameterName = "@MessageKey";
        keyParam.Value = key;
        command.Parameters.Add(keyParam);

        var cultureParam = command.CreateParameter();
        cultureParam.ParameterName = "@Culture";
        cultureParam.Value = culture.Name;
        command.Parameters.Add(cultureParam);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    /// <summary>
    /// Escapes the identifier (table or column name) according to the specific database provider's rules.
    /// </summary>
    /// <param name="identifier">The identifier to escape.</param>
    /// <returns>The escaped identifier.</returns>
    protected abstract string EscapeIdentifier(string identifier);

    /// <summary>
    /// Escapes a possibly schema-qualified identifier (e.g., <c>dbo.LocalizationMessages</c>) by
    /// escaping each dot-separated segment with the vendor-specific rules and rejoining with '.'.
    /// </summary>
    /// <param name="identifier">The (possibly dotted) identifier to escape.</param>
    /// <returns>The per-segment escaped identifier.</returns>
    private string EscapeQualifiedIdentifier(string identifier) =>
        string.Join('.', identifier.Split('.').Select(EscapeIdentifier));
}
