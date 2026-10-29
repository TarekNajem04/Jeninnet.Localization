//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions.Store;

/// <summary>
/// One code-first resource declaration: the canonical input the
/// resource synchronizer consumes. The <see cref="LocalizedResourceAttribute"/> is
/// one carrier; callers can also build declarations directly (the trimmed/AOT-safe
/// path — discovery through reflection is annotated and opt-in).
/// </summary>
public sealed class ResourceDeclaration {
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceDeclaration"/> class.
    /// </summary>
    /// <param name="resourceSet">The resource-set name.</param>
    /// <param name="key">The message key.</param>
    /// <param name="defaultTemplate">The default-culture template.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public ResourceDeclaration(string resourceSet, string key, string defaultTemplate) {
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
    /// <see langword="null"/> when the declaration carries no note. A declared note is
    /// applied on create and — only when updates are allowed — upserted when it
    /// differs; a declaration without a note never touches existing notes.
    /// </summary>
    public string? Note { get; init; }
}
