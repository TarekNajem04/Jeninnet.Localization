//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the gettext-style plural/context API:
/// <c>ngettext</c>-shaped category selection through <see cref="PluralRules"/>,
/// <c>pgettext</c>-shaped context isolation via the EOT (End of Transmission) composite key, miss
/// semantics, and cancellation propagation. The provider stays exact-match;
/// all fallback and selection behavior is owned by
/// <see cref="DefaultMessageLocalizer"/>.
/// </summary>
[TestClass]
public class GettextStyleLocalizerTests {
    private static readonly IReadOnlyDictionary<string, object?> _noParameters = new Dictionary<string, object?>();

    /// <summary>
    /// Performs the parameters operation.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The parameters result.</returns>
    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] entries) =>
        entries.ToDictionary(static entry => entry.Name, static entry => entry.Value);

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(RecordingProvider provider, string defaultCulture = "en") =>
        new(provider, new DefaultMessageFormatter(), new LocalizationOptions { DefaultCulture = defaultCulture });

    /// <summary>
    /// Builds the expected gettext-canonical composite provider key
    /// (<c>context + EOT + key</c>), mirroring the localizer's encoding so the
    /// tests pin the  interop contract instead of duplicating it.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="key">The message key.</param>
    /// <returns>The context key text.</returns>
    private static string ContextKey(string context, string key) => context + '\u0004' + key;

    /// <summary>
    /// Verifies that localize plural async selects english singular only for one.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_SelectsEnglishSingularOnlyForOneAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["book.one"] = "{count} book", ["book.other"] = "{count} books" }
        });
        var localizer = CreateLocalizer(provider);
        var culture = new CultureInfo("en");

        (await localizer.LocalizePluralAsync("book.one", "book.other", 1, Parameters(("count", 1)), culture, CancellationToken.None))
            .Should().Be("1 book");
        (await localizer.LocalizePluralAsync("book.one", "book.other", 0, Parameters(("count", 0)), culture, CancellationToken.None))
            .Should().Be("0 books");
        (await localizer.LocalizePluralAsync("book.one", "book.other", 2, Parameters(("count", 2)), culture, CancellationToken.None))
            .Should().Be("2 books");
        (await localizer.LocalizePluralAsync("book.one", "book.other", 5, Parameters(("count", 5)), culture, CancellationToken.None))
            .Should().Be("5 books");
    }

    /// <summary>
    /// Verifies that localize plural async routes arabic six form rule through singular plural pair.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(0, "P")]
    [DataRow(1, "S")]
    [DataRow(2, "P")]
    [DataRow(3, "P")]
    [DataRow(11, "P")]
    [DataRow(100, "P")]
    public async Task LocalizePluralAsync_RoutesArabicSixFormRuleThroughSingularPluralPairAsync(int count, string expected) {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", count, _noParameters, new CultureInfo("ar"), CancellationToken.None);

        result.Should().Be(expected, "only the 'one' category selects the singular template in the six-form Arabic rule");
    }

    /// <summary>
    /// Verifies that localize plural async routes russian three form rule through singular plural pair.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(1, "S")]
    [DataRow(21, "S")]
    [DataRow(2, "P")]
    [DataRow(5, "P")]
    [DataRow(11, "P")]
    public async Task LocalizePluralAsync_RoutesRussianThreeFormRuleThroughSingularPluralPairAsync(int count, string expected) {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ru"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", count, _noParameters, new CultureInfo("ru"), CancellationToken.None);

        result.Should().Be(expected);
    }

    /// <summary>
    /// Verifies that localize plural async treats french zero as singular.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="expected">The expected value.</param>
    [TestMethod]
    [DataRow(0, "S")]
    [DataRow(1, "S")]
    [DataRow(2, "P")]
    public async Task LocalizePluralAsync_TreatsFrenchZeroAsSingularAsync(int count, string expected) {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["fr"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", count, _noParameters, new CultureInfo("fr"), CancellationToken.None);

        result.Should().Be(expected);
    }

    /// <summary>
    /// Verifies that localize plural async falls back to available form on partial miss.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_FallsBackToAvailableFormOnPartialMissAsync() {
        var singularOnly = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["k.single"] = "one thing" }
        });
        var pluralOnly = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["k.plural"] = "many things" }
        });
        var culture = new CultureInfo("en");

        (await CreateLocalizer(singularOnly).LocalizePluralAsync("k.single", "k.plural", 5, _noParameters, culture, CancellationToken.None))
            .Should().Be("one thing", "the available form is returned instead of throwing or fabricating");
        (await CreateLocalizer(pluralOnly).LocalizePluralAsync("k.single", "k.plural", 1, _noParameters, culture, CancellationToken.None))
            .Should().Be("many things");
    }

    /// <summary>
    /// Verifies that localize plural async returns null when both forms miss.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_ReturnsNullWhenBothFormsMissAsync() {
        var localizer = CreateLocalizer(new RecordingProvider([]));

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", 5, _noParameters, new CultureInfo("en"), CancellationToken.None);

        result.Should().BeNull("a total miss keeps the existing null miss semantics");
    }

    /// <summary>
    /// Verifies that localize plural async resolves through the culture fallback chain.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_ResolvesThroughTheCultureFallbackChainAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", 2, _noParameters, new CultureInfo("en-GB"), CancellationToken.None);

        result.Should().Be("P");
        provider.Requests.Should().HaveCount(2);
        provider.Requests[0].Key.Should().Be("k.plural");
        provider.Requests[0].Culture.Should().Be("en-GB");
        provider.Requests[1].Key.Should().Be("k.plural");
        provider.Requests[1].Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that localize plural async selects category in the requested culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_SelectsCategoryInTheRequestedCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizePluralAsync("k.single", "k.plural", 2, _noParameters, new CultureInfo("ar-SY"), CancellationToken.None);

        result.Should().Be("P", "count 2 is 'two' (non-singular) in Arabic even though the template falls back from ar-SY to ar");
        provider.Requests[0].Key.Should().Be("k.plural");
        provider.Requests[0].Culture.Should().Be("ar-SY");
        provider.Requests[1].Key.Should().Be("k.plural");
        provider.Requests[1].Culture.Should().Be("ar");
    }

    /// <summary>
    /// Verifies that localize plural async formats in the requested culture.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_FormatsInTheRequestedCultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var formatter = new RecordingCultureFormatter();
        var localizer = new DefaultMessageLocalizer(provider, formatter, new LocalizationOptions { DefaultCulture = "en" });
        var requested = new CultureInfo("ar");

        await localizer.LocalizePluralAsync("k.single", "k.plural", 3, _noParameters, requested, CancellationToken.None);

        formatter.Cultures.Should().ContainSingle().Which.Should().Be(requested);
    }

    /// <summary>
    /// Verifies that localize plural async convenience overload uses current UICulture.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_ConvenienceOverloadUsesCurrentUICultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");

            var result = await localizer.LocalizePluralAsync("k.single", "k.plural", 3, _noParameters, CancellationToken.None);

            result.Should().Be("P");
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Verifies that localize plural async validates arguments.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_ValidatesArgumentsAsync() {
        var localizer = CreateLocalizer(new RecordingProvider([]));
        var culture = new CultureInfo("en");

        Func<Task> nullSingular = async () => await localizer.LocalizePluralAsync(null!, "k.plural", 1, _noParameters, culture, CancellationToken.None);
        Func<Task> nullPlural = async () => await localizer.LocalizePluralAsync("k.single", null!, 1, _noParameters, culture, CancellationToken.None);
        Func<Task> nullParameters = async () => await localizer.LocalizePluralAsync("k.single", "k.plural", 1, null!, culture, CancellationToken.None);
        Func<Task> nullCulture = async () => await localizer.LocalizePluralAsync("k.single", "k.plural", 1, _noParameters, null!, CancellationToken.None);

        await nullSingular.Should().ThrowAsync<ArgumentNullException>();
        await nullPlural.Should().ThrowAsync<ArgumentNullException>();
        await nullParameters.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize plural async pre cancelled token throws before provider work.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_PreCancelledTokenThrowsBeforeProviderWorkAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        });
        var localizer = CreateLocalizer(provider);

        Func<Task> act = async () => await localizer.LocalizePluralAsync("k.single", "k.plural", 1, _noParameters, new CultureInfo("en"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>("cancellation is never converted into a missing-resource result");
        provider.Requests.Should().BeEmpty("a pre-cancelled token must be observed before provider work");
    }

    /// <summary>
    /// Verifies that localize plural async cancellation during lookup stops fallback walk.
    /// </summary>
    [TestMethod]
    public async Task LocalizePluralAsync_CancellationDuringLookupStopsFallbackWalkAsync() {
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["k.single"] = "S", ["k.plural"] = "P" }
        }, onRequest: () => cancellation.Cancel());
        var localizer = CreateLocalizer(provider);

        Func<Task> act = async () => await localizer.LocalizePluralAsync("k.single", "k.plural", 2, _noParameters, new CultureInfo("en-GB"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        // No parent-culture or default-culture lookup may follow observed cancellation.
        provider.Requests.Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that localize with context async keeps identical keys independent.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_KeepsIdenticalKeysIndependentAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() {
                [ContextKey("menu", "open")] = "Open menu",
                [ContextKey("dialog", "open")] = "Open dialog",
                ["open"] = "Open!"
            }
        });
        var localizer = CreateLocalizer(provider);
        var culture = new CultureInfo("en");

        (await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, culture, CancellationToken.None))
            .Should().Be("Open menu");
        (await localizer.LocalizeWithContextAsync("open", "dialog", _noParameters, culture, CancellationToken.None))
            .Should().Be("Open dialog");
        (await localizer.LocalizeAsync("open", _noParameters, culture, CancellationToken.None))
            .Should().Be("Open!", "the context-free key is independent of its contextualized twins");
    }

    /// <summary>
    /// Verifies that localize with context async keeps contexts independent across cultures.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_KeepsContextsIndependentAcrossCulturesAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { [ContextKey("menu", "open")] = "Open menu" },
            ["ar"] = new() { [ContextKey("menu", "open")] = "ar-menu-open" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, new CultureInfo("ar"), CancellationToken.None);

        result.Should().Be("ar-menu-open");
    }

    /// <summary>
    /// Verifies that localize with context async returns null when context entry misses.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_ReturnsNullWhenContextEntryMissesAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["open"] = "Open!" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeWithContextAsync("open", "unknown-context", _noParameters, new CultureInfo("en"), CancellationToken.None);

        result.Should().BeNull("a missing context entry keeps the null miss semantics and never falls back to the context-free key");
    }

    /// <summary>
    /// Verifies that localize with context async empty context behaves like plain lookup.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_EmptyContextBehavesLikePlainLookupAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { ["open"] = "Open!" }
        });
        var localizer = CreateLocalizer(provider);
        var culture = new CultureInfo("en");

        var contextual = await localizer.LocalizeWithContextAsync("open", string.Empty, _noParameters, culture, CancellationToken.None);
        var plain = await localizer.LocalizeAsync("open", _noParameters, culture, CancellationToken.None);

        contextual.Should().Be("Open!");
        contextual.Should().Be(plain);
        provider.Requests.Should().HaveCount(2);
        provider.Requests[0].Key.Should().Be("open");
        provider.Requests[0].Culture.Should().Be("en");
        provider.Requests[1].Key.Should().Be("open");
        provider.Requests[1].Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that localize with context async resolves through the culture fallback chain.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_ResolvesThroughTheCultureFallbackChainAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { [ContextKey("menu", "open")] = "Open menu" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, new CultureInfo("en-GB"), CancellationToken.None);

        result.Should().Be("Open menu");
        provider.Requests.Should().HaveCount(2);
        provider.Requests[0].Key.Should().Be(ContextKey("menu", "open"));
        provider.Requests[0].Culture.Should().Be("en-GB");
        provider.Requests[1].Key.Should().Be(ContextKey("menu", "open"));
        provider.Requests[1].Culture.Should().Be("en");
    }

    /// <summary>
    /// Verifies that localize with context async formats with parameters.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_FormatsWithParametersAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { [ContextKey("cart", "checkout")] = "Pay {Total} now" }
        });
        var localizer = CreateLocalizer(provider);

        var result = await localizer.LocalizeWithContextAsync("checkout", "cart", Parameters(("Total", 42)), new CultureInfo("en"), CancellationToken.None);

        result.Should().Be("Pay 42 now");
    }

    /// <summary>
    /// Verifies that localize with context async convenience overload uses current UICulture.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_ConvenienceOverloadUsesCurrentUICultureAsync() {
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["ar"] = new() { [ContextKey("menu", "open")] = "ar-menu-open" }
        });
        var localizer = CreateLocalizer(provider);
        var previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");

            var result = await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, CancellationToken.None);

            result.Should().Be("ar-menu-open");
        }
        finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Verifies that localize with context async validates arguments.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_ValidatesArgumentsAsync() {
        var localizer = CreateLocalizer(new RecordingProvider([]));
        var culture = new CultureInfo("en");

        Func<Task> nullKey = async () => await localizer.LocalizeWithContextAsync(null!, "menu", _noParameters, culture, CancellationToken.None);
        Func<Task> nullContext = async () => await localizer.LocalizeWithContextAsync("open", null!, _noParameters, culture, CancellationToken.None);
        Func<Task> nullParameters = async () => await localizer.LocalizeWithContextAsync("open", "menu", null!, culture, CancellationToken.None);
        Func<Task> nullCulture = async () => await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, null!, CancellationToken.None);

        await nullKey.Should().ThrowAsync<ArgumentNullException>();
        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await nullParameters.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that localize with context async pre cancelled token throws before provider work.
    /// </summary>
    [TestMethod]
    public async Task LocalizeWithContextAsync_PreCancelledTokenThrowsBeforeProviderWorkAsync() {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var provider = new RecordingProvider(new Dictionary<string, Dictionary<string, string>> {
            ["en"] = new() { [ContextKey("menu", "open")] = "Open menu" }
        });
        var localizer = CreateLocalizer(provider);

        Func<Task> act = async () => await localizer.LocalizeWithContextAsync("open", "menu", _noParameters, new CultureInfo("en"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>("cancellation is never converted into a missing-resource result");
        provider.Requests.Should().BeEmpty("a pre-cancelled token must be observed before provider work");
    }

    /// <summary>
    /// Provides recording.
    /// </summary>
    /// <param name="templates">The templates.</param>
    /// <param name="onRequest">The on request.</param>
    private sealed class RecordingProvider(Dictionary<string, Dictionary<string, string>> templates, Action? onRequest = null) : IMessageProvider {
        /// <summary>
        /// Gets the requests.
        /// </summary>
        public List<(string Key, string Culture)> Requests { get; } = [];

        /// <summary>
        /// Gets message template.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A value task whose result is message template.</returns>
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            _ = cancellationToken;
            Requests.Add((key, culture.Name));
            onRequest?.Invoke();
            return ValueTask.FromResult(
                templates.TryGetValue(culture.Name, out var byKey) && byKey.TryGetValue(key, out var template) ? template : null);
        }
    }

    /// <summary>
    /// Test double for a culture formatter.
    /// </summary>
    private sealed class RecordingCultureFormatter : ICultureAwareMessageFormatter {
        /// <summary>
        /// Gets the cultures.
        /// </summary>
        public List<CultureInfo> Cultures { get; } = [];

        /// <summary>
        /// Performs the format operation.
        /// </summary>
        /// <param name="template">The message template.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>The format text.</returns>
        public string Format(string template, IReadOnlyDictionary<string, object?> parameters) =>
            Format(template, parameters, CultureInfo.CurrentUICulture);

        /// <summary>
        /// Performs the format operation.
        /// </summary>
        /// <param name="template">The message template.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The format text.</returns>
        public string Format(string template, IReadOnlyDictionary<string, object?> parameters, CultureInfo culture) {
            _ = parameters;
            Cultures.Add(culture);
            return "formatted:" + template;
        }
    }
}
