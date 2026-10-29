//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Reference.Shared.Localization;

/// <summary>
/// The culture definitions shared by the reference layer: the default-culture fallback target and
/// the supported culture codes.
/// </summary>
public static class ReferenceCultures {
    /// <summary>
    /// The default culture used as the final fallback stage by the message localizer.
    /// </summary>
    public const string DEFAULT_CULTURE = "en";

    /// <summary>
    /// The cultures supplied by the shared localization content, in registration order.
    /// </summary>
    public static IReadOnlyList<string> SupportedCultures { get; } = ["en", "ar", "ar-SY"];
}
