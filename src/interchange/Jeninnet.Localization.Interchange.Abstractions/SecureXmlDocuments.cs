//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Abstractions;

/// <summary>
/// Builds the hardened <see cref="XmlReaderSettings"/> every interchange XML read uses.
/// External entities are never resolved (<see cref="XmlReaderSettings.XmlResolver"/> stays
/// <see langword="null"/>), and entity expansion plus total document size are capped so a
/// hostile catalog cannot exhaust memory. The DTD itself is accepted
/// (<see cref="DtdProcessing.Parse"/>) because catalogs in the wild may carry a harmless
/// internal subset; blocking external resolution and capping expansion closes the attack
/// without changing which documents parse.
/// </summary>
internal static class SecureXmlDocuments {
    /// <summary>
    /// The maximum XML text size accepted, in characters (16 MiB).
    /// Catalogs are small data files; anything larger is rejected before parsing completes.
    /// </summary>
    internal const long MAX_CHARACTERS_IN_DOCUMENT = 16_777_216;

    /// <summary>
    /// The maximum characters produced by entity expansion (4 MiB).
    /// Bounds billion-laughs style internal-entity abuse while leaving normal use untouched.
    /// </summary>
    internal const long MAX_CHARACTERS_FROM_ENTITIES = 4_194_304;

    /// <summary>
    /// Creates the hardened reader settings shared by all interchange XML reads.
    /// </summary>
    /// <returns>The configured settings.</returns>
    internal static XmlReaderSettings CreateReaderSettings() => new() {
        DtdProcessing = DtdProcessing.Parse,
        XmlResolver = null,
        MaxCharactersInDocument = MAX_CHARACTERS_IN_DOCUMENT,
        MaxCharactersFromEntities = MAX_CHARACTERS_FROM_ENTITIES,
    };

    /// <summary>
    /// Parses XML text with the hardened settings.
    /// </summary>
    /// <param name="text">The XML document text.</param>
    /// <param name="options">The load options (line info is preserved).</param>
    /// <returns>The parsed document.</returns>
    internal static XDocument ParseText(string text, LoadOptions options) {
        using var reader = XmlReader.Create(new StringReader(text), CreateReaderSettings());
        return XDocument.Load(reader, options);
    }

    /// <summary>
    /// Loads an XML file with the hardened settings.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The load options (line info is preserved).</param>
    /// <returns>The parsed document.</returns>
    internal static XDocument LoadFile(string path, LoadOptions options) {
        using var reader = XmlReader.Create(path, CreateReaderSettings());
        return XDocument.Load(reader, options);
    }
}
