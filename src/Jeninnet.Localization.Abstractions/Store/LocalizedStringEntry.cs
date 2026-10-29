//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Represents a localized string entry stored in a database or external JSON source.
/// </summary>
public class LocalizedStringEntry {
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedStringEntry"/> class.
    /// This constructor assumes that the resource was found in the source.
    /// </summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="value">The localized value.</param>
    [SetsRequiredMembers]
    public LocalizedStringEntry(Guid id, string resourceSet, string culture, string name, string value)
        : this(id, resourceSet, culture, name, value, resourceNotFound: false) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedStringEntry"/> class.
    /// </summary>
    /// <param name="id">The unique identifier of the entry.</param>
    /// <param name="resourceSet">The name of the resource set.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="name">The key name of the localized string.</param>
    /// <param name="value">The localized value.</param>
    /// <param name="resourceNotFound">
    /// A value indicating whether the localized string was not found in the original source.
    /// </param>
    [SetsRequiredMembers]
    public LocalizedStringEntry(Guid id, string resourceSet, string culture, string name, string value, bool resourceNotFound) {
        // Validate required parameters.
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        Id = id;
        ResourceSet = resourceSet;
        Culture = culture;
        Name = name;
        Value = value;
        ResourceNotFound = resourceNotFound;
    }

    /// <summary>
    /// Gets the unique identifier of the localized entry.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the name of the resource set.
    /// </summary>
    public required string ResourceSet { get; init; }

    /// <summary>
    /// Gets the culture code of the localized string (e.g., "en-US").
    /// </summary>
    public required string Culture { get; init; }

    /// <summary>
    /// Gets the key name of the localized string.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the localized string value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Gets a value indicating whether this entry was not found in the source and is therefore a fallback.
    /// </summary>
    public bool ResourceNotFound { get; }

    /// <summary>
    /// Determines whether this localized string entry represents a missing resource.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the resource is missing; otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsMissing() => ResourceNotFound;
    /// <summary>
    /// Returns the localized string value. If the resource was not found, a formatted placeholder is returned.
    /// </summary>
    /// <returns>
    /// The localized string if found; otherwise, a placeholder string indicating the resource is missing.
    /// </returns>
    public override string ToString() {
        if(ResourceNotFound) {
            // Avoid double-prefixing if the value already contains a missing marker.
            return Value.Contains("MISSING", StringComparison.OrdinalIgnoreCase)
                ? Value
                : $"[MISSING] {Value}";
        }

        return Value;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LocalizedStringEntry other && Id.Equals(other.Id);

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Performs an implicit conversion from <see cref="LocalizedStringEntry"/> to a <see cref="string"/>.
    /// </summary>
    /// <param name="entry">The <see cref="LocalizedStringEntry"/> to convert.</param>
    /// <returns>
    /// The localized string value if <paramref name="entry"/> is not null; otherwise, null.
    /// </returns>
    [return: NotNullIfNotNull(nameof(entry))]
    public static implicit operator string?(LocalizedStringEntry entry) => entry?.Value;

    /// <summary>
    /// Creates a placeholder entry indicating a missing localized string.
    /// </summary>
    /// <param name="resourceSet">The resource set name.</param>
    /// <param name="culture">The culture code (e.g., "en-US").</param>
    /// <param name="name">The key name of the missing localized string.</param>
    /// <returns>
    /// A <see cref="LocalizedStringEntry"/> with a placeholder value signifying that the resource is missing.
    /// </returns>
    public static LocalizedStringEntry Missing(string resourceSet, string culture, string name) =>
        new(
            id: Guid.Empty,
            resourceSet: resourceSet,
            culture: culture,
            name: name,
            value: $"[MISSING:{name}]",
            resourceNotFound: true
        );
}
