//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the file provider convention tests.
/// </summary>
[TestClass]
public class FileProviderConventionTests {
    /// <summary>
    /// Verifies that json options takes single file path.
    /// </summary>
    [TestMethod]
    public void JsonOptions_TakesSingleFilePath() {
        var filePathProp = typeof(JsonLocalizationOptions).GetProperty("FilePath");

        filePathProp.Should().NotBeNull("JSON options must have a FilePath property for single-file-per-culture");
        filePathProp!.PropertyType.Should().Be(typeof(string), "JSON options must use a single file path, not a collection");

        // Must NOT have a collection property (multiple files/cultures would violate one-file-per-culture)
        var collectionProp = typeof(JsonLocalizationOptions).GetProperties()
            .FirstOrDefault(static p => p.Name is "Files" or "FilePaths" or "Cultures" or "Directories");
        collectionProp.Should().BeNull("JSON options must not support multiple files or cultures");
    }

    /// <summary>
    /// Verifies that xml options takes single file path.
    /// </summary>
    [TestMethod]
    public void XmlOptions_TakesSingleFilePath() {
        var filePathProp = typeof(XmlLocalizationOptions).GetProperty("FilePath");

        filePathProp.Should().NotBeNull("XML options must have a FilePath property for single-file-per-culture");
        filePathProp!.PropertyType.Should().Be(typeof(string), "XML options must use a single file path, not a collection");

        var collectionProp = typeof(XmlLocalizationOptions).GetProperties()
            .FirstOrDefault(static p => p.Name is "Files" or "FilePaths" or "Cultures" or "Directories");
        collectionProp.Should().BeNull("XML options must not support multiple files or cultures");
    }

    /// <summary>
    /// Verifies that po options takes single file path.
    /// </summary>
    [TestMethod]
    public void PoOptions_TakesSingleFilePath() {
        var filePathProp = typeof(PoLocalizationOptions).GetProperty("FilePath");

        filePathProp.Should().NotBeNull("PO options must have a FilePath property for single-file-per-culture");
        filePathProp!.PropertyType.Should().Be(typeof(string), "PO options must use a single file path, not a collection");

        var collectionProp = typeof(PoLocalizationOptions).GetProperties()
            .FirstOrDefault(static p => p.Name is "Files" or "FilePaths" or "Cultures" or "Directories");
        collectionProp.Should().BeNull("PO options must not support multiple files or cultures");
    }

    /// <summary>
    /// Verifies that resx options uses base name and assembly.
    /// </summary>
    [TestMethod]
    public void ResxOptions_UsesBaseNameAndAssembly() {
        var optionsType = typeof(ResXMessageProvider).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Name == "ResXLocalizationOptions");
        optionsType.Should().NotBeNull("ResXLocalizationOptions must exist in the ResX assembly");

        var baseNameProp = optionsType!.GetProperty("BaseName");
        var assemblyProp = optionsType.GetProperty("ResourceAssembly");

        baseNameProp.Should().NotBeNull("ResX options must have BaseName");
        assemblyProp.Should().NotBeNull("ResX options must have ResourceAssembly");
    }

    /// <summary>
    /// Verifies that file providers support opt in hot reload options.
    /// </summary>
    [TestMethod]
    public void FileProviders_SupportOptInHotReloadOptions() {
        var jsonOptions = new JsonLocalizationOptions();
        var xmlOptions = new XmlLocalizationOptions();
        var poOptions = new PoLocalizationOptions();

        jsonOptions.WatchForChanges.Should().BeFalse("hot-reload is opt-in only");
        xmlOptions.WatchForChanges.Should().BeFalse("hot-reload is opt-in only");
        poOptions.WatchForChanges.Should().BeFalse("hot-reload is opt-in only");

        var expectedDebounce = TimeSpan.FromMilliseconds(500);
        jsonOptions.ReloadDebounce.Should().Be(expectedDebounce);
        xmlOptions.ReloadDebounce.Should().Be(expectedDebounce);
        poOptions.ReloadDebounce.Should().Be(expectedDebounce);

        foreach(var providerType in new[] { typeof(JsonMessageProvider), typeof(XmlMessageProvider), typeof(PoMessageProvider) }) {
            typeof(IRefreshableMessageProvider).IsAssignableFrom(providerType).Should().BeTrue(
                $"file provider {providerType.Name} reloads on demand");
            typeof(IProviderEventSource).IsAssignableFrom(providerType).Should().BeTrue(
                $"file provider {providerType.Name} surfaces reloads through the capability events");
        }
    }

    /// <summary>
    /// Verifies that file providers do not accept culture in constructor.
    /// </summary>
    [TestMethod]
    public void FileProviders_DoNotAcceptCultureInConstructor() {
        var jsonCtor = typeof(JsonMessageProvider).GetConstructors()
            .SelectMany(static c => c.GetParameters())
            .Any(static p => p.Name is "culture" or "cultureName");

        var xmlCtor = typeof(XmlMessageProvider).GetConstructors()
            .SelectMany(static c => c.GetParameters())
            .Any(static p => p.Name is "culture" or "cultureName");

        var poCtor = typeof(PoMessageProvider).GetConstructors()
            .SelectMany(static c => c.GetParameters())
            .Any(static p => p.Name is "culture" or "cultureName");

        jsonCtor.Should().BeFalse("JSON provider constructor must not accept culture");
        xmlCtor.Should().BeFalse("XML provider constructor must not accept culture");
        poCtor.Should().BeFalse("PO provider constructor must not accept culture");
    }
}
