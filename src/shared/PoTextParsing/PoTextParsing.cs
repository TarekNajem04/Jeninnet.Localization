//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
// Shared source: the PO (Portable Object) text grammar is compiled into both
// Jeninnet.Localization.Providers.Po and Jeninnet.Localization.Interchange.Po as a
// linked file, so the provider parser and the interchange codec share one
// implementation while remaining independent packages (neither references the other,
// and the grammar is not duplicated — the quality gate measures zero duplicated
// lines). Each consumer maps the raw entries into its own model, owns its duplicate-key
// validation, header interpretation, and fuzzy policy, and supplies its own public
// diagnostic exception through PoTextParseErrorFactory (the shared code never declares
// an exception type, so no internal exception leaks into either package).
//
// The namespace is selected per project through DefineConstants because the two
// consumers have different root namespaces (identity invariant §20).

#if JENINNET_PO_TEXT_PROVIDER
namespace Jeninnet.Localization.Providers.Po;
#elif JENINNET_PO_TEXT_INTERCHANGE
namespace Jeninnet.Localization.Interchange.Po;
#else
#error Compile PoTextParsing.cs with JENINNET_PO_TEXT_PROVIDER or JENINNET_PO_TEXT_INTERCHANGE defined.
#endif

/// <summary>
/// Creates the consumer's public diagnostic exception for one PO text grammar violation.
/// </summary>
/// <param name="filePath">The file path the violation was found in.</param>
/// <param name="lineNumber">The 1-based line number of the violation.</param>
/// <param name="message">The violation description.</param>
/// <returns>The exception to throw.</returns>
internal delegate Exception PoTextParseErrorFactory(string filePath, int lineNumber, string message);

/// <summary>
/// One parsed PO entry in grammar order. The header entry surfaces as an entry with an
/// empty <see cref="MsgId"/> and no context or plural key; fuzzy entries are returned
/// with the flag set so each consumer applies its own policy.
/// </summary>
internal sealed class PoRawEntry {
    /// <summary>Gets the 1-based line number of the entry's first keyword.</summary>
    internal int StartLine { get; init; }

    /// <summary>Gets the <c>msgctxt</c> value, or <see langword="null"/> when the entry has no context.</summary>
    internal string? Context { get; init; }

    /// <summary>Gets the <c>msgid</c> value (empty for the header entry).</summary>
    internal string MsgId { get; init; } = string.Empty;

    /// <summary>Gets the <c>msgid_plural</c> value, or <see langword="null"/> for singular entries.</summary>
    internal string? MsgIdPlural { get; init; }

    /// <summary>Gets the translation forms: the plain <c>msgstr</c> as one element, or the contiguous indexed <c>msgstr[n]</c> forms.</summary>
    internal string[] Forms { get; init; } = [];

    /// <summary>Gets a value indicating whether the entry carries the <c>#, fuzzy</c> flag.</summary>
    internal bool Fuzzy { get; init; }

    /// <summary>Gets the joined <c>#.</c> translator notes, or <see langword="null"/> when the entry has none.</summary>
    internal string? Note { get; init; }
}

/// <summary>
/// The shared PO text reader: header entries, flat, contextual, and
/// plural entries, C-style escapes, <c>#.</c> notes, <c>#, fuzzy</c> flags, BOM (Byte Order Mark)
/// tolerance, and file/line diagnostics for every structural violation. Malformed input
/// fails fast and never yields partial tables.
/// </summary>
internal static class PoTextParsing {
    /// <summary>The <c>msgctxt</c> keyword.</summary>
    private const string MSGCTXT = "msgctxt";

    /// <summary>The <c>msgid</c> keyword.</summary>
    private const string MSGID = "msgid";

    /// <summary>The <c>msgid_plural</c> keyword.</summary>
    private const string MSGID_PLURAL = "msgid_plural";

    /// <summary>The <c>msgstr</c> keyword.</summary>
    private const string MSGSTR = "msgstr";

    /// <summary>
    /// Parses PO text into raw entries in grammar order.
    /// </summary>
    /// <param name="text">The PO document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="createError">Creates the consumer's public diagnostic exception for a violation.</param>
    /// <returns>The parsed entries.</returns>
    internal static List<PoRawEntry> Parse(string text, string filePath, PoTextParseErrorFactory createError) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(createError);

        if(text.StartsWith('\uFEFF')) {
            text = text[1..];
        }

