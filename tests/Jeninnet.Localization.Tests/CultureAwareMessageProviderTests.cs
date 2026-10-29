//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the culture aware message provider tests.
/// </summary>
[TestClass]
public class CultureAwareMessageProviderTests {
    /// <summary>
    /// Verifies that get message template async uses exact culture name provider.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_UsesExactCultureNameProviderAsync() {
        var en = new MockProvider("en-value");
        var ar = new MockProvider("ar-value");

        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["en"] = en, ["ar"] = ar });

        var result = await dispatcher.GetMessageTemplateAsync("key", new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("en-value");
        en.CallCount.Should().Be(1);
        ar.CallCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that get message template async falls back to two letter iso name.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_FallsBackToTwoLetterIsoNameAsync() {
        IMessageProvider? provider = null;
        CultureInfo? capturedCulture = null;
        provider = new MockProvider("fr-CA-value") {
            OnGetTemplate = (_, culture) => capturedCulture = culture
        };

        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["fr"] = provider });

        var testCulture = new CultureInfo("fr-CA");
        var result = await dispatcher.GetMessageTemplateAsync("key", testCulture, CancellationToken.None);

        result.Should().Be("fr-CA-value");
        capturedCulture.Should().Be(testCulture, "the dispatcher must pass the original requested culture to the selected provider");
    }

    /// <summary>
    /// Verifies that get message template async prefers exact culture over two letter iso.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_PrefersExactCultureOverTwoLetterIsoAsync() {
        var exact = new MockProvider("exact-value");
        var iso = new MockProvider("iso-value");

        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["fr"] = iso, ["fr-CA"] = exact });

        var result = await dispatcher.GetMessageTemplateAsync("key", new CultureInfo("fr-CA"), CancellationToken.None);

        result.Should().Be("exact-value");
        exact.CallCount.Should().Be(1);
        iso.CallCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that get message template async does not walk parent cultures.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_DoesNotWalkParentCulturesAsync() {
        var unrelated = new MockProvider("unrelated-value");

        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["fr-CA"] = unrelated });

        var result = await dispatcher.GetMessageTemplateAsync("key", new CultureInfo("fr-CH"), CancellationToken.None);

        result.Should().BeNull("no parent-culture traversal: fr-CH must not resolve through a fr-CA provider");
        unrelated.CallCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that get message template async returns null when no provider matches.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_ReturnsNullWhenNoProviderMatchesAsync() {
        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["en"] = new MockProvider("en-value") });

        var result = await dispatcher.GetMessageTemplateAsync("key", new CultureInfo("de"), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that get message template async does not query invariant culture.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_DoesNotQueryInvariantCultureAsync() {
        var invariantProvider = new ThrowingProvider();

        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider> { ["iv"] = invariantProvider });

        var result = await dispatcher.GetMessageTemplateAsync("key", CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().BeNull("the dispatcher must not query the invariant culture");
    }

    /// <summary>
    /// Verifies that get message template async throws on null key.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_ThrowsOnNullKeyAsync() {
        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.GetMessageTemplateAsync(null!, CultureInfo.CurrentCulture, CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Verifies that get message template async throws on null culture.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_ThrowsOnNullCultureAsync() {
        var dispatcher = new CultureAwareMessageProvider(
            new Dictionary<string, IMessageProvider>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.GetMessageTemplateAsync("key", null!, CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Provides mock.
    /// </summary>
    /// <param name="template">The message template.</param>
    private sealed class MockProvider(string? template) : IMessageProvider {
        /// <summary>
        /// Gets or sets the call count.
        /// </summary>
        public int CallCount { get; private set; }
        /// <summary>
        /// Gets or sets the on get template.
        /// </summary>
        public Action<string, CultureInfo>? OnGetTemplate { get; set; }

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            CallCount++;
            OnGetTemplate?.Invoke(key, culture);
            return ValueTask.FromResult(template);
        }
    }

    /// <summary>
    /// Provides throwing.
    /// </summary>
    private sealed class ThrowingProvider : IMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The invariant culture must never reach a provider");
    }
}
