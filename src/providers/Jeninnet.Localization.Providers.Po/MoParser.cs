//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// Reads GNU MO (Machine Object) binary catalogs (the compiled gettext format) into the same
/// <see cref="PoCatalog"/> the PO (Portable Object) text parser produces: both carriers
/// feed <see cref="PoCatalogBuilder"/>, so keys, contexts, plurals, headers, and
/// <c>Plural-Forms</c> validation exist once.
/// <para>
/// Format contract: magic <c>0x950412de</c> (little-endian) or <c>0xde120495</c>
/// (big-endian); revisions 0.x and 1.x accepted (the minor/hash fields are not
/// consumed); string tables are bounds-checked; strings are strict UTF-8 (invalid
/// bytes fail, never replacement-decoded); contexts use <c>msgctxt + EOT (End of Transmission)</c> and
/// plurals use NUL-joined originals and translations. Malformed binaries fail fast
/// with structure diagnostics and never yield partial tables.
/// </para>
/// </summary>
internal static class MoParser {
    private const int HEADER_SIZE = 28;
    private const uint LITTLE_ENDIAN_MAGIC = 0x950412de;
    private const uint BIG_ENDIAN_MAGIC = 0xde120495;
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Parses a MO binary catalog.
    /// </summary>
    /// <param name="bytes">The MO file bytes.</param>
    /// <param name="filePath">The MO file path (diagnostics only).</param>
    /// <returns>The parsed catalog.</returns>
    /// <exception cref="PoParseException">Thrown when the binary is malformed.</exception>
    internal static PoCatalog Parse(byte[] bytes, string filePath) {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(filePath);

        if(bytes.Length < HEADER_SIZE) {
            throw new PoParseException(
                filePath,
                0,
                $"MO file is too small ({bytes.Length.ToString(CultureInfo.InvariantCulture)} bytes) to contain a {HEADER_SIZE.ToString(CultureInfo.InvariantCulture)}-byte header");
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var littleEndian = magic == LITTLE_ENDIAN_MAGIC || (magic == BIG_ENDIAN_MAGIC
                ? false
                : throw new PoParseException(
                filePath,
                0,
                $"Invalid MO magic 0x{magic.ToString("x8", CultureInfo.InvariantCulture)}; expected 0x950412de (little-endian) or 0xde120495 (big-endian)"));
        uint ReadUInt32(int offset) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset))
            : BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));

        var revision = ReadUInt32(4);
        var major = revision >> 16;
        if(major > 1) {
            throw new PoParseException(
                filePath,
                0,
                $"Unsupported MO revision {(revision >> 16).ToString(CultureInfo.InvariantCulture)}.{(revision & 0xFFFF).ToString(CultureInfo.InvariantCulture)}; supported revisions are 0.x and 1.x");
        }

        var count = ReadUInt32(8);
        var originalsTable = ReadUInt32(12);
        var translationsTable = ReadUInt32(16);

        ValidateTable(originalsTable, count, "original", bytes.Length, filePath);
        ValidateTable(translationsTable, count, "translation", bytes.Length, filePath);

        var builder = new PoCatalogBuilder();
        for(var i = 0u; i < count; i++) {
            var originalEntry = (int)((long)originalsTable + (i * 8));
            var translationEntry = (int)((long)translationsTable + (i * 8));
            var original = ReadString(
                ReadUInt32(originalEntry + 4),
                ReadUInt32(originalEntry),
                "original",
                i,
                bytes,
                filePath);
            var translation = ReadString(
                ReadUInt32(translationEntry + 4),
                ReadUInt32(translationEntry),
                "translation",
                i,
                bytes,
                filePath);

            builder.AddEntry(BuildEntry(original, translation, i, filePath), filePath);
        }

        return builder.Build(filePath);
    }

    /// <summary>
    /// Builds entry.
    /// </summary>
    /// <param name="original">The original.</param>
    /// <param name="translation">The translation.</param>
    /// <param name="index">The index.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>The entry result.</returns>
    private static PoEntry BuildEntry(string original, string translation, uint index, string filePath) {
        if(original.Length == 0) {
            return new PoEntry {
                Context = null,
                MsgId = string.Empty,
                MsgIdPlural = null,
                Forms = [translation],
                LineNumber = 0,
            };
        }

        string? context = null;
        var body = original;
        var contextSeparator = original.IndexOf(PoCatalog.CONTEXT_SEPARATOR);
        if(contextSeparator >= 0) {
            context = original[..contextSeparator];
            body = original[(contextSeparator + 1)..];
        }

        string msgId;
        string? msgIdPlural = null;
        var pluralSeparator = body.IndexOf('\0');
        if(pluralSeparator >= 0) {
            msgId = body[..pluralSeparator];
            msgIdPlural = body[(pluralSeparator + 1)..];
            if(msgIdPlural.Contains('\0')) {
                throw new PoParseException(
                    filePath,
                    0,
                    $"MO original string {index.ToString(CultureInfo.InvariantCulture)} contains more than one NUL separator");
            }
        } else {
            msgId = body;
        }

        string[] forms;
        if(msgIdPlural is null) {
            if(translation.Contains('\0')) {
                throw new PoParseException(
                    filePath,
                    0,
                    $"MO translation string {index.ToString(CultureInfo.InvariantCulture)} contains NUL separators but its original has no msgid_plural");
            }

            forms = [translation];
        } else {
            forms = translation.Split('\0');
        }

        return new PoEntry {
            Context = context,
            MsgId = msgId,
            MsgIdPlural = msgIdPlural,
            Forms = forms,
            LineNumber = 0,
        };
    }

    /// <summary>
    /// Validates table.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="table">The table.</param>
    /// <param name="fileLength">The file length.</param>
    /// <param name="filePath">The file path.</param>
    private static void ValidateTable(uint offset, uint count, string table, int fileLength, string filePath) {
        var end = (long)offset + ((long)count * 8);
        if(end > fileLength) {
            throw new PoParseException(
                filePath,
                0,
                $"MO {table} string table of {count.ToString(CultureInfo.InvariantCulture)} entries at offset {offset.ToString(CultureInfo.InvariantCulture)} exceeds the file size");
        }
    }

    /// <summary>
    /// Reads string.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="table">The table.</param>
    /// <param name="index">The index.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>The string text.</returns>
    private static string ReadString(uint offset, uint length, string table, uint index, byte[] bytes, string filePath) {
        if((long)offset + length > bytes.Length) {
            throw new PoParseException(
                filePath,
                0,
                $"MO {table} string {index.ToString(CultureInfo.InvariantCulture)} at offset {offset.ToString(CultureInfo.InvariantCulture)} with length {length.ToString(CultureInfo.InvariantCulture)} exceeds the file size");
        }

        try {
            return _strictUtf8.GetString(bytes, (int)offset, (int)length);
        }
        catch(DecoderFallbackException) {
            throw new PoParseException(
                filePath,
                0,
                $"MO {table} string {index.ToString(CultureInfo.InvariantCulture)} is not valid UTF-8");
        }
    }
}
