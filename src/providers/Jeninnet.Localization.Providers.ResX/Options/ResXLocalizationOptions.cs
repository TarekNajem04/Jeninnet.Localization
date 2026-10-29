//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.ResX.Options;

/// <summary>
/// Options for RESX localization provider.
/// </summary>
public class ResXLocalizationOptions {
    /// <summary>
    /// Gets or sets the base name of the resource file.
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly containing the resource file.
    /// </summary>
    public Assembly ResourceAssembly { get; set; } = typeof(ResXMessageProvider).Assembly;
}
