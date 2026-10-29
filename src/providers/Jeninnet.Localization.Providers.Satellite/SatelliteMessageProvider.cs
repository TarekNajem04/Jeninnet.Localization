//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Satellite;

/// <summary>
/// Provides access to localized messages from compiled .NET resources: the neutral
/// resources embedded in an assembly plus its satellite packs
/// (<c>{culture}/{AssemblyName}.resources.dll</c>), resolved through
/// <see cref="ResourceManager"/> satellite probing.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Exact-match only (the cross-assembly contract ruleI): the provider queries
/// <see cref="ResourceManager.GetResourceSet(CultureInfo, bool, bool)"/> with
/// <c>tryParents: false</c> and never re-walks parent cultures — culture fallback is
/// owned exclusively by <c>DefaultMessageLocalizer</c>, so there is no duplicate
/// fallback. Satellite probing (library packs and host packs alike — both are just a
/// <c>(baseName, assembly)</c> pair) is delegated to <see cref="ResourceManager"/>.
/// </item>
/// <item>Strings only: a non-string resource makes the BCL (Base Class Library) lookup throw
/// <see cref="InvalidOperationException"/> ("resource is not a string"), which the
/// provider converts to the documented miss result (<see langword="null"/>) — nothing
/// is converted or serialized.
/// </item>
/// <item>A missing satellite is a clean miss (<see langword="null"/>), never an
/// exception. A missing resource <em>base</em> (misconfigured base name) still throws
/// <see cref="MissingManifestResourceException"/>, preserving
/// <see cref="ResourceManager.GetString(string, CultureInfo)"/> semantics.
/// </item>
/// </list>
/// <c>Providers.ResX</c> is untouched by design: it documents the ResX-authoring
/// workflow, while this provider documents compiled-pack interop (including
/// third-party packs authored outside this repository).
/// </remarks>
public class SatelliteMessageProvider : MessageProviderBase, IInnerMessageProvider {
    private readonly ResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SatelliteMessageProvider"/> class with the specified options.
    /// </summary>
    /// <param name="options">The satellite localization options containing the base name and resource assembly.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/>.BaseName is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/>.ResourceAssembly is null.</exception>
    public SatelliteMessageProvider(SatelliteLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.BaseName);
        ArgumentNullException.ThrowIfNull(options.ResourceAssembly);

        _resourceManager = new ResourceManager(options.BaseName, options.ResourceAssembly);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SatelliteMessageProvider"/> class with the specified base name and resource assembly.
    /// </summary>
    /// <param name="baseName">The base name of the compiled resources.</param>
    /// <param name="resourceAssembly">The assembly that owns the resources and next to which satellite packs are probed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="baseName"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceAssembly"/> is null.</exception>
    public SatelliteMessageProvider(string baseName, Assembly resourceAssembly)
        : this(
              new SatelliteLocalizationOptions {
                  BaseName = baseName,
                  ResourceAssembly = resourceAssembly
              }
        ) { }

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ResourceManagerLookup.GetStringOrNull(_resourceManager, key, culture));
    }
}
