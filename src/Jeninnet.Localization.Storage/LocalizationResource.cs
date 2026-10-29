//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Storage;

/// <summary>
/// <para>
/// Represents a single culture's complete set of localized message templates,
/// typically loaded from a JSON file, XML file, or database query.
/// </para>
/// <para>
/// This is the canonical in-memory representation of a culture-specific resource file.
/// Each <see cref="LocalizationResource"/> instance corresponds to exactly one
/// language/locale (e.g., <c>"en-US"</c>, <c>"ar-SY"</c>).
/// </para>
/// <para>The <see cref="Entries"/> collection contains one entry per message key.</para>
/// <para>Loading pattern (JSON example):</para>
/// <code>
/// {
///   "NotEmpty":  { "MessageKey": "NotEmpty",  "Template": "{PropertyName} must not be empty." },
///   "Email":     { "MessageKey": "Email",     "Template": "{PropertyName} is not a valid email address." }
/// }
/// </code>
///
/// This type is the key payload type for future Import/Export tooling
/// and synchronization services built on top of <c>Localization.Storage</c>.
/// </summary>
public sealed class LocalizationResource {
    /// <summary>
    /// <para>The collection of localized message entries for this culture.</para>
    /// <para>
    /// Each <see cref="LocalizedMessageEntry"/> contains a <see cref="LocalizedMessageEntry.MessageKey"/>
    /// and its corresponding <see cref="LocalizedMessageEntry.Template"/>.
    /// </para>
    /// </summary>
    public required IReadOnlyCollection<LocalizedMessageEntry> Entries { get; init; }
}
