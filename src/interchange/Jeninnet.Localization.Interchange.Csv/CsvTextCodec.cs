//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Csv;

/// <summary>
/// The strict RFC 4180 reader/writer for the  CSV subset (hand-rolled; no
/// third-party parser). Writes the fixed header, CRLF (Carriage Return and Line Feed) records, and minimal RFC 4180
/// quoting; reads with BOM (Byte Order Mark) tolerance, CRLF/LF/lone-CR record separators, and
/// file/line diagnostics for every structural violation.
/// </summary>
internal static class CsvTextCodec {
    /// <summary>
    /// The fixed column header (ordinal order): key, culture, value, note.
    /// </summary>
    internal static readonly string[] Header = ["key", "culture", "value", "note"];

    /// <summary>
    /// Writes rows as CSV text: the header, then one CRLF-separated record per row,
    /// with minimal RFC 4180 quoting and no trailing newline.
    /// </summary>
    /// <param name="rows">The rows in deterministic order.</param>
    /// <returns>The CSV document text (no BOM; the file writer adds it).</returns>
    internal static string Write(IReadOnlyList<CsvRow> rows) {
        var builder = new StringBuilder();
        AppendRecord(builder, Header);
        foreach(var row in rows) {
            AppendRecord(builder, [row.Key, row.Culture, row.Value, row.Note ?? string.Empty]);
        }

        return builder.ToString();

        static void AppendRecord(StringBuilder builder, string[] fields) {
            if(builder.Length > 0) {
                builder.Append("\r\n");
            }

            for(var i = 0; i < fields.Length; i++) {
                if(i > 0) {
                    builder.Append(',');
                }

                AppendField(builder, fields[i]);
            }
        }
    }

    /// <summary>
    /// Parses CSV text against the  subset: optional BOM, exactly the fixed
    /// header, four fields per record, non-empty keys/cultures, no duplicate
    /// (key, culture) rows, and no conflicting non-empty notes for one key. Blank
    /// records (empty lines) are skipped.
    /// </summary>
    /// <param name="text">The CSV document text.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <returns>The parsed rows in file order.</returns>
    /// <exception cref="CsvInterchangeException">Thrown when the text violates the subset.</exception>
    internal static List<CsvRow> Parse(string text, string filePath) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if(text.StartsWith('\uFEFF')) {
            text = text[1..];
        }

        var records = SplitRecords(text, filePath);
        if(records.Count == 0) {
            throw new CsvInterchangeException(filePath, 1, "Empty CSV document: the header row is required.");
        }

        ValidateHeader(records[0], filePath);

        var rows = new List<CsvRow>(Math.Max(0, records.Count - 1));
        var seen = new Dictionary<(string Key, string Culture), int>();
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach(var record in records.Skip(1)) {
            rows.Add(ParseRecord(record, filePath, seen, notes));
        }

