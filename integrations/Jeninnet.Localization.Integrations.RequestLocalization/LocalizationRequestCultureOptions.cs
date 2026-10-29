//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.RequestLocalization;

/// <summary>
/// The deployment configuration for the request-localization bridge
///: the explicit culture overrides consumed when populating
/// <see cref="RequestLocalizationOptions"/>.
/// </summary>
/// <remarks>
/// Both overrides are optional. When <see cref="SupportedCultures"/> is empty, the
/// served set is discovered from the registered <see cref="ILocalizationStore"/>; when
/// <see cref="DefaultCulture"/> is not set, the registered
/// <see cref="LocalizationOptions.DefaultCulture"/> is used, falling back to
/// <c>"en"</c>. Explicit values are validated at registration time.
/// </remarks>
public sealed class LocalizationRequestCultureOptions {
    /// <summary>
    /// Gets or sets the default (fallback) request culture. When unset, the registered
    /// <see cref="LocalizationOptions.DefaultCulture"/> is used, falling back to
    /// <c>"en"</c> (the <see cref="LocalizationOptions"/> default). The default culture is
    /// always part of the served set.
    /// </summary>
    public string? DefaultCulture { get; set; }

    /// <summary>
    /// Gets the explicit served cultures (both content and UI cultures). When empty, the
    /// served set is discovered through
    /// <see cref="ILocalizationStore.GetSupportedCulturesAsync(CancellationToken)"/>; when
    /// no store is registered or the store is unreachable, the served set deterministically
    /// falls back to the default culture alone.
    /// </summary>
    public IList<string> SupportedCultures { get; } = [];

    /// <summary>
    /// Gets or sets the route value key that carries the request culture
    /// (default <c>"culture"</c>). The route source is evaluated first, then query string,
    /// cookie, and <c>Accept-Language</c>.
    /// </summary>
    public string RouteCultureKey { get; set; } = RouteValueRequestCultureProvider.DEFAULT_ROUTE_CULTURE_KEY;
}
