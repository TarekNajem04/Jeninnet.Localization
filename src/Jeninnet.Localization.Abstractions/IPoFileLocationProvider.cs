//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Resolves the file-system path of a per-culture gettext PO (Portable Object) catalog.
/// </summary>
/// <remarks>
/// Architecture decision  (file layout, recorded before the PO provider):
/// PO catalogs use a per-culture <c>.po</c> file layout
/// (<c>{baseDirectory}/{subdirectory}/{culture}.po</c>, for example
/// <c>Localization/fr.po</c>), mirroring the JSON/XML provider convention and the
/// OrchardCore <c>ILocalizationFileLocationProvider</c> idea. This interface is the
/// cross-assembly location SPI (the cross-assembly contract rule: cross-assembly contracts live in
/// <c>Abstractions</c>); the default directory-convention implementation lives in the
/// PO provider package. The provider stays exact-match (the cross-assembly contract ruleI): this
/// SPI only constructs paths, it never walks parent cultures — culture fallback is
/// owned exclusively by <c>DefaultMessageLocalizer</c>.
/// </remarks>
public interface IPoFileLocationProvider {
    /// <summary>
    /// Gets the PO file path for the specified culture.
    /// </summary>
    /// <param name="culture">The culture to resolve a PO file path for.</param>
    /// <returns>The file-system path of the PO catalog for <paramref name="culture"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is null.</exception>
    string GetPoFilePath(CultureInfo culture);
}
