//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.JavaScript.Extensions;

/// <summary>
/// Registers and maps the JavaScript resource endpoint.
/// </summary>
public static class JavaScriptLocalizationExtensions {
    /// <summary>
    /// Adds the JavaScript resource endpoint services: the options snapshot and the
    /// payload builder over the canonical <see cref="IMessageProvider"/>.
    /// </summary>
    /// <remarks>
    /// The host's provider composition (Core <c>AddLocalization()</c> plus exactly one
    /// message provider) must be registered before the builder is resolved. The
    /// allow-list is mandatory and explicit — whole stores are never dumped. Map the
    /// endpoint with <see cref="MapJavaScriptLocalization(IEndpointRouteBuilder)"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional configuration delegate.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the configured options are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the composition is registered twice.</exception>
    public static IServiceCollection AddJavaScriptLocalization(this IServiceCollection services, Action<JavaScriptLocalizationOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        if(services.Any(static descriptor => descriptor.ServiceType == typeof(JavaScriptLocalizationOptions))) {
            throw new InvalidOperationException(
                $"JavaScript localization is already registered in the service collection. Configure it through a single {nameof(AddJavaScriptLocalization)} call.");
        }

        var options = new JavaScriptLocalizationOptions();
        configure?.Invoke(options);

        ValidateOptions(options);

        services.AddSingleton(options);
        services.TryAddSingleton<JavaScriptResourcePayloadBuilder>();

        return services;
    }

    /// <summary>
    /// Maps the <c>GET</c> resource endpoint on the configured route pattern, with
    /// content-hash ETag revalidation (<c>304</c> on a matching <c>If-None-Match</c>).
    /// </summary>
    /// <remarks>
    /// The endpoint is mapped as a <see cref="RequestDelegate"/>: no
    /// delegate-parameter binding is used, so the mapping carries no
    /// trim/AOT-unfriendly reflection and works in trimmed and native-AOT hosts.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder (minimal API or MVC routing).</param>
    /// <returns>The endpoint convention builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoints"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="AddJavaScriptLocalization(IServiceCollection, Action{JavaScriptLocalizationOptions})"/> was not called.</exception>
    public static IEndpointConventionBuilder MapJavaScriptLocalization(this IEndpointRouteBuilder endpoints) {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetService<JavaScriptLocalizationOptions>()
            ?? throw new InvalidOperationException(
                $"{nameof(JavaScriptLocalizationOptions)} is not registered. Call {nameof(AddJavaScriptLocalization)} during service composition before mapping the endpoint.");

        return endpoints.MapMethods(options.RoutePattern, ["GET"], HandleRequestAsync);
    }

    /// <summary>
    /// Handles request.
    /// </summary>
    /// <param name="httpContext">The http context.</param>
    private static async Task HandleRequestAsync(HttpContext httpContext) {
        var culture = httpContext.Request.RouteValues["culture"] as string ?? string.Empty;
        var builder = httpContext.RequestServices.GetRequiredService<JavaScriptResourcePayloadBuilder>();
        var options = httpContext.RequestServices.GetRequiredService<JavaScriptLocalizationOptions>();
        var result = await HandleAsync(httpContext, culture, builder, options, httpContext.RequestAborted).ConfigureAwait(false);
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs the handle operation.
    /// </summary>
    /// <param name="httpContext">The http context.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="builder">The builder.</param>
    /// <param name="options">The options instance.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is handle.</returns>
    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        string culture,
        JavaScriptResourcePayloadBuilder builder,
        JavaScriptLocalizationOptions options,
        CancellationToken cancellationToken) {
        var payload = await builder.BuildAsync(culture, cancellationToken).ConfigureAwait(false);
        if(payload is null) {
            return Results.NotFound();
        }

        httpContext.Response.Headers.ETag = payload.ETag;
        if(!string.IsNullOrEmpty(options.CacheControlHeader)) {
            httpContext.Response.Headers.CacheControl = options.CacheControlHeader;
        }

        return MatchesIfNoneMatch(httpContext.Request.Headers.IfNoneMatch, payload.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Bytes(payload.Utf8Json.ToArray(), "application/json; charset=utf-8");
    }

    /// <summary>
    /// RFC 9110 weak comparison for <c>If-None-Match</c>: the list matches when any listed
    /// entity tag equals the payload tag (weak <c>W/"…"</c> forms accepted) or when the
    /// value is <c>*</c>.
    /// </summary>
    /// <param name="ifNoneMatch">The raw <c>If-None-Match</c> header value, possibly <see langword="null"/>.</param>
    /// <param name="eTag">The payload entity tag.</param>
    /// <returns><see langword="true"/> when the request may be answered with 304.</returns>
    private static bool MatchesIfNoneMatch(string? ifNoneMatch, string eTag) {
        if(string.IsNullOrWhiteSpace(ifNoneMatch)) {
            return false;
        }

        if(ifNoneMatch.Trim() == "*") {
            return true;
        }

        foreach(var candidate in ifNoneMatch.Split(',')) {
            var value = candidate.Trim();
            if(value.StartsWith("W/", StringComparison.Ordinal)) {
                value = value[2..];
            }

            if(string.Equals(value, eTag, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates options.
    /// </summary>
    /// <param name="options">The options instance.</param>
    private static void ValidateOptions(JavaScriptLocalizationOptions options) {
        if(string.IsNullOrWhiteSpace(options.RoutePattern) || !options.RoutePattern.Contains("{culture}", StringComparison.Ordinal)) {
            throw new ArgumentException(
                $"{nameof(JavaScriptLocalizationOptions)}.{nameof(JavaScriptLocalizationOptions.RoutePattern)} must be a non-empty route pattern containing the '{{culture}}' parameter.",
                nameof(options));
        }

        if(options.AllowedKeys.Count == 0) {
            throw new ArgumentException(
                $"{nameof(JavaScriptLocalizationOptions)}.{nameof(JavaScriptLocalizationOptions.AllowedKeys)} must contain at least one key; whole stores are never dumped.",
                nameof(options));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach(var key in options.AllowedKeys) {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if(!seen.Add(key)) {
                throw new ArgumentException(
                    $"{nameof(JavaScriptLocalizationOptions)}.{nameof(JavaScriptLocalizationOptions.AllowedKeys)} contains duplicate key '{key}'.",
                    nameof(options));
            }
        }

        foreach(var culture in options.SupportedCultures) {
            ArgumentException.ThrowIfNullOrWhiteSpace(culture);
            _ = CultureInfo.GetCultureInfo(culture);
        }
    }
}
