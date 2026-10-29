//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the localization provider selector tests.
/// </summary>
[TestClass]
public class LocalizationProviderSelectorTests {
    /// <summary>
    /// Verifies that zero providers returns null preserving core policy.
    /// </summary>
    [TestMethod]
    public async Task ZeroProviders_ReturnsNull_PreservingCorePolicyAsync() {
        var selector = new LocalizationProviderSelector([]);

        var result = await selector.GetMessageTemplateAsync("key", CultureInfo.CurrentCulture, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that single provider delegates.
    /// </summary>
    [TestMethod]
    public async Task SingleProvider_DelegatesAsync() {
        var inner = new MockProvider("FromInner");
        var selector = new LocalizationProviderSelector([inner]);

        var result = await selector.GetMessageTemplateAsync("key", CultureInfo.CurrentCulture, CancellationToken.None);

        result.Should().Be("FromInner");
        inner.CallCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that multiple providers throws at construction naming types.
    /// </summary>
    [TestMethod]
    public void MultipleProviders_ThrowsAtConstruction_NamingTypes() {
        var act = () => _ = new LocalizationProviderSelector([
            new MockProvider("a"),
            new MockProvider("b")
        ]);

        var exception = act.Should().Throw<InvalidOperationException>().Exception;
        exception.Message.Should().Contain("2");
        exception.Message.Should().Contain(typeof(MockProvider).FullName!);
    }

    /// <summary>
    /// Verifies that multiple providers throws even when one returns null.
    /// </summary>
    [TestMethod]
    public void MultipleProviders_ThrowsEvenWhenOneReturnsNull() {
        var act = () => _ = new LocalizationProviderSelector([
            new MockProvider(null),
            new MockProvider(null)
        ]);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that null providers collection throws argument null exception.
    /// </summary>
    [TestMethod]
    public void NullProvidersCollection_ThrowsArgumentNullException() {
        var act = () => _ = new LocalizationProviderSelector(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that no culture fallback.
    /// </summary>
    [TestMethod]
    public async Task NoCultureFallbackAsync() {
        var provider = new CultureAwareMockProvider(new Dictionary<string, string> {
            ["en"] = "en-value"
        });
        var selector = new LocalizationProviderSelector([provider]);

        var resultForEn = await selector.GetMessageTemplateAsync("key", new CultureInfo("en"), CancellationToken.None);
        var resultForAr = await selector.GetMessageTemplateAsync("key", new CultureInfo("ar"), CancellationToken.None);

        resultForEn.Should().Be("en-value");
        resultForAr.Should().BeNull();
    }

    /// <summary>
    /// Verifies that get message template async null key throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullKey_ThrowsArgumentNullExceptionAsync() {
        var selector = new LocalizationProviderSelector([new MockProvider("value")]);

        Func<Task> act = async () => await selector.GetMessageTemplateAsync(null!, CultureInfo.CurrentCulture, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null key");
    }

    /// <summary>
    /// Verifies that get message template async null culture throws argument null exception.
    /// </summary>
    [TestMethod]
    public async Task GetMessageTemplateAsync_NullCulture_ThrowsArgumentNullExceptionAsync() {
        var selector = new LocalizationProviderSelector([new MockProvider("value")]);

        Func<Task> act = async () => await selector.GetMessageTemplateAsync("key", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>("the canonical contract defines an ArgumentNullException for a null culture");
    }

    /// <summary>
    /// Verifies that passes culture through.
    /// </summary>
    [TestMethod]
    public async Task PassesCultureThroughAsync() {
        CultureInfo? capturedCulture = null;
        var provider = new MockProvider("value") {
            OnGetTemplate = (_, culture) => capturedCulture = culture
        };
        var selector = new LocalizationProviderSelector([provider]);

        var testCulture = new CultureInfo("ar-SY");
        await selector.GetMessageTemplateAsync("key", testCulture, CancellationToken.None);

        capturedCulture.Should().Be(testCulture);
    }

    /// <summary>
    /// Verifies that propagates inner exceptions.
    /// </summary>
    [TestMethod]
    public async Task PropagatesInnerExceptionsAsync() {
        var selector = new LocalizationProviderSelector([new ExceptionProvider()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.GetMessageTemplateAsync("key", CultureInfo.CurrentCulture, CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Provides mock.
    /// </summary>
    /// <param name="template">The message template.</param>
    private sealed class MockProvider(string? template = null) : IInnerMessageProvider {
        private readonly string? _template = template;

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
            return ValueTask.FromResult(_template);
        }
    }

    /// <summary>
    /// Provides exception.
    /// </summary>
    private sealed class ExceptionProvider : IInnerMessageProvider {
        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Error");
    }

    /// <summary>
    /// Provides culture aware mock.
    /// </summary>
    /// <param name="cultureTemplates">The culture templates.</param>
    private sealed class CultureAwareMockProvider(Dictionary<string, string> cultureTemplates) : IInnerMessageProvider {
        private readonly Dictionary<string, string> _cultureTemplates = cultureTemplates;

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            return ValueTask.FromResult(_cultureTemplates.TryGetValue(culture.Name, out var t) ? t : null);
        }
    }
}
