//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Satellite.Options;

/// <summary>
/// Options for the satellite-assembly localization provider.
/// </summary>
public class SatelliteLocalizationOptions {
    /// <summary>
    /// Gets or sets the base name of the compiled resources (the
    /// <c>{RootNamespace}.{ResourcePath}</c> manifest name, without culture or extension).
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly that owns the resources. Satellite packs
    /// (<c>{culture}/{AssemblyName}.resources.dll</c>) are probed next to this assembly,
    /// so library packs and host packs are addressed identically.
    /// </summary>
    public Assembly ResourceAssembly { get; set; } = typeof(SatelliteMessageProvider).Assembly;
}
