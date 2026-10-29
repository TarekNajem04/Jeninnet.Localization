//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using Jeninnet.Localization.Abstractions;
using Microsoft.Extensions.DependencyInjection;

#if JENINNET_PROVIDER_SHARED_JSON
namespace Jeninnet.Localization.Providers.Json;
#elif JENINNET_PROVIDER_SHARED_XML
namespace Jeninnet.Localization.Providers.Xml;
#elif JENINNET_PROVIDER_SHARED_PO
namespace Jeninnet.Localization.Providers.Po;
#else
#error A provider project must define JENINNET_PROVIDER_SHARED_JSON, JENINNET_PROVIDER_SHARED_XML, or JENINNET_PROVIDER_SHARED_PO.
#endif

internal static class ProviderRegistration {
    public static IServiceCollection AddFileProvider<TProvider>(
        IServiceCollection services,
        string filePath,
        string formatLabel,
        Func<TProvider> factory
    ) where TProvider : class, IInnerMessageProvider {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if(!File.Exists(filePath)) {
            throw new FileNotFoundException($"The specified {formatLabel} file was not found: {filePath}");
        }

        services.AddSingleton<IInnerMessageProvider>(factory());

        return services;
    }

    public static IServiceCollection AddCultureProviders(
        IServiceCollection services,
        IReadOnlyCollection<string> cultures,
        Func<string, string> pathResolver,
        Func<string, IMessageProvider> factory
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentNullException.ThrowIfNull(pathResolver);
        ArgumentNullException.ThrowIfNull(factory);

        if(cultures.Count == 0) {
            throw new ArgumentException("At least one culture is required.", nameof(cultures));
        }

        var providers = new Dictionary<string, IMessageProvider>(cultures.Count, StringComparer.Ordinal);
        foreach(var culture in cultures) {
            ArgumentException.ThrowIfNullOrEmpty(culture);
            providers[culture] = factory(pathResolver(culture));
        }

        services.AddSingleton<IInnerMessageProvider>(new CultureAwareMessageProvider(providers));

        return services;
    }

    public static IServiceCollection AddDiscoveredProviders(
        IServiceCollection services,
        string baseDirectory,
        string subdirectory,
        string extension,
        string formatLabel,
        Func<string, IMessageProvider> factory
    ) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);

        var dir = Path.Combine(baseDirectory, subdirectory);
        if(!Directory.Exists(dir)) {
            throw new DirectoryNotFoundException(
                $"Localization directory not found: '{dir}'. " +
                $"Expected {formatLabel} files named by culture code (e.g., '{Path.Combine(dir, "en" + extension)}').");
        }

        var cultures = Directory.EnumerateFiles(dir, "*" + extension)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(static c => c.Length > 0)
            .ToArray();

        if(cultures.Length == 0) {
            throw new InvalidOperationException(
                $"No {formatLabel} files found in '{dir}'. " +
                $"Name files by culture code (e.g., en{extension}, ar{extension}).");
        }

        return AddCultureProviders(services, cultures, culture => Path.Combine(dir, culture + extension), factory);
    }

    public static IServiceCollection AddBuilderProvider<TBuilder>(
        IServiceCollection services,
        Action<TBuilder> configure,
        Func<TBuilder, IInnerMessageProvider> build
    ) where TBuilder : new() {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new TBuilder();
        configure(builder);
        services.AddSingleton(build(builder));

        return services;
    }
}

internal sealed class CulturePathProviderBuilder(string extension, Func<string, IMessageProvider> factory) {
    private readonly Dictionary<string, string> _culturePaths = new(StringComparer.Ordinal);

    public void AddCulture(string culture, string filePath) {
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _culturePaths[culture] = filePath;
    }

    public void AddCultures(string baseDirectory, string[] cultures, string subdirectory) {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        ArgumentNullException.ThrowIfNull(cultures);

        if(cultures.Length == 0) {
            throw new ArgumentException("At least one culture is required.", nameof(cultures));
        }

        foreach(var culture in cultures) {
            ArgumentException.ThrowIfNullOrEmpty(culture);
            _culturePaths[culture] = Path.Combine(baseDirectory, subdirectory, culture + extension);
        }
    }

    public void AddCultures(IEnumerable<string> cultures, Func<string, string> pathResolver) {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentNullException.ThrowIfNull(pathResolver);

        var cultureArray = cultures as string[] ?? [.. cultures];
        if(cultureArray.Length == 0) {
            throw new ArgumentException("At least one culture is required.", nameof(cultures));
        }

        foreach(var culture in cultureArray) {
            ArgumentException.ThrowIfNullOrEmpty(culture);
            _culturePaths[culture] = pathResolver(culture);
        }
    }

    public IInnerMessageProvider Build() {
        if(_culturePaths.Count == 0) {
            throw new InvalidOperationException("At least one culture must be added before building.");
        }

        var providers = new Dictionary<string, IMessageProvider>(_culturePaths.Count, StringComparer.Ordinal);
        foreach(var kvp in _culturePaths) {
            providers[kvp.Key] = factory(kvp.Value);
        }

        return new CultureAwareMessageProvider(providers);
    }
}
