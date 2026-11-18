//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Parses the gettext subset of <c>.po</c> catalogs for key generation: every entry
/// contributes its <c>msgid</c> as the key constant, independent of culture. Entries
/// marked fuzzy (<c>#, fuzzy</c>) are skipped, matching the runtime provider, as are
/// obsolete (<c>#~</c>) and header (empty <c>msgid</c>) entries. Plural entries
/// contribute the singular <c>msgid</c> once; contextual variants (<c>msgctxt</c>)
/// share their <c>msgid</c> constant, so only the first occurrence is kept and no
/// duplicate is reported for contextual variants. Continuation lines concatenate.
/// </summary>
internal static class PoCatalogParser {
    private const string MsgCtxtKeyword = "msgctxt";
    private const string MsgIdKeyword = "msgid";
    private const string MsgIdPluralKeyword = "msgid_plural";
    private const string MsgStrKeyword = "msgstr";
    private const string MsgStrIndexedPrefix = "msgstr[";

    /// <summary>
    /// Parses PO text into catalog entries in document order.
    /// </summary>
    /// <param name="text">The PO catalog text.</param>
    /// <returns>The entries plus, when the catalog is malformed, a single syntax error.</returns>
    internal static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset) Parse(string text) {
        var entries = new List<ParsedCatalogEntry>();
        var seenPairs = new HashSet<string>(StringComparer.Ordinal);
        var emittedMsgids = new HashSet<string>(StringComparer.Ordinal);
        var starts = CatalogLineMap.LineStarts(text);
        var lines = text.Split('\n');
        var state = new EntryState();

        for(var index = 0; index < lines.Length; index++) {
            var error = ProcessLine(lines, index, state, entries, seenPairs, emittedMsgids, starts, text);
            if(error is not null) {
                return error.Value;
            }
        }

        CompleteEntry(state, entries, seenPairs, emittedMsgids, starts, text);
        return (entries, null, 0);
    }

    /// <summary>
    /// Processes one source line, completing entries and opening fields.
    /// </summary>
    /// <param name="lines">All source lines.</param>
    /// <param name="index">The 0-based line index.</param>
    /// <param name="state">The entry state.</param>
    /// <param name="entries">The entries sink.</param>
    /// <param name="seenPairs">The seen msgctxt/msgid pairs.</param>
    /// <param name="emittedMsgids">The emitted msgids.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
    private static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessLine(string[] lines, int index, EntryState state, List<ParsedCatalogEntry> entries, HashSet<string> seenPairs, HashSet<string> emittedMsgids, int[] starts, string text) {
        var lineNumber = index + 1;
        var trimmed = lines[index].TrimEnd('\r').Trim();
        if(trimmed.Length == 0) {
            CompleteEntry(state, entries, seenPairs, emittedMsgids, starts, text);
            return null;
        }

        if(trimmed[0] == '#') {
            ProcessCommentLine(trimmed, state);
            return null;
        }

        if(!TrySplitKeyword(trimmed, out var keyword, out var rest)) {
            return (entries, $"PO line {lineNumber} is not a keyword, a continuation string, or a comment.", CatalogLineMap.OffsetOf(starts, text.Length, lineNumber, 1));
        }

        if(keyword is null) {
            return ProcessContinuation(state, rest, lineNumber, starts, text, entries);
        }

        return ProcessKeyword(state, keyword, rest, lineNumber, index, lines, entries, seenPairs, emittedMsgids, starts, text);
    }

    /// <summary>
    /// Processes one continuation line against the open field.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <param name="rest">The remainder after the keyword split.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    /// <param name="entries">The entries sink.</param>
    /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
    private static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessContinuation(EntryState state, string rest, int lineNumber, int[] starts, string text, List<ParsedCatalogEntry> entries) {
        var continuation = AppendContinuation(state, rest, lineNumber, starts, text);
        if(continuation is not null) {
            return (entries, continuation.Value.Error, continuation.Value.Offset);
        }

        return null;
    }

    /// <summary>
    /// Processes one keyword line, completing the pending entry when required.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <param name="keyword">The keyword.</param>
    /// <param name="rest">The remainder after the keyword.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <param name="index">The 0-based line index.</param>
    /// <param name="lines">All source lines.</param>
    /// <param name="entries">The entries sink.</param>
    /// <param name="seenPairs">The seen msgctxt/msgid pairs.</param>
    /// <param name="emittedMsgids">The emitted msgids.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
    private static (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessKeyword(EntryState state, string keyword, string rest, int lineNumber, int index, string[] lines, List<ParsedCatalogEntry> entries, HashSet<string> seenPairs, HashSet<string> emittedMsgids, int[] starts, string text) {
        var transition = CheckKeywordTransition(keyword, state, lineNumber, out var completePrevious);
        if(transition is not null) {
            return (entries, transition, CatalogLineMap.OffsetOf(starts, text.Length, lineNumber, 1));
        }

        if(completePrevious) {
            CompleteEntry(state, entries, seenPairs, emittedMsgids, starts, text);
        }

        if(!TryDecodeValue(rest, lineNumber, starts, text, out var fieldValue, out var fieldError, out var fieldErrorOffset)) {
            return (entries, fieldError, fieldErrorOffset);
        }

        var valueOffset = ValueOffsetOf(lines[index], starts, lineNumber, text);
        state.Open(keyword, fieldValue, valueOffset, lineNumber);
        return null;
    }

    /// <summary>
    /// Processes one comment line: obsolete markers and fuzzy flags.
    /// </summary>
    /// <param name="trimmed">The trimmed source line starting with <c>#</c>.</param>
    /// <param name="state">The entry state.</param>
    private static void ProcessCommentLine(string trimmed, EntryState state) {
        if(trimmed.StartsWith("#~", StringComparison.Ordinal)) {
            state.Obsolete = true;
        } else if(trimmed.StartsWith("#,", StringComparison.Ordinal) && trimmed.Contains("fuzzy", StringComparison.Ordinal)) {
            state.Fuzzy = true;
        }
    }

    /// <summary>
    /// Appends one continuation string to the open field.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <param name="rest">The remainder of the line after the keyword split.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The error and offset, or <see langword="null"/> when the continuation applied.</returns>
    private static (string? Error, int Offset)? AppendContinuation(EntryState state, string rest, int lineNumber, int[] starts, string text) {
        if(state.Field is null) {
            return ($"PO line {lineNumber} continues a string without an open field.", CatalogLineMap.OffsetOf(starts, text.Length, lineNumber, 1));
        }

        if(!TryDecodeValue(rest, lineNumber, starts, text, out var continuation, out var error, out var errorOffset)) {
            return (error, errorOffset);
        }

        state.Append(continuation);
        return null;
    }

    /// <summary>
    /// Validates a keyword transition against the pending entry state.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="state">The entry state.</param>
    /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
    /// <param name="completePrevious">Whether the pending completed entry must be completed first.</param>
    /// <returns>The error message, or <see langword="null"/> when the transition is legal.</returns>
    private static string? CheckKeywordTransition(string keyword, EntryState state, int lineNumber, out bool completePrevious) {
        completePrevious = false;
        if(keyword is MsgCtxtKeyword or MsgIdKeyword) {
            if(state.HasMsgId && state.Closed) {
                completePrevious = true;
                return null;
            }

            return state.HasMsgId
                ? $"PO entry repeats '{keyword}' without a msgstr."
                : null;
        }

        if(keyword == MsgIdPluralKeyword && (!state.HasMsgId || state.HasPlural)) {
            return $"PO line {lineNumber} has '{MsgIdPluralKeyword}' without a singular '{MsgIdKeyword}' or repeats it.";
        }

        if((keyword == MsgStrKeyword || keyword.StartsWith(MsgStrIndexedPrefix, StringComparison.Ordinal)) && !state.HasMsgId) {
            return $"PO line {lineNumber} has '{MsgStrKeyword}' without a '{MsgIdKeyword}'.";
        }

        if(keyword == MsgStrKeyword && state.HasSingularStr) {
            return $"PO entry repeats '{MsgStrKeyword}' without a new '{MsgIdKeyword}'.";
        }

        return null;
    }

    /// <summary>
    /// Completes the pending entry and resets the state. Contextual variants share one
    /// msgid constant (first occurrence wins); an exact duplicate (same context and
    /// msgid) is emitted again so the generator reports the duplicate key.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <param name="entries">The entries collected so far.</param>
    /// <param name="seenPairs">The (msgid, context) pairs already completed.</param>
    /// <param name="emittedMsgids">The msgids already emitted.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    private static void CompleteEntry(EntryState state, List<ParsedCatalogEntry> entries, HashSet<string> seenPairs, HashSet<string> emittedMsgids, int[] starts, string text) {
        if(state.HasMsgId) {
            var skip = state.Obsolete || state.Fuzzy || (state.MsgId.Length == 0 && !state.HasPlural);
            if(!skip) {
                var pair = state.MsgId + "\0" + (state.MsgCtxt ?? string.Empty);
                var exactDuplicate = !seenPairs.Add(pair);
                if(exactDuplicate || emittedMsgids.Add(state.MsgId)) {
                    EmitEntry(state, entries, starts, text);
                }
            }
        }

        state.Reset();
    }

    /// <summary>
    /// Emits one entry for the pending state.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <param name="entries">The entries collected so far.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    private static void EmitEntry(EntryState state, List<ParsedCatalogEntry> entries, int[] starts, string text) {
        var rawLine = CatalogLineMap.LineText(text, starts, state.KeyLine);
        CatalogLineMap.QuotedKeyPosition(rawLine, CatalogLineMap.OffsetOf(starts, text.Length, state.KeyLine, 1), state.MsgId, out var keyOffset, out var keyLength);
        entries.Add(new ParsedCatalogEntry(state.MsgId, keyOffset, keyLength, state.MsgStr, true, state.ValueOffset));
    }

    /// <summary>
    /// Splits one content line into its keyword and the remainder.
    /// A lone quoted string is a continuation line (<see langword="null"/> keyword).
    /// </summary>
    /// <param name="trimmed">The trimmed source line.</param>
    /// <param name="keyword">The keyword, <see langword="null"/> for a continuation line, or <see langword="null"/> with <see langword="false"/> when unknown.</param>
    /// <param name="rest">The remainder after the keyword.</param>
    /// <returns>false when the line starts an unknown keyword.</returns>
    private static bool TrySplitKeyword(string trimmed, out string? keyword, out string rest) {
        keyword = null;
        rest = trimmed;
        if(trimmed.StartsWith("\"", StringComparison.Ordinal)) {
            return true;
        }

        var space = trimmed.IndexOfAny([' ', '\t']);
        if(space < 0) {
            return false;
        }

        keyword = trimmed.Substring(0, space);
        if(keyword is not (MsgCtxtKeyword or MsgIdKeyword or MsgIdPluralKeyword or MsgStrKeyword) && !keyword.StartsWith(MsgStrIndexedPrefix, StringComparison.Ordinal)) {
            keyword = null;
            return false;
        }

        rest = trimmed.Substring(space).TrimStart();
        return true;
    }

    /// <summary>
    /// Decodes one quoted PO string, resolving C-style escapes.
    /// </summary>
    /// <param name="rest">The trimmed remainder starting at the opening quote.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="text">The file content.</param>
    /// <param name="value">The decoded value.</param>
    /// <param name="error">The syntax error message, when malformed.</param>
    /// <param name="errorOffset">The raw offset of the syntax error.</param>
    /// <returns>true when the string parsed; otherwise, false.</returns>
    private static bool TryDecodeValue(string rest, int lineNumber, int[] starts, string text, out string value, out string? error, out int errorOffset) {
        value = string.Empty;
        error = null;
        errorOffset = CatalogLineMap.OffsetOf(starts, text.Length, lineNumber, 1);
        if(!rest.StartsWith("\"", StringComparison.Ordinal)) {
            error = $"PO line {lineNumber} has a keyword without a quoted string.";
            return false;
        }

        var builder = new StringBuilder(rest.Length);
        var index = 1;
        while(index < rest.Length && rest[index] != '"') {
            if(rest[index] == '\\' && index + 1 < rest.Length) {
                builder.Append(rest[index + 1] switch {
                    'n' => "\n",
                    't' => "\t",
                    'r' => "\r",
                    '"' => "\"",
                    '\\' => "\\",
                    var escape => "\\" + escape,
                });
                index += 2;
                continue;
            }

            builder.Append(rest[index]);
            index++;
        }

        if(index >= rest.Length) {
            error = $"PO line {lineNumber} has an unterminated string.";
            return false;
        }

        if(rest.Substring(index + 1).Trim().Length != 0) {
            error = $"PO line {lineNumber} has trailing content after the string.";
            return false;
        }

        value = builder.ToString();
        return true;
    }

    /// <summary>
    /// Locates the opening quote of a field value within its raw source line.
    /// </summary>
    /// <param name="line">The raw source line.</param>
    /// <param name="starts">The line-start offsets.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The raw offset of the opening quote, or the line start.</returns>
    private static int ValueOffsetOf(string line, int[] starts, int lineNumber, string text) {
        var lineStart = CatalogLineMap.OffsetOf(starts, text.Length, lineNumber, 1);
        var quote = line.IndexOf('"');
        return quote < 0 ? lineStart : lineStart + quote;
    }

    /// <summary>
    /// One entry under construction.
    /// </summary>
    private sealed class EntryState {
        /// <summary>
        /// Gets the open field keyword, or <see langword="null"/> when no field is open.
        /// </summary>
        public string? Field { get; private set; }

        /// <summary>
        /// Gets whether the entry is marked fuzzy.
        /// </summary>
        public bool Fuzzy { get; set; }

        /// <summary>
        /// Gets whether the entry is obsolete.
        /// </summary>
        public bool Obsolete { get; set; }

        /// <summary>
        /// Gets whether a msgid was seen.
        /// </summary>
        public bool HasMsgId => _msgId is not null;

        /// <summary>
        /// Gets whether the entry is closed (it already produced a msgstr or plural form).
        /// </summary>
        public bool Closed => HasSingularStr || HasPlural;

        /// <summary>
        /// Gets whether a plural form was seen.
        /// </summary>
        public bool HasPlural { get; private set; }

        /// <summary>
        /// Gets whether a singular msgstr was seen.
        /// </summary>
        public bool HasSingularStr { get; private set; }

        /// <summary>
        /// Gets the message context (<c>msgctxt</c>), or <see langword="null"/> when absent.
        /// </summary>
        public string? MsgCtxt { get; private set; }

        /// <summary>
        /// Gets the msgid.
        /// </summary>
        public string MsgId => _msgId ?? string.Empty;

        /// <summary>
        /// Gets the 1-based msgid line.
        /// </summary>
        public int KeyLine { get; private set; }

        /// <summary>
        /// Gets the singular msgstr.
        /// </summary>
        public string MsgStr { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the raw offset of the singular msgstr value.
        /// </summary>
        public int ValueOffset { get; private set; }

        private string? _msgId;

        /// <summary>
        /// Opens a keyword field, committing any open field first.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="value">The decoded value.</param>
        /// <param name="valueOffset">The raw offset of the value.</param>
        /// <param name="lineNumber">The 1-based line number.</param>
        public void Open(string keyword, string value, int valueOffset, int lineNumber) {
            if(keyword == MsgCtxtKeyword) {
                MsgCtxt = value;
            } else if(keyword == MsgIdKeyword) {
                _msgId = value;
                KeyLine = lineNumber;
            } else if(keyword == MsgStrKeyword) {
                MsgStr = value;
                ValueOffset = valueOffset;
                HasSingularStr = true;
            } else if(keyword == MsgIdPluralKeyword || keyword.StartsWith(MsgStrIndexedPrefix, StringComparison.Ordinal)) {
                HasPlural = true;
            }

            Field = keyword;
        }

        /// <summary>
        /// Appends a continuation value to the open msgid, msgctxt, or singular msgstr field.
        /// </summary>
        /// <param name="value">The decoded continuation.</param>
        public void Append(string value) {
            if(Field == MsgIdKeyword && _msgId is not null) {
                _msgId += value;
            } else if(Field == MsgCtxtKeyword && MsgCtxt is not null) {
                MsgCtxt += value;
            } else if(Field == MsgStrKeyword) {
                MsgStr += value;
            }
        }

        /// <summary>
        /// Resets the state.
        /// </summary>
        public void Reset() {
            Field = null;
            Fuzzy = false;
            Obsolete = false;
            _msgId = null;
            MsgCtxt = null;
            MsgStr = string.Empty;
            HasSingularStr = false;
            HasPlural = false;
            KeyLine = 0;
            ValueOffset = 0;
        }
    }
}
