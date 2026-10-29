//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.HostSamples.Shared;

/// <summary>
/// The cultures supported by the host samples.
/// </summary>
public static class HostSampleCultures {
    /// <summary>The default culture.</summary>
    public const string DEFAULT_CULTURE = "en";

    /// <summary>The supported culture codes, in display order.</summary>
    public static IReadOnlyList<string> Supported { get; } = ["en", "ar"];
}
