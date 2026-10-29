//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.RequestLocalization;

/// <summary>
/// Determines the request culture from a route value. The
/// framework ships query-string, cookie, and <c>Accept-Language</c> providers but no
/// route-value provider; this type supplies that missing source without re-implementing
/// the framework providers.
/// </summary>
/// <remarks>
/// The route value sets both the culture and the UI culture. Returned values are not
/// narrowed here; compose with <see cref="ServedCultureRequestCultureProvider"/> (or rely
/// on the framework <see cref="RequestLocalizationMiddleware"/> narrowing) so only served
/// cultures are resolved.
/// </remarks>
public sealed class RouteValueRequestCultureProvider : RequestCultureProvider {
    /// <summary>
    /// The default route value key that carries the request culture (<c>"culture"</c>).
    /// </summary>
    public const string DEFAULT_ROUTE_CULTURE_KEY = "culture";

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteValueRequestCultureProvider"/> class.
    /// </summary>
    /// <param name="routeValueKey">The route value key that carries the request culture.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="routeValueKey"/> is null, empty, or whitespace.</exception>
    public RouteValueRequestCultureProvider(string routeValueKey = DEFAULT_ROUTE_CULTURE_KEY) {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueKey);

        RouteValueKey = routeValueKey;
    }

    /// <summary>
    /// Gets the route value key that carries the request culture.
    /// </summary>
    public string RouteValueKey { get; }

    /// <inheritdoc/>
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) {
        ArgumentNullException.ThrowIfNull(httpContext);

        if(!httpContext.Request.RouteValues.TryGetValue(RouteValueKey, out var value)) {
            return NullProviderCultureResult;
        }

        var culture = value?.ToString();
        if(string.IsNullOrWhiteSpace(culture)) {
            return NullProviderCultureResult;
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture));
    }
}
