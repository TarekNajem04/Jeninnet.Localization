//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#if JENINNET_TRANSLATION_AZURE
namespace Jeninnet.Localization.Translation.Azure;
#elif JENINNET_TRANSLATION_DEEPL
namespace Jeninnet.Localization.Translation.DeepL;
#else
#error Compile TranslationPipeline sources with JENINNET_TRANSLATION_AZURE or JENINNET_TRANSLATION_DEEPL defined.
#endif

/// <summary>
/// Shared capability mapping for machine-translation providers: every vendor supports the
/// same Translation-02 surface; only preload and caching hinge on the engine's cache switch.
/// </summary>
internal static class TranslationCapability {
    /// <summary>
    /// Resolves whether the machine-translation surface supports a feature.
    /// </summary>
    /// <param name="cachingEnabled">Whether the engine maintains its in-memory cache.</param>
    /// <param name="feature">The queried feature.</param>
    /// <returns>True when supported.</returns>
    internal static bool Supports(bool cachingEnabled, ProviderFeatures feature) => feature switch {
        ProviderFeatures.MultiKeys => true,
        ProviderFeatures.EventsListening => true,
        ProviderFeatures.Preload => cachingEnabled,
        ProviderFeatures.Refresh => true,
        ProviderFeatures.Caching => cachingEnabled,
        ProviderFeatures.SourceTemplates => true,
        _ => false,
    };
}

/// <summary>
/// The single wire-specific seam of the shared machine-translation engine: sending one chunk
/// and parsing its response. Everything else — caching, single-flight coalescing, batching,
/// translation memory, glossary protection, resilience, and events — lives in
/// <see cref="MachineTranslationEngine"/> and behaves identically for every vendor.
/// </summary>
internal interface ITranslationTransport {
    /// <summary>
    /// Gets the maximum texts per translation request. Batch administration chunks to the same
    /// size so dry-run estimates and real runs group keys identically.
    /// </summary>
    int MaxChunkSize { get; }

    /// <summary>
    /// Gets the circuit-breaker noun used in resilience exceptions ("Azure translation").
    /// </summary>
    string CircuitNoun { get; }

    /// <summary>
    /// Gets the service display name used in failure messages ("Azure Translator").
    /// </summary>
    string ServiceDisplayName { get; }

    /// <summary>
    /// Sends one chunk HTTP exchange. A fresh request message is built per call because a sent
    /// message can never be re-sent; the resilience pipeline calls this once per attempt.
    /// </summary>
    /// <param name="requestTexts">The glossary-projected source texts.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task whose result is send chunk.</returns>
    Task<HttpResponseMessage> SendChunkAsync(IReadOnlyList<string> requestTexts, string targetCulture, CancellationToken cancellationToken);

    /// <summary>
    /// Parses one chunk response into translated templates aligned with
    /// <paramref name="requestTexts"/>. Terminal client errors throw here with the
    /// vendor-specific message; the engine restores glossary placeholders afterwards.
    /// </summary>
    /// <param name="response">The received response.</param>
    /// <param name="requestTexts">The glossary-projected source texts, in request order.</param>
    /// <param name="targetCulture">The target culture name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The translated templates, aligned with <paramref name="requestTexts"/>.</returns>
    Task<IReadOnlyList<string?>> ParseChunkResponseAsync(HttpResponseMessage response, IReadOnlyList<string> requestTexts, string targetCulture, CancellationToken cancellationToken);
}
