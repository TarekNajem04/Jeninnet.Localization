//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.RequestLocalization;

/// <summary>
/// Populates <see cref="RequestLocalizationOptions"/> from the localization
/// infrastructure: served cultures from the explicit override or the
/// registered <see cref="ILocalizationStore"/>, the default request culture from the
/// explicit override, <see cref="LocalizationOptions.DefaultCulture"/>, or the
/// <c>"en"</c> fallback, and the served-culture provider composition
/// (route → query string → cookie → <c>Accept-Language</c>).
/// </summary>
/// <param name="serviceProvider">The application service provider.</param>
/// <param name="options">The localization request-culture options.</param>
internal sealed class LocalizationRequestCultureOptionsSetup(IServiceProvider serviceProvider, LocalizationRequestCultureOptions options) : IConfigureOptions<RequestLocalizationOptions> {
    private const string FALLBACK_DEFAULT_CULTURE = "en";

    private static readonly Action<ILogger, Exception?> _logStoreUnreachable = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, "LocalizationStoreUnreachable"),
        $"The {nameof(ILocalizationStore)} is unreachable; request localization falls back to the default culture alone. " +
        $"Configure {nameof(LocalizationRequestCultureOptions)}.{nameof(LocalizationRequestCultureOptions.SupportedCultures)} explicitly " +
        "to declare the served cultures without the store.");

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly LocalizationRequestCultureOptions _options = options;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the registered store reports zero supported cultures or a configured/store
    /// culture name is not recognized.
    /// </exception>
    public void Configure(RequestLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        var defaultCulture = ResolveDefaultCulture();
        var servedCultures = BuildServedCultures(defaultCulture, ResolveCultureNames());

        options.SupportedCultures = servedCultures;
        options.SupportedUICultures = servedCultures;
        options.DefaultRequestCulture = new RequestCulture(defaultCulture);
        options.RequestCultureProviders = BuildProviders(options);
    }

    /// <summary>
    /// Resolves default culture.
    /// </summary>
    /// <returns>The default culture result.</returns>
    private CultureInfo ResolveDefaultCulture() {
        var configured = _options.DefaultCulture;
        if(string.IsNullOrWhiteSpace(configured)) {
            configured = _serviceProvider.GetService<LocalizationOptions>()?.DefaultCulture;
        }

        if(string.IsNullOrWhiteSpace(configured)) {
            configured = FALLBACK_DEFAULT_CULTURE;
        }

        return ParseCulture(configured, "default request culture");
    }

    /// <summary>
    /// Resolves culture names.
    /// </summary>
    /// <returns>The culture names sequence.</returns>
    private List<string> ResolveCultureNames() {
        if(_options.SupportedCultures.Count > 0) {
            return [.. _options.SupportedCultures];
        }

        var store = _serviceProvider.GetService<ILocalizationStore>();
        if(store is null) {
            return [];
        }

        List<string> cultures;
        try {
            cultures = ExecuteAtSynchronousFrameworkBoundary(store.GetSupportedCulturesAsync(CancellationToken.None));
        }
        catch(Exception exception) when(exception is not OperationCanceledException) {
            var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<LocalizationRequestCultureOptionsSetup>();
            if(logger is not null) {
                _logStoreUnreachable(logger, exception);
            }

            return [];
        }

        if(cultures.Count == 0) {
            throw new InvalidOperationException(
                $"The registered {nameof(ILocalizationStore)} reports zero supported cultures, so no request culture can be served. " +
                "Seed the store with at least the default culture, or declare the served cultures explicitly through " +
                $"{nameof(LocalizationRequestCultureOptions)}.{nameof(LocalizationRequestCultureOptions.SupportedCultures)} in the " +
                $"{nameof(Extensions.LocalizationRequestCultureExtensions)} registration.");
        }

        return cultures;
    }

    /// <summary>
    /// Builds served cultures.
    /// </summary>
    /// <param name="defaultCulture">The default culture.</param>
    /// <param name="cultureNames">The culture names.</param>
    /// <returns>The served cultures sequence.</returns>
    private static List<CultureInfo> BuildServedCultures(CultureInfo defaultCulture, List<string> cultureNames) {
        var servedCultures = new List<CultureInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach(var name in cultureNames) {
            var culture = ParseCulture(name, "supported culture");
            if(seen.Add(culture.Name)) {
                servedCultures.Add(culture);
            }
        }

        if(seen.Add(defaultCulture.Name)) {
            servedCultures.Add(defaultCulture);
        }

        return servedCultures;
    }

    /// <summary>
    /// Builds providers.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <returns>The providers sequence.</returns>
    private List<IRequestCultureProvider> BuildProviders(RequestLocalizationOptions options) {
        var providers = new List<IRequestCultureProvider>(4);
        Add(new RouteValueRequestCultureProvider(_options.RouteCultureKey));
        Add(new QueryStringRequestCultureProvider());
        Add(new CookieRequestCultureProvider());
        Add(new AcceptLanguageHeaderRequestCultureProvider());
        return providers;

        void Add(RequestCultureProvider provider) {
            provider.Options = options;
            providers.Add(new ServedCultureRequestCultureProvider(provider) { Options = options });
        }
    }

    /// <summary>
    /// Parses culture.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="source">The source.</param>
    /// <returns>The culture result.</returns>
    private static CultureInfo ParseCulture(string name, string source) {
        try {
            return CultureInfo.GetCultureInfo(name);
        }
        catch(CultureNotFoundException exception) {
            throw new InvalidOperationException(
                $"The {source} '{name}' is not a recognized culture name.",
                exception);
        }
    }

    /// <summary>
    /// Isolates the synchronous <see cref="IConfigureOptions{TOptions}"/> construction from
    /// the asynchronous store contract (the framework exposes no asynchronous configuration
    /// hook). This is the bridge's single intentional synchronous boundary, mirroring the
    /// established <c>CustomStringLocalizer</c> pattern.
    /// </summary>
    /// <typeparam name="T">The awaited result type.</typeparam>
    /// <param name="operation">The asynchronous operation to bridge.</param>
    /// <returns>The at synchronous framework boundary result.</returns>
    private static T ExecuteAtSynchronousFrameworkBoundary<T>(Task<T> operation) =>
        operation.ConfigureAwait(false).GetAwaiter().GetResult();
}