        return new Reader(filePath, createError).Read(text);
    }

    /// <summary>
    /// Splits a PO header block into <c>Key: Value</c> pairs.
    /// </summary>
    /// <param name="headerBlock">The header block (the header entry's translation).</param>
    /// <returns>The header pairs in block order.</returns>
    internal static IEnumerable<KeyValuePair<string, string>> SplitHeaders(string headerBlock) {
        foreach(var line in headerBlock.Split('\n')) {
            var trimmed = line.Trim().TrimEnd('\r');
            if(trimmed.Length == 0) {
                continue;
            }

            var colon = trimmed.IndexOf(':');
            if(colon <= 0) {
                continue;
            }

            yield return new KeyValuePair<string, string>(
                trimmed[..colon].Trim(),
                trimmed[(colon + 1)..].Trim());
        }
    }

    /// <summary>
    /// Parses quoted.
    /// </summary>
    /// <param name="trimmed">The trimmed.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <param name="createError">The create error.</param>
    /// <returns>The quoted text.</returns>
    private static string ParseQuoted(string trimmed, string filePath, int lineNumber, PoTextParseErrorFactory createError) {
        if(trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"') {
            throw createError(filePath, lineNumber, $"Malformed quoted string '{trimmed}': expected a double-quoted string.");
        }

        var inner = trimmed[1..^1];
        var builder = new StringBuilder(inner.Length);
        var position = 0;
        while(position < inner.Length) {
            var c = inner[position];
            position++;
            if(c != '\\') {
                builder.Append(c);
                continue;
            }

            if(position >= inner.Length) {
                throw createError(filePath, lineNumber, $"Malformed escape in '{trimmed}': trailing backslash.");
            }

            var escaped = inner[position];
            position++;
            builder.Append(escaped switch {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                '0' => '\0',
                _ => throw createError(filePath, lineNumber, $"Malformed escape '\\{escaped}' in '{trimmed}'."),
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// The line-oriented reader state: comment/note accumulation, the active entry, and
    /// the entry finalization rules.
    /// </summary>
    /// <param name="filePath">The file path for error reporting.</param>
    /// <param name="createError">The factory for creating parse errors.</param>
    private sealed class Reader(string filePath, PoTextParseErrorFactory createError) {
        private readonly string _filePath = filePath;
        private readonly PoTextParseErrorFactory _createError = createError;
        private readonly List<PoRawEntry> _entries = [];
        private readonly List<string> _pendingNote = [];
        private EntryBuilder? _current;
        private bool _pendingFuzzy;
        private int _lineNumber;

        /// <summary>
        /// Performs the read operation.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The read sequence.</returns>
        internal List<PoRawEntry> Read(string text) {
            using var reader = new StringReader(text);
            string? line;
            while((line = reader.ReadLine()) is not null) {
                _lineNumber++;
                ProcessLine(line);
            }

            FinalizeCurrent();

            return _entries;
        }

        /// <summary>
        /// Processes line.
        /// </summary>
        /// <param name="line">The line.</param>
        private void ProcessLine(string line) {
            var trimmed = line.Trim();
            if(trimmed.Length == 0) {
                FinalizeCurrent();
                _pendingNote.Clear();
                _pendingFuzzy = false;
                return;
            }

            if(trimmed[0] == '#') {
                ReadComment(trimmed);
                return;
            }

            if(trimmed[0] == '"') {
                AppendContinuation(trimmed);
                return;
            }

            ReadKeywordLine(trimmed);
        }

        /// <summary>
        /// Reads comment.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        private void ReadComment(string trimmed) {
            if(trimmed.StartsWith("#.", StringComparison.Ordinal)) {
                var noteLine = trimmed[2..].Trim();
                if(noteLine.Length > 0) {
                    _pendingNote.Add(noteLine);
                }
            } else if(IsFuzzyComment(trimmed)) {
                _pendingFuzzy = true;
            }
        }

        /// <summary>
        /// Appends continuation.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        private void AppendContinuation(string trimmed) {
            if(_current is null) {
                throw _createError(_filePath, _lineNumber, "Unexpected string continuation without a preceding keyword.");
            }

            _current.AppendContinuation(ParseQuoted(trimmed, _filePath, _lineNumber, _createError), _filePath, _lineNumber);
        }

        /// <summary>
        /// Reads keyword line.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        private void ReadKeywordLine(string trimmed) {
            var keyword = ReadKeyword(trimmed, _filePath, _lineNumber, _createError);
            if(keyword is MSGCTXT or MSGID) {
                if(_current?.HasMsgId == true) {
                    FinalizeCurrent();
                }

                _current ??= new EntryBuilder(_lineNumber, _pendingFuzzy, _pendingNote.Count == 0 ? null : string.Join('\n', _pendingNote), _createError);
                _pendingFuzzy = false;
                _pendingNote.Clear();
            }

            if(_current is null) {
                throw _createError(_filePath, _lineNumber, $"Unexpected keyword '{keyword}' outside an entry.");
            }

            _current.SetKeyword(keyword, trimmed, _filePath, _lineNumber);
        }

        /// <summary>
        /// Performs the finalize current operation.
        /// </summary>
        private void FinalizeCurrent() {
            if(_current is null) {
                return;
            }

            _entries.Add(_current.Build(_filePath));
            _current = null;
            _pendingNote.Clear();
            _pendingFuzzy = false;
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

            var flags = trimmed[2..].Split(',');
            return flags.Any(static flag => flag.Trim().Equals("fuzzy", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads keyword.
        /// </summary>
        /// <param name="trimmed">The trimmed.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="createError">The create error.</param>
        /// <returns>The keyword text.</returns>
        private static string ReadKeyword(string trimmed, string filePath, int lineNumber, PoTextParseErrorFactory createError) {
            foreach(var keyword in new[] { MSGCTXT, MSGID_PLURAL, MSGID, MSGSTR }) {
                if(trimmed.StartsWith(keyword, StringComparison.Ordinal)
                    && (trimmed.Length == keyword.Length
                        || char.IsWhiteSpace(trimmed[keyword.Length])
                        || trimmed[keyword.Length] == '['
                        || trimmed[keyword.Length] == '"')) {
                    return keyword;
                }
            }

            throw createError(filePath, lineNumber, $"Unrecognized line '{trimmed}'.");
        }
    }

    /// <summary>
    /// Builds entry.
    /// </summary>
    /// <param name="startLine">The start line.</param>
    /// <param name="fuzzy">The fuzzy.</param>
    /// <param name="note">The note.</param>
    /// <param name="createError">The create error.</param>
    private sealed class EntryBuilder(int startLine, bool fuzzy, string? note, PoTextParseErrorFactory createError) {
        private readonly PoTextParseErrorFactory _createError = createError;
        private string? _context;
        private bool _hasContext;
        private string? _msgId;
        private string? _msgIdPlural;
        private string? _msgStr;
        private readonly Dictionary<int, string> _indexed = [];
        private string? _active;
        private int? _activeIndex;
        private readonly StringBuilder _activeText = new();

        /// <summary>
        /// Gets the start line.
        /// </summary>
        internal int StartLine { get; } = startLine;

        /// <summary>
        /// Gets a value indicating whether has msg id.
        /// </summary>
        internal bool HasMsgId => _msgId is not null;

        /// <summary>
        /// Sets keyword.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="line">The line.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        internal void SetKeyword(string keyword, string line, string filePath, int lineNumber) {
            FlushActive();

            if(keyword == MSGCTXT) {
                SetContext(line, filePath, lineNumber);
                return;
            }

            if(keyword == MSGID) {
                SetMsgId(line, filePath, lineNumber);
                return;
            }

            if(keyword == MSGID_PLURAL) {
                SetMsgIdPlural(line, filePath, lineNumber);
                return;
            }

            if(keyword == MSGSTR) {
                SetMsgStr(line, filePath, lineNumber);
                return;
            }

            throw _createError(filePath, lineNumber, $"Unrecognized keyword '{keyword}'.");
        }

        /// <summary>
        /// Appends continuation.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        internal void AppendContinuation(string text, string filePath, int lineNumber) {
            if(_active is null) {
                throw _createError(filePath, lineNumber, "Unexpected string continuation without a preceding keyword.");
            }

            _activeText.Append(text);
        }

        /// <summary>
        /// Performs the build operation.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The build result.</returns>
        internal PoRawEntry Build(string filePath) {
            FlushActive();
            if(_msgId is null) {
                throw _createError(filePath, StartLine, "Entry without msgid.");
            }

            if(_msgIdPlural is null) {
                if(_msgStr is null) {
                    throw _createError(filePath, StartLine, $"Entry '{_msgId}' without msgstr.");
                }

                if(_indexed.Count != 0) {
                    throw _createError(filePath, StartLine, $"Singular entry '{_msgId}' must use plain msgstr, not msgstr[n].");
                }

                return new PoRawEntry {
                    StartLine = StartLine,
                    Context = _hasContext ? _context : null,
                    MsgId = _msgId,
                    Forms = [_msgStr],
                    Fuzzy = fuzzy,
                    Note = note,
                };
            }

            if(_msgStr is not null) {
                throw _createError(filePath, StartLine, $"Plural entry '{_msgId}' must use indexed msgstr[n], not plain msgstr.");
            }

            if(_indexed.Count == 0) {
                throw _createError(filePath, StartLine, $"Plural entry '{_msgId}' without msgstr[n].");
            }

            var max = _indexed.Keys.Max();
            for(var i = 0; i <= max; i++) {
                if(!_indexed.ContainsKey(i)) {
                    throw _createError(filePath, StartLine, $"Plural entry '{_msgId}' misses msgstr[{i.ToString(CultureInfo.InvariantCulture)}].");
                }
            }

            return new PoRawEntry {
                StartLine = StartLine,
                Context = _hasContext ? _context : null,
                MsgId = _msgId,
                MsgIdPlural = _msgIdPlural,
                Forms = [.. Enumerable.Range(0, max + 1).Select(i => _indexed[i])],
                Fuzzy = fuzzy,
                Note = note,
            };
        }

        /// <summary>
        /// Sets context.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void SetContext(string line, string filePath, int lineNumber) {
            if(_hasContext || _msgId is not null) {
                throw _createError(filePath, lineNumber, "Duplicate msgctxt in entry.");
            }

            _hasContext = true;
            BeginActive(MSGCTXT, index: null);
            _activeText.Append(ExtractQuoted(line, MSGCTXT, filePath, lineNumber));
        }

        /// <summary>
        /// Sets msg id.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void SetMsgId(string line, string filePath, int lineNumber) {
            if(_msgId is not null) {
                throw _createError(filePath, lineNumber, "Duplicate msgid in entry.");
            }

            BeginActive(MSGID, index: null);
            _activeText.Append(ExtractQuoted(line, MSGID, filePath, lineNumber));
        }

        /// <summary>
        /// Sets msg id plural.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void SetMsgIdPlural(string line, string filePath, int lineNumber) {
            if(_msgId is null) {
                throw _createError(filePath, lineNumber, "msgid_plural before msgid.");
            }

            if(_msgIdPlural is not null) {
                throw _createError(filePath, lineNumber, "Duplicate msgid_plural in entry.");
            }

            BeginActive(MSGID_PLURAL, index: null);
            _activeText.Append(ExtractQuoted(line, MSGID_PLURAL, filePath, lineNumber));
        }

        /// <summary>
        /// Sets msg str.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void SetMsgStr(string line, string filePath, int lineNumber) {
            var bracket = line.IndexOf('[');
            var quote = line.IndexOf('"');
            if(bracket >= 0 && bracket < quote) {
                SetIndexedMsgStr(line, bracket, quote, filePath, lineNumber);
                return;
            }

            if(_msgStr is not null || _indexed.Count != 0) {
                throw _createError(filePath, lineNumber, "Duplicate msgstr in entry.");
            }

            BeginActive(MSGSTR, index: null);
            _activeText.Append(ExtractQuoted(line, MSGSTR, filePath, lineNumber));
        }

        /// <summary>
        /// Sets indexed msg str.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="bracket">The bracket.</param>
        /// <param name="quote">The quote.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        private void SetIndexedMsgStr(string line, int bracket, int quote, string filePath, int lineNumber) {
            var close = line.IndexOf(']', bracket);
            if(close < 0 || close > quote) {
                throw _createError(filePath, lineNumber, $"Malformed indexed msgstr in '{line.Trim()}'.");
            }

            var indexText = line[(bracket + 1)..close];
            if(!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index < 0) {
                throw _createError(filePath, lineNumber, $"Malformed msgstr index in '{line.Trim()}'.");
            }

            if(_indexed.ContainsKey(index)) {
                throw _createError(filePath, lineNumber, $"Duplicate msgstr[{indexText}] in entry.");
            }

            BeginActive(MSGSTR, index);
            _activeText.Append(ExtractQuoted(line[(close + 1)..], MSGSTR, filePath, lineNumber));
        }

        /// <summary>
        /// Begins active.
        /// </summary>
        /// <param name="keyword">The keyword.</param>
        /// <param name="index">The index.</param>
        private void BeginActive(string keyword, int? index) {
            _active = keyword;
            _activeIndex = index;
        }

        /// <summary>
        /// Performs the flush active operation.
        /// </summary>
        private void FlushActive() {
            if(_active is null) {
                return;
            }

            var value = _activeText.ToString();
            _activeText.Clear();
            if(_active == MSGCTXT) {
                _context = value;
            } else if(_active == MSGID) {
                _msgId = value;
            } else if(_active == MSGID_PLURAL) {
                _msgIdPlural = value;
            } else if(_activeIndex.HasValue) {
                _indexed[_activeIndex.Value] = value;
            } else {
                _msgStr = value;
            }

            _active = null;
            _activeIndex = null;
        }

        /// <summary>
        /// Extracts quoted.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="keyword">The keyword.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <returns>The quoted text.</returns>
        private string ExtractQuoted(string line, string keyword, string filePath, int lineNumber) {
            var quote = line.IndexOf('"');
            if(quote < 0) {
                throw _createError(filePath, lineNumber, $"Malformed {keyword} line: expected a double-quoted string.");
            }

            return ParseQuoted(line[quote..].Trim(), filePath, lineNumber, _createError);
        }
    }
}
