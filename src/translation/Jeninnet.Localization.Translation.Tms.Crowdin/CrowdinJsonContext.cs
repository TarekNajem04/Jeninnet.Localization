//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Crowdin;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the Crowdin payloads
/// the connector sends and reads, so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(CrowdinStringListResponse))]
[JsonSerializable(typeof(CrowdinTranslationListResponse))]
[JsonSerializable(typeof(CrowdinStorageResponse))]
[JsonSerializable(typeof(CrowdinUploadTranslationsRequest))]
[JsonSerializable(typeof(CrowdinUploadTranslationsResponse))]
internal sealed partial class CrowdinJsonContext : JsonSerializerContext;
