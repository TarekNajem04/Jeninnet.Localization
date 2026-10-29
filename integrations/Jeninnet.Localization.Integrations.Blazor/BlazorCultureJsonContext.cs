//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Integrations.Blazor;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for JS-interop string encoding:
/// the culture bridge serializes plain strings into JavaScript source, so the contract is
/// a single string type — no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(string))]
internal sealed partial class BlazorCultureJsonContext : JsonSerializerContext;
