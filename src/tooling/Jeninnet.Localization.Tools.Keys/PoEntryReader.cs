//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// One served PO (Portable Object) pair mirroring the provider's flattened table.
/// </summary>
internal sealed class PoServedPair {
    /// <summary>
    /// Gets the served key (EOT-encoded for contexts).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the served template.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based msgid line.
    /// </summary>
    public int Line { get; set; } = 1;
}

/// <summary>
/// Focused PO reader mirroring the provider's served table: fuzzy,
/// header, and empty entries are skipped; singular entries map
/// <c>msgid → msgstr</c>; plural entries map the runtime pair
/// (<c>msgid → msgstr[0]</c>, <c>msgid_plural → msgstr[1]</c>, falling back to
/// <c>msgstr[0]</c> for single-form tables); contexts use the EOT (End of Transmission) encoding.
/// Structural problems become <see cref="CatalogParseException"/> with line numbers.
/// </summary>
internal static class PoEntryReader {
    /// <summary>The <c>msgctxt</c> keyword.</summary>
    private const string MSGCTXT = "msgctxt";

    /// <summary>The <c>msgid</c> keyword.</summary>
    private const string MSGID = "msgid";

    /// <summary>The <c>msgid_plural</c> keyword.</summary>
    private const string MSGID_PLURAL = "msgid_plural";

    /// <summary>The <c>msgstr</c> keyword.</summary>
    private const string MSGSTR = "msgstr";

    /// <summary>
    /// Reads the served pairs of one PO file.
    /// </summary>
    /// <param name="fullPath">The PO file path.</param>
    /// <returns>The served pairs in file order.</returns>
    /// <exception cref="CatalogParseException">Thrown when the file is malformed.</exception>
    public static List<PoServedPair> Read(string fullPath) {
        ArgumentNullException.ThrowIfNull(fullPath);

        return new Reader().ReadLines(File.ReadAllLines(fullPath));
    }

    /// <summary>
    /// The line-oriented reader state: the entry accumulator, the duplicate-key claims, and
    /// the served pairs in file order.
    /// </summary>
    private sealed class Reader {
        private readonly List<PoServedPair> _pairs = [];
        private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);
        private readonly EntryAccumulator _entry = new();

        /// <summary>
        /// Reads lines.
        /// </summary>
        /// <param name="lines">The lines.</param>
        /// <returns>The lines sequence.</returns>
        internal List<PoServedPair> ReadLines(string[] lines) {
            for(var index = 0; index < lines.Length; index++) {
                ProcessLine(lines[index].Trim(), index + 1);
            }

            FinalizeEntry();

            return _pairs;
        }

        /// <summary>
        /// Processes line.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        /// <param name="lineNumber">The line number.</param>
        private void ProcessLine(string trimmed, int lineNumber) {
            if(trimmed.Length == 0) {
                FinalizeEntry();
                _entry.ResetFuzzy();
                return;
            }

            if(trimmed[0] == '#') {
                if(IsFuzzyComment(trimmed)) {
                    _entry.MarkFuzzy();
                }

                return;
            }

            if(trimmed[0] == '"') {
                _entry.AppendContinuation(ParseQuoted(trimmed, 0, trimmed.Length, lineNumber), lineNumber);
                return;
            }

            var keyword = ReadKeyword(trimmed, lineNumber);
            if((keyword == MSGCTXT || keyword == MSGID) && _entry.HasMsgId) {
                FinalizeEntry();
            }

            _entry.SetKeyword(keyword, trimmed, lineNumber);
        }

        /// <summary>
        /// Performs the finalize entry operation.
        /// </summary>
        private void FinalizeEntry() {
            foreach(var pair in _entry.Drain()) {
                if(!_claimed.Add(pair.Key)) {
                    throw new CatalogParseException($"Duplicate message key '{pair.Key}'.", pair.Line);
                }

                _pairs.Add(pair);
            }
        }

        /// <summary>
        /// Determines whether the value is fuzzy comment.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        private static bool IsFuzzyComment(string trimmed) {
            if(!trimmed.StartsWith("#,", StringComparison.Ordinal)) {
                return false;
            }

