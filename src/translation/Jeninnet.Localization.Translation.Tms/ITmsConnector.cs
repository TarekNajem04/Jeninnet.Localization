//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// The vendor-neutral contract for translation management system (TMS) connectors.
/// One implementation per vendor (Crowdin for SaaS, Weblate for self-hosted)
/// shares this surface so governed workflows — pull, translator round trip, push —
/// never fork per vendor.
/// </summary>
/// <remarks>
/// Pull returns the translated snapshot for one culture (templates plus translator
/// notes where the vendor carries them). Push uploads translated catalogs with a
/// dry-run preview and per-key conflict reporting that reuses the interchange
/// <see cref="CatalogKeyStatus"/> vocabulary, so TMS conflicts read exactly like
/// <c>CsvCatalogInterchange</c>/<c>PoCatalogInterchange</c> import reports.
/// Applying an approved push to the governed store stays the caller's composition
/// (through <c>IResourceAdminStore</c>); connectors never write catalog files or
/// store entries themselves.
/// </remarks>
public interface ITmsConnector {
    /// <summary>
    /// Pulls the translated snapshot for one culture.
    /// </summary>
    /// <param name="culture">The target culture to pull.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The translated templates and translator notes keyed by catalog key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    /// <exception cref="TmsConnectorException">Thrown when the vendor API cannot serve the snapshot.</exception>
    Task<TmsPullResult> PullAsync(CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes translated catalogs with a dry-run preview and per-key conflict reporting.
    /// A dry-run run compares against the vendor snapshot and reports without uploading
    /// anything; a live run uploads the translated file after the same comparison.
    /// </summary>
    /// <param name="request">The push request (culture, source governance, proposed target, format, dry-run).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The per-key report reusing the <see cref="CatalogKeyStatus"/> vocabulary.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="TmsConnectorException">Thrown when the vendor API cannot complete the push.</exception>
    Task<TmsPushReport> PushAsync(TmsPushRequest request, CancellationToken cancellationToken = default);
}
