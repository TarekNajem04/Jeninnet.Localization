//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contract tests for the  translation meters: the Azure
/// provider records cache misses/hits, outbound translation requests, and duration.
/// </summary>
[TestClass]
public class TranslationObservabilityTests {
    /// <summary>
    /// Verifies that translation lookup records cache miss then hit request and duration.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task TranslationLookup_RecordsCacheMissThenHit_RequestAndDurationAsync() {
        var measurements = new List<ObservabilityTestHarness.InstrumentMeasurement>();
        using var listener = ObservabilityTestHarness.CreateMeterListener(measurements);

        var requestCount = 0;
        var handler = new MockHandler((_, _) => {
            requestCount++;
            return Task.FromResult(TranslationResponse("Bonjour le monde"));
        });
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var french = CultureInfo.GetCultureInfo("fr");

        await provider.GetMessageTemplateAsync("Hello world", french, CancellationToken.None);
        await provider.GetMessageTemplateAsync("Hello world", french, CancellationToken.None);

        requestCount.Should().Be(1, "the second lookup is served from the provider cache");

        int Count(string instrumentName) => measurements.Count(m => m.InstrumentName == instrumentName);
        Count(LocalizationDiagnostics.TRANSLATION_CACHE_MISSES_INSTRUMENT_NAME).Should().Be(1);
        Count(LocalizationDiagnostics.TRANSLATION_CACHE_HITS_INSTRUMENT_NAME).Should().Be(1);
        Count(LocalizationDiagnostics.TRANSLATION_REQUESTS_INSTRUMENT_NAME).Should().Be(1);

        var durations = measurements.Where(m => m.InstrumentName == LocalizationDiagnostics.TRANSLATION_DURATION_INSTRUMENT_NAME).ToList();
        durations.Should().HaveCount(1, "one outbound translation call records one duration sample");
        durations[0].Value.Should().BeGreaterThan(0);
        measurements.Should().OnlyContain(m => m.HasTag(LocalizationDiagnostics.CULTURE_TAG_NAME, "fr"));
    }

    /// <summary>
    /// Verifies that batch lookup records one request per chunk and cache accounting.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public async Task BatchLookup_RecordsOneRequestPerChunk_AndCacheAccountingAsync() {
        var measurements = new List<ObservabilityTestHarness.InstrumentMeasurement>();
        using var listener = ObservabilityTestHarness.CreateMeterListener(measurements);

        var requestCount = 0;
        var handler = new MockHandler(async (request, cancellationToken) => {
            requestCount++;
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var count = document.RootElement.GetArrayLength();
            var items = Enumerable.Range(0, count)
                .Select(static index => new { translations = new[] { new { text = "t" + index, to = "fr" } } })
                .ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
        });
        using var client = new HttpClient(handler);
        var provider = CreateProvider(client);
        var french = CultureInfo.GetCultureInfo("fr");
        var keys = Enumerable.Range(0, AzureTranslationProvider.MAX_CHUNK_SIZE + 1).Select(static index => "key-" + index).ToArray();

        await provider.GetMessageTemplatesAsync(keys, french, CancellationToken.None);
        await provider.GetMessageTemplatesAsync(keys, french, CancellationToken.None);

        requestCount.Should().Be(2, "one outbound request per chunk; the second batch is served from the cache");

        int Count(string instrumentName) => measurements.Count(m => m.InstrumentName == instrumentName);
        Count(LocalizationDiagnostics.TRANSLATION_CACHE_MISSES_INSTRUMENT_NAME).Should().Be(keys.Length);
        Count(LocalizationDiagnostics.TRANSLATION_CACHE_HITS_INSTRUMENT_NAME).Should().Be(keys.Length);
        Count(LocalizationDiagnostics.TRANSLATION_REQUESTS_INSTRUMENT_NAME).Should().Be(2,
            "one request per outbound chunk (not per key, not per batch) — ");
        Count(LocalizationDiagnostics.TRANSLATION_DURATION_INSTRUMENT_NAME).Should().Be(2);
    }

    /// <summary>
    /// Creates provider.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <returns>The provider result.</returns>
    private static AzureTranslationProvider CreateProvider(HttpClient client) {
        var options = new AzureTranslationOptions {
            ApiKey = "test-api-key",
            Endpoint = "https://api.cognitive.microsofttranslator.com",
            FromLanguage = "en",
            CachingEnabled = true,
            CacheExpiration = AzureTranslationOptions.DefaultCacheExpiration,
        };

        return new AzureTranslationProvider(Options.Create(options), client);
    }

    /// <summary>
    /// Performs the translation response operation.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The translation response result.</returns>
    private static HttpResponseMessage TranslationResponse(string text) {
        var items = new[] {
            new {
                translations = new[] {
                    new { text, to = "fr" },
                },
            },
        };

        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(items),
        };
    }

    /// <summary>
    /// Handles mock.
    /// </summary>
    /// <param name="sendAsync">The send async.</param>
    private sealed class MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _sendAsync(request, cancellationToken);
    }
}
