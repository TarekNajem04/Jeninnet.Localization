//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The connection configuration for one server-backed database provider. Only the connection string
/// is required; the sample owns the database and table names it initializes.
/// </summary>
public sealed class ReferenceDatabaseOptions {
    /// <summary>
    /// Gets or sets the connection string used to reach the server and (re)create the sample-owned
    /// database. Local development supplies this value through .NET User Secrets, never through a
    /// committed configuration file.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// The configuration for the <see cref="ReferenceProviderNames.AZURE_TRANSLATION"/> provider. Binds
/// from the <c>ReferenceLocalization:AzureTranslation</c> configuration section. The
/// <see cref="ApiKey"/> is a local secret supplied through .NET User Secrets; the remaining settings
/// are optional overrides of the production <c>AzureTranslationOptions</c> defaults.
/// </summary>
public sealed class ReferenceAzureTranslationOptions {
    /// <summary>
    /// Gets or sets the Azure Translator API key. Sent as the <c>Ocp-Apim-Subscription-Key</c>
    /// header; bound from <c>ReferenceLocalization:AzureTranslation:ApiKey</c> and required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the region associated with the API key. Optional; sent as the
    /// <c>Ocp-Apim-Subscription-Region</c> header only when non-blank. Bound from
    /// <c>ReferenceLocalization:AzureTranslation:Region</c>.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base API endpoint for Azure Translator. Optional; when blank the production
    /// default global endpoint is used. Bound from <c>ReferenceLocalization:AzureTranslation:Endpoint</c>.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source language used to disambiguate the translation. Optional; when blank
    /// Azure auto-detects the source. Bound from <c>ReferenceLocalization:AzureTranslation:FromLanguage</c>.
    /// </summary>
    public string FromLanguage { get; set; } = string.Empty;
}

/// <summary>
/// The configuration for the <see cref="ReferenceProviderNames.GOOGLE_TRANSLATION"/> provider. Binds
/// from the <c>ReferenceLocalization:GoogleTranslation</c> configuration section. The
/// <see cref="ApiKey"/> is a local secret supplied through .NET User Secrets; the endpoint is an
/// optional override of the production <c>GoogleTranslationOptions</c> default.
/// </summary>
public sealed class ReferenceGoogleTranslationOptions {
    /// <summary>
    /// Gets or sets the Google Cloud Translation API key. Sent as the <c>key</c> query parameter;
    /// bound from <c>ReferenceLocalization:GoogleTranslation:ApiKey</c> and required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base API endpoint for Google Cloud Translation. Optional; when blank the
    /// production v2 default endpoint is used. Bound from
    /// <c>ReferenceLocalization:GoogleTranslation:Endpoint</c>.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>
/// Options consumed by the reference composition entry point.
/// </summary>
public sealed class ReferenceLocalizationOptions {
    /// <summary>
    /// Gets or sets the localization provider name. Must be one of
    /// <see cref="ReferenceProviderNames.SupportedProviders"/>,
    /// <see cref="ReferenceProviderNames.InfrastructureGatedProviders"/>, or
    /// <see cref="ReferenceProviderNames.CredentialGatedProviders"/>.
    /// </summary>
    public string Provider { get; set; } = ReferenceProviderNames.JSON;

    /// <summary>
    /// Gets or sets the directory that contains the <c>Localization</c> subdirectory with the
    /// per-culture content files. Defaults to the application base directory.
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Gets or sets the sample-owned data directory that contains the SQLite database file used by
    /// the <see cref="ReferenceProviderNames.SQLITE"/> provider. Defaults to a deterministic
    /// <c>Data</c> directory under the application base directory.
    /// </summary>
    public string SqliteDataDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Data");

    /// <summary>
    /// Gets or sets the connection configuration for the
    /// <see cref="ReferenceProviderNames.SQL_SERVER"/> provider. Bound from
    /// <c>ReferenceLocalization:SqlServer:ConnectionString</c>.
    /// </summary>
    public ReferenceDatabaseOptions SqlServer { get; set; } = new();

    /// <summary>
    /// Gets or sets the connection configuration for the
    /// <see cref="ReferenceProviderNames.POSTGRE_SQL"/> provider. Bound from
    /// <c>ReferenceLocalization:PostgreSql:ConnectionString</c>.
    /// </summary>
    public ReferenceDatabaseOptions PostgreSql { get; set; } = new();

