//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.DeepL;

/// <summary>
/// One request of the DeepL translate payload: the texts to translate plus the target (and
/// optional source) DeepL language codes. A single call carries the whole chunk; the response
/// translations align with <see cref="Text"/> by index.
/// </summary>
internal sealed class DeepLTranslateRequest {
    /// <summary>
    /// Gets or sets the texts to translate, in request order.
    /// </summary>
    [JsonPropertyName("text")]
    public string[] Text { get; init; } = [];

    /// <summary>
    /// Gets or sets the DeepL target language code (for example <c>DE</c>).
    /// </summary>
    [JsonPropertyName("target_lang")]
    public string TargetLang { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the DeepL source language code (for example <c>EN</c>), or
    /// <see langword="null"/> to let the service auto-detect the source language.
    /// </summary>
    [JsonPropertyName("source_lang")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceLang { get; init; }
}

/// <summary>
/// One response of the DeepL translate payload: the translations aligned with the request
/// texts by index.
/// </summary>
internal sealed class DeepLTranslateResponse {
    /// <summary>
    /// Gets the translations, aligned with the request texts by index.
    /// </summary>
    [JsonPropertyName("translations")]
    public DeepLTranslation[] Translations { get; init; } = [];
}

/// <summary>
/// One translation of a DeepL translate response.
/// </summary>
internal sealed class DeepLTranslation {
    /// <summary>
    /// Gets the translated text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Gets the detected source language reported by the service.
    /// </summary>
    [JsonPropertyName("detected_source_language")]
    public string? DetectedSourceLanguage { get; init; }
}
