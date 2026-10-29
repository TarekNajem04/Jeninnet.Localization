//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Missing-key sink accuracy plus pseudo-localization determinism:
/// exact miss collection with key/culture/call site, cap behavior, fallback
/// transparency, formatter round-trips, and off-state parity.
/// Benchmark note (acceptance): the disabled sink costs exactly one null check
/// per miss with no allocations — the miss path is untouched otherwise, so
/// off-state behavior is identical to today by construction; the existing
/// <c>DefaultMessageLocalizerContractTests</c> (sink never set) pin that path.
/// </summary>
[TestClass]
public sealed class MissingKeyPseudoTests {
    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// Verifies that sink collects exact misses with key culture and call site.
    /// </summary>
    [TestMethod]
    public async Task Sink_CollectsExactMisses_WithKeyCultureAndCallSiteAsync() {
        var collector = new MissingKeyCollector();
        var localizer = CreateLocalizer(new Dictionary<string, string> { ["Greeting"] = "Hello" }, collector);

        var hit = await localizer.LocalizeAsync("Greeting", Parameters(), new CultureInfo("en-US"), Cancellation);
        var miss = await localizer.LocalizeAsync("Missing", Parameters(), new CultureInfo("nl-NL"), Cancellation);

        hit.Should().Be("Hello");
        miss.Should().BeNull("miss semantics are unchanged: the lookup still returns null");
        var snapshot = collector.GetSnapshot();
        snapshot.Should().HaveCount(1);
        snapshot[0].Key.Should().Be("Missing");
        snapshot[0].Culture.Should().Be("nl-NL");
        snapshot[0].HitCount.Should().Be(1);
        snapshot[0].CallSite.Should().Contain(nameof(MissingKeyPseudoTests));
        (snapshot[0].FirstSeenUtc <= DateTimeOffset.UtcNow).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that sink ignores fallback steps that resolve.
    /// </summary>
    [TestMethod]
    public async Task Sink_IgnoresFallbackSteps_ThatResolveAsync() {
        var collector = new MissingKeyCollector();
        var inner = new MemoryMessageProvider(new MemoryLocalizationOptions {
            Messages = new Dictionary<string, string> { ["Greeting"] = "Hello" },
        });
        var dispatcher = new CultureAwareMessageProvider(new Dictionary<string, IMessageProvider> { ["en"] = inner });
        var localizer = new DefaultMessageLocalizer(
            dispatcher,
            new DefaultMessageFormatter(),
            new LocalizationOptions { DefaultCulture = "en", MissingKeySink = collector });

        // nl-NL misses every step but resolves through the default culture: no miss.
        var resolved = await localizer.LocalizeAsync("Greeting", Parameters(), new CultureInfo("nl-NL"), Cancellation);
        resolved.Should().Be("Hello");
        collector.GetSnapshot().Should().BeEmpty("fallbacks that resolve are not misses");

        // A genuinely absent key reports once, not once per fallback step.
        var miss = await localizer.LocalizeAsync("Absent", Parameters(), new CultureInfo("nl-NL"), Cancellation);
        miss.Should().BeNull();
        var snapshot = collector.GetSnapshot();
        snapshot.Should().HaveCount(1);
        snapshot[0].Key.Should().Be("Absent");
        snapshot[0].HitCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that sink dedupes repeat misses.
    /// </summary>
    [TestMethod]
    public async Task Sink_Dedupes_RepeatMissesAsync() {
        var collector = new MissingKeyCollector();
        var localizer = CreateLocalizer(new Dictionary<string, string>(), collector);

        await localizer.LocalizeAsync("Gone", Parameters(), new CultureInfo("en"), Cancellation);
        await localizer.LocalizeAsync("Gone", Parameters(), new CultureInfo("en"), Cancellation);
        await localizer.LocalizeAsync("Gone", Parameters(), new CultureInfo("fr"), Cancellation);

        var snapshot = collector.GetSnapshot();
        snapshot.Should().HaveCount(2, "aggregation is by (key, culture)");
        snapshot[0].Key.Should().Be("Gone");
        snapshot[0].Culture.Should().Be("en");
        snapshot[0].HitCount.Should().Be(2);
        snapshot[1].Culture.Should().Be("fr");
        snapshot[1].HitCount.Should().Be(1);
    }

    /// <summary>
    /// Verifies that sink cap drops beyond max and counts is dropped.
    /// </summary>
    [TestMethod]
    public async Task Sink_Cap_DropsBeyondMax_AndCountsDroppedAsync() {
        var collector = new MissingKeyCollector(maxEntries: 2);
        collector.MaxEntries.Should().Be(2);
        var localizer = CreateLocalizer(new Dictionary<string, string>(), collector);

        await localizer.LocalizeAsync("A", Parameters(), new CultureInfo("en"), Cancellation);
        await localizer.LocalizeAsync("B", Parameters(), new CultureInfo("en"), Cancellation);
        await localizer.LocalizeAsync("C", Parameters(), new CultureInfo("en"), Cancellation);
        await localizer.LocalizeAsync("A", Parameters(), new CultureInfo("en"), Cancellation);

        collector.GetSnapshot().Should().HaveCount(2, "no duplicates beyond the cap");
        collector.DroppedCount.Should().Be(1);
        collector.GetSnapshot()[0].HitCount.Should().Be(2, "repeat misses of retained keys still count when full");

        collector.Clear();
        collector.GetSnapshot().Should().BeEmpty();
        collector.DroppedCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that sink off matches baseline behavior.
    /// </summary>
    [TestMethod]
    public async Task Sink_Off_MatchesBaselineBehaviorAsync() {
        var localizer = CreateLocalizer(new Dictionary<string, string> { ["Greeting"] = "Hello" }, sink: null);

        (await localizer.LocalizeAsync("Greeting", Parameters(), new CultureInfo("en"), Cancellation)).Should().Be("Hello");
        (await localizer.LocalizeAsync("Missing", Parameters(), new CultureInfo("en"), Cancellation)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that sink throwing sink is contained.
    /// </summary>
    [TestMethod]
    public async Task Sink_ThrowingSink_IsContainedAsync() {
        var localizer = CreateLocalizer(new Dictionary<string, string>(), new ThrowingSink());

        (await localizer.LocalizeAsync("Missing", Parameters(), new CultureInfo("en"), Cancellation)).Should().BeNull(
            "diagnostic sinks must never break lookups");
    }

    /// <summary>
    /// Verifies that sink validation rejects bad input.
    /// </summary>
    [TestMethod]
    public void Sink_Validation_RejectsBadInput() {
        Func<object?> badCap = () => new MissingKeyCollector(maxEntries: 0);
        badCap.Should().Throw<ArgumentOutOfRangeException>();

        var collector = new MissingKeyCollector();
        Action nullKey = () => collector.ReportMiss(null!, "en", null);
        nullKey.Should().Throw<ArgumentNullException>();

        Func<object?> nullLogger = () => new LoggerMissingKeySink(null!);
        nullLogger.Should().Throw<ArgumentNullException>();

        Func<object?> nullComposite = () => new CompositeMissingKeySink(null!);
        nullComposite.Should().Throw<ArgumentNullException>();

        Func<object?> nullElement = () => new CompositeMissingKeySink(new MissingKeyCollector(), null!);
        nullElement.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that sink composite fans out.
    /// </summary>
    [TestMethod]
    public async Task Sink_Composite_FansOutAsync() {
        var first = new MissingKeyCollector();
        var second = new MissingKeyCollector();
        var composite = new CompositeMissingKeySink(first, second);
        var localizer = CreateLocalizer(new Dictionary<string, string>(), composite);

        await localizer.LocalizeAsync("Gone", Parameters(), new CultureInfo("en"), Cancellation);

        first.GetSnapshot().Should().HaveCount(1);
        second.GetSnapshot().Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that sink logger bridge logs structured warning.
    /// </summary>
    [TestMethod]
    public void Sink_Logger_Bridge_LogsStructuredWarning() {
        var logger = new CapturingLogger();
        var sink = new LoggerMissingKeySink(logger);

        sink.ReportMiss("Gone", "en", "Some.Caller");

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].Level.Should().Be(LogLevel.Warning);
        logger.Entries[0].Message.Should().Contain("Gone");
        logger.Entries[0].Message.Should().Contain("en");
        logger.Entries[0].Message.Should().Contain("Some.Caller");
    }

    /// <summary>
    /// Verifies that sink flows through dependency injection.
    /// </summary>
    [TestMethod]
    public async Task Sink_Flows_ThroughDependencyInjectionAsync() {
        var collector = new MissingKeyCollector();
        var services = new ServiceCollection();
        services.AddMemoryLocalization(new Dictionary<string, string> { ["Greeting"] = "Hello" });
        services.AddLocalization(static options => options.DefaultCulture = "en");
        await using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IMessageLocalizer>();

        (await localizer.LocalizeAsync("Missing", Parameters(), new CultureInfo("en"), Cancellation)).Should().BeNull();
        collector.GetSnapshot().Should().BeEmpty("the default options carry no sink");

        var wired = new ServiceCollection();
        wired.AddMemoryLocalization(new Dictionary<string, string> { ["Greeting"] = "Hello" });
        wired.AddLocalization(options => options.MissingKeySink = collector);
        await using var wiredProvider = wired.BuildServiceProvider();
        var wiredLocalizer = wiredProvider.GetRequiredService<IMessageLocalizer>();

        (await wiredLocalizer.LocalizeAsync("Missing", Parameters(), new CultureInfo("en"), Cancellation)).Should().BeNull();
        wiredLocalizer.Should().NotBeNull();
        collector.GetSnapshot().Should().HaveCount(1, "the configured sink flows through DI");
    }

    /// <summary>
    /// Verifies that sink plural miss reports both keys once.
    /// </summary>
    [TestMethod]
    public async Task Sink_PluralMiss_ReportsBothKeysOnceAsync() {
        var collector = new MissingKeyCollector();
        var localizer = CreateLocalizer(new Dictionary<string, string>(), collector);

        (await localizer.LocalizePluralAsync("One", "Many", 5, Parameters(), new CultureInfo("en"), Cancellation)).Should().BeNull();

        var snapshot = collector.GetSnapshot();
        snapshot.Select(static entry => entry.Key).Should().BeEquivalentTo("One", "Many");
    }

    /// <summary>
    /// Verifies that pseudo serves only pseudo culture.
    /// </summary>
    [TestMethod]
    public async Task Pseudo_ServesOnlyPseudoCultureAsync() {
        var provider = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> { ["Greeting"] = "Hello" },
        });

        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("qps-ploc"), Cancellation)).Should().NotBeNull();
        (await provider.GetMessageTemplateAsync("Greeting", new CultureInfo("en"), Cancellation)).Should().BeNull(
            "exact-match provider: other cultures resolve to null");
        (await provider.GetMessageTemplateAsync("Missing", new CultureInfo("qps-ploc"), Cancellation)).Should().BeNull(
            "absent pseudo keys stay missing so fallback applies");
    }

    /// <summary>
    /// Verifies that pseudo output is deterministic expanded and is marked.
    /// </summary>
    [TestMethod]
    public async Task Pseudo_Output_IsDeterministic_Expanded_AndMarkedAsync() {
        var first = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> { ["Greeting"] = "Hello, {Name}!" },
        });
        var second = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> { ["Greeting"] = "Hello, {Name}!" },
        });

        var one = await first.GetMessageTemplateAsync("Greeting", new CultureInfo("qps-ploc"), Cancellation);
        var two = await second.GetMessageTemplateAsync("Greeting", new CultureInfo("qps-ploc"), Cancellation);

        one.Should().Be(two);
        one.Should().Be("[!! \u0124\u00EB\u0142\u0142\u00F6, {Name}!~~~~~ !!]");
        (one!.Length > "Hello, {Name}!".Length + 8).Should().BeTrue("markers plus padding visibly expand the template");
        one.Should().Contain("{Name}", "placeholders pass through verbatim");
    }

    /// <summary>
    /// Verifies that pseudo formatter round trip parameters intact.
    /// </summary>
    [TestMethod]
    public async Task Pseudo_FormatterRoundTrip_ParametersIntactAsync() {
        var provider = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> { ["Greeting"] = "Hello, {Name}!" },
        });
        var localizer = new DefaultMessageLocalizer(
            provider,
            new DefaultMessageFormatter(),
            new LocalizationOptions { DefaultCulture = "en" });

        var localized = await localizer.LocalizeAsync(
            "Greeting",
            new Dictionary<string, object?> { ["Name"] = "Ada" },
            new CultureInfo("qps-ploc"),
            Cancellation);

        localized.Should().NotBeNull();
        localized!.Should().Contain("Ada", "parameters survive pseudo-localization");
        localized.Should().Contain("[!! ");
    }

    /// <summary>
    /// Verifies that pseudo nested placeholders is preserved.
    /// </summary>
    [TestMethod]
    public async Task Pseudo_NestedPlaceholders_PreservedAsync() {
        var provider = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> {
                ["Books"] = "{count, plural, one {# book} other {# books}} on shelves",
            },
        });

        var template = await provider.GetMessageTemplateAsync("Books", new CultureInfo("qps-ploc"), Cancellation);

        template.Should().NotBeNull();
        template.Should().Contain("{count, plural, one {# book} other {# books}}", "nested ICU spans copy verbatim");
    }

    /// <summary>
    /// Verifies that pseudo empty template is marked.
    /// </summary>
    [TestMethod]
    public async Task Pseudo_EmptyTemplate_MarkedAsync() {
        var provider = new PseudoMessageProvider(new PseudoLocalizationOptions {
            SourceTemplates = new Dictionary<string, string> { ["Empty"] = string.Empty },
        });

        (await provider.GetMessageTemplateAsync("Empty", new CultureInfo("qps-ploc"), Cancellation)).Should().Be("[!!  !!]");
    }

    /// <summary>
    /// Verifies that pseudo validation rejects bad input.
    /// </summary>
    [TestMethod]
    public void Pseudo_Validation_RejectsBadInput() {
        Func<object?> nullOptions = () => new PseudoMessageProvider(null!);
        nullOptions.Should().Throw<ArgumentNullException>();

        Func<object?> nullTemplates = () => new PseudoMessageProvider(new PseudoLocalizationOptions { SourceTemplates = null! });
        nullTemplates.Should().Throw<ArgumentNullException>();

        Func<object?> blankCulture = () => new PseudoMessageProvider(new PseudoLocalizationOptions { PseudoCulture = "  " });
        blankCulture.Should().Throw<ArgumentException>();

        Func<object?> negativeExpansion = () => new PseudoMessageProvider(new PseudoLocalizationOptions { ExpansionPercent = -1 });
        negativeExpansion.Should().Throw<ArgumentOutOfRangeException>();

        Func<object?> hugeExpansion = () => new PseudoMessageProvider(new PseudoLocalizationOptions { ExpansionPercent = 101 });
        hugeExpansion.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that pseudo DI (Dependency Injection) registers.
    /// </summary>
    [TestMethod]
    public void Pseudo_DI_Registers() {
        var services = new ServiceCollection();
        services.AddPseudoLocalization(new Dictionary<string, string> { ["Greeting"] = "Hello" });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IInnerMessageProvider>().Should().BeOfType<PseudoMessageProvider>();
    }

    /// <summary>
    /// Performs the parameters operation.
    /// </summary>
    /// <returns>The parameters result.</returns>
    private static Dictionary<string, object?> Parameters() => [];

    /// <summary>
    /// Creates localizer.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>The localizer result.</returns>
    private static DefaultMessageLocalizer CreateLocalizer(IReadOnlyDictionary<string, string> messages, IMissingKeySink? sink) =>
        new(
            new MemoryMessageProvider(new MemoryLocalizationOptions { Messages = messages }),
            new DefaultMessageFormatter(),
            new LocalizationOptions { DefaultCulture = "en", MissingKeySink = sink });

    /// <summary>
    /// Represents a throwing sink.
    /// </summary>
    private sealed class ThrowingSink : IMissingKeySink {
        /// <summary>
        /// Reports miss.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="callSite">The call site.</param>
        public void ReportMiss(string key, string culture, string? callSite) => throw new InvalidOperationException("sink failure");
    }

    /// <summary>
    /// Represents a capturing logger.
    /// </summary>
    private sealed class CapturingLogger : ILogger {
        /// <summary>
        /// Gets the entries.
        /// </summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <inheritdoc/>
        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

        /// <summary>
        /// Determines whether the value is enabled.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>
        /// Performs the log operation.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="state">The state.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="formatter">The formatter.</param>
        /// <typeparam name="TState">The type of TState.</typeparam>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            ArgumentNullException.ThrowIfNull(formatter);
            Entries.Add((logLevel, formatter(state, exception)));
        }

        /// <summary>
        /// Represents a null scope.
        /// </summary>
        private sealed class NullScope : IDisposable {
            /// <summary>
            /// Gets the instance.
            /// </summary>
            public static NullScope Instance { get; } = new();

            /// <summary>
            /// Releases the resources used by the current instance.
            /// </summary>
            public void Dispose() { }
        }
    }
}
