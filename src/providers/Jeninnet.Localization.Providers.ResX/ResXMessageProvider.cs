//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.ResX;

/// <summary>
/// Provides access to localized messages from RESX resources.
/// </summary>
public class ResXMessageProvider : MessageProviderBase, IInnerMessageProvider {
    private readonly ResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResXMessageProvider"/> class with the specified options.
    /// </summary>
    /// <param name="options">The RESX localization options containing the base name and resource assembly.</param>
    public ResXMessageProvider(ResXLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.BaseName);
        ArgumentNullException.ThrowIfNull(options.ResourceAssembly);

        _resourceManager = new ResourceManager(options.BaseName, options.ResourceAssembly);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResXMessageProvider"/> class with the specified base name and resource assembly.
    /// </summary>
    /// <param name="baseName">The base name of the resource file.</param>
    /// <param name="resourceAssembly">The assembly containing the resource file.</param>
    public ResXMessageProvider(string baseName, Assembly resourceAssembly)
        : this(
              new ResXLocalizationOptions {
                  BaseName = baseName,
                  ResourceAssembly = resourceAssembly
              }
        ) { }

    /// <inheritdoc />
    public override ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ResourceManagerLookup.GetString(_resourceManager, key, culture));
    }
}
