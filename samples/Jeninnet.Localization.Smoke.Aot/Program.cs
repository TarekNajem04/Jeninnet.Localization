//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Smoke.Aot;

/// <summary>
///  native-AOT/trimming smoke consumer: exercises the canonical localize + format
/// pipeline, the in-memory store cache decorator, and the Azure translation provider with a
/// fake HTTP transport (so the source-generated JSON path is covered) in a publish shape
/// that carries no reflection-based runtime dependencies. Exits 0 with <c>SMOKE OK</c> on
/// success; any check failure prints the failure and exits 1.
/// </summary>
internal static class Program {
    /// <summary>
    /// Runs the application entry point.
    /// </summary>
    /// <returns>A task whose result is main.</returns>
    private static async Task<int> Main() {
        try {
            await CheckLocalizeAndFormatAsync();
            CheckIcuFormatting();
            await CheckStoreCachingAsync();
            await CheckFakeTranslationAsync();
        }
        catch(Exception exception) {
            await Console.Error.WriteLineAsync("SMOKE FAILED: " + exception);
            return 1;
        }

        await Console.Out.WriteLineAsync("SMOKE OK: localize/format/cache/translate under AOT/trimming");
        return 0;
    }

    /// <summary>
    /// Localize + format through the canonical pipeline: Core localization services over
    /// the in-memory provider, formatted with named template arguments.
    /// </summary>
    private static async Task CheckLocalizeAndFormatAsync() {
        var services = new ServiceCollection();
        JeninnetLocalizationExtensions.AddLocalization(services);
        services.AddMemoryLocalization(new Dictionary<string, string>(StringComparer.Ordinal) {
            ["App.Title"] = "Hello {name}!",
        });
        await using var provider = services.BuildServiceProvider();

        var localizer = provider.GetRequiredService<IMessageLocalizer>();
        var text = await localizer.LocalizeAsync(
            "App.Title",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "AOT" },
            CultureInfo.GetCultureInfo("en-US"),
            CancellationToken.None);

