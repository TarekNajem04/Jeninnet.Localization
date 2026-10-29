//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.JavaScript;

/// <summary>
/// Configuration for the JavaScript resource endpoint and static export
///.
/// </summary>
public sealed class JavaScriptLocalizationOptions {
    /// <summary>
    /// Gets or sets the endpoint route pattern; it must contain the <c>{culture}</c>
    /// route parameter. Defaults to <c>/localization/v1/{culture}</c> — the version is
    /// the URL segment.
    /// </summary>
    public string RoutePattern { get; set; } = "/localization/v1/{culture}";

    /// <summary>
    /// Gets or sets the <c>Cache-Control</c> response header; <see langword="null"/> or
    /// empty omits the header. Defaults to <c>public, max-age=0, must-revalidate</c>
    /// (ETag-driven revalidation).
    /// </summary>
    public string? CacheControlHeader { get; set; } = "public, max-age=0, must-revalidate";

    /// <summary>
    /// Gets the explicit key allow-list. Only these keys are ever requested from the
    /// provider; the list must be non-empty and duplicate-free at registration (whole
    /// stores are never dumped —).
    /// </summary>
    public IList<string> AllowedKeys { get; } = [];

    /// <summary>
    /// Gets the cultures the endpoint serves. Empty (the default) serves any recognized
    /// culture; unrecognized or unlisted cultures produce <c>404</c> and are skipped by
    /// static export.
    /// </summary>
    public IList<string> SupportedCultures { get; } = [];
}
