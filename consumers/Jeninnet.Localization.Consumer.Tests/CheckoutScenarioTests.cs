//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Platform/
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Consumer.Tests;

/// <summary>
/// Drives the consumer scenario end to end. Every assertion exercises a package resolved
/// from the local feed: the Memory provider pipeline, the JSON provider catalogs, the CSV
/// interchange, and the generator-emitted key constants.
/// </summary>
[TestClass]
public class CheckoutScenarioTests {
    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies that scenario resolves every step through the packed packages.
    /// </summary>
    [TestMethod]
    public async Task Scenario_ResolvesEveryStepThroughThePackedPackagesAsync() {
        var result = await CheckoutScenario.RunAsync(AppContext.BaseDirectory, TestContext.CancellationToken);

        result.Thanks.Should().Be("Thank you, Lina!", "the Memory provider serves the generated key with formatted arguments");
        result.Total.Should().Be("Total: 133.38");
        result.ArabicThanks.Should().Contain("Lina", "the 'ar' JSON catalog resolves the same key through the canonical pipeline");
        result.ArabicThanks.Should().StartWith("شكراً");
    }

    /// <summary>
    /// Verifies that scenario exports the translator pair as csv.
    /// </summary>
    [TestMethod]
    public async Task Scenario_ExportsTheTranslatorPairAsCsvAsync() {
        var result = await CheckoutScenario.RunAsync(AppContext.BaseDirectory, TestContext.CancellationToken);

        result.Csv.Should().Contain("key,culture,value,note", "the interchange package emits the canonical CSV schema");
        result.Csv.Should().Contain("Checkout.ThankYou");
        result.Csv.Should().Contain("Thank you, {customer}!");
        result.Csv.Should().Contain("شكراً");
    }

    /// <summary>
    /// Verifies that generated keys match the catalog contract.
    /// </summary>
    [TestMethod]
    public void GeneratedKeys_MatchTheCatalogContract() {
        LocalizationKeys.CHECKOUT_THANK_YOU.Should().Be("Checkout.ThankYou", "the packed generator emits one constant per catalog key");
        LocalizationKeys.CHECKOUT_TOTAL.Should().Be("Checkout.Total");
        LocalizationKeys.All.Should().Contain(LocalizationKeys.CHECKOUT_THANK_YOU, "the generated table carries every key");
        LocalizationKeys.All.Should().Contain(LocalizationKeys.CHECKOUT_TOTAL);
    }

    /// <summary>
    /// Verifies that scenario requires the base directory.
    /// </summary>
    [TestMethod]
    public async Task Scenario_RequiresTheBaseDirectoryAsync() {
        Func<Task> run = () => CheckoutScenario.RunAsync(string.Empty, TestContext.CancellationToken);

        await run.Should().ThrowAsync<ArgumentException>();
    }
}
