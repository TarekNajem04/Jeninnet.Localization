//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Integrations.JavaScript;

/// <summary>
/// Builds per-culture JavaScript resource payloads and writes static exports
///: one serialization feeds both the endpoint and the export, so the
/// bytes can never disagree.
/// </summary>
/// <remarks>
/// Keys resolve through the exact requested culture only; keys the provider does not
/// serve for that culture are omitted. Culture fallback is owned by
/// <c>DefaultMessageLocalizer</c> and is deliberately not re-implemented here —
/// clients choose the culture they want, or hosts allow-list the fallback culture's
/// keys as well. Only <see cref="JavaScriptLocalizationOptions.AllowedKeys"/> are ever
/// requested from the provider.
/// </remarks>
public sealed class JavaScriptResourcePayloadBuilder {
    private readonly IMessageProvider _provider;
    private readonly JavaScriptLocalizationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaScriptResourcePayloadBuilder"/> class.
    /// </summary>
    /// <param name="provider">The canonical message provider (exact-match template source).</param>
    /// <param name="options">The endpoint/export options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> or <paramref name="options"/> is null.</exception>
    public JavaScriptResourcePayloadBuilder(IMessageProvider provider, JavaScriptLocalizationOptions options) {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        _provider = provider;
        _options = options;
    }

    /// <summary>
    /// Builds the payload for a culture.
    /// </summary>
    /// <param name="cultureName">The culture name to build for.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>
    /// The payload, or <see langword="null"/> when the culture name is unrecognized or not in
    /// <see cref="JavaScriptLocalizationOptions.SupportedCultures"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cultureName"/> is null or empty.</exception>
    public async ValueTask<JavaScriptResourcePayload?> BuildAsync(string cultureName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(cultureName);

        if(!TryResolveCulture(cultureName, out var culture) || !IsSupported(culture.Name)) {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();
        await using(var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default })) {
            writer.WriteStartObject();
            foreach(var key in _options.AllowedKeys) {
                cancellationToken.ThrowIfCancellationRequested();
                var template = await _provider.GetMessageTemplateAsync(key, culture, cancellationToken).ConfigureAwait(false);
                if(template is not null) {
                    writer.WriteString(key, template);
                }
            }

            writer.WriteEndObject();
        }

        var utf8Json = buffer.WrittenSpan.ToArray();
        return new JavaScriptResourcePayload(culture.Name, utf8Json, ComputeETag(utf8Json));
    }

    /// <summary>
    /// Builds payloads for several cultures (static-export mode); cultures that cannot be
    /// built (unrecognized or not in <see cref="JavaScriptLocalizationOptions.SupportedCultures"/>)
    /// are skipped.
    /// </summary>
    /// <param name="cultures">The culture names to build for.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>The built payloads, in the requested order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultures"/> is null.</exception>
    public async ValueTask<List<JavaScriptResourcePayload>> ExportAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(cultures);

        var payloads = new List<JavaScriptResourcePayload>();
        foreach(var culture in cultures) {
            var payload = await BuildAsync(culture, cancellationToken).ConfigureAwait(false);
            if(payload is not null) {
                payloads.Add(payload);
            }
        }

        return payloads;
    }

    /// <summary>
    /// Writes static exports to a directory as <c>{culture}.json</c> files whose bytes are
    /// identical to the endpoint response bodies (build-time embedding for SPA/WASM (WebAssembly)/offline
    /// shells).
    /// </summary>
    /// <param name="directory">The target directory (created when missing).</param>
    /// <param name="cultures">The culture names to export.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task that completes when every payload is written.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cultures"/> is null.</exception>
    public async ValueTask ExportToDirectoryAsync(string directory, IEnumerable<string> cultures, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(cultures);

        Directory.CreateDirectory(directory);
        foreach(var payload in await ExportAsync(cultures, cancellationToken).ConfigureAwait(false)) {
            var path = Path.Combine(directory, payload.Culture + ".json");
            await File.WriteAllBytesAsync(path, payload.Utf8Json.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Computes ETag.
    /// </summary>
    /// <param name="utf8Json">The utf8Json.</param>
    /// <returns>The ETag text.</returns>
    private static string ComputeETag(byte[] utf8Json) =>
        "\"" +
#if NET9_0_OR_GREATER
        Convert.ToHexStringLower(SHA256.HashData(utf8Json)) +
#else
        Convert.ToHexString(SHA256.HashData(utf8Json)).ToLowerInvariant() +
#endif
        "\"";

    /// <summary>
    /// Attempts to resolve culture.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private static bool TryResolveCulture(string cultureName, out CultureInfo culture) {
        try {
            culture = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch(ArgumentException) {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }

    /// <summary>
    /// Determines whether the value is supported.
    /// </summary>
    /// <param name="cultureName">The culture.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private bool IsSupported(string cultureName) {
        if(_options.SupportedCultures.Count == 0) {
            return true;
        }

        for(var index = 0; index < _options.SupportedCultures.Count; index++) {
            if(string.Equals(_options.SupportedCultures[index], cultureName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
