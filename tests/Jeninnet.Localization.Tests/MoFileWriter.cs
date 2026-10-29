//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Test-only GNU MO (Machine Object) binary writer: fixtures are authored in-repo; no
/// compiled artifacts are copied. Produces minimal spec-shaped binaries (header,
/// original/translation string tables, NUL-terminated data section, absent hash
/// table) in either endianness, and exposes raw patching so malformed-input tests
/// can corrupt individual header fields.
/// </summary>
internal static class MoFileWriter {
    internal const int HEADER_SIZE = 28;

    /// <summary>
    /// Writes a MO binary for the given entries.
    /// </summary>
    /// <param name="entries">The (original, translation) pairs; originals may embed
    /// <c>msgctxt + EOT (End of Transmission)</c> and <c>msgid + NUL + msgid_plural</c> as the format prescribes.</param>
    /// <param name="bigEndian">Whether to emit a big-endian catalog (magic <c>0xde120495</c>).</param>
    /// <param name="revision">The raw revision word (major in the high 16 bits).</param>
    /// <returns>The MO file bytes.</returns>
    internal static byte[] Write(IReadOnlyList<(string Original, string Translation)> entries, bool bigEndian = false, uint revision = 0) {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var n = entries.Count;
        var count = (uint)n;
        const int originalsTable = HEADER_SIZE;
        var translationsTable = originalsTable + (n * 8);
        var dataStart = translationsTable + (n * 8);

        var originalBytes = new byte[n][];
        var translationBytes = new byte[n][];
        var originalOffsets = new int[n];
        var translationOffsets = new int[n];
        var offset = dataStart;
        for(var i = 0; i < n; i++) {
            originalBytes[i] = encoding.GetBytes(entries[i].Original);
            originalOffsets[i] = offset;
            offset += originalBytes[i].Length + 1;
        }

        for(var i = 0; i < n; i++) {
            translationBytes[i] = encoding.GetBytes(entries[i].Translation);
            translationOffsets[i] = offset;
            offset += translationBytes[i].Length + 1;
        }

        var bytes = new byte[offset];
        PatchUInt32(bytes, 0, 0x950412deu, bigEndian);
        PatchUInt32(bytes, 4, revision, bigEndian);
        PatchUInt32(bytes, 8, count, bigEndian);
        PatchUInt32(bytes, 12, (uint)originalsTable, bigEndian);
        PatchUInt32(bytes, 16, (uint)translationsTable, bigEndian);

        for(var i = 0; i < n; i++) {
            PatchUInt32(bytes, originalsTable + (i * 8), (uint)originalBytes[i].Length, bigEndian);
            PatchUInt32(bytes, originalsTable + (i * 8) + 4, (uint)originalOffsets[i], bigEndian);
            PatchUInt32(bytes, translationsTable + (i * 8), (uint)translationBytes[i].Length, bigEndian);
            PatchUInt32(bytes, translationsTable + (i * 8) + 4, (uint)translationOffsets[i], bigEndian);
        }

        for(var i = 0; i < n; i++) {
            originalBytes[i].CopyTo(bytes, originalOffsets[i]);
            translationBytes[i].CopyTo(bytes, translationOffsets[i]);
        }

        return bytes;
    }

    /// <summary>
    /// Patches one little-/big-endian <c>uint32</c> at the given offset.
    /// </summary>
    /// <param name="bytes">The MO file bytes to patch in place.</param>
    /// <param name="offset">The byte offset.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="bigEndian">The endianness of the containing file.</param>
    internal static void PatchUInt32(byte[] bytes, int offset, uint value, bool bigEndian = false) {
        if(bigEndian) {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
        } else {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
