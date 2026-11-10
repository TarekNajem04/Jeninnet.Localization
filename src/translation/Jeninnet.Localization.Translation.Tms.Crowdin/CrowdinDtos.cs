//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Crowdin;

/// <summary>
/// One Crowdin string list page: the string rows plus the pagination cursor.
/// </summary>
internal sealed class CrowdinStringListResponse {
    /// <summary>
    /// Gets or sets the string rows.
    /// </summary>
    [JsonPropertyName("data")]
    public List<CrowdinStringEntry> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination cursor.
    /// </summary>
    [JsonPropertyName("pagination")]
    public CrowdinPagination Pagination { get; set; } = new();
}

/// <summary>
/// One Crowdin string row wrapper.
/// </summary>
internal sealed class CrowdinStringEntry {
    /// <summary>
    /// Gets or sets the string.
    /// </summary>
    [JsonPropertyName("data")]
    public CrowdinString Data { get; set; } = new();
}

/// <summary>
/// One Crowdin project string: the numeric id, the key-based identifier (empty for
/// file-based strings without an identifier), the source text, and the context.
/// </summary>
internal sealed class CrowdinString {
    /// <summary>
    /// Gets or sets the numeric string id translations join on.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the key-based identifier, or null/empty for file-based strings.
    /// </summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>
    /// Gets or sets the source text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string context (the translator-note carrier).
    /// </summary>
    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

/// <summary>
/// One Crowdin language-translation list page.
/// </summary>
internal sealed class CrowdinTranslationListResponse {
    /// <summary>
    /// Gets or sets the translation rows.
    /// </summary>
    [JsonPropertyName("data")]
    public List<CrowdinTranslationEntry> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination cursor.
    /// </summary>
    [JsonPropertyName("pagination")]
    public CrowdinPagination Pagination { get; set; } = new();
}

/// <summary>
/// One Crowdin translation row wrapper.
/// </summary>
internal sealed class CrowdinTranslationEntry {
    /// <summary>
    /// Gets or sets the translation.
    /// </summary>
    [JsonPropertyName("data")]
    public CrowdinTranslation Data { get; set; } = new();
}

/// <summary>
/// One Crowdin translation: the owning string id, the translated content (null when
/// untranslated), and the plural form name (null for singular strings).
/// </summary>
internal sealed class CrowdinTranslation {
    /// <summary>
    /// Gets or sets the owning string id.
    /// </summary>
    [JsonPropertyName("stringId")]
    public long StringId { get; set; }

    /// <summary>
    /// Gets or sets the translated content, or null when untranslated.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the plural form name, or null for singular strings.
    /// </summary>
    [JsonPropertyName("pluralFormName")]
    public string? PluralFormName { get; set; }
}

/// <summary>
/// A Crowdin pagination cursor.
/// </summary>
internal sealed class CrowdinPagination {
    /// <summary>
    /// Gets or sets the page offset.
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// Gets or sets the page limit.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }
}

/// <summary>
/// A Crowdin storage upload response: the storage id later calls reference.
/// </summary>
internal sealed class CrowdinStorageResponse {
    /// <summary>
    /// Gets or sets the storage.
    /// </summary>
    [JsonPropertyName("data")]
    public CrowdinStorage Data { get; set; } = new();
}

/// <summary>
/// A Crowdin storage reference.
/// </summary>
internal sealed class CrowdinStorage {
    /// <summary>
    /// Gets or sets the storage id.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

/// <summary>
/// A Crowdin upload-translations request: the uploaded storage plus the project
/// file the translations attach to.
/// </summary>
internal sealed class CrowdinUploadTranslationsRequest {
    /// <summary>
    /// Gets or sets the storage id of the uploaded translated file.
    /// </summary>
    [JsonPropertyName("storageId")]
    public long StorageId { get; set; }

    /// <summary>
    /// Gets or sets the project file id the translations attach to.
    /// </summary>
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }
}

/// <summary>
/// A Crowdin upload-translations response.
/// </summary>
internal sealed class CrowdinUploadTranslationsResponse {
    /// <summary>
    /// Gets or sets the upload result.
    /// </summary>
    [JsonPropertyName("data")]
    public CrowdinUploadTranslationsResult Data { get; set; } = new();
}

/// <summary>
/// A Crowdin upload-translations result.
/// </summary>
internal sealed class CrowdinUploadTranslationsResult {
    /// <summary>
    /// Gets or sets the project id.
    /// </summary>
    [JsonPropertyName("projectId")]
    public long ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the storage id.
    /// </summary>
    [JsonPropertyName("storageId")]
    public long StorageId { get; set; }

    /// <summary>
    /// Gets or sets the project file id.
    /// </summary>
    [JsonPropertyName("fileId")]
    public long FileId { get; set; }

    /// <summary>
    /// Gets or sets the language id.
    /// </summary>
    [JsonPropertyName("languageId")]
    public string LanguageId { get; set; } = string.Empty;
}
