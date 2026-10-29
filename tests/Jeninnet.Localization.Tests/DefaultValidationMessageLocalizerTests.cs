//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the default validation message localizer tests.
/// </summary>
[TestClass]
public class DefaultValidationMessageLocalizerTests {
    private readonly IMessageFormatter _formatter = new DefaultMessageFormatter();
    private readonly LocalizationOptions _options = new() { DefaultCulture = "en" };

    /// <summary>
    /// Verifies that localize async when provider returns null and no default culture returns null.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_WhenProviderReturnsNull_AndNoDefaultCulture_ReturnsNullAsync() {
        var provider = new MockProvider(null, null);
        var localizer = new DefaultMessageLocalizer(provider, _formatter, null);

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("fr"), CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that localize async when provider returns null but default culture exists returns default.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_WhenProviderReturnsNull_ButDefaultCultureExists_ReturnsDefaultAsync() {
        var provider = new MockProvider(new Dictionary<string, string> {
            { "en", "Default Template" }
        });
        var localizer = new DefaultMessageLocalizer(provider, _formatter, _options);

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("fr"), CancellationToken.None);

        result.Should().Be("Default Template");
    }

    /// <summary>
    /// Verifies that localize async traverses culture hierarchy.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_TraversesCultureHierarchyAsync() {
        var provider = new MockProvider(new Dictionary<string, string> {
            { "ar", "Arabic Template" }
        });
        var localizer = new DefaultMessageLocalizer(provider, _formatter, _options);

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("ar-SY"), CancellationToken.None);

        result.Should().Be("Arabic Template");
    }

    /// <summary>
    /// Verifies that localize async terminates at invariant culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_TerminatesAtInvariantCultureAsync() {
        var provider = new MockProvider(
            new Dictionary<string, string> {
                { "en", "English Template" }
            }
        );
        var localizer = new DefaultMessageLocalizer(provider, _formatter, _options);

        // Try to resolve a culture that is already invariant or resolves to invariant
        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), CultureInfo.InvariantCulture, CancellationToken.None);

        // Should return the template for invariant if present, or proceed to DefaultCulture
        result.Should().Be("English Template");
    }

    /// <summary>
    /// Verifies that localize async null key throws.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_NullKey_ThrowsAsync() {
        var localizer = new DefaultMessageLocalizer(new MockProvider("template"), _formatter, _options);

        Func<Task> act = async () => await localizer.LocalizeAsync(null!, new Dictionary<string, object?>(), new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async null parameters throws.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_NullParameters_ThrowsAsync() {
        var localizer = new DefaultMessageLocalizer(new MockProvider("template"), _formatter, _options);

        Func<Task> act = async () => await localizer.LocalizeAsync("key", null!, new CultureInfo("en"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async null culture throws.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_NullCulture_ThrowsAsync() {
        var localizer = new DefaultMessageLocalizer(new MockProvider("template"), _formatter, _options);

        Func<Task> act = async () => await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that constructor null provider throws.
    /// </summary>
    [TestMethod]
    public void Constructor_NullProvider_Throws() {
        var act = () => new DefaultMessageLocalizer(null!, _formatter, _options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that constructor null formatter throws.
    /// </summary>
    [TestMethod]
    public void Constructor_NullFormatter_Throws() {
        var act = () => new DefaultMessageLocalizer(new MockProvider("template"), null!, _options);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize async default culture is snapshotted at construction.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_DefaultCultureIsSnapshottedAtConstructionAsync() {
        var options = new LocalizationOptions { DefaultCulture = "en" };
        var provider = new MockProvider(new Dictionary<string, string> { ["en"] = "Snapshot Template" });
        var localizer = new DefaultMessageLocalizer(provider, _formatter, options);

        var beforeMutation = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("fr"), CancellationToken.None);
        beforeMutation.Should().Be("Snapshot Template");

        options.DefaultCulture = "zz";

        var afterMutation = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("fr"), CancellationToken.None);
        afterMutation.Should().Be("Snapshot Template");
    }

    /// <summary>
    /// Provides mock.
    /// </summary>
    private sealed class MockProvider : IMessageProvider {
        private readonly Dictionary<string, string>? _templates;
        private readonly string? _singleTemplate;

        /// <summary>
        /// Initializes a new instance of the MockProvider class.
        /// </summary>
        /// <param name="singleTemplate">The single template.</param>
        /// <param name="templates">The templates.</param>
        public MockProvider(string? singleTemplate, Dictionary<string, string>? templates = null) {
            _singleTemplate = singleTemplate;
            _templates = templates;
        }

        /// <summary>
        /// Initializes a new instance of the MockProvider class.
        /// </summary>
        /// <param name="templates">The templates.</param>
        public MockProvider(Dictionary<string, string>? templates) {
            _singleTemplate = null;
            _templates = templates;
        }

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            if(_singleTemplate is not null) {
                return ValueTask.FromResult<string?>(_singleTemplate);
            }

            return ValueTask.FromResult(_templates?.TryGetValue(culture.Name, out var t) == true ? t : null);
        }
    }
}
