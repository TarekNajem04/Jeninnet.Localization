//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Opt-in ordered composite provider tests: first-exact-wins ordering over
/// Memory+JSON+INI inners, failure propagation (never silent fallthrough),
/// honest capability aggregation, plural/preload/refresh forwarding, events,
/// disposal, validation, and selector-boundary behavior.
/// </summary>
[TestClass]
public class CompositeMessageProviderTests {
    /// <summary>
    /// Verifies that ordering first exact wins.
    /// </summary>
    [TestMethod]
    public async Task Ordering_FirstExactWinsAsync() {
        var tempFiles = new List<string>();
        try {
            var overlay = Inner(new Dictionary<string, string> { ["Shared"] = "overlay", ["OnlyOverlay"] = "o" });
            var (middling, jsonPath) = await JsonInnerAsync(new Dictionary<string, string> { ["Shared"] = "json", ["OnlyJson"] = "j" });
            var (bottom, iniPath) = await IniInnerAsync("Shared = ini\nOnlyIni = i\n");
            tempFiles.Add(jsonPath);
            tempFiles.Add(iniPath);
            var composite = new CompositeMessageProvider([overlay, middling, bottom]);
            var culture = CultureInfo.InvariantCulture;

            (await composite.GetMessageTemplateAsync("Shared", culture, CancellationToken.None)).Should().Be("overlay");
            (await composite.GetMessageTemplateAsync("OnlyOverlay", culture, CancellationToken.None)).Should().Be("o");
            (await composite.GetMessageTemplateAsync("OnlyJson", culture, CancellationToken.None)).Should().Be("j");
            (await composite.GetMessageTemplateAsync("OnlyIni", culture, CancellationToken.None)).Should().Be("i");
            (await composite.GetMessageTemplateAsync("Missing", culture, CancellationToken.None)).Should().BeNull();
        }
        finally {
            foreach(var tempFile in tempFiles) {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Verifies that empty string value is template not miss.
    /// </summary>
    [TestMethod]
    public async Task EmptyStringValue_IsTemplateNotMissAsync() {
        var composite = new CompositeMessageProvider([
            Inner(new Dictionary<string, string> { ["Empty"] = string.Empty }),
            Inner(new Dictionary<string, string> { ["Empty"] = "base" }),
        ]);

        (await composite.GetMessageTemplateAsync("Empty", CultureInfo.InvariantCulture, CancellationToken.None))
            .Should().Be(string.Empty, "an empty template is served, never skipped");
    }

    /// <summary>
    /// Verifies that inner throws propagates with failed event.
    /// </summary>
    [TestMethod]
    public async Task InnerThrows_PropagatesWithFailedEventAsync() {
        var failure = new InvalidDataException("broken catalog");
        var throwing = new ThrowingInner(failure);
        var fallback = new RecordingInner(new Dictionary<string, string> { ["K"] = "v" });
        var composite = new CompositeMessageProvider([throwing, fallback]);

        var failed = new List<Exception>();
        composite.AcquisitionFailed += (_, args) => failed.Add(args.Exception);

        Func<Task> act = async () => await composite.GetMessageTemplateAsync("K", CultureInfo.InvariantCulture, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidDataException>()).Which.Should().BeSameAs(failure);
        failed.Should().ContainSingle().Which.Should().BeSameAs(failure);
        fallback.Calls.Should().Be(0, "remaining inners are skipped after a failure");
    }

    /// <summary>
    /// Verifies that supports aggregates over capable inners.
    /// </summary>
    [TestMethod]
    public void Supports_AggregatesOverCapableInners() {
        var plain = new CompositeMessageProvider([Inner([])]);
        plain.Supports(ProviderFeatures.MultiKeys).Should().BeFalse();
        plain.Supports(ProviderFeatures.Preload).Should().BeFalse();
        plain.Supports(ProviderFeatures.Refresh).Should().BeFalse();
        plain.Supports(ProviderFeatures.EventsListening).Should().BeTrue("the composite owns its events");
        plain.Supports(ProviderFeatures.Caching).Should().BeFalse("the composite caches nothing itself");
        plain.Supports(ProviderFeatures.None).Should().BeFalse();

        var cached = new CachedMessageProvider(
            Inner([]),
            Options.Create(new MessageProviderCacheOptions()));
        var decorated = new CompositeMessageProvider([cached]);
        decorated.Supports(ProviderFeatures.MultiKeys).Should().BeTrue();
        decorated.Supports(ProviderFeatures.Preload).Should().BeTrue();
        decorated.Supports(ProviderFeatures.Refresh).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that multi key first wins per key.
    /// </summary>
    [TestMethod]
    public async Task MultiKey_FirstWinsPerKeyAsync() {
        var composite = new CompositeMessageProvider([
            Inner(new Dictionary<string, string> { ["K1"] = "a1", ["K2"] = "a2" }),
            Inner(new Dictionary<string, string> { ["K2"] = "b2", ["K3"] = "b3" }),
        ]);

        IReadOnlyCollection<string> keys = ["K1", "K2", "K3", "Missing"];
        var result = await composite.GetMessageTemplatesAsync(keys, CultureInfo.InvariantCulture, CancellationToken.None);

        result.Should().HaveCount(4, "every requested key maps, null when unresolved");
        result["K1"].Should().Be("a1");
        result["K2"].Should().Be("a2", "the first inner wins per key");
        result["K3"].Should().Be("b3");
        result["Missing"].Should().BeNull();
    }

    /// <summary>
    /// Verifies that multi key fans out to batch capable inners.
    /// </summary>
    [TestMethod]
    public async Task MultiKey_FansOutToBatchCapableInnersAsync() {
        var first = new RecordingInner(new Dictionary<string, string> { ["K1"] = "a1" });
        var second = new RecordingInner(new Dictionary<string, string> { ["K2"] = "b2" });
        var composite = new CompositeMessageProvider([first, second]);

        var result = await composite.GetMessageTemplatesAsync(["K1", "K2"], CultureInfo.InvariantCulture, CancellationToken.None);

        result["K1"].Should().Be("a1");
        result["K2"].Should().Be("b2");
        first.BatchCalls.Should().ContainSingle().Which.Should().BeEquivalentTo("K1", "K2");
        second.BatchCalls.Should().ContainSingle().Which.Should().ContainSingle().Which.Should().Be("K2");
    }

    /// <summary>
    /// Verifies that preload forwards to preloadable inners.
    /// </summary>
    [TestMethod]
    public async Task Preload_ForwardsToPreloadableInnersAsync() {
        var capable = new RecordingInner([]);
        var plain = Inner([]);
        var composite = new CompositeMessageProvider([plain, capable]);

        await composite.PreloadAsync(["K"], CultureInfo.GetCultureInfo("de"), CancellationToken.None);

        capable.PreloadCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that refresh forwards to refreshable inners.
    /// </summary>
    [TestMethod]
    public async Task Refresh_ForwardsToRefreshableInnersAsync() {
        var capable = new RecordingInner([]);
        var composite = new CompositeMessageProvider([capable]);

        await composite.RefreshAsync(CancellationToken.None);

        capable.RefreshCalls.Should().Be(1);
    }

    /// <summary>
    /// Verifies that plural forwards to first plural inner.
    /// </summary>
    [TestMethod]
    public async Task Plural_ForwardsToFirstPluralInnerAsync() {
        var first = new RecordingInner(new Dictionary<string, string> { ["one"] = "eins", ["other"] = "viele" });
        var second = new RecordingInner(new Dictionary<string, string> { ["one"] = "ONE", ["other"] = "OTHER" });
        var composite = new CompositeMessageProvider([Inner([]), first, second]);

        var template = await composite.GetPluralTemplateAsync("one", "other", 5, CultureInfo.GetCultureInfo("de"), CancellationToken.None);

        template.Should().Be("viele");
        first.PluralCalls.Should().Be(1);
        second.PluralCalls.Should().Be(0);
    }

    /// <summary>
    /// Verifies that plural without capable inner returns null.
    /// </summary>
    [TestMethod]
    public async Task Plural_WithoutCapableInner_ReturnsNullAsync() {
        var composite = new CompositeMessageProvider([Inner([])]);

        (await composite.GetPluralTemplateAsync("one", "other", 1, CultureInfo.InvariantCulture, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that events report one acquired per resolved lookup.
    /// </summary>
    [TestMethod]
    public async Task Events_ReportOneAcquiredPerResolvedLookupAsync() {
        var composite = new CompositeMessageProvider([Inner(new Dictionary<string, string> { ["K"] = "v" })]);

        var acquired = 0;
        var missed = 0;
        composite.TranslationAcquired += (_, _) => acquired++;
        composite.AcquisitionFailed += (_, _) => missed++;

        await composite.GetMessageTemplateAsync("K", CultureInfo.InvariantCulture, CancellationToken.None);
        await composite.GetMessageTemplateAsync("Nope", CultureInfo.InvariantCulture, CancellationToken.None);

        acquired.Should().Be(1, "one event per resolved lookup, never per inner call");
        missed.Should().Be(0, "a total miss is not a failure");
    }

    /// <summary>
    /// Verifies that null arguments throw argument null exception.
    /// </summary>
    [TestMethod]
    public async Task NullArguments_ThrowArgumentNullExceptionAsync() {
        var composite = new CompositeMessageProvider([Inner([])]);

        Func<Task> nullKey = async () => await composite.GetMessageTemplateAsync(null!, CultureInfo.InvariantCulture, CancellationToken.None);
        Func<Task> nullCulture = async () => await composite.GetMessageTemplateAsync("K", null!, CancellationToken.None);
        Func<Task> nullKeys = async () => await composite.GetMessageTemplatesAsync(null!, CultureInfo.InvariantCulture, CancellationToken.None);
        Func<Task> nullSingular = async () => await composite.GetPluralTemplateAsync(null!, "P", 1, CultureInfo.InvariantCulture, CancellationToken.None);

        await nullKey.Should().ThrowAsync<ArgumentNullException>();
        await nullCulture.Should().ThrowAsync<ArgumentNullException>();
        await nullKeys.Should().ThrowAsync<ArgumentNullException>();
        await nullSingular.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that pre cancelled token throws operation canceled exception.
    /// </summary>
    [TestMethod]
    public async Task PreCancelledToken_ThrowsOperationCanceledExceptionAsync() {
        var composite = new CompositeMessageProvider([Inner(new Dictionary<string, string> { ["K"] = "v" })]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Func<Task> act = async () => await composite.GetMessageTemplateAsync("K", CultureInfo.InvariantCulture, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Verifies that construction validation rejects bad input.
    /// </summary>
    [TestMethod]
    public void ConstructionValidation_RejectsBadInput() {
        var inner = Inner([]);
        var nullList = () => new CompositeMessageProvider(null!);
        var empty = () => new CompositeMessageProvider([]);
        var nullEntry = () => new CompositeMessageProvider([inner, null!]);
        var duplicate = () => new CompositeMessageProvider([inner, inner]);

        nullList.Should().Throw<ArgumentNullException>();
        empty.Should().Throw<ArgumentException>();
        nullEntry.Should().Throw<ArgumentNullException>();
        duplicate.Should().Throw<ArgumentException>("the same instance twice is a configuration error");
    }

    /// <summary>
    /// Verifies that composite implements no disposal abstraction.
    /// </summary>
    [TestMethod]
    public void Composite_ImplementsNoDisposalAbstraction() =>
        typeof(IDisposable).IsAssignableFrom(typeof(CompositeMessageProvider)).Should().BeFalse(
            "the composite owns no watcher; inner lifetimes belong to their owners");

    /// <summary>
    /// Verifies that dependency injection registers single composite.
    /// </summary>
    [TestMethod]
    public async Task DependencyInjection_RegistersSingleCompositeAsync() {
        var services = new ServiceCollection();
        services.AddCompositeLocalization(
            _ => Inner(new Dictionary<string, string> { ["K"] = "overlay" }),
            _ => Inner(new Dictionary<string, string> { ["K"] = "base" }));

        await using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IInnerMessageProvider>();

        resolved.Should().BeOfType<CompositeMessageProvider>();
        (await resolved.GetMessageTemplateAsync("K", CultureInfo.InvariantCulture, CancellationToken.None)).Should().Be("overlay");
    }

    /// <summary>
    /// Verifies that dependency injection validation rejects bad factories.
    /// </summary>
    [TestMethod]
    public void DependencyInjection_Validation_RejectsBadFactories() {
        var empty = new ServiceCollection();
        Action noFactories = () => empty.AddCompositeLocalization();

        noFactories.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies that selector boundary composite plus provider throws.
    /// </summary>
    [TestMethod]
    public void SelectorBoundary_CompositePlusProvider_Throws() {
        var services = new ServiceCollection();
        services.AddSingleton<IInnerMessageProvider>(new CompositeMessageProvider([Inner([])]));
        services.AddSingleton<IInnerMessageProvider>(Inner([]));
        CoreJeninnetLocalizationExtensions.AddLocalization(services);
        using var provider = services.BuildServiceProvider();

        Action act = () => provider.GetRequiredService<IMessageProvider>();

        act.Should().Throw<InvalidOperationException>("the composite IS the single provider");
    }

    /// <summary>
    /// Creates an in-memory inner provider.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <returns>The inner provider.</returns>
    private static MemoryMessageProvider Inner(Dictionary<string, string> messages) =>
        new(new MemoryLocalizationOptions { Messages = messages });

    /// <summary>
    /// Creates a JSON file inner provider.
    /// </summary>
    /// <param name="messages">The messages.</param>
    /// <returns>A task whose result is the inner provider and its file.</returns>
    private async Task<(JsonMessageProvider Provider, string Path)> JsonInnerAsync(Dictionary<string, string> messages) {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(messages), TestContext?.CancellationToken ?? CancellationToken.None);
        return (new JsonMessageProvider(tempFile), tempFile);
    }

    /// <summary>
    /// Creates an INI file inner provider.
    /// </summary>
    /// <param name="ini">The INI text.</param>
    /// <returns>A task whose result is the inner provider and its file.</returns>
    private async Task<(IniMessageProvider Provider, string Path)> IniInnerAsync(string ini) {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, ini, TestContext?.CancellationToken ?? CancellationToken.None);
        return (new IniMessageProvider(tempFile), tempFile);
    }

    /// <summary>
    /// A recording inner provider for fan-out verification.
    /// </summary>
    /// <param name="messages">The messages.</param>
    private sealed class RecordingInner(Dictionary<string, string> messages) : IInnerMessageProvider, IMultiKeyMessageProvider, IPluralFormMessageProvider, IPreloadableMessageProvider, IRefreshableMessageProvider {
        private readonly Dictionary<string, string> _messages = new(messages, StringComparer.Ordinal);

        /// <summary>
        /// Gets the single-key calls.
        /// </summary>
        internal int Calls { get; private set; }

        /// <summary>
        /// Gets the batch calls.
        /// </summary>
        internal List<IReadOnlyCollection<string>> BatchCalls { get; } = [];

        /// <summary>
        /// Gets the preload calls.
        /// </summary>
        internal int PreloadCalls { get; private set; }

        /// <summary>
        /// Gets the refresh calls.
        /// </summary>
        internal int RefreshCalls { get; private set; }

        /// <summary>
        /// Gets the plural calls.
        /// </summary>
        internal int PluralCalls { get; private set; }

        /// <inheritdoc />
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(key);
            Calls++;
            return ValueTask.FromResult(_messages.TryGetValue(key, out var template) ? template : null);
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyDictionary<string, string?>> GetMessageTemplatesAsync(
            IReadOnlyCollection<string> keys,
            CultureInfo culture,
            CancellationToken cancellationToken = default) {
            BatchCalls.Add(keys);
            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach(var key in keys) {
                result[key] = _messages.TryGetValue(key, out var template) ? template : null;
            }

            return ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(result);
        }

        /// <inheritdoc />
        public ValueTask<string?> GetPluralTemplateAsync(string singularKey, string pluralKey, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) {
            PluralCalls++;
            var key = count == 1 ? singularKey : pluralKey;
            return ValueTask.FromResult(_messages.TryGetValue(key, out var template) ? template : null);
        }

        /// <inheritdoc />
        public ValueTask<string?> GetPluralTemplateWithContextAsync(string singularKey, string pluralKey, string context, decimal count, CultureInfo culture, CancellationToken cancellationToken = default) =>
            GetPluralTemplateAsync(singularKey, pluralKey, count, culture, cancellationToken);

        /// <inheritdoc />
        public ValueTask PreloadAsync(IReadOnlyCollection<string> keys, CultureInfo culture, CancellationToken cancellationToken = default) {
            PreloadCalls++;
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask RefreshAsync(CancellationToken cancellationToken = default) {
            RefreshCalls++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// An inner provider that always throws.
    /// </summary>
    /// <param name="failure">The failure.</param>
    private sealed class ThrowingInner(Exception failure) : IInnerMessageProvider {
        /// <inheritdoc />
        public ValueTask<string?> GetMessageTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<string?>(failure);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
