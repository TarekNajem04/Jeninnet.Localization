//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The sample-owned, deterministic SQLite database demonstrated by the SQLite provider on every
/// reference host. This type owns only the demonstration schema and the canonical-content seed; the
/// shipping SQLite provider reads the table exclusively through the production contract.
/// </summary>
public static class ReferenceSqliteDatabase {
    /// <summary>
    /// The canonical table name read by the production <c>IMessageProvider</c>.
    /// </summary>
    public const string TABLE_NAME = "LocalizationMessages";

    /// <summary>
    /// The deterministic database file name used by every reference host.
    /// </summary>
    public const string FILE_NAME = "reference-localization.db";

    /// <summary>
    /// Gets the deterministic database file path for the specified data directory.
    /// </summary>
    /// <param name="dataDirectory">The sample-owned data directory.</param>
    /// <returns>The absolute database file path.</returns>
    public static string GetDatabasePath(string dataDirectory) => Path.Combine(dataDirectory, FILE_NAME);

    /// <summary>
    /// Creates (when absent) and seeds the sample database so it mirrors the canonical reference
    /// content exactly, then returns the connection string consumed by the production provider.
    /// </summary>
    /// <remarks>
    /// Deterministic and idempotent: the table is created once per database, and every
    /// initialization rewrites the rows from <see cref="LocalizationContent"/> so repeated starts never
    /// duplicate rows and stale rows cannot silently alter the sample's behavior.
    /// </remarks>
    /// <param name="dataDirectory">The sample-owned data directory.</param>
    /// <returns>The SQLite connection string.</returns>
    public static string EnsureInitialized(string dataDirectory) {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        Directory.CreateDirectory(dataDirectory);

        // Pooling is disabled so the single-file demonstration database is fully released whenever a
        // connection is disposed; this keeps startup, reset, and reproduction deterministic.
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = GetDatabasePath(dataDirectory),
            Pooling = false
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        using(var create = connection.CreateCommand()) {
            create.Transaction = transaction;
            // Table and identifiers come from trusted sample constants; all row values are parameters.
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
            create.CommandText = $"""
                CREATE TABLE IF NOT EXISTS "{TABLE_NAME}" (
                    "MessageKey" TEXT NOT NULL,
                    "Culture" TEXT NOT NULL,
                    "Template" TEXT NOT NULL,
                    PRIMARY KEY ("MessageKey", "Culture")
                )
                """;
#pragma warning restore S2077 // SQL queries should not be dynamically formatted
            create.ExecuteNonQuery();
        }

        using(var clear = connection.CreateCommand()) {
            clear.Transaction = transaction;
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
            clear.CommandText = $"DELETE FROM \"{TABLE_NAME}\"";
#pragma warning restore S2077 // SQL queries should not be dynamically formatted
            clear.ExecuteNonQuery();
        }

        using(var insert = connection.CreateCommand()) {
            insert.Transaction = transaction;
#pragma warning disable S2077 // SQL queries should not be dynamically formatted
            insert.CommandText = $"""
                INSERT INTO "{TABLE_NAME}" ("MessageKey", "Culture", "Template")
                VALUES (@MessageKey, @Culture, @Template)
                """;
#pragma warning restore S2077 // SQL queries should not be dynamically formatted
            var key = insert.CreateParameter();
            key.ParameterName = "@MessageKey";
            insert.Parameters.Add(key);
            var culture = insert.CreateParameter();
            culture.ParameterName = "@Culture";
            insert.Parameters.Add(culture);
            var template = insert.CreateParameter();
            template.ParameterName = "@Template";
            insert.Parameters.Add(template);

            foreach(var cultureCode in ReferenceCultures.SupportedCultures) {
                foreach(var (messageKey, value) in LocalizationContent.GetTemplates(cultureCode)) {
                    key.Value = messageKey;
                    culture.Value = cultureCode;
                    template.Value = value;
                    insert.ExecuteNonQuery();
                }
            }
        }

        transaction.Commit();
        return connectionString;
    }

    /// <summary>
    /// Deletes the sample database (and any SQLite sidecar files) so the next startup recreates it
    /// deterministically. Deleting just the data directory also works, because initialization is idempotent.
    /// </summary>
    /// <param name="dataDirectory">The sample-owned data directory.</param>
    public static void Reset(string dataDirectory) {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);

        foreach(var suffix in new[] { string.Empty, "-wal", "-shm" }) {
            var path = GetDatabasePath(dataDirectory) + suffix;
            if(File.Exists(path)) {
                File.Delete(path);
            }
        }
    }
}