        return rows;
    }

    /// <summary>
    /// Validates the fixed header record: exactly the four expected columns in order.
    /// </summary>
    /// <param name="header">The raw header record.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <exception cref="CsvInterchangeException">Thrown when the header has the wrong column count or names.</exception>
    private static void ValidateHeader(RawRecord header, string filePath) {
        if(header.Fields.Length != Header.Length) {
            throw new CsvInterchangeException(filePath, header.Line, $"The header must be exactly '{string.Join(',', Header)}' but has {header.Fields.Length.ToString(CultureInfo.InvariantCulture)} column(s).");
        }

        for(var i = 0; i < Header.Length; i++) {
            if(!string.Equals(header.Fields[i], Header[i], StringComparison.Ordinal)) {
                throw new CsvInterchangeException(filePath, header.Line, $"The header must be exactly '{string.Join(',', Header)}' (column {i + 1} is '{header.Fields[i]}').");
            }
        }
    }

    /// <summary>
    /// Validates one data record and converts it into a <see cref="CsvRow"/>, tracking
    /// duplicate (key, culture) pairs and the translator note per key.
    /// </summary>
    /// <param name="record">The raw data record.</param>
    /// <param name="filePath">The file path used in diagnostics.</param>
    /// <param name="seen">The line number of each (key, culture) pair seen so far.</param>
    /// <param name="notes">The non-empty translator note per key seen so far.</param>
    /// <returns>The parsed row.</returns>
    /// <exception cref="CsvInterchangeException">Thrown when the record violates the subset.</exception>
    private static CsvRow ParseRecord(RawRecord record, string filePath, Dictionary<(string Key, string Culture), int> seen, Dictionary<string, string> notes) {
        if(record.Fields.Length != Header.Length) {
            throw new CsvInterchangeException(filePath, record.Line, $"Expected {Header.Length.ToString(CultureInfo.InvariantCulture)} fields but found {record.Fields.Length.ToString(CultureInfo.InvariantCulture)}.");
        }

        var key = record.Fields[0];
        var culture = record.Fields[1];
        var value = record.Fields[2];
        var note = record.Fields[3].Length == 0 ? null : record.Fields[3];

        if(key.Length == 0) {
            throw new CsvInterchangeException(filePath, record.Line, "The key must not be empty.");
        }

        if(culture.Length == 0) {
            throw new CsvInterchangeException(filePath, record.Line, "The culture must not be empty.");
        }

        if(!seen.TryAdd((key, culture), record.Line)) {
            throw new CsvInterchangeException(filePath, record.Line, $"Duplicate row for key '{key}' and culture '{culture}'.");
        }

        if(note is not null && notes.TryGetValue(key, out var existing) && !string.Equals(existing, note, StringComparison.Ordinal)) {
            throw new CsvInterchangeException(filePath, record.Line, $"Conflicting translator notes for key '{key}'.");
        }

        if(note is not null) {
            notes[key] = note;
        }

        return new CsvRow {
            Key = key,
            Culture = culture,
            Value = value,
            Note = note,
        };
    }

    /// <summary>
    /// Appends field.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="value">The value.</param>
    private static void AppendField(StringBuilder builder, string value) {
        if(value.IndexOfAny([',', '"', '\r', '\n']) < 0) {
            builder.Append(value);
            return;
        }

        builder.Append('"')
            .Append(value.Replace("\"", "\"\"", StringComparison.Ordinal))
            .Append('"');
    }

    /// <summary>
    /// Splits records.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>The split records sequence.</returns>
    private static List<RawRecord> SplitRecords(string text, string filePath) =>
        new RecordScanner(text, filePath).Scan();

    /// <summary>
    /// The RFC 4180 record-splitting state machine: tracks the current field, the record
    /// start line, and the quoting state while building the raw records.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="filePath">The file path for error reporting.</param>
    private sealed class RecordScanner(string text, string filePath) {
        private readonly string _text = text;
        private readonly string _filePath = filePath;
        private readonly List<RawRecord> _records = [];
        private readonly List<string> _fields = [];
        private readonly StringBuilder _field = new();
        private int _line = 1;
        private int _recordStartLine = 1;
        private bool _recordHasContent;
        private FieldState _state = FieldState.FieldStart;
        private int _index;

        /// <summary>
        /// Scans the whole document, rejects an unterminated quoted field, and flushes the
        /// final record.
        /// </summary>
        /// <returns>The raw records in file order (blank records omitted).</returns>
        /// <exception cref="CsvInterchangeException">Thrown when the text violates the subset.</exception>
        internal List<RawRecord> Scan() {
            while(_index < _text.Length) {
                var c = _text[_index];
                switch(_state) {
                    case FieldState.FieldStart:
                        ScanFieldStart(c);
                        break;
                    case FieldState.InUnquotedField:
                        ScanUnquotedField(c);
                        break;
                    case FieldState.InQuotedField:
                        ScanQuotedField(c);
                        break;
                    case FieldState.AfterClosingQuote:
                        ScanAfterClosingQuote(c);
                        break;
                }
            }

            if(_state == FieldState.InQuotedField) {
                throw new CsvInterchangeException(_filePath, _recordStartLine, "Unterminated quoted field.");
            }

            if(_state is FieldState.InUnquotedField or FieldState.AfterClosingQuote || _fields.Count > 0 || _field.Length > 0) {
                EndRecord();
            }

            return _records;
        }

        /// <summary>
        /// Advances the scanner at the start of a field: opens a quoted field, appends an
        /// empty field on a comma, ends the record on a newline, or starts an unquoted field.
        /// </summary>
        /// <param name="c">The current character.</param>
        private void ScanFieldStart(char c) {
            if(c == '"') {
                _state = FieldState.InQuotedField;
                _recordHasContent = true;
                _index++;
            } else if(c == ',') {
                _fields.Add(string.Empty);
                _index++;
            } else if(c is '\r' or '\n') {
                EndRecord();
                _index = ConsumeNewLine();
            } else {
                _field.Append(c);
                _state = FieldState.InUnquotedField;
                _recordHasContent = true;
                _index++;
            }
        }

        /// <summary>
        /// Advances the scanner inside an unquoted field: splits on a comma, rejects a
        /// quote, ends the record on a newline, or appends the character.
        /// </summary>
        /// <param name="c">The current character.</param>
        /// <exception cref="CsvInterchangeException">Thrown when a quote appears in an unquoted field.</exception>
        private void ScanUnquotedField(char c) {
            if(c == ',') {
                _fields.Add(_field.ToString());
                _field.Clear();
                _state = FieldState.FieldStart;
                _index++;
            } else if(c == '"') {
                throw new CsvInterchangeException(_filePath, _line, "Unexpected quote in an unquoted field: quote the whole field or double the quote.");
            } else if(c is '\r' or '\n') {
                EndRecord();
                _index = ConsumeNewLine();
            } else {
                _field.Append(c);
                _index++;
            }
        }

        /// <summary>
        /// Advances the scanner inside a quoted field: unescapes a doubled quote, closes the
        /// field on a lone quote, or appends the character (counting embedded newlines).
        /// </summary>
        /// <param name="c">The current character.</param>
        private void ScanQuotedField(char c) {
            if(c == '"') {
                if(_index + 1 < _text.Length && _text[_index + 1] == '"') {
                    _field.Append('"');
                    _index += 2;
                } else {
                    _state = FieldState.AfterClosingQuote;
                    _index++;
                }
            } else {
                if(c == '\n') {
                    _line++;
                }

                _field.Append(c);
                _index++;
            }
        }

        /// <summary>
        /// Advances the scanner after a closing quote: splits on a comma, ends the record on
        /// a newline, or rejects any other character.
        /// </summary>
        /// <param name="c">The current character.</param>
        /// <exception cref="CsvInterchangeException">Thrown when any other character follows a closing quote.</exception>
        private void ScanAfterClosingQuote(char c) {
            if(c == ',') {
                _fields.Add(_field.ToString());
                _field.Clear();
                _state = FieldState.FieldStart;
                _index++;
            } else if(c is '\r' or '\n') {
                EndRecord();
                _index = ConsumeNewLine();
            } else {
                throw new CsvInterchangeException(_filePath, _line, "Unexpected character after a closing quote.");
            }
        }

        /// <summary>
        /// Finalizes the current record: appends the pending field and records the row
        /// unless it is a single empty field with no content.
        /// </summary>
        private void EndRecord() {
            _fields.Add(_field.ToString());
            _field.Clear();

            if(_fields.Count != 1 || _fields[0].Length != 0 || _recordHasContent) {
                _records.Add(new RawRecord(_recordStartLine, [.. _fields]));
            }

            _fields.Clear();
            _recordHasContent = false;
            _recordStartLine = _line + 1;
        }

        /// <summary>
        /// Consumes one CRLF, LF, or lone-CR record separator, advancing the line counter.
        /// </summary>
        /// <returns>The index of the next character after the separator.</returns>
        private int ConsumeNewLine() {
            _line++;
            if(_text[_index] == '\r' && _index + 1 < _text.Length && _text[_index + 1] == '\n') {
                return _index + 2;
            }

            return _index + 1;
        }
    }

    /// <summary>
    /// Specifies the field state values.
    /// </summary>
    private enum FieldState {
        FieldStart = 0,
        InUnquotedField = 1,
        InQuotedField = 2,
        AfterClosingQuote = 3,
    }

    /// <summary>
    /// Represents a raw record.
    /// </summary>
    /// <param name="Line">The line.</param>
    /// <param name="Fields">The fields.</param>
    private sealed record RawRecord(int Line, string[] Fields);
}
