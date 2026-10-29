//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the res XProvider file behavior tests.
/// </summary>
[TestClass]
public sealed class ResXProviderFileBehaviorTests {
    private const string TEST_RESOURCES_BASE_NAME = "Jeninnet.Localization.Tests.TestResources";

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <returns>The provider result.</returns>
    private static ResXMessageProvider CreateProvider() =>
        new(new ResXLocalizationOptions {
            BaseName = TEST_RESOURCES_BASE_NAME,
            ResourceAssembly = typeof(ResXProviderFileBehaviorTests).Assembly
        });

    /// <summary>
    /// Verifies that res XProvider returns existing key.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_ReturnsExistingKeyAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().Be("Hello from ResX");
    }

    /// <summary>
    /// Verifies that res XProvider returns null for missing key.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_ReturnsNullForMissingKeyAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync("Missing_Key_ZZZ", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that res XProvider matching is case sensitive.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_MatchingIsCaseSensitiveAsync() {
        var provider = CreateProvider();

        var exact = await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None);
        var upper = await provider.GetMessageTemplateAsync("GREETING", CultureInfo.InvariantCulture, CancellationToken.None);

        exact.Should().Be("Hello from ResX");
        upper.Should().BeNull();
    }

    /// <summary>
    /// Verifies that res XProvider preserves unicode values.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_PreservesUnicodeValuesAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync("Arabic", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().Be("مرحبا");
    }

    /// <summary>
    /// Verifies that res XProvider empty string value distinct from missing.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_EmptyStringValue_DistinctFromMissingAsync() {
        var provider = CreateProvider();

        var empty = await provider.GetMessageTemplateAsync("Empty", CultureInfo.InvariantCulture, CancellationToken.None);
        var missing = await provider.GetMessageTemplateAsync("Missing_Key_ZZZ", CultureInfo.InvariantCulture, CancellationToken.None);

        empty.Should().Be("");
        missing.Should().BeNull();
    }

    /// <summary>
    /// Verifies that res XProvider preserves whitespace.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_PreservesWhitespaceAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync("Whitespace", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().Be("   ");
    }

    /// <summary>
    /// Verifies that res XProvider honors culture parameter.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_HonorsCultureParameterAsync() {
        var provider = CreateProvider();

        var invariant = await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None);
        var arabic = await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("ar"), CancellationToken.None);
        var syrianArabic = await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("ar-SY"), CancellationToken.None);

        invariant.Should().Be("Hello from ResX");
        arabic.Should().Be("مرحبا من العربية");
        syrianArabic.Should().Be("مرحبا من سوريا");
    }

    /// <summary>
    /// Verifies that res XProvider does not fall back to parent culture.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_DoesNotFallBackToParentCultureAsync() {
        var provider = CreateProvider();

        var result = await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("ar-IQ"), CancellationToken.None);

        result.Should().BeNull("the provider must resolve only the requested resource set; parent fallback belongs to DefaultMessageLocalizer");
    }

    /// <summary>
    /// Verifies that default message localizer uses parent culture fallback with res x.
    /// </summary>
    [TestMethod]
    public async Task DefaultMessageLocalizer_UsesParentCultureFallbackWithResXAsync() {
        var localizer = new DefaultMessageLocalizer(CreateProvider(), new DefaultMessageFormatter(), null);

        var result = await localizer.LocalizeAsync(
            "Greeting",
            new Dictionary<string, object?>(),
            new CultureInfo("ar-IQ"),
            CancellationToken.None);

        result.Should().Be("مرحبا من العربية", "parent fallback belongs to DefaultMessageLocalizer");
    }

    /// <summary>
    /// Verifies that res XProvider missing base name construction does not throw.
    /// </summary>
    [TestMethod]
    public void ResXProvider_MissingBaseName_ConstructionDoesNotThrow() {
        var options = new ResXLocalizationOptions {
            BaseName = "Jeninnet.Localization.Tests.NoSuchResource",
            ResourceAssembly = typeof(ResXProviderFileBehaviorTests).Assembly
        };

        var provider = new ResXMessageProvider(options);

        provider.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that res XProvider missing base name first lookup throws.
    /// </summary>
    [TestMethod]
    public async Task ResXProvider_MissingBaseName_FirstLookupThrowsAsync() {
        var provider = new ResXMessageProvider(
            "Jeninnet.Localization.Tests.NoSuchResource",
            typeof(ResXProviderFileBehaviorTests).Assembly
        );

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None);

        await act.Should().ThrowAsync<System.Resources.MissingManifestResourceException>();
    }
}
