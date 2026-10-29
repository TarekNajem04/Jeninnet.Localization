//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.RequestLocalization;

/// <summary>
/// Narrows another <see cref="RequestCultureProvider"/>'s result to the cultures the
/// application can actually serve.
/// </summary>
/// <remarks>
/// <para>
/// The served set is read from <see cref="RequestCultureProvider.Options"/>
/// (<see cref="RequestLocalizationOptions.SupportedCultures"/> /
/// <see cref="RequestLocalizationOptions.SupportedUICultures"/>); the wrapper must be
/// assigned to that options instance before use — the bridge registration does this.
/// Matching mirrors the framework middleware: exact culture-name match
/// (ordinal, case-insensitive) first, then a bounded parent walk when the corresponding
/// <see cref="RequestLocalizationOptions.FallBackToParentCultures"/> /
/// <see cref="RequestLocalizationOptions.FallBackToParentUICultures"/> flag is set. Resolved
/// values are returned as the canonical served culture names. A result that matches nothing
/// yields <see langword="null"/>, so the middleware deterministically applies
/// <see cref="RequestLocalizationOptions.DefaultRequestCulture"/>. A side whose supported
/// list is <see langword="null"/> passes through unchanged (the middleware treats a
/// <see langword="null"/> list as no narrowing).
/// </para>
/// <para>
/// The wrapper performs no ambient-culture mutation; the framework middleware owns the
/// per-request culture application.
/// </para>
/// </remarks>
public sealed class ServedCultureRequestCultureProvider : RequestCultureProvider {
    private const int MAX_PARENT_FALLBACK_DEPTH = 5;

    private readonly IRequestCultureProvider _innerProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServedCultureRequestCultureProvider"/> class.
    /// </summary>
    /// <param name="innerProvider">The provider whose result is narrowed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerProvider"/> is null.</exception>
    public ServedCultureRequestCultureProvider(IRequestCultureProvider innerProvider) {
        ArgumentNullException.ThrowIfNull(innerProvider);

        _innerProvider = innerProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="RequestCultureProvider.Options"/> has not been assigned.</exception>
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) {
        ArgumentNullException.ThrowIfNull(httpContext);

        var result = await _innerProvider.DetermineProviderCultureResult(httpContext).ConfigureAwait(false);
        if(result is null) {
            return null;
        }

        var options = Options ?? throw new InvalidOperationException(
            $"{nameof(ServedCultureRequestCultureProvider)} requires the current {nameof(RequestLocalizationOptions)} to be assigned " +
            $"through {nameof(RequestCultureProvider.Options)} before use (the {nameof(LocalizationRequestCultureOptions)} bridge registration assigns it).");

        var cultures = Resolve(result.Cultures, options.SupportedCultures, options.FallBackToParentCultures);
        var uiCultures = Resolve(result.UICultures, options.SupportedUICultures, options.FallBackToParentUICultures);
        if(cultures is null && uiCultures is null) {
            return null;
        }

        return new ProviderCultureResult(cultures ?? [], uiCultures ?? []);
    }

    /// <summary>
    /// Performs the resolve operation.
    /// </summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="fallBackToParent">The fall back to parent.</param>
    /// <returns>The resolve sequence.</returns>
    private static IList<StringSegment>? Resolve(IList<StringSegment> candidates, IList<CultureInfo>? supportedCultures, bool fallBackToParent) {
        if(candidates is null || candidates.Count == 0) {
            return null;
        }

        if(supportedCultures is null) {
            return candidates;
        }

        foreach(var candidate in candidates) {
            if(StringSegment.IsNullOrEmpty(candidate)) {
                continue;
            }

            var exact = FindServed(supportedCultures, candidate);
            if(exact is not null) {
                return [exact.Name];
            }

            if(fallBackToParent && FindServedParent(supportedCultures, candidate) is { } parent) {
                return [parent.Name];
            }
        }

        return null;
    }

    /// <summary>
    /// Finds served.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="name">The name.</param>
    /// <returns>The served result.</returns>
    private static CultureInfo? FindServed(IList<CultureInfo> supportedCultures, StringSegment name) =>
        supportedCultures.FirstOrDefault(culture => StringSegment.Equals(culture.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds served parent.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="name">The name.</param>
    /// <returns>The served parent result.</returns>
    private static CultureInfo? FindServedParent(IList<CultureInfo> supportedCultures, StringSegment name) {
        CultureInfo culture;
        try {
            culture = CultureInfo.GetCultureInfo(name.ToString());
        }
        catch(CultureNotFoundException) {
            return null;
        }

        for(var depth = 0; depth < MAX_PARENT_FALLBACK_DEPTH; depth++) {
            var parent = culture.Parent;
            if(string.IsNullOrEmpty(parent.Name)) {
                return null;
            }

            var match = FindServed(supportedCultures, parent.Name);
            if(match is not null) {
                return match;
            }

            culture = parent;
        }

        return null;
    }
}
