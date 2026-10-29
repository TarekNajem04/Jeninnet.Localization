//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po.Options;

/// <summary>
/// Selects how the PO (Portable Object) provider reads its catalog file.
/// </summary>
public enum PoCatalogFormat {
    /// <summary>
    /// Selects the reader by file extension: <c>.mo</c> (ordinal-ignore-case) uses the
    /// GNU MO (Machine Object) binary reader; any other extension uses the PO text reader. No content
    /// probing is performed, so extensionless or misnamed files keep the text path.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Forces the PO text reader regardless of the file extension.
    /// </summary>
    Po = 1,

    /// <summary>
    /// Forces the GNU MO binary reader regardless of the file extension.
    /// </summary>
    Mo = 2,
}
