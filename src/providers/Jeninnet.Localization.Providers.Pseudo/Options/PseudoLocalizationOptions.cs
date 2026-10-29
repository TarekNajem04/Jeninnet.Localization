//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Pseudo.Options;

/// <summary>
/// Options for the pseudo-localization provider: the source catalog to
/// transform, the synthetic culture served, and the expansion amount.
/// </summary>
public class PseudoLocalizationOptions {
    /// <summary>
    /// Gets or sets the source templates transformed for the pseudo culture.
    /// </summary>
    public IReadOnlyDictionary<string, string> SourceTemplates { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the synthetic culture served (default <c>qps-ploc</c>).
    /// Compared ordinal-ignore-case against the requested culture; never resolved
    /// through <see cref="CultureInfo.GetCultureInfo(string)"/> because pseudo
    /// codes are host-ICU-dependent while matching itself is exact.
    /// </summary>
    public string PseudoCulture { get; set; } = "qps-ploc";

    /// <summary>
    /// Gets or sets the padding added as a percentage of the accented length
    /// (default 35). Validated to 0–100.
    /// </summary>
    public int ExpansionPercent { get; set; } = 35;
}
