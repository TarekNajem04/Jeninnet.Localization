//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The canonical <c>Localization:Provider</c> values understood by the reference composition helper.
/// </summary>
public static class ReferenceProviderNames {
    /// <summary>The Memory (culture-agnostic) provider.</summary>
    public const string MEMORY = "Memory";
    /// <summary>The per-culture JSON file provider.</summary>
    public const string JSON = "Json";
    /// <summary>The per-culture XML file provider.</summary>
    public const string XML = "Xml";
    /// <summary>The ResourceManager-backed RESX provider.</summary>
    public const string RES_X = "ResX";
    /// <summary>The file-backed SQLite database provider.</summary>
    public const string SQLITE = "SQLite";
    /// <summary>The Microsoft SQL Server database provider.</summary>
    public const string SQL_SERVER = "SqlServer";
    /// <summary>The PostgreSQL database provider.</summary>
    public const string POSTGRE_SQL = "PostgreSql";
    /// <summary>The MySQL database provider.</summary>
    public const string MY_SQL = "MySql";
    /// <summary>The Azure Translator REST provider.</summary>
    public const string AZURE_TRANSLATION = "AzureTranslation";
    /// <summary>The Google Cloud Translation REST provider.</summary>
    public const string GOOGLE_TRANSLATION = "GoogleTranslation";

    /// <summary>
    /// The provider names implemented and exercisable by the current shared foundation without any
    /// external infrastructure. These are the only providers ordinary tests compose.
    /// </summary>
    public static IReadOnlyList<string> SupportedProviders { get; } = [MEMORY, JSON, XML, RES_X, SQLITE];

    /// <summary>
    /// The server-backed database provider names implemented by the shared foundation but requiring
    /// a reachable database server and a configured connection string. They are selectable through
    /// the same helper yet are never composed by ordinary tests.
    /// </summary>
    public static IReadOnlyList<string> InfrastructureGatedProviders { get; } = [SQL_SERVER, POSTGRE_SQL, MY_SQL];

    /// <summary>
    /// The translation provider names implemented by the shared foundation but requiring a
    /// credential (API key) supplied through .NET User Secrets. They are selectable through the same
    /// helper, yet are never composed by ordinary tests and are never the configured default.
    /// </summary>
    public static IReadOnlyList<string> CredentialGatedProviders { get; } = [AZURE_TRANSLATION, GOOGLE_TRANSLATION];

    /// <summary>
    /// Provider names that exist in the library but whose reference demonstrations are not
    /// implemented. Empty while every provider has reference coverage; retains the partition
    /// shape so the exhaustive/disjoint invariant stays testable.
    /// </summary>
    public static IReadOnlyList<string> DeferredProviders { get; } = [];
}