            return trimmed[2..].Split(',').Any(static flag => flag.Trim().Equals("fuzzy", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads keyword.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <returns>The keyword text.</returns>
        private static string ReadKeyword(string trimmed, int lineNumber) {
            foreach(var keyword in new[] { MSGCTXT, MSGID_PLURAL, MSGID, MSGSTR }) {
                if(trimmed.StartsWith(keyword, StringComparison.Ordinal) &&
                   (trimmed.Length == keyword.Length ||
                    char.IsWhiteSpace(trimmed[keyword.Length]) ||
                    trimmed[keyword.Length] == '[' ||
                    trimmed[keyword.Length] == '"')) {
                    return keyword;
                }
            }

            throw new CatalogParseException($"Unrecognized line '{trimmed}'.", lineNumber);
        }
    }

    /// <summary>
    /// Parses quoted.
    /// </summary>
    /// <param name="trimmed">The trimmed.</param>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <returns>The quoted text.</returns>
    private static string ParseQuoted(string trimmed, int start, int end, int lineNumber) {
        if(end - start < 2 || trimmed[start] != '"' || trimmed[end - 1] != '"') {
            throw new CatalogParseException($"Malformed quoted string '{trimmed}': expected a double-quoted string.", lineNumber);
        }

        var builder = new System.Text.StringBuilder(end - start);
        var position = start + 1;
        while(position < end - 1) {
            var current = trimmed[position];
            position++;
            if(current != '\\') {
                builder.Append(current);
                continue;
            }

            if(position >= end - 1) {
                throw new CatalogParseException($"Malformed escape in '{trimmed}': trailing backslash.", lineNumber);
            }

            var escape = trimmed[position];
            position++;
            builder.Append(escape switch {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                '0' => '\0',
                _ => throw new CatalogParseException($"Malformed escape '\\{escape}' in '{trimmed}'.", lineNumber),
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Represents an entry accumulator.
    /// </summary>
    private sealed class EntryAccumulator {
        private string _context = string.Empty;
        private bool _hasContext;
        private string? _msgId;
        private int _msgIdLine;
        private string? _msgIdPlural;
        private string? _msgStr;
        private readonly SortedDictionary<int, string> _forms = [];
        private string? _activeKind;
        private int? _activeForm;
        private bool _fuzzy;
        private bool _pendingFuzzy;

        /// <summary>
        /// Gets a value indicating whether has msg id.
        /// </summary>
        public bool HasMsgId => _msgId is not null;

        /// <summary>
        /// Performs the mark fuzzy operation.
        /// </summary>
        public void MarkFuzzy() => _pendingFuzzy = true;

        /// <summary>
        /// Resets fuzzy.
        /// </summary>
        public void ResetFuzzy() => _pendingFuzzy = false;

        /// <summary>
        /// Appends continuation.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="lineNumber">The line number.</param>
        public void AppendContinuation(string text, int lineNumber) {
            if(_activeKind is null) {
                throw new CatalogParseException("Unexpected string continuation without a preceding keyword.", lineNumber);
            }

            AppendActive(text);
        }

        /// <summary>
        /// Sets keyword.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="trimmed">The trimmed.</param>
        /// <param name="lineNumber">The line number.</param>
        public void SetKeyword(string keyword, string trimmed, int lineNumber) {
            switch(keyword) {
                case MSGCTXT:
                    _hasContext = true;
                    _context = ParseKeywordValue(trimmed, keyword, lineNumber);
                    _activeKind = MSGCTXT;
                    _activeForm = null;
                    _fuzzy = _pendingFuzzy;
                    _pendingFuzzy = false;
                    break;
                case MSGID:
                    _msgId = ParseKeywordValue(trimmed, keyword, lineNumber);
                    _msgIdLine = lineNumber;
                    _activeKind = MSGID;
                    _activeForm = null;
                    if(!_hasContext) {
                        _fuzzy = _pendingFuzzy;
                        _pendingFuzzy = false;
                    }

                    break;
                case MSGID_PLURAL:
                    _msgIdPlural = ParseKeywordValue(trimmed, keyword, lineNumber);
                    _activeKind = MSGID_PLURAL;
                    _activeForm = null;
                    break;
                case MSGSTR:
                    SetMsgStr(trimmed, lineNumber);
                    break;
            }
        }

        /// <summary>
        /// Records one plain or indexed <c>msgstr</c> line.
        /// </summary>
        /// <param name="trimmed">The trimmed line.</param>
        /// <param name="lineNumber">The 1-based line number (diagnostics).</param>
        /// <exception cref="CatalogParseException">Thrown on duplicate or malformed <c>msgstr</c> lines.</exception>
        private void SetMsgStr(string trimmed, int lineNumber) {
            var bracket = trimmed.IndexOf('[');
            if(bracket < 0) {
                if(_msgStr is not null) {
                    throw new CatalogParseException("Duplicate msgstr in entry.", lineNumber);
                }

                _msgStr = ParseKeywordValue(trimmed, MSGSTR, lineNumber);
                _activeForm = null;
            } else {
                var close = trimmed.IndexOf(']', bracket);
                if(close < 0 || !int.TryParse(trimmed.AsSpan(bracket + 1, close - bracket - 1), out var formIndex)) {
                    throw new CatalogParseException($"Malformed indexed string '{trimmed}'.", lineNumber);
                }

                if(_forms.ContainsKey(formIndex)) {
                    throw new CatalogParseException($"Duplicate msgstr[{formIndex}] in entry.", lineNumber);
                }

                _forms[formIndex] = ParseKeywordValue(string.Concat(trimmed.AsSpan(0, bracket), trimmed.AsSpan(close + 1)), MSGSTR, lineNumber);
                _activeForm = formIndex;
            }

            _activeKind = MSGSTR;
        }

        /// <summary>
        /// Performs the drain operation.
        /// </summary>
        /// <returns>The drain sequence.</returns>
        public List<PoServedPair> Drain() {
            var pairs = new List<PoServedPair>();
            try {
                if(_msgId is null) {
                    return pairs;
                }

                var context = _hasContext ? _context : null;
                if(_msgId.Length == 0 && context is null && _msgIdPlural is null) {
                    return pairs;
                }

                if(_fuzzy) {
                    return pairs;
                }

                if(_msgIdPlural is null) {
                    AddSingular(pairs, _msgId, context);
                } else {
                    AddPlural(pairs, _msgId, _msgIdPlural, context);
                }

                return pairs;
            }
            finally {
                Reset();
            }
        }

        /// <summary>
        /// Projects one singular entry into its served pair.
        /// </summary>
        /// <param name="pairs">The pairs being collected.</param>
        /// <param name="msgId">The entry's <c>msgid</c> (non-empty here).</param>
        /// <param name="context">The entry's context, or <see langword="null"/>.</param>
        /// <exception cref="CatalogParseException">Thrown on a missing or indexed <c>msgstr</c>.</exception>
        private void AddSingular(List<PoServedPair> pairs, string msgId, string? context) {
            if(_msgStr is null) {
                throw new CatalogParseException($"Singular entry '{msgId}' is missing msgstr.", _msgIdLine);
            }

            if(_forms.Count > 0) {
                throw new CatalogParseException($"Singular entry '{msgId}' must use plain msgstr, not msgstr[n].", _msgIdLine);
            }

            if(_msgStr.Length != 0) {
                pairs.Add(new PoServedPair {
                    Key = context is null ? msgId : context + CatalogReader.CONTEXT_SEPARATOR + msgId,
                    Template = _msgStr,
                    Line = _msgIdLine,
                });
            }
        }

        /// <summary>
        /// Projects one plural entry into its singular and plural served pairs.
        /// </summary>
        /// <param name="pairs">The pairs being collected.</param>
        /// <param name="msgId">The entry's <c>msgid</c>.</param>
        /// <param name="msgIdPlural">The entry's <c>msgid_plural</c>.</param>
        /// <param name="context">The entry's context, or <see langword="null"/>.</param>
        /// <exception cref="CatalogParseException">Thrown on missing, plain, or non-contiguous forms.</exception>
        private void AddPlural(List<PoServedPair> pairs, string msgId, string msgIdPlural, string? context) {
            if(_forms.Count == 0) {
                throw new CatalogParseException($"Plural entry '{msgId}' is missing msgstr[n].", _msgIdLine);
            }

            if(_msgStr is not null) {
                throw new CatalogParseException($"Plural entry '{msgId}' must use indexed msgstr[n], not plain msgstr.", _msgIdLine);
            }

            for(var form = 0; form < _forms.Count; form++) {
                if(!_forms.ContainsKey(form)) {
                    throw new CatalogParseException($"Plural entry '{msgId}' misses msgstr[{form}].", _msgIdLine);
                }
            }

            var singularKey = context is null ? msgId : context + CatalogReader.CONTEXT_SEPARATOR + msgId;
            var pluralKey = context is null ? msgIdPlural : context + CatalogReader.CONTEXT_SEPARATOR + msgIdPlural;
            if(_forms[0].Length != 0) {
                pairs.Add(new PoServedPair { Key = singularKey, Template = _forms[0], Line = _msgIdLine });
            }

            if(_forms.Count > 1) {
                if(_forms[1].Length != 0) {
                    pairs.Add(new PoServedPair { Key = pluralKey, Template = _forms[1], Line = _msgIdLine });
                }
            } else if(_forms[0].Length != 0) {
                pairs.Add(new PoServedPair { Key = pluralKey, Template = _forms[0], Line = _msgIdLine });
            }
        }

        /// <summary>
        /// Appends active.
        /// </summary>
        /// <param name="text">The text.</param>
        private void AppendActive(string text) {
            switch(_activeKind) {
                case MSGCTXT:
                    _context += text;
                    break;
                case MSGID:
                    _msgId += text;
                    break;
                case MSGID_PLURAL:
                    _msgIdPlural += text;
                    break;
                case MSGSTR:
                    if(_activeForm is int form) {
                        _forms[form] += text;
                    } else {
                        _msgStr += text;
                    }

                    break;
            }
        }

        /// <summary>
        /// Performs the reset operation.
        /// </summary>
        private void Reset() {
            _context = string.Empty;
            _hasContext = false;
            _msgId = null;
            _msgIdLine = 0;
            _msgIdPlural = null;
            _msgStr = null;
            _forms.Clear();
            _activeKind = null;
            _activeForm = null;
            _fuzzy = false;
            _pendingFuzzy = false;
        }

        /// <summary>
        /// Parses keyword value.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        /// <param name="keyword">The keyword.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <returns>The keyword value text.</returns>
        private static string ParseKeywordValue(string trimmed, string keyword, int lineNumber) {
            var quote = trimmed.IndexOf('"');
            if(quote < 0) {
                throw new CatalogParseException($"Malformed {keyword} line '{trimmed}'.", lineNumber);
            }

            return ParseQuoted(trimmed, quote, trimmed.Length, lineNumber);
        }
    }
}