        Require(text == "Hello AOT!", $"localize+format produced '{text}'");
    }

    /// <summary>
    /// ICU (International Components for Unicode) MessageFormat formatting (plural selection) through the shipped opt-in formatter.
    /// </summary>
    private static void CheckIcuFormatting() {
        var formatter = new IcuMessageFormatter(new DefaultMessageFormatter());
        var text = formatter.Format(
            "{count, plural, one {# cat} other {# cats}}",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 3 },
            CultureInfo.GetCultureInfo("en-US"));

        Require(text == "3 cats", $"ICU formatting produced '{text}'");
    }

    /// <summary>
    /// The in-memory store cache decorator collapses repeated lookups to one backend call.
    /// </summary>
    private static async Task CheckStoreCachingAsync() {
        var inner = new CountingLocalizationStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new CachedLocalizationStoreDecorator(inner, cache);

        var first = await store.GetStringAsync("SmokeSet", "en-US", "Key", CancellationToken.None);
        var second = await store.GetStringAsync("SmokeSet", "en-US", "Key", CancellationToken.None);

        Require(first.Value == "cached-value" && second.Value == "cached-value", "the store cache returned the value");
        Require(inner.GetResourceSetCalls == 1, $"the cache decorator collapsed {inner.GetResourceSetCalls} backend calls into one");
    }

    /// <summary>
    /// Translation through the Azure provider with a fake HTTP transport: request
    /// serialization and response parsing run through the source-generated JSON contract.
    /// </summary>
    private static async Task CheckFakeTranslationAsync() {
        var handler = new FakeTranslatorHandler();
        using var client = new HttpClient(handler);
        var options = Options.Create(new AzureTranslationOptions { ApiKey = "smoke-key" });
        var translator = new AzureTranslationProvider(options, client);

        var translated = await translator.GetMessageTemplateAsync(
            "App.Title",
            CultureInfo.GetCultureInfo("fr-FR"),
            CancellationToken.None);

        Require(translated == "Bonjour", $"fake translation produced '{translated}'");
        Require(handler.RequestCount == 1, $"the fake transport observed {handler.RequestCount} request(s)");
    }

    /// <summary>
    /// Performs the require operation.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="detail">The detail.</param>
    private static void Require(bool condition, string detail) {
        if(!condition) {
            throw new InvalidOperationException(detail);
        }
    }

    /// <summary>
    /// Minimal store returning one entry; counts resource-set acquisitions so the cache
    /// decorator's collapse is observable.
    /// </summary>
    private sealed class CountingLocalizationStore : ILocalizationStore {
        /// <summary>
        /// Gets or sets the get resource set calls.
        /// </summary>
        public int GetResourceSetCalls { get; private set; }

        /// <summary>
        /// Gets resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource set.</returns>
        public Task<ILocalizationResourceSet?> GetResourceSetAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            GetResourceSetCalls++;
            return Task.FromResult<ILocalizationResourceSet?>(new SmokeResourceSet(resourceSet, culture));
        }

        /// <summary>
        /// Gets resource sets.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is resource sets.</returns>
        public Task<List<ILocalizationResourceSet>> GetResourceSetsAsync(string culture, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ILocalizationResourceSet> { new SmokeResourceSet("SmokeSet", culture) });

        /// <summary>
        /// Attempts to get resource set.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="localizationResourceSet">The localization resource set.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetResourceSet(string resourceSet, string culture, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILocalizationResourceSet? localizationResourceSet) {
            localizationResourceSet = new SmokeResourceSet(resourceSet, culture);
            return true;
        }

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public async Task<LocalizedStringEntry> GetStringAsync(string resourceSet, string culture, string name, CancellationToken cancellationToken = default) {
            var set = await GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);
            return set is null ? LocalizedStringEntry.Missing(resourceSet, culture, name) : await set.GetStringAsync(name, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(string resourceSet, string culture, CancellationToken cancellationToken = default) {
            var set = await GetResourceSetAsync(resourceSet, culture, cancellationToken).ConfigureAwait(false);
            return set is null ? [] : await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all strings for culture.
        /// </summary>
        /// <param name="culture">The culture.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings for culture.</returns>
        public async Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsForCultureAsync(string culture, CancellationToken cancellationToken = default) {
            var set = new SmokeResourceSet("SmokeSet", culture);
            return await set.GetAllStringsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="resourceSet">The resource set.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string resourceSet, string culture, string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            entry = new LocalizedStringEntry(Guid.NewGuid(), resourceSet, culture, name, "cached-value");
            return true;
        }

        /// <summary>
        /// Performs the reload operation.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Gets supported cultures.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is supported cultures.</returns>
        public Task<List<string>> GetSupportedCulturesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string> { "en-US" });
    }

    /// <summary>
    /// Represents a smoke resource set.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="culture">The culture.</param>
    private sealed class SmokeResourceSet(string resourceSet, string culture) : ILocalizationResourceSet {
        /// <summary>
        /// Gets the resource set.
        /// </summary>
        public string ResourceSet => resourceSet;

        /// <summary>
        /// Gets the culture.
        /// </summary>
        public string Culture => culture;

        /// <summary>
        /// Gets culture info.
        /// </summary>
        /// <returns>The culture info result.</returns>
        public CultureInfo GetCultureInfo() => new(culture);

        /// <summary>
        /// Gets string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is string.</returns>
        public Task<LocalizedStringEntry> GetStringAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LocalizedStringEntry(Guid.NewGuid(), ResourceSet, Culture, name, "cached-value"));

        /// <summary>
        /// Gets all strings.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task whose result is all strings.</returns>
        public Task<Dictionary<string, LocalizedStringEntry>> GetAllStringsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, LocalizedStringEntry>(StringComparer.Ordinal) {
                ["Key"] = new(Guid.NewGuid(), ResourceSet, Culture, "Key", "cached-value"),
            });

        /// <summary>
        /// Attempts to get string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="entry">The entry.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        public bool TryGetString(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LocalizedStringEntry? entry) {
            entry = new LocalizedStringEntry(Guid.NewGuid(), ResourceSet, Culture, name, "cached-value");
            return true;
        }

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the add operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the update operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(LocalizedStringEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// Performs the delete operation.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task DeleteAsync(IEnumerable<LocalizedStringEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Fake Azure Translator transport: answers every request with one canned translation,
    /// so request serialization and response parsing execute without network access.
    /// </summary>
    private sealed class FakeTranslatorHandler : HttpMessageHandler {
        private const string RESPONSE_JSON = """[{"translations":[{"text":"Bonjour","to":"fr"}]}]""";

        /// <summary>
        /// Gets or sets the request count.
        /// </summary>
        public int RequestCount { get; private set; }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(RESPONSE_JSON, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
