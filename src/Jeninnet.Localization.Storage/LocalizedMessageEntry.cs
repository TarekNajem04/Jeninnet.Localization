//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Storage;

/// <summary>
/// <para>Canonical model for a single localized message entry.</para>
/// <para>Represents the base concept: <em>One message key maps to one message template</em>.</para>
/// <para>
/// Every provider — Memory, Json, Xml, ResX, Database — stores or produces data
/// that can be represented as a collection of <see cref="LocalizedMessageEntry"/> instances.
/// </para>
/// <para>
/// This is the core data contract that the <c>Localization.Storage</c> project defines
/// as the shared canonical schema for the entire localization ecosystem.
/// </para>
/// <para>
/// Schema equivalence:
///   - JSON:      { "Key": "...", "Template": "..." }
///   - XML:       &lt;entry key="..." template="..." /&gt;
///   - Database:  LocalizationMessages(MessageKey, Culture, Template)
///   - ResX:      Name = MessageKey, Value = Template
///   - Memory:    Dictionary{string, string} entry
/// </para>
/// </summary>
public class LocalizedMessageEntry {
    /// <summary>
    /// <para>The unique message identifier used to look up the corresponding localized template.</para>
    /// <para>Examples: <c>"NotEmpty"</c>, <c>"Email"</c>, <c>"GreaterThan"</c>, <c>"InclusiveBetween"</c>.</para>
    /// <para>This key matches the <c>ValidationError.MessageKey</c> produced by validation rules.</para>
    /// </summary>
    public required string MessageKey { get; init; }

    /// <summary>
    /// <para>The message template containing placeholders that are replaced with dynamic values at runtime.</para>
    /// <para>Example: <c>"{PropertyName} must be between {From} and {To}."</c></para>
    /// <para>
    /// Recognized placeholders include <c>{PropertyName}</c>, <c>{ComparisonValue}</c>,
    /// <c>{From}</c>, <c>{To}</c>, <c>{MinLength}</c>, <c>{MaxLength}</c>, and any
    /// parameter supplied via the validation error's <c>MessageParameters</c>.
    /// </para>
    /// </summary>
    public required string Template { get; init; }
}
