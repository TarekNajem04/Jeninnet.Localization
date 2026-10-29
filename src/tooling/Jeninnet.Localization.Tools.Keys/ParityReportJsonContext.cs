//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Trimming/AOT-safe source-generated JSON contract for the versioned parity report.
/// The report schema is byte-stable: property order follows declaration order and the
/// indented formatting mirrors the previous reflection-based writer output exactly.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ParityReport))]
internal sealed partial class ParityReportJsonContext : JsonSerializerContext;
