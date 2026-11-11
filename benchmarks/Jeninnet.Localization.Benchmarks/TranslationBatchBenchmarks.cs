//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Jeninnet.Localization.Translation.DeepL;
using Jeninnet.Localization.Translation.DeepL.Options;
using Microsoft.Extensions.Options;

namespace Jeninnet.Localization.Benchmarks;

/// <summary>
/// DeepL chunk-batching shape with a stubbed transport (1, 10, and 50 keys): the fake
/// handler answers immediately with index-aligned translations, so the benchmarks
/// measure provider-side batching and cache lookup, never the network. Caching is
/// disabled so every invocation exercises the batch path. Short-run job keeps local
/// iterations fast.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class TranslationBatchBenchmarks : IDisposable {
    private HttpClient _client = null!;
    private DeepLTranslationProvider _provider = null!;
    private string[] _oneKey = null!;
    private string[] _tenKeys = null!;
    private string[] _fiftyKeys = null!;
    private CultureInfo _culture = null!;
    private bool _disposed;

    /// <summary>
    /// Builds one provider over the fake transport plus the three key batches.
    /// </summary>
    [GlobalSetup]
    public void Setup() {
        var handler = new FakeDeepLHandler();
        _client = new HttpClient(handler);
        var options = Options.Create(new DeepLTranslationOptions {
            ApiKey = "bench-key",
            Endpoint = "https://api.deepl.com",
            CachingEnabled = false,
        });
        _provider = new DeepLTranslationProvider(options, _client);

        _oneKey = ["Hello world"];
        _tenKeys = [.. Enumerable.Range(0, 10).Select(static i => $"Hello world {i}")];
        _fiftyKeys = [.. Enumerable.Range(0, 50).Select(static i => $"Hello world {i}")];
        _culture = CultureInfo.GetCultureInfo("de");
    }

    /// <summary>
    /// Releases the HTTP client (and its fake handler) once per process.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <inheritdoc/>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the HTTP client (and its fake handler).
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected virtual void Dispose(bool disposing) {
        if(_disposed) {
            return;
        }

        _disposed = true;
        if(disposing) {
            _client.Dispose();
        }
    }

    /// <summary>Single-key batch through the provider batch API.</summary>
    [Benchmark]
    public async ValueTask<IReadOnlyDictionary<string, string?>> Batch1Async() =>
        await _provider.GetMessageTemplatesAsync(_oneKey, _culture);

    /// <summary>Ten-key batch (one chunk).</summary>
    [Benchmark]
    public async ValueTask<IReadOnlyDictionary<string, string?>> Batch10Async() =>
        await _provider.GetMessageTemplatesAsync(_tenKeys, _culture);

    /// <summary>Fifty-key batch (one full 50-text chunk).</summary>
    [Benchmark]
    public async ValueTask<IReadOnlyDictionary<string, string?>> Batch50Async() =>
        await _provider.GetMessageTemplatesAsync(_fiftyKeys, _culture);

    /// <summary>
    /// Fake DeepL transport: parses the request <c>text</c> array and answers with
    /// one index-aligned translation per element. No network, no key, no delay.
    /// </summary>
    private sealed class FakeDeepLHandler : HttpMessageHandler {
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var requestJson = JsonDocument.Parse(body);
            var count = requestJson.RootElement.GetProperty("text").GetArrayLength();
            var translations = string.Join(",", Enumerable.Range(0, count)
                .Select(static i => $"{{\"text\":\"Hallo Welt {i}\",\"detected_source_language\":\"EN\"}}"));
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent($"{{\"translations\":[{translations}]}}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