    /// <summary>
    /// Gets or sets the connection configuration for the
    /// <see cref="ReferenceProviderNames.MY_SQL"/> provider. Bound from
    /// <c>ReferenceLocalization:MySql:ConnectionString</c>.
    /// </summary>
    public ReferenceDatabaseOptions MySql { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration for the <see cref="ReferenceProviderNames.AZURE_TRANSLATION"/>
    /// provider. Bound from the <c>ReferenceLocalization:AzureTranslation</c> configuration section;
    /// the API key is a local secret supplied through .NET User Secrets.
    /// </summary>
    public ReferenceAzureTranslationOptions AzureTranslation { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration for the <see cref="ReferenceProviderNames.GOOGLE_TRANSLATION"/>
    /// provider. Bound from the <c>ReferenceLocalization:GoogleTranslation</c> configuration section;
    /// the API key is a local secret supplied through .NET User Secrets.
    /// </summary>
    public ReferenceGoogleTranslationOptions GoogleTranslation { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the immutable <see cref="ReferenceResourceSetStore"/>
    /// is registered as the <see cref="ILocalizationStore"/> implementation.
    /// </summary>
    public bool RegisterReferenceStore { get; set; } = true;
}

/// <summary>
/// The shared localization-composition entry point exercised by the reference host applications.
/// </summary>
public static class ReferenceLocalization {
    /// <summary>
    /// The base name of the RESX resource set that mirrors the canonical reference content in the
    /// Shared assembly: the neutral set plus the <c>en</c>, <c>ar</c>, and <c>ar-SY</c> satellites.
    /// </summary>
    public const string RES_X_RESOURCE_BASE_NAME = "Jeninnet.Localization.Reference.Shared.Content.Localization.ResX.ReferenceContent";

    /// <summary>
    /// Registers the canonical reference localization stack: exactly one message provider, the
    /// library's default pipeline with the shared default culture, and the immutable reference store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The optional provider configuration.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured provider is not supported by the shared layer.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provider is recognized but not implemented by the shared reference layer.</exception>
    /// <exception cref="InvalidOperationException">Thrown when exactly one message provider cannot be guaranteed, when localization is already configured, when a server-backed provider is selected without a usable connection configuration, or when a credential-gated translation provider is selected without a configured API key.</exception>
    public static IServiceCollection AddReferenceLocalizationProvider(
        this IServiceCollection services,
        Action<ReferenceLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ReferenceLocalizationOptions();
        configure?.Invoke(options);
        return services.AddReferenceLocalizationProvider(options);
    }

    /// <summary>
    /// Registers the canonical reference localization stack from explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The provider configuration.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured provider is not supported by the shared layer.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provider is recognized but not implemented by the shared reference layer.</exception>
    /// <exception cref="InvalidOperationException">Thrown when exactly one message provider cannot be guaranteed, when localization is already configured, when a server-backed provider is selected without a usable connection configuration, or when a credential-gated translation provider is selected without a configured API key.</exception>
    public static IServiceCollection AddReferenceLocalizationProvider(
        this IServiceCollection services,
        ReferenceLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Provider);

        AddProvider(services, options);

        if(services.Count(static descriptor => descriptor.ServiceType == typeof(IInnerMessageProvider)) != 1) {
            throw new InvalidOperationException(
                "The reference localization stack requires exactly one message provider, but multiple IInnerMessageProvider " +
                "registrations were detected. Register at most one provider via AddReferenceLocalizationProvider.");
        }

        services.AddLocalization(localizationOptions => localizationOptions.DefaultCulture = ReferenceCultures.DEFAULT_CULTURE);

        if(options.RegisterReferenceStore) {
            services.AddSingleton<ILocalizationStore>(new ReferenceResourceSetStore());
        }

        return services;
    }

    /// <summary>
    /// Adds provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The options instance.</param>
    private static void AddProvider(IServiceCollection services, ReferenceLocalizationOptions options) {
        switch(options.Provider) {
            case ReferenceProviderNames.MEMORY:
                services.AddMemoryLocalization(LocalizationContent.GetTemplates(ReferenceCultures.DEFAULT_CULTURE));
                return;
            case ReferenceProviderNames.JSON:
                services.AddJsonLocalizationProviders(options.ContentRootPath, [.. ReferenceCultures.SupportedCultures], "Localization");
                return;
            case ReferenceProviderNames.XML:
                services.AddXmlLocalizationProviders(options.ContentRootPath, [.. ReferenceCultures.SupportedCultures], "Localization");
                return;
            case ReferenceProviderNames.RES_X:
                services.AddResXLocalization(new ResXLocalizationOptions {
                    BaseName = RES_X_RESOURCE_BASE_NAME,
                    ResourceAssembly = typeof(ReferenceLocalization).Assembly
                });
                return;
            case ReferenceProviderNames.SQLITE:
                var connectionString = ReferenceSqliteDatabase.EnsureInitialized(options.SqliteDataDirectory);
                services.AddSqliteLocalization(connectionString, ReferenceSqliteDatabase.TABLE_NAME);
                return;
            case ReferenceProviderNames.SQL_SERVER:
                services.AddSqlServerLocalization(
                    ReferenceServerDatabase.EnsureInitialized(ReferenceServerVendor.SqlServer, options.SqlServer.ConnectionString),
                    ReferenceServerDatabase.TABLE_NAME);
                return;
            case ReferenceProviderNames.POSTGRE_SQL:
                services.AddPostgreSqlLocalization(
                    ReferenceServerDatabase.EnsureInitialized(ReferenceServerVendor.PostgreSql, options.PostgreSql.ConnectionString),
                    ReferenceServerDatabase.TABLE_NAME);
                return;
            case ReferenceProviderNames.MY_SQL:
                services.AddMySqlLocalization(
                    ReferenceServerDatabase.EnsureInitialized(ReferenceServerVendor.MySql, options.MySql.ConnectionString),
                    ReferenceServerDatabase.TABLE_NAME);
                return;
            case ReferenceProviderNames.AZURE_TRANSLATION:
                RequireTranslationApiKey(options.AzureTranslation.ApiKey, "ReferenceLocalization:AzureTranslation:ApiKey");
                services.AddAzureTranslationLocalization(translation => {
                    translation.ApiKey = options.AzureTranslation.ApiKey;
                    translation.Region = options.AzureTranslation.Region;
                    if(!string.IsNullOrWhiteSpace(options.AzureTranslation.Endpoint)) {
                        translation.Endpoint = options.AzureTranslation.Endpoint;
                    }

                    translation.FromLanguage = options.AzureTranslation.FromLanguage;
                });
                return;
            case ReferenceProviderNames.GOOGLE_TRANSLATION:
                RequireTranslationApiKey(options.GoogleTranslation.ApiKey, "ReferenceLocalization:GoogleTranslation:ApiKey");
                services.AddGoogleTranslationLocalization(translation => {
                    translation.ApiKey = options.GoogleTranslation.ApiKey;
                    if(!string.IsNullOrWhiteSpace(options.GoogleTranslation.Endpoint)) {
                        translation.Endpoint = options.GoogleTranslation.Endpoint;
                    }
                });
                return;
        }

        if(ReferenceProviderNames.DeferredProviders.Contains(options.Provider, StringComparer.Ordinal)) {
            throw new NotSupportedException(
                $"The localization provider '{options.Provider}' is recognized but not implemented by the shared reference layer.");
        }

        throw new ArgumentException(
            $"Unknown localization provider '{options.Provider}'. Supported: {string.Join(", ", ReferenceProviderNames.SupportedProviders)}. " +
            $"Infrastructure-gated: {string.Join(", ", ReferenceProviderNames.InfrastructureGatedProviders)}. " +
            $"Credential-gated: {string.Join(", ", ReferenceProviderNames.CredentialGatedProviders)}.");
    }

    /// <summary>
    /// Performs the require translation api key operation.
    /// </summary>
    /// <param name="apiKey">The api key.</param>
    /// <param name="configurationKey">The configuration key.</param>
    private static void RequireTranslationApiKey(string apiKey, string configurationKey) {
        if(string.IsNullOrWhiteSpace(apiKey)) {
            throw new InvalidOperationException(
                $"The selected translation provider requires an API key. Configure '{configurationKey}' through .NET User Secrets " +
                "(or the equivalent configuration provider) before selecting the provider.");
        }
    }
}
