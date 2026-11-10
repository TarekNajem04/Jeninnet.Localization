//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms;

/// <summary>
/// The catalog file shape a connector uploads on push. Both shapes round-trip
/// translator notes where the format carries them (PO <c>#.</c> comments, XLIFF
/// translator notes); the vendor boundary never invents keys.
/// </summary>
public enum TmsCatalogFormat {
    /// <summary>
    /// PO (gettext text): <c>msgid</c> is the catalog key, <c>msgstr</c> the template.
    /// </summary>
    Po = 0,

    /// <summary>
    /// XLIFF 2.0: unit ids are the catalog keys, targets the templates.
    /// </summary>
    Xliff = 1,
}
