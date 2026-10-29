//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Xliff;

/// <summary>
/// Shared constants for the  XLIFF (XML Localization Interchange File Format) 2.0 subset.
/// </summary>
internal static class XliffConstants {
    /// <summary>
    /// The gettext context separator; illegal in XML 1.0 content.
    /// </summary>
    internal const char CONTEXT_SEPARATOR = '\u0004';

    /// <summary>
    /// The printable stand-in for <see cref="CONTEXT_SEPARATOR"/> in XLIFF unit ids
    /// (U+2404 SYMBOL FOR END OF TRANSMISSION, legal in XML 1.0).
    /// </summary>
    internal const char ENCODED_CONTEXT_SEPARATOR = '\u2404';

    /// <summary>
    /// The supported segment states (XLIFF 2.0 core).
    /// </summary>
    internal static readonly string[] States = ["initial", "translated", "reviewed", "final"];

    /// <summary>
    /// The metadata note categories of the  subset.
    /// </summary>
    internal const string NOTE_CONTEXT = "context";

    /// <summary>
    /// The metadata note categories of the  plural encoding.
    /// </summary>
    internal const string NOTE_PLURAL_SINGULAR = "plural-singular";

    /// <summary>
    /// The metadata note categories of the  plural encoding.
    /// </summary>
    internal const string NOTE_PLURAL_OTHER = "plural-other";

    /// <summary>
    /// The metadata note categories of the  plural encoding.
    /// </summary>
    internal const string NOTE_PLURAL_INDEX = "plural-index";

    /// <summary>
    /// The metadata note category of the  plural encoding.
    /// </summary>
    internal const string NOTE_PLURAL_COUNT = "plural-count";

    /// <summary>
    /// The translator-note category: key-level translator context carried
    /// beside the structural metadata notes.
    /// </summary>
    internal const string NOTE_TRANSLATOR = "translator";

    /// <summary>
    /// Encodes a catalog key as an XLIFF unit id.
    /// </summary>
    /// <param name="key">The catalog key.</param>
    /// <returns>The unit id with the context separator encoded.</returns>
    internal static string EncodeId(string key) =>
        key.Replace(CONTEXT_SEPARATOR.ToString(), ENCODED_CONTEXT_SEPARATOR.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Encodes a plural-form unit id from its base id and form index.
    /// </summary>
    /// <param name="baseId">The base unit id.</param>
    /// <param name="index">The plural-form index.</param>
    /// <returns>The plural-form unit id.</returns>
    internal static string EncodePluralId(string baseId, int index) =>
        $"{baseId}[{index.ToString(CultureInfo.InvariantCulture)}]";
}
