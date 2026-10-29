//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Translation.Azure;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the Azure Translator HTTP
/// payloads: request bodies and response items are serialized and
/// deserialized through this context, so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(TranslationRequestBody[]))]
[JsonSerializable(typeof(TranslationItem[]))]
internal sealed partial class AzureTranslationJsonContext : JsonSerializerContext;
