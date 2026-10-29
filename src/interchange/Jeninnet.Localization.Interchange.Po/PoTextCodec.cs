//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Po;

/// <summary>
/// The gettext PO (Portable Object) text writer/reader for the  subset. Writes the standard header
/// (<c>Content-Type</c>, <c>Language</c>, optional verbatim <c>Plural-Forms</c>) plus
/// flat, contextual, and plural entries with <c>#.</c> translator notes, single-line
/// strings with C-style escapes, and deterministic ordinal ordering; reads with BOM (Byte Order Mark)
/// tolerance and file/line diagnostics for every structural violation. The text grammar
/// itself is the shared <c>PoTextParsing</c> source; this codec owns the
/// header interpretation, the duplicate-key validation with context encoding, the
/// nplurals validation, and the retained fuzzy flags.
/// </summary>
internal static class PoTextCodec {
    /// <summary>
    /// The gettext-canonical separator joining context and key.
    /// </summary>
    internal const char CONTEXT_SEPARATOR = '\u0004';

    /// <summary>
    /// Writes entries as standard PO text: the header block, then one blank-line
    /// separated entry per row in the given (ordinal) order, no trailing newline.
    /// </summary>
    /// <param name="entries">The entries in deterministic order.</param>
    /// <param name="culture">The file's culture (the <c>Language</c> header value).</param>
    /// <param name="pluralForms">The verbatim <c>Plural-Forms</c> header value, or <see langword="null"/> to omit the header.</param>
    /// <returns>The PO document text (no BOM; the file writer omits it — gettext-canonical).</returns>
    internal static string Write(IReadOnlyList<PoEntry> entries, string culture, string? pluralForms) {
        var lines = new List<string> {
            "msgid \"\"",
            "msgstr \"\"",
            "\"Content-Type: text/plain; charset=UTF-8\\n\"",
            $"\"Language: {Escape(culture)}\\n\"",
        };

        if(pluralForms is not null) {
            lines.Add($"\"Plural-Forms: {Escape(pluralForms)}\\n\"");
        }

        foreach(var entry in entries) {
            AppendEntry(lines, entry);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Appends one entry to the document lines: the blank-line separator, the
    /// <c>#.</c> translator notes, the <c>#, fuzzy</c> flag, the optional context, and
    /// the <c>msgid</c>/<c>msgstr</c> block (singular or plural forms).
    /// </summary>
    /// <param name="lines">The PO document lines accumulated so far.</param>
    /// <param name="entry">The entry to append.</param>
    private static void AppendEntry(List<string> lines, PoEntry entry) {
        lines.Add(string.Empty);
        if(entry.TranslatorNote is not null) {
            foreach(var noteLine in entry.TranslatorNote.Split('\n')) {
                lines.Add($"#. {noteLine}");
            }
        }

        if(entry.Fuzzy) {
            lines.Add("#, fuzzy");
        }

        if(entry.Context is not null) {
            lines.Add($"msgctxt \"{Escape(entry.Context)}\"");
        }

        lines.Add($"msgid \"{Escape(entry.Key)}\"");
        if(entry.PluralKey is not null) {
            lines.Add($"msgid_plural \"{Escape(entry.PluralKey)}\"");
            for(var index = 0; index < entry.Forms!.Count; index++) {
                lines.Add($"msgstr[{index.ToString(CultureInfo.InvariantCulture)}] \"{Escape(entry.Forms[index])}\"");
            }
        } else {
            lines.Add($"msgstr \"{Escape(entry.Value ?? string.Empty)}\"");
        }
    }

    /// <summary>
    /// Parses PO text against the  subset: an optional header entry
    /// (<c>msgid ""</c>), flat/contextual/plural entries with unique keys, contiguous
    /// <c>msgstr[n]</c> forms, C-style escapes, <c>#.</c> notes, and <c>#, fuzzy</c>
    /// flags (retained); obsolete (<c>#~</c>) and other comment kinds are ignored.
    /// </summary>
    /// <param name="text">The PO document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed headers and entries (in file order).</returns>
    /// <exception cref="PoInterchangeException">Thrown when the text violates the subset.</exception>
    internal static (IReadOnlyDictionary<string, string> Headers, List<PoEntry> Entries) Parse(string text, string filePath) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = new List<PoEntry>();
        var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
        string? pluralFormsValue = null;
        var pluralFormsLine = 0;
        var headerSeen = false;

        foreach(var raw in PoTextParsing.Parse(text, filePath, static (path, line, message) => new PoInterchangeException(path, line, message))) {
            if(raw.MsgId.Length == 0 && raw.Context is null && raw.MsgIdPlural is null) {
                ReadHeader(raw, filePath, headers, ref headerSeen, ref pluralFormsValue, ref pluralFormsLine);
                continue;
            }

            AddParsedEntry(raw, filePath, claimedKeys, entries);
        }

        if(pluralFormsValue is not null) {
            var nplurals = ParseNPlurals(pluralFormsValue, filePath, pluralFormsLine);
            var mismatch = entries.FirstOrDefault(entry =>
                entry.PluralKey is not null && entry.Forms is not null && entry.Forms.Count != nplurals);
            if(mismatch is not null) {
                throw new PoInterchangeException(
                    filePath,
                    0,
                    $"Plural entry '{mismatch.Key}' has {mismatch.Forms!.Count.ToString(CultureInfo.InvariantCulture)} forms but Plural-Forms declares nplurals={nplurals.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        return (headers, entries);
    }

    /// <summary>
    /// Reads one header entry into the header dictionary, rejecting a second header and
    /// retaining the <c>Plural-Forms</c> value and line for later nplurals validation.
    /// </summary>
    /// <param name="raw">The raw header entry.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="headers">The header dictionary being filled.</param>
    /// <param name="headerSeen">Whether a header entry was already read.</param>
    /// <param name="pluralFormsValue">The <c>Plural-Forms</c> value, when present.</param>
    /// <param name="pluralFormsLine">The line of the header entry that declared <c>Plural-Forms</c>.</param>
    /// <exception cref="PoInterchangeException">Thrown when a second header entry appears.</exception>
    private static void ReadHeader(
        PoRawEntry raw,
        string filePath,
        Dictionary<string, string> headers,
        ref bool headerSeen,
        ref string? pluralFormsValue,
        ref int pluralFormsLine) {
        if(headerSeen) {
            throw new PoInterchangeException(filePath, raw.StartLine, "Duplicate header entry (msgid \"\").");
        }

        headerSeen = true;
        pluralFormsLine = raw.StartLine;
        foreach(var pair in PoTextParsing.SplitHeaders(raw.Forms[0])) {
            headers[pair.Key] = pair.Value;
            if(pair.Key.Equals("Plural-Forms", StringComparison.OrdinalIgnoreCase)) {
                pluralFormsValue = pair.Value;
            }
        }
    }

    /// <summary>
    /// Converts one raw entry into a <see cref="PoEntry"/>, claims its keys, and appends
    /// it to the parsed entries.
    /// </summary>
    /// <param name="raw">The raw entry to convert.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="claimedKeys">The keys already claimed by earlier entries.</param>
    /// <param name="entries">The parsed entries being filled.</param>
    /// <exception cref="PoInterchangeException">Thrown when the entry's key duplicates an earlier entry's key.</exception>
    private static void AddParsedEntry(PoRawEntry raw, string filePath, HashSet<string> claimedKeys, List<PoEntry> entries) {
        var entry = new PoEntry {
            Key = raw.MsgId,
            Context = raw.Context,
            Value = raw.MsgIdPlural is null ? raw.Forms[0] : null,
            PluralKey = raw.MsgIdPlural,
            Forms = raw.MsgIdPlural is null ? null : raw.Forms,
            TranslatorNote = raw.Note,
            Fuzzy = raw.Fuzzy,
        };

        AddEntry(entry, filePath, claimedKeys, raw.StartLine);
        entries.Add(entry);
    }

    /// <summary>
    /// Adds entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="claimedKeys">The claimed keys.</param>
    /// <param name="lineNumber">The line number.</param>
    private static void AddEntry(PoEntry entry, string filePath, HashSet<string> claimedKeys, int lineNumber) {
        var context = entry.Context is { Length: 0 } ? null : entry.Context;

        if(entry.PluralKey is null) {
            var key = context is null ? entry.Key : context + CONTEXT_SEPARATOR + entry.Key;
            if(!claimedKeys.Add(key)) {
                throw new PoInterchangeException(filePath, lineNumber, $"Duplicate message key '{entry.Key}'.");
            }

            return;
        }

        var singular = context is null ? entry.Key : context + CONTEXT_SEPARATOR + entry.Key;
        var plural = context is null ? entry.PluralKey : context + CONTEXT_SEPARATOR + entry.PluralKey;
        if(!claimedKeys.Add(singular)) {
            throw new PoInterchangeException(filePath, lineNumber, $"Duplicate message key '{entry.Key}'.");
        }

        if(!claimedKeys.Add(plural)) {
            throw new PoInterchangeException(filePath, lineNumber, $"Duplicate message key '{entry.PluralKey}'.");
        }
    }

    /// <summary>
    /// Parses NPlurals.
    /// </summary>
    /// <param name="headerValue">The header value.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <returns>The NPlurals value.</returns>
    private static int ParseNPlurals(string headerValue, string filePath, int lineNumber) {
        const string search = "nplurals=";
        var start = headerValue.IndexOf(search, StringComparison.Ordinal);
        if(start < 0) {
            throw new PoInterchangeException(filePath, lineNumber, $"Invalid Plural-Forms header: missing 'nplurals=' in '{headerValue}'.");
        }

        start += search.Length;
        var end = headerValue.IndexOf(';', start);
        var raw = (end < 0 ? headerValue[start..] : headerValue[start..end]).Trim();
        if(!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var nplurals) || nplurals < 1 || nplurals > 6) {
            throw new PoInterchangeException(filePath, lineNumber, $"Invalid Plural-Forms nplurals '{raw}': expected an integer 1-6.");
        }

        var pluralStart = headerValue.IndexOf("plural=", StringComparison.Ordinal);
        if(pluralStart < 0 || headerValue[(pluralStart + "plural=".Length)..].Trim().Length == 0) {
            throw new PoInterchangeException(filePath, lineNumber, $"Invalid Plural-Forms header: missing plural expression in '{headerValue}'.");
        }

        return nplurals;
    }

    /// <summary>
    /// Performs the escape operation.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape text.</returns>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
}
