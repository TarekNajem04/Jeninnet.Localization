//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Tms.Weblate;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the Weblate payloads
/// the connector reads, so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(WeblateUnitListResponse))]
[JsonSerializable(typeof(WeblateUploadResponse))]
internal sealed partial class WeblateJsonContext : JsonSerializerContext;
