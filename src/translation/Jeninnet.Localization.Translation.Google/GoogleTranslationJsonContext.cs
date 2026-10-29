//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Google;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the Google Translate HTTP
/// payloads: request and response bodies are serialized and deserialized
/// through this context, so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(GoogleTranslationProvider.TranslateRequest))]
[JsonSerializable(typeof(GoogleTranslationProvider.TranslateResponse))]
internal sealed partial class GoogleTranslationJsonContext : JsonSerializerContext;
