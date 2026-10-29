//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// Parses gettext PO (Portable Object) text into a <see cref="PoCatalog"/>.
/// Multiline strings, contexts (<c>msgctxt</c>), plurals (<c>msgid_plural</c> /
/// <c>msgstr[n]</c>), and the header <c>Plural-Forms</c> line are supported;
/// compiled <c>.mo</c> binaries are read by <see cref="MoParser"/> (same mapping,
///). The text grammar itself is the shared <c>PoTextParsing</c> source
///; this parser owns the catalog mapping, the fuzzy-entry policy, and
/// duplicate-key validation. Malformed input fails fast with file/line diagnostics
/// and never yields partial tables.
/// </summary>
internal static class PoParser {
    /// <summary>
    /// Parses PO text.
    /// </summary>
    /// <param name="text">The PO file text.</param>
    /// <param name="filePath">The PO file path (diagnostics only).</param>
    /// <returns>The parsed catalog.</returns>
    /// <exception cref="PoParseException">Thrown when the text is malformed.</exception>
    internal static PoCatalog Parse(string text, string filePath) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);

        var builder = new PoCatalogBuilder();

        foreach(var entry in PoTextParsing.Parse(text, filePath, static (path, line, message) => new PoParseException(path, line, message))) {
            if(entry.Fuzzy) {
                continue;
            }

            builder.AddEntry(new PoEntry {
                Context = entry.Context,
                MsgId = entry.MsgId,
                MsgIdPlural = entry.MsgIdPlural,
                Forms = entry.Forms,
                LineNumber = entry.StartLine,
            }, filePath);
        }

        return builder.Build(filePath);
    }
}
