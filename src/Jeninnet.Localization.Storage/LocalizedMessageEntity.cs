//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Storage;

/// <summary>
/// <para>Full persistence entity representing a single localized message record in a database.</para>
/// <para>
/// This type includes an auto-generated <see cref="Id"/> for ORM (Object-Relational Mapping) mapping, change tracking,
/// and entity framework integration.
/// </para>
/// <para>
/// Use <see cref="LocalizedMessageEntity"/> when reading/writing to a database table
/// via Entity Framework, Dapper, or ADO.NET.
/// </para>
/// <para>Expected database schema:</para>
/// <code>
/// CREATE TABLE LocalizationMessages (
///     Id          INTEGER PRIMARY KEY AUTOINCREMENT,
///     MessageKey  TEXT    NOT NULL,
///     Culture     TEXT    NOT NULL,
///     Template    TEXT    NOT NULL,
///     UNIQUE(MessageKey, Culture)
/// );
/// </code>
/// </summary>
public sealed class LocalizedMessageEntity {
    /// <summary>
    /// Auto-generated primary key for this localized message record.
    /// Used by ORMs for identity tracking and change detection.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// <para>The unique message identifier matching <c>ValidationError.MessageKey</c>.</para>
    /// <para>Examples: <c>"NotEmpty"</c>, <c>"Email"</c>, <c>"GreaterThan"</c>.</para>
    /// <para>Combined with <see cref="Culture"/> to form a unique constraint.</para>
    /// </summary>
    public required string MessageKey { get; init; }

    /// <summary>
    /// <para>The culture code (e.g., <c>"en-US"</c>, <c>"ar-SY"</c>) for which this
    /// localized message template is intended. Combined with <see cref="MessageKey"/>
    /// to form a unique constraint.</para>
    /// </summary>
    public required string Culture { get; init; }

    /// <summary>
    /// <para>The localized message template with placeholders for dynamic values.</para>
    /// <para>Example: <c>"{PropertyName} must be greater than {ComparisonValue}."</c></para>
    /// <para>
    /// Placeholders are resolved at runtime by the message formatter
    /// using values from the validation error's <c>MessageParameters</c>.
    /// </para>
    /// </summary>
    public required string Template { get; init; }
}
