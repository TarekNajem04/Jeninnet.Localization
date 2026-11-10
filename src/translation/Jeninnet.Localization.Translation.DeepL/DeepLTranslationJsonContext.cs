//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.DeepL;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the DeepL HTTP
/// payloads: translate requests are serialized and translate responses deserialized through
/// this context, so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(DeepLTranslateRequest))]
[JsonSerializable(typeof(DeepLTranslateResponse))]
internal sealed partial class DeepLTranslationJsonContext : JsonSerializerContext;
