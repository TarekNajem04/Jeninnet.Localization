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
    private const string MSG_CTXT_KEYWORD = "msgctxt";
    private const string MSG_ID_KEYWORD = "msgid";
    private const string MSG_ID_PLURAL_KEYWORD = "msgid_plural";
    private const string MSG_STR_KEYWORD = "msgstr";
    private const string MSG_STR_INDEXED_PREFIX = "msgstr[";

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
        var session = new ParseSession(entries, seenPairs, emittedMsgids, starts, text, lines, state);

        for(var index = 0; index < lines.Length; index++) {
            var error = session.ProcessLine(index);
            if(error is not null) {
                return error.Value;
            }
        }

        CompleteEntry(state, entries, seenPairs, emittedMsgids, starts, text);
        return (entries, null, 0);
    }

    /// <summary>
    /// Tracks one in-progress PO parse across lines, threading the shared session state.
    /// All per-line helpers live here so the outer parser stays thin.
    /// </summary>
    private sealed class ParseSession {
        private readonly List<ParsedCatalogEntry> _entries;
        private readonly HashSet<string> _seenPairs;
        private readonly HashSet<string> _emittedMsgids;
        private readonly int[] _starts;
        private readonly string _text;
        private readonly string[] _lines;
        private readonly EntryState _state;

        internal ParseSession(List<ParsedCatalogEntry> entries, HashSet<string> seenPairs, HashSet<string> emittedMsgids, int[] starts, string text, string[] lines, EntryState state) {
            _entries = entries;
            _seenPairs = seenPairs;
            _emittedMsgids = emittedMsgids;
            _starts = starts;
            _text = text;
            _lines = lines;
            _state = state;
        }

        /// <summary>
        /// Processes one source line, completing entries and opening fields.
        /// </summary>
        /// <param name="index">The 0-based line index.</param>
        /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
        internal (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessLine(int index) {
            var lineNumber = index + 1;
            var trimmed = _lines[index].TrimEnd('\r').Trim();
            if(trimmed.Length == 0) {
                CompleteEntry(_state, _entries, _seenPairs, _emittedMsgids, _starts, _text);
                return null;
            }

            if(trimmed[0] == '#') {
                ProcessCommentLine(trimmed);
                return null;
            }

            if(!TrySplitKeyword(trimmed, out var keyword, out var rest)) {
                return (_entries, $"PO line {lineNumber} is not a keyword, a continuation string, or a comment.", CatalogLineMap.OffsetOf(_starts, _text.Length, lineNumber, 1));
            }

            if(keyword is null) {
                return ProcessContinuation(rest, lineNumber);
            }

            return ProcessKeyword(keyword, rest, lineNumber, index);
        }

        /// <summary>
        /// Processes one continuation line against the open field.
        /// </summary>
        /// <param name="rest">The remainder after the keyword split.</param>
        /// <param name="lineNumber">The 1-based line number.</param>
        /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
        private (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessContinuation(string rest, int lineNumber) {
            var continuation = AppendContinuation(rest, lineNumber);
            if(continuation is not null) {
                return (_entries, continuation.Value.Error, continuation.Value.Offset);
            }

            return null;
        }

        /// <summary>
        /// Processes one keyword line, completing the pending entry when required.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="rest">The remainder after the keyword.</param>
        /// <param name="lineNumber">The 1-based line number.</param>
        /// <param name="index">The 0-based line index.</param>
        /// <returns>The parse error, or <see langword="null"/> to continue.</returns>
        private (List<ParsedCatalogEntry> Entries, string? SyntaxError, int SyntaxOffset)? ProcessKeyword(string keyword, string rest, int lineNumber, int index) {
            var transition = CheckKeywordTransition(keyword, lineNumber, out var completePrevious);
            if(transition is not null) {
                return (_entries, transition, CatalogLineMap.OffsetOf(_starts, _text.Length, lineNumber, 1));
            }

            if(completePrevious) {
                CompleteEntry(_state, _entries, _seenPairs, _emittedMsgids, _starts, _text);
            }

            if(!TryDecodeValue(rest, lineNumber, out var fieldValue, out var fieldError, out var fieldErrorOffset)) {
                return (_entries, fieldError, fieldErrorOffset);
            }

            var valueOffset = ValueOffsetOf(_lines[index], lineNumber);
            _state.Open(keyword, fieldValue, valueOffset, lineNumber);
            return null;
        }

        /// <summary>
        /// Processes one comment line: obsolete markers and fuzzy flags.
        /// </summary>
        /// <param name="trimmed">The trimmed source line starting with <c>#</c>.</param>
        private void ProcessCommentLine(string trimmed) {
            if(trimmed.StartsWith("#~", StringComparison.Ordinal)) {
                _state.Obsolete = true;
            } else if(trimmed.StartsWith("#,", StringComparison.Ordinal) && trimmed.Contains("fuzzy", StringComparison.Ordinal)) {
                _state.Fuzzy = true;
            }
        }

        /// <summary>
        /// Appends one continuation string to the open field.
        /// </summary>
        /// <param name="rest">The remainder of the line after the keyword split.</param>
        /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
        /// <returns>The error and offset, or <see langword="null"/> when the continuation applied.</returns>
        private (string? Error, int Offset)? AppendContinuation(string rest, int lineNumber) {
            if(_state.Field is null) {
                return ($"PO line {lineNumber} continues a string without an open field.", CatalogLineMap.OffsetOf(_starts, _text.Length, lineNumber, 1));
            }

            if(!TryDecodeValue(rest, lineNumber, out var continuation, out var error, out var errorOffset)) {
                return (error, errorOffset);
            }

            _state.Append(continuation);
            return null;
        }

        /// <summary>
        /// Validates a keyword transition against the pending entry state.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="lineNumber">The 1-based line number used in diagnostics.</param>
        /// <param name="completePrevious">Whether the pending completed entry must be completed first.</param>
        /// <returns>The error message, or <see langword="null"/> when the transition is legal.</returns>
        private string? CheckKeywordTransition(string keyword, int lineNumber, out bool completePrevious) {
            completePrevious = false;
            if(keyword is MSG_CTXT_KEYWORD or MSG_ID_KEYWORD) {
                if(_state.HasMsgId && _state.Closed) {
                    completePrevious = true;
                    return null;
                }

                return _state.HasMsgId
                    ? $"PO entry repeats '{keyword}' without a msgstr."
                    : null;
            }

            if(keyword == MSG_ID_PLURAL_KEYWORD && (!_state.HasMsgId || _state.HasPlural)) {
                return $"PO line {lineNumber} has '{MSG_ID_PLURAL_KEYWORD}' without a singular '{MSG_ID_KEYWORD}' or repeats it.";
            }

            if((keyword == MSG_STR_KEYWORD || keyword.StartsWith(MSG_STR_INDEXED_PREFIX, StringComparison.Ordinal)) && !_state.HasMsgId) {
                return $"PO line {lineNumber} has '{MSG_STR_KEYWORD}' without a '{MSG_ID_KEYWORD}'.";
            }

            if(keyword == MSG_STR_KEYWORD && _state.HasSingularStr) {
                return $"PO entry repeats '{MSG_STR_KEYWORD}' without a new '{MSG_ID_KEYWORD}'.";
            }

            return null;
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
            if(keyword is not (MSG_CTXT_KEYWORD or MSG_ID_KEYWORD or MSG_ID_PLURAL_KEYWORD or MSG_STR_KEYWORD) && !keyword.StartsWith(MSG_STR_INDEXED_PREFIX, StringComparison.Ordinal)) {
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
        /// <param name="value">The decoded value.</param>
        /// <param name="error">The syntax error message, when malformed.</param>
        /// <param name="errorOffset">The raw offset of the syntax error.</param>
        /// <returns>true when the string parsed; otherwise, false.</returns>
        private bool TryDecodeValue(string rest, int lineNumber, out string value, out string? error, out int errorOffset) {
            value = string.Empty;
            error = null;
            errorOffset = CatalogLineMap.OffsetOf(_starts, _text.Length, lineNumber, 1);
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
        /// <param name="lineNumber">The 1-based line number.</param>
        /// <returns>The raw offset of the opening quote, or the line start.</returns>
        private int ValueOffsetOf(string line, int lineNumber) {
            var lineStart = CatalogLineMap.OffsetOf(_starts, _text.Length, lineNumber, 1);
            var quote = line.IndexOf('"');
            return quote < 0 ? lineStart : lineStart + quote;
        }
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
            if(keyword == MSG_CTXT_KEYWORD) {
                MsgCtxt = value;
            } else if(keyword == MSG_ID_KEYWORD) {
                _msgId = value;
                KeyLine = lineNumber;
            } else if(keyword == MSG_STR_KEYWORD) {
                MsgStr = value;
                ValueOffset = valueOffset;
                HasSingularStr = true;
            } else if(keyword == MSG_ID_PLURAL_KEYWORD || keyword.StartsWith(MSG_STR_INDEXED_PREFIX, StringComparison.Ordinal)) {
                HasPlural = true;
            }

            Field = keyword;
        }

        /// <summary>
        /// Appends a continuation value to the open msgid, msgctxt, or singular msgstr field.
        /// </summary>
        /// <param name="value">The decoded continuation.</param>
        public void Append(string value) {
            if(Field == MSG_ID_KEYWORD && _msgId is not null) {
                _msgId += value;
            } else if(Field == MSG_CTXT_KEYWORD && MsgCtxt is not null) {
                MsgCtxt += value;
            } else if(Field == MSG_STR_KEYWORD) {
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
