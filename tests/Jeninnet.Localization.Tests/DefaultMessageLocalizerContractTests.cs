//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused runtime-contract evidence for <see cref="DefaultMessageLocalizer"/> (Architecture §5,
/// §30.7): exact culture resolution, multi-level parent traversal, invariant-culture exclusion,
/// default-culture reachability, and formatter invocation semantics. The single fallback owner is the
/// localizer; the provider is exact-match only and the formatter runs only after a template resolves.
/// </summary>
[TestClass]
public class DefaultMessageLocalizerContractTests {
    /// <summary>
    /// Verifies that localize async exact culture hit queries only the requested culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_ExactCultureHit_QueriesOnlyTheRequestedCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["en-GB"] = "British English",
            ["en"] = "English",
            ["ar"] = "Arabic"
        });
        var localizer = CreateLocalizer(provider, defaultCulture: "en");

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("en-GB"), CancellationToken.None);

        result.Should().Be("British English");
        provider.RequestedCultures.Should().HaveCount(1);
        provider.RequestedCultures[0].Should().Be("en-GB");
    }

    /// <summary>
    /// Verifies that localize async multi level parent traversal resolves at third level.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_MultiLevelParentTraversal_ResolvesAtThirdLevelAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["sr"] = "Serbian"
        });
        var localizer = CreateLocalizer(provider, defaultCulture: "en");

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("sr-Latn-RS"), CancellationToken.None);

        // sr-Latn-RS -> sr-Latn -> sr -> InvariantCulture (verified chain).
        result.Should().Be("Serbian");
        provider.RequestedCultures.Should().HaveCount(3);
        provider.RequestedCultures[0].Should().Be("sr-Latn-RS");
        provider.RequestedCultures[1].Should().Be("sr-Latn");
        provider.RequestedCultures[2].Should().Be("sr");
    }

    /// <summary>
    /// Verifies that localize async fallback walk never queries invariant culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_FallbackWalk_NeverQueriesInvariantCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["en"] = "English"
        });
        var localizer = CreateLocalizer(provider, defaultCulture: "en");

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None);

        // "de" misses, its parent is InvariantCulture (never queried), then the default culture resolves.
        result.Should().Be("English");
        provider.RequestedCultures.Should().HaveCount(2);
        provider.RequestedCultures[0].Should().Be("de");
        provider.RequestedCultures[1].Should().Be("en");
        provider.RequestedCultures.Should().NotContain(CultureInfo.InvariantCulture.Name);
    }

    /// <summary>
    /// Verifies that localize async parent resolution does not call default culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_ParentResolution_DoesNotCallDefaultCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["ar"] = "Arabic",
            ["en"] = "English"
        });
        var localizer = CreateLocalizer(provider, defaultCulture: "en");

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("ar-SY"), CancellationToken.None);

        result.Should().Be("Arabic");
        provider.RequestedCultures.Should().HaveCount(2);
        provider.RequestedCultures[0].Should().Be("ar-SY");
        provider.RequestedCultures[1].Should().Be("ar");
    }

    /// <summary>
    /// Verifies that localize async all candidates miss including default culture returns null.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_AllCandidatesMiss_IncludingDefaultCulture_ReturnsNullAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["fr"] = "French"
        });
        var localizer = CreateLocalizer(provider, defaultCulture: "en");

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None);

        // "de" misses, its parent InvariantCulture is excluded, and the default culture "en" also misses.
        result.Should().BeNull();
        provider.RequestedCultures.Should().HaveCount(2);
        provider.RequestedCultures[0].Should().Be("de");
        provider.RequestedCultures[1].Should().Be("en");
    }

    /// <summary>
    /// Verifies that localize async convenience overload uses current ui culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_ConvenienceOverload_UsesCurrentUiCultureAsync() {
        var provider = new RecordingProvider([]);
        var localizer = CreateLocalizer(provider, defaultCulture: null);

        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar-SY");

            var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), CancellationToken.None);

            // Omitted culture is not null; the convenience overload selects the current UI culture.
            result.Should().BeNull();
            provider.RequestedCultures.Should().HaveCount(2);
            provider.RequestedCultures[0].Should().Be("ar-SY");
            provider.RequestedCultures[1].Should().Be("ar");
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Verifies that localize async missing template does not invoke formatter.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_MissingTemplate_DoesNotInvokeFormatterAsync() {
        var provider = new RecordingProvider([]);
        var formatter = new RecordingFormatter();
        var localizer = new DefaultMessageLocalizer(provider, formatter, new LocalizationOptions { DefaultCulture = "en" });

        var result = await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("de"), CancellationToken.None);

        result.Should().BeNull();
        formatter.CallCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that localize async formatter receives resolved template and parameters.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_FormatterReceivesResolvedTemplateAndParametersAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["en"] = "Hello {Name}"
        });
        var formatter = new RecordingFormatter();
        var localizer = new DefaultMessageLocalizer(provider, formatter, new LocalizationOptions { DefaultCulture = "en" });
        var parameters = new Dictionary<string, object?> { ["Name"] = "World" };

        var result = await localizer.LocalizeAsync("key", parameters, new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("formatted: Hello {Name}");
        formatter.CallCount.Should().Be(1);
        formatter.FormattedTemplates.Should().ContainSingle().Which.Should().Be("Hello {Name}");
        formatter.FormattedParameters.Should().ContainSingle();
        Assert.AreSame(parameters, formatter.FormattedParameters[0], "the localizer passes the caller arguments through without copying");
    }

    /// <summary>
    /// Verifies that localize async formatter exception propagates to caller.
    /// </summary>
    [TestMethod]
    public async Task LocalizeAsync_FormatterException_PropagatesToCallerAsync() {
        var provider = new RecordingProvider(new Dictionary<string, string> {
            ["en"] = "template"
        });
        var localizer = new DefaultMessageLocalizer(provider, new ThrowingFormatter(), new LocalizationOptions { DefaultCulture = "en" });

        Func<Task> act = async () => await localizer.LocalizeAsync("key", new Dictionary<string, object?>(), new CultureInfo("en"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Exception.Message.Should().Be("formatter failure");
    }

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(RecordingProvider provider, string? defaultCulture) =>
        new(provider, new DefaultMessageFormatter(), defaultCulture is null ? null : new LocalizationOptions { DefaultCulture = defaultCulture });

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    private sealed class RecordingProvider(Dictionary<string, string> templates) : IMessageProvider {
        /// <summary>
        /// Gets the requested cultures.
        /// </summary>
        public List<string> RequestedCultures { get; } = [];

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = key;
            _ = cancellationToken;
            RequestedCultures.Add(culture.Name);
            return ValueTask.FromResult(templates.TryGetValue(culture.Name, out var template) ? template : null);
        }
    }

    /// <summary>
    /// Test double for a formatter.
    /// </summary>
    private sealed class RecordingFormatter : IMessageFormatter {
        /// <summary>
        /// Gets or sets the call count.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Gets the formatted templates.
        /// </summary>
        public List<string> FormattedTemplates { get; } = [];

        /// <summary>
        /// Gets the formatted parameters.
        /// </summary>
        public List<IReadOnlyDictionary<string, object?>> FormattedParameters { get; } = [];

        /// <summary>
        /// Performs the format operation.
        /// </summary>
        /// <param name="template">The message template.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>The format text.</returns>
        public string Format(string template, IReadOnlyDictionary<string, object?> parameters) {
            CallCount++;
            FormattedTemplates.Add(template);
            FormattedParameters.Add(parameters);
            return $"formatted: {template}";
        }
    }

    /// <summary>
    /// Represents a throwing formatter.
    /// </summary>
    private sealed class ThrowingFormatter : IMessageFormatter {
        /// <summary>
        /// Performs the format operation.
        /// </summary>
        /// <param name="template">The message template.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>The format text.</returns>
        public string Format(string template, IReadOnlyDictionary<string, object?> parameters)
            => throw new InvalidOperationException("formatter failure");
    }
}
