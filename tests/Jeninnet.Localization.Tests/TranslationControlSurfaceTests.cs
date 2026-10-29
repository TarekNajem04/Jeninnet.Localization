//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the translation control surface tests.
/// </summary>
[TestClass]
public class TranslationControlSurfaceTests {
    // ─── Azure: options validation ──────────────────────────────────────

    /// <summary>
    /// Verifies that azure options blank api key throws argument exception.
    /// </summary>
    [TestMethod]
    public void AzureOptions_BlankApiKey_ThrowsArgumentException() {
        var act = () => new AzureTranslationOptions { ApiKey = " " };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure options blank endpoint throws argument exception.
    /// </summary>
    [TestMethod]
    public void AzureOptions_BlankEndpoint_ThrowsArgumentException() {
        var act = () => new AzureTranslationOptions { Endpoint = "" };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure options relative endpoint throws argument exception.
    /// </summary>
    [TestMethod]
    public void AzureOptions_RelativeEndpoint_ThrowsArgumentException() {
        var act = () => new AzureTranslationOptions { Endpoint = "/translate" };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure options non http endpoint throws argument exception.
    /// </summary>
    [TestMethod]
    public void AzureOptions_NonHttpEndpoint_ThrowsArgumentException() {
        var act = () => new AzureTranslationOptions { Endpoint = "ftp://example.com/translate" };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure options valid configuration succeeds.
    /// </summary>
    [TestMethod]
    public void AzureOptions_ValidConfiguration_Succeeds() {
        var options = new AzureTranslationOptions { ApiKey = "key" };

        options.ApiKey.Should().Be("key");
        options.Endpoint.Should().Be("https://api.cognitive.microsofttranslator.com");
        options.Region.Should().Be(string.Empty);
    }

    // ─── Azure: registration validation ─────────────────────────────────

    /// <summary>
    /// Verifies that azure registration missing api key fails at registration.
    /// </summary>
    [TestMethod]
    public void AzureRegistration_MissingApiKey_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddAzureTranslationLocalization(static _ => { });

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("ApiKey is not configured");
    }

    /// <summary>
    /// Verifies that azure registration blank api key fails at registration.
    /// </summary>
    [TestMethod]
    public void AzureRegistration_BlankApiKey_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddAzureTranslationLocalization(static options => options.ApiKey = " ");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure registration invalid endpoint fails at registration.
    /// </summary>
    [TestMethod]
    public void AzureRegistration_InvalidEndpoint_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddAzureTranslationLocalization(static options => options.Endpoint = "ftp://example.com/translate");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that azure registration valid configuration resolves provider.
    /// </summary>
    [TestMethod]
    public void AzureRegistration_ValidConfiguration_ResolvesProvider() {
        var services = new ServiceCollection();
        services.AddAzureTranslationLocalization(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<AzureTranslationProvider>();
    }

    /// <summary>
    /// Verifies that azure registration registers as IInner message provider.
    /// </summary>
    [TestMethod]
    public void AzureRegistration_RegistersAsIInnerMessageProvider() {
        var services = new ServiceCollection();
        services.AddAzureTranslationLocalization(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IInnerMessageProvider>();
        (resolved is not null).Should()
                              .BeTrue("[Architecture] Azure translation Provider must be resolvable through the canonical IInnerMessageProvider contract");
    }

    // ─── Azure: no-argument registration semantics ──────────────────────

    /// <summary>
    /// Verifies that azure no arg registration without options fails at resolution.
    /// </summary>
    [TestMethod]
    public void AzureNoArgRegistration_WithoutOptions_FailsAtResolution() {
        var services = new ServiceCollection();
        services.AddAzureTranslationLocalization();
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IInnerMessageProvider>();

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("ApiKey is not configured");
    }

    /// <summary>
    /// Verifies that azure no arg registration with host configured options resolves provider.
    /// </summary>
    [TestMethod]
    public void AzureNoArgRegistration_WithHostConfiguredOptions_ResolvesProvider() {
        var services = new ServiceCollection();
        services.AddAzureTranslationLocalization();
        services.Configure<AzureTranslationOptions>(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<AzureTranslationProvider>();
    }

    // ─── Azure: direct construction validation ──────────────────────────

    /// <summary>
    /// Verifies that azure provider direct construction invalid options fails at construction.
    /// </summary>
    [TestMethod]
    public void AzureProvider_DirectConstruction_InvalidOptions_FailsAtConstruction() {
        var act = () => new AzureTranslationProvider(
            Options.Create(new AzureTranslationOptions()),
            new HttpClient());

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that azure provider direct construction valid options succeeds.
    /// </summary>
    [TestMethod]
    public void AzureProvider_DirectConstruction_ValidOptions_Succeeds() {
        var provider = new AzureTranslationProvider(
            Options.Create(new AzureTranslationOptions { ApiKey = "key" }),
            new HttpClient());

        provider.Should().NotBeNull();
    }

    // ─── Google: options validation ─────────────────────────────────────

    /// <summary>
    /// Verifies that google options blank api key throws argument exception.
    /// </summary>
    [TestMethod]
    public void GoogleOptions_BlankApiKey_ThrowsArgumentException() {
        var act = () => new GoogleTranslationOptions { ApiKey = " " };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that google options blank endpoint throws argument exception.
    /// </summary>
    [TestMethod]
    public void GoogleOptions_BlankEndpoint_ThrowsArgumentException() {
        var act = () => new GoogleTranslationOptions { Endpoint = "" };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that google options non http endpoint throws argument exception.
    /// </summary>
    [TestMethod]
    public void GoogleOptions_NonHttpEndpoint_ThrowsArgumentException() {
        var act = () => new GoogleTranslationOptions { Endpoint = "ftp://example.com/translate/v2" };
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that google options valid configuration succeeds.
    /// </summary>
    [TestMethod]
    public void GoogleOptions_ValidConfiguration_Succeeds() {
        var options = new GoogleTranslationOptions { ApiKey = "key" };

        options.ApiKey.Should().Be("key");
        options.Endpoint.Should().Be("https://translation.googleapis.com/language/translate/v2");
    }

    // ─── Google: registration validation ────────────────────────────────

    /// <summary>
    /// Verifies that google registration missing api key fails at registration.
    /// </summary>
    [TestMethod]
    public void GoogleRegistration_MissingApiKey_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddGoogleTranslationLocalization(static _ => { });

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("ApiKey is not configured");
    }

    /// <summary>
    /// Verifies that google registration blank api key fails at registration.
    /// </summary>
    [TestMethod]
    public void GoogleRegistration_BlankApiKey_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddGoogleTranslationLocalization(static options => options.ApiKey = " ");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that google registration invalid endpoint fails at registration.
    /// </summary>
    [TestMethod]
    public void GoogleRegistration_InvalidEndpoint_FailsAtRegistration() {
        var services = new ServiceCollection();
        var act = () => services.AddGoogleTranslationLocalization(static options => options.Endpoint = "ftp://example.com/translate/v2");

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that google registration valid configuration resolves provider.
    /// </summary>
    [TestMethod]
    public void GoogleRegistration_ValidConfiguration_ResolvesProvider() {
        var services = new ServiceCollection();
        services.AddGoogleTranslationLocalization(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<GoogleTranslationProvider>();
    }

    /// <summary>
    /// Verifies that google registration registers as IInner message provider.
    /// </summary>
    [TestMethod]
    public void GoogleRegistration_RegistersAsIInnerMessageProvider() {
        var services = new ServiceCollection();
        services.AddGoogleTranslationLocalization(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IInnerMessageProvider>();
        (resolved is not null).Should()
                              .BeTrue("[Architecture] Google translation Provider must be resolvable through the canonical IInnerMessageProvider contract");
    }

    // ─── Google: no-argument registration semantics ─────────────────────

    /// <summary>
    /// Verifies that google no arg registration without options fails at resolution.
    /// </summary>
    [TestMethod]
    public void GoogleNoArgRegistration_WithoutOptions_FailsAtResolution() {
        var services = new ServiceCollection();
        services.AddGoogleTranslationLocalization();
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IInnerMessageProvider>();

        act.Should().Throw<InvalidOperationException>().Exception.Message.Should().Contain("ApiKey is not configured");
    }

    /// <summary>
    /// Verifies that google no arg registration with host configured options resolves provider.
    /// </summary>
    [TestMethod]
    public void GoogleNoArgRegistration_WithHostConfiguredOptions_ResolvesProvider() {
        var services = new ServiceCollection();
        services.AddGoogleTranslationLocalization();
        services.Configure<GoogleTranslationOptions>(static options => options.ApiKey = "key");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<GoogleTranslationProvider>();
    }

    // ─── Google: direct construction validation ─────────────────────────

    /// <summary>
    /// Verifies that google provider direct construction invalid options fails at construction.
    /// </summary>
    [TestMethod]
    public void GoogleProvider_DirectConstruction_InvalidOptions_FailsAtConstruction() {
        var act = () => new GoogleTranslationProvider(Options.Create(new GoogleTranslationOptions()), new HttpClient());

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that google provider direct construction valid options succeeds.
    /// </summary>
    [TestMethod]
    public void GoogleProvider_DirectConstruction_ValidOptions_Succeeds() {
        var provider = new GoogleTranslationProvider(
            Options.Create(new GoogleTranslationOptions { ApiKey = "key" }),
            new HttpClient());

        provider.Should().NotBeNull();
    }
}
