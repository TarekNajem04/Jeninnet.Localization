//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// One request item of the Azure Translator translate payload. Namespace-level so the
/// source-generated JSON contract (<see cref="AzureTranslationJsonContext"/>) can
/// reference it directly.
/// </summary>
internal sealed class TranslationRequestBody {
    /// <summary>
    /// Gets or sets the text to translate.
    /// </summary>
    [JsonPropertyName("Text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// One response item of the Azure Translator translate payload: the aligned translation
/// candidates for one requested text.
/// </summary>
internal sealed class TranslationItem {
    /// <summary>
    /// Gets the translation candidates (one per target language).
    /// </summary>
    [JsonPropertyName("translations")]
    public TranslationResult[] Translations { get; init; } = [];
}

/// <summary>
/// One translation candidate of an Azure Translator response item.
/// </summary>
internal sealed class TranslationResult {
    /// <summary>
    /// Gets the translated text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Gets the target language of the translation.
    /// </summary>
    [JsonPropertyName("to")]
    public string? To { get; init; }
}
