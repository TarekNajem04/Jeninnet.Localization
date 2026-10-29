//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Database.Abstractions.Options;

/// <summary>
/// Options for database-based localization providers.
/// </summary>
public abstract partial class DatabaseLocalizationOptions {
    private const string DEFAULT_TABLE_NAME = "LocalizationMessages";

    /// <summary>
    /// Performs the table name regex operation.
    /// </summary>
    /// <returns>The table name regex result.</returns>
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{0,127}(\.[a-zA-Z_][a-zA-Z0-9_]{0,127}){0,2}$")]
    private static partial Regex TableNameRegex();

    /// <summary>
    /// Gets or sets the connection string for the database.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the table that contains the localization messages.
    /// </summary>
    /// <remarks>
    /// Must be a valid SQL identifier: alphanumeric characters, underscores, and periods (as segment separators).
    /// Each segment must start with a letter or underscore and contain 1-128 characters.
    /// Up to 3 segments are permitted (e.g., 'dbo.LocalizationMessages').
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the table name is null, empty, or not a valid SQL identifier.</exception>
    public string TableName {
        get;
        set {
            ArgumentException.ThrowIfNullOrEmpty(value);
            if(!TableNameRegex().IsMatch(value)) {
                throw new ArgumentException(
                    $"""
                    Table name '{value}' is not a valid SQL identifier.                    
                    Only alphanumeric characters, underscores, and periods (as segment separators) are allowed. 
                    Each segment must start with a letter or underscore, contain 1-128 characters, and up to 3 segments are permitted (e.g., 'dbo.LocalizationMessages').
                    """,
                    nameof(value));
            }

            field = value;
        }
    } = DEFAULT_TABLE_NAME;
}
