//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// Declares one code-first localized resource: the resource set,
/// the message key, and the default-culture template. Canonical-only — the resource
/// set and key are explicit strings, with no namespace or member-name derivation —
/// and the attribute is one carrier of a <see cref="ResourceDeclaration"/>; discovery
/// through reflection is honest about trimming (see the Core discovery helper).
/// </summary>
/// <remarks>
/// The declared template is the <em>default-culture</em> value the synchronizer
/// creates when the key is missing; other cultures are never overwritten (their
/// values are translations). An optional <see cref="Note"/> carries the
/// translator note.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class LocalizedResourceAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedResourceAttribute"/> class.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="key">The message key.</param>
    /// <param name="defaultTemplate">The default-culture template.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public LocalizedResourceAttribute(string resourceSet, string key, string defaultTemplate) {
        ArgumentNullException.ThrowIfNull(resourceSet);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(defaultTemplate);

        ResourceSet = resourceSet;
        Key = key;
        DefaultTemplate = defaultTemplate;
    }

    /// <summary>
    /// Gets the resource-set name.
    /// </summary>
    public string ResourceSet { get; }

    /// <summary>
    /// Gets the message key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the default-culture template.
    /// </summary>
    public string DefaultTemplate { get; }

    /// <summary>
    /// Gets the optional translator note applied with the declaration, or
    /// <see langword="null"/> when the declaration carries no note.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Converts this attribute to its canonical <see cref="ResourceDeclaration"/>.
    /// </summary>
    /// <returns>The declaration.</returns>
    public ResourceDeclaration ToDeclaration() => new(ResourceSet, Key, DefaultTemplate) { Note = Note };
}
