//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
///  satellite-assembly interop tests: compiled de/ar packs resolve through the
/// canonical pipeline with fallback intact, exact-match only (no duplicate fallback),
/// clean misses, strings-only behavior, and cross-assembly packs.
/// Fixture satellites are built from <c>SatelliteFixture[.de|.ar].resx</c> by the build.
/// </summary>
[TestClass]
public class SatelliteMessageProviderTests {
    private const string FIXTURE_BASE_NAME = "Jeninnet.Localization.Tests.SatelliteFixture";

    private static readonly Assembly _testAssembly = typeof(SatelliteMessageProviderTests).Assembly;

    /// <summary>
    /// Performs the fixture provider operation.
    /// </summary>
    /// <returns>The fixture provider result.</returns>
    private static SatelliteMessageProvider FixtureProvider() => new(FIXTURE_BASE_NAME, _testAssembly);

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(IMessageProvider provider, string defaultCulture = "en") =>
        new(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = defaultCulture });

    /// <summary>
    /// Verifies that satellite packs resolve through pipeline.
    /// </summary>
    [TestMethod]
    public async Task SatellitePacks_ResolveThroughPipelineAsync() {
        var localizer = CreateLocalizer(FixtureProvider());

        (await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None))
            .Should().Be("Hallo aus dem deutschen Satelliten");
        (await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("ar"), CancellationToken.None))
            .Should().Be("مرحبا من القمر الصناعي العربي");
        (await localizer.LocalizeAsync("Farewell", new Dictionary<string, object?>(), new CultureInfo("ar"), CancellationToken.None))
            .Should().Be("وداعا من القمر الصناعي العربي");
    }

    /// <summary>
    /// Verifies that pipeline fallback reaches satellite without provider double walk.
    /// </summary>
    [TestMethod]
    public async Task PipelineFallback_ReachesSatelliteWithoutProviderDoubleWalkAsync() {
        var provider = FixtureProvider();

        // Exact-match only: de-DE has no satellite of its own, so the provider misses
        // even though the de satellite exists — it must not re-walk parents itself.
        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("de-DE"), CancellationToken.None))
            .Should().BeNull("the provider must not walk parent cultures the pipeline walks");
        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("de"), CancellationToken.None))
            .Should().Be("Hallo aus dem deutschen Satelliten");

        // The pipeline fallback chain (de-DE → de) resolves through the satellite.
        var localizer = CreateLocalizer(provider);
        (await localizer.LocalizeAsync("Greeting", new Dictionary<string, object?>(), new CultureInfo("de-DE"), CancellationToken.None))
            .Should().Be("Hallo aus dem deutschen Satelliten", "pipeline fallback stays intact on top");
    }

    /// <summary>
    /// Verifies that missing satellite is clean miss.
    /// </summary>
    [TestMethod]
    public async Task MissingSatellite_IsCleanMissAsync() {
        var provider = FixtureProvider();

        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("fr"), CancellationToken.None))
            .Should().BeNull("a missing satellite is a clean miss, never an exception");
    }

    /// <summary>
    /// Verifies that missing key returns null.
    /// </summary>
    [TestMethod]
    public async Task MissingKey_ReturnsNullAsync() {
        var provider = FixtureProvider();

        (await provider.GetMessageTemplateAsync("NoSuchKey", new CultureInfo("de"), CancellationToken.None)).Should().BeNull();
        (await provider.GetMessageTemplateAsync("NoSuchKey", CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that non string resources resolve to miss.
    /// </summary>
    [TestMethod]
    public async Task NonStringResources_ResolveToMissAsync() {
        var provider = FixtureProvider();

        (await provider.GetMessageTemplateAsync("Blob", CultureInfo.InvariantCulture, CancellationToken.None))
            .Should().BeNull("non-string resources are out of scope and resolve to the miss result");
    }

    /// <summary>
    /// Verifies that invariant pack resolves directly.
    /// </summary>
    [TestMethod]
    public async Task InvariantPack_ResolvesDirectlyAsync() {
        var provider = FixtureProvider();

        (await provider.GetMessageTemplateAsync("Greeting", CultureInfo.InvariantCulture, CancellationToken.None))
            .Should().Be("Hello from the invariant pack");
    }

    /// <summary>
    /// Verifies that cross assembly pack resolves from another assembly.
    /// </summary>
    [TestMethod]
    public async Task CrossAssemblyPack_ResolvesFromAnotherAssemblyAsync() {
        var provider = new SatelliteMessageProvider(
            "Jeninnet.Localization.Core.LocalizationResources",
            typeof(DefaultMessageLocalizer).Assembly);

        (await provider.GetMessageTemplateAsync("FailedToReadData", CultureInfo.InvariantCulture, CancellationToken.None))
            .Should().Be("Failed to read data.", "library packs in other assemblies resolve identically");
    }

    /// <summary>
    /// Verifies that missing base name throws missing manifest resource exception.
    /// </summary>
    [TestMethod]
    public async Task MissingBaseName_ThrowsMissingManifestResourceExceptionAsync() {
        var provider = new SatelliteMessageProvider("Jeninnet.Localization.Tests.NoSuchBase", _testAssembly);

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("de"), CancellationToken.None);

        await act.Should().ThrowAsync<MissingManifestResourceException>(
            "a misconfigured base name surfaces instead of silently missing");
    }

    /// <summary>
    /// Verifies that ctor null options throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullOptions_ThrowsArgumentNullException() {
        var act = static () => new SatelliteMessageProvider((SatelliteLocalizationOptions)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ctor empty base name throws argument exception.
    /// </summary>
    [TestMethod]
    public void Ctor_EmptyBaseName_ThrowsArgumentException() {
        var act = () => new SatelliteMessageProvider("", _testAssembly);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that ctor null assembly throws argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullAssembly_ThrowsArgumentNullException() {
        var act = () => new SatelliteMessageProvider(FIXTURE_BASE_NAME, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that get message template async pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var provider = FixtureProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("de"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancellation is observed before any source access and never becomes a missing-resource result");
    }

    /// <summary>
    /// Verifies that get message template async null arguments throw argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullArguments_ThrowArgumentNullExceptionAsync() {
        var provider = FixtureProvider();

        Func<Task> nullKey = async () => await provider.GetMessageTemplateAsync(null!, new CultureInfo("de"), CancellationToken.None);
        Func<Task> nullCulture = async () => await provider.GetMessageTemplateAsync("Greeting", null!, CancellationToken.None);

        await nullKey.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that get message template async null culture with pre cancelled token still throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_WithPreCancelledToken_StillThrowsArgumentNullExceptionAsync() {
        var provider = FixtureProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await provider.GetMessageTemplateAsync("Greeting", null!, cancellation.Token);

        await act.Should().ThrowAsync<ArgumentNullException>(
            "argument validation retains precedence over cancellation at the provider boundary");
    }

    /// <summary>
    /// Verifies that di add satellite localization registers inner provider.
    /// </summary>
    [TestMethod]
    public void Di_AddSatelliteLocalization_RegistersInnerProvider() {
        var services = new ServiceCollection();
        services.AddSatelliteLocalization(FIXTURE_BASE_NAME, _testAssembly);

        var provider = services.BuildServiceProvider().GetService<IInnerMessageProvider>();

        provider.Should().BeOfType<SatelliteMessageProvider>();
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
