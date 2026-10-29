//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.Diagnostics;

/// <summary>
/// Configuration for the localization health checks (; registered as a
/// singleton by <c>AddLocalizationHealthChecks</c>).
/// </summary>
public sealed class LocalizationHealthChecksOptions {
    /// <summary>
    /// Gets or sets the message key the provider probe resolves. When unset, the provider
    /// health check is not registered (the probe never dumps undefined keys —
    /// decision 5).
    /// </summary>
    public string? ProviderProbeKey { get; set; }

    /// <summary>
    /// Gets or sets the culture the provider probe resolves the key for. When unset, the
    /// ambient UI culture at check time is used; hosts wanting deterministic probes set it
    /// explicitly.
    /// </summary>
    public string? ProviderProbeCulture { get; set; }

    /// <summary>
    /// Gets or sets the registered name of the provider health check. Defaults to
    /// <c>jeninnet-localization-provider</c>.
    /// </summary>
    public string ProviderCheckName { get; set; } = "jeninnet-localization-provider";

    /// <summary>
    /// Gets or sets the registered name of the store health check. Defaults to
    /// <c>jeninnet-localization-store</c>.
    /// </summary>
    public string StoreCheckName { get; set; } = "jeninnet-localization-store";
}
