//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Providers.Json;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the flat catalog payload
///: the provider deserializes through this context,
/// so no reflection-based serializer path remains.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class JsonProviderJsonContext : JsonSerializerContext;
