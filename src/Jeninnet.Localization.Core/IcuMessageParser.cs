//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Core;

/// <summary>
/// Base interface for parsed ICU (International Components for Unicode) message nodes.
/// </summary>
internal interface IIcuNode;

/// <summary>
/// Literal text.
/// </summary>
/// <param name="text">The literal text.</param>
internal sealed class IcuTextNode(string text) : IIcuNode {
    /// <summary>
    /// Gets the literal text.
    /// </summary>
    public string Text { get; } = text;
}

/// <summary>
/// A <c>#</c> placeholder inside a plural message (number minus offset).
/// </summary>
internal sealed class IcuPoundNode : IIcuNode;

/// <summary>
/// An <c>{argument}</c> placeholder.
/// </summary>
/// <param name="name">The argument name.</param>
/// <param name="format">The argument format.</param>
internal sealed class IcuArgumentNode(string name, IIcuArgumentFormat format) : IIcuNode {
    /// <summary>
    /// Gets the argument name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the argument format.
    /// </summary>
    public IIcuArgumentFormat Format { get; } = format;
}

/// <summary>
/// Base interface for argument formats.
/// </summary>
internal interface IIcuArgumentFormat;

/// <summary>
/// A simple <c>{name}</c> substitution.
/// </summary>
internal sealed class IcuSimpleFormat : IIcuArgumentFormat;

/// <summary>
/// A <c>{name, number[, style|::skeleton]}</c> argument.
/// </summary>
/// <param name="style">The named style, or <see langword="null"/> for the default or when a skeleton is present.</param>
/// <param name="skeleton">The <c>::</c> skeleton, or <see langword="null"/> when a named style or the default applies.</param>
internal sealed class IcuNumberFormat(string? style, string? skeleton) : IIcuArgumentFormat {
    /// <summary>
    /// Gets the named style (<see langword="null"/> for the default or when a skeleton is present), e.g. <c>integer</c>.
    /// </summary>
    public string? Style { get; } = style;

    /// <summary>
    /// Gets the <c>::</c> skeleton (<see langword="null"/> when a named style or the default applies).
    /// </summary>
    public string? Skeleton { get; } = skeleton;
}

/// <summary>
/// A <c>{name, date[, style|::skeleton]}</c> argument.
/// </summary>
/// <param name="style">The named style, or <see langword="null"/> for the default or when a skeleton is present.</param>
/// <param name="skeleton">The <c>::</c> skeleton, or <see langword="null"/> when a named style or the default applies.</param>
internal sealed class IcuDateFormat(string? style, string? skeleton) : IIcuArgumentFormat {
    /// <summary>
    /// Gets the named style (<see langword="null"/> for the default or when a skeleton is present).
    /// </summary>
    public string? Style { get; } = style;

    /// <summary>
    /// Gets the <c>::</c> skeleton (<see langword="null"/> when a named style or the default applies).
    /// </summary>
    public string? Skeleton { get; } = skeleton;
}

/// <summary>
/// A <c>{name, time[, style|::skeleton]}</c> argument.
/// </summary>
/// <param name="style">The named style, or <see langword="null"/> for the default or when a skeleton is present.</param>
/// <param name="skeleton">The <c>::</c> skeleton, or <see langword="null"/> when a named style or the default applies.</param>
internal sealed class IcuTimeFormat(string? style, string? skeleton) : IIcuArgumentFormat {
    /// <summary>
    /// Gets the named style (<see langword="null"/> for the default or when a skeleton is present).
    /// </summary>
    public string? Style { get; } = style;

    /// <summary>
    /// Gets the <c>::</c> skeleton (<see langword="null"/> when a named style or the default applies).
    /// </summary>
    public string? Skeleton { get; } = skeleton;
}

/// <summary>
/// A <c>{name, select, ...}</c> argument.
/// </summary>
/// <param name="options">The options (always contains an <c>other</c> option).</param>
internal sealed class IcuSelectFormat(IReadOnlyList<IcuOption> options) : IIcuArgumentFormat {
    /// <summary>
    /// Gets the options (always contains an <c>other</c> option).
    /// </summary>
    public IReadOnlyList<IcuOption> Options { get; } = options;
}

/// <summary>
/// A <c>{name, plural|selectordinal, ...}</c> argument.
/// </summary>
/// <param name="options">The options (always contains an <c>other</c> option).</param>
/// <param name="offset">The subtracted offset, or <see langword="null"/> when absent.</param>
/// <param name="ordinal">Whether ordinal (rather than cardinal) categories apply.</param>
internal sealed class IcuPluralFormat(IReadOnlyList<IcuOption> options, decimal? offset, bool ordinal) : IIcuArgumentFormat {
    /// <summary>
    /// Gets the options (always contains an <c>other</c> option).
    /// </summary>
    public IReadOnlyList<IcuOption> Options { get; } = options;

    /// <summary>
    /// Gets the subtracted offset (<see langword="null"/> when absent).
    /// </summary>
    public decimal? Offset { get; } = offset;

    /// <summary>
    /// Gets whether ordinal (rather than cardinal) categories apply.
    /// </summary>
    public bool Ordinal { get; } = ordinal;
}

/// <summary>
/// One selector branch of a plural/select argument.
/// </summary>
/// <param name="explicitValue">The explicit <c>=number</c> value, or <see langword="null"/> for keyword options.</param>
/// <param name="keyword">The keyword option, or <see langword="null"/> for explicit-value options.</param>
/// <param name="message">The branch message nodes.</param>
internal sealed class IcuOption(decimal? explicitValue, string? keyword, IReadOnlyList<IIcuNode> message) {
    /// <summary>
    /// Gets the explicit <c>=number</c> value (<see langword="null"/> for keyword options).
    /// </summary>
    public decimal? ExplicitValue { get; } = explicitValue;

    /// <summary>
    /// Gets the keyword (<see langword="null"/> for explicit-value options).
    /// </summary>
    public string? Keyword { get; } = keyword;

    /// <summary>
    /// Gets the branch message.
    /// </summary>
    public IReadOnlyList<IIcuNode> Message { get; } = message;
}

/// <summary>
/// Recursive-descent parser for the ICU MessageFormat subset supported by
/// <see cref="IcuMessageFormatter"/>.
/// </summary>
/// <remarks>
/// Supported: simple <c>{name}</c> args; <c>number</c>/<c>date</c>/<c>time</c> with
/// optional named styles or <c>::</c> skeletons; <c>select</c>;
/// <c>plural</c>/<c>selectordinal</c> with optional
/// <c>offset:N</c>, explicit <c>=number</c> selectors, and nested messages.
/// Apostrophe quoting follows ICU double-optional rules (<c>''</c> is a literal
/// apostrophe; <c>'{...}'</c> is literal text; a lone apostrophe before other
/// characters is literal). Anything structurally invalid throws
/// <see cref="FormatException"/> with the pattern offset — never partial output.
/// </remarks>
internal sealed class IcuMessageParser {
    private const int MAX_DEPTH = 64;

    private readonly string _pattern;
    private int _position;
    private bool _hasComplexArgument;

    /// <summary>
    /// Initializes a new instance of the <see cref="IcuMessageParser"/> class.
    /// </summary>
    /// <param name="pattern">The pattern to parse.</param>
    public IcuMessageParser(string pattern) {
        ArgumentNullException.ThrowIfNull(pattern);
        _pattern = pattern;
    }

    /// <summary>
    /// Parses the pattern.
    /// </summary>
    /// <returns>The nodes and whether any culture-sensitive argument was found.</returns>
    /// <exception cref="FormatException">Thrown when the pattern is malformed.</exception>
    public (IReadOnlyList<IIcuNode> Nodes, bool HasComplexArgument) Parse() {
        var nodes = ParseMessage(0, inPlural: false);
        if(!IsAtEnd()) {
            throw Fail("Unmatched '}'.");
        }

        return (nodes, _hasComplexArgument);
    }

    /// <summary>
    /// Parses message.
    /// </summary>
    /// <param name="depth">The depth.</param>
    /// <param name="inPlural">The in plural.</param>
    /// <returns>The message sequence.</returns>
    private List<IIcuNode> ParseMessage(int depth, bool inPlural) {
        if(depth > MAX_DEPTH) {
            throw Fail("Maximum nesting depth exceeded.");
        }

        var nodes = new List<IIcuNode>();
        while(!IsAtEnd()) {
            var current = _pattern[_position];
            if(current == '}') {
                return nodes;
            }

            if(current == '{') {
                nodes.Add(ParseArgument(depth, inPlural));
                continue;
            }

            if(current == '\'') {
                nodes.Add(new IcuTextNode(ParseQuoted()));
                continue;
            }

            if(current == '#' && inPlural) {
                _position++;
                nodes.Add(new IcuPoundNode());
                continue;
            }

            nodes.Add(new IcuTextNode(ParseText(inPlural)));
        }

        return nodes;
    }

    /// <summary>
    /// Parses text.
    /// </summary>
    /// <param name="inPlural">The in plural.</param>
    /// <returns>The text.</returns>
    private string ParseText(bool inPlural) {
        var start = _position;
        while(!IsAtEnd()) {
            var current = _pattern[_position];
            if(current is '{' or '\'' or '}' || (current == '#' && inPlural)) {
                break;
            }

            _position++;
        }

        return _pattern[start.._position];
    }

    /// <summary>
    /// Parses quoted.
    /// </summary>
    /// <returns>The quoted text.</returns>
    private string ParseQuoted() {
        // Current character is '\''.
        if(Peek(1) == '\'') {
            _position += 2;
            return "'";
        }

        if(Peek(1) is '{' or '}' or '#') {
            _position++;
            var start = _position;
            while(!IsAtEnd()) {
                if(_pattern[_position] == '\'') {
                    if(Peek(1) == '\'') {
                        _position += 2;
                        continue;
                    }

                    var quoted = _pattern[start.._position];
                    _position++;
                    return quoted.Replace("''", "'", StringComparison.Ordinal);
                }

                _position++;
            }

            // Lenient unterminated quote: the rest is literal text.
            return _pattern[start..];
        }

        _position++;
        return "'";
    }

    /// <summary>
    /// Parses argument.
    /// </summary>
    /// <param name="depth">The depth.</param>
    /// <param name="inPlural">The in plural.</param>
    /// <returns>The argument result.</returns>
    private IcuArgumentNode ParseArgument(int depth, bool inPlural) {
        _position++; // '{'
        SkipWhitespace();
        var name = ParseName();
        SkipWhitespace();
        if(TryConsume('}')) {
            return new IcuArgumentNode(name, new IcuSimpleFormat());
        }

        Consume(',');
        SkipWhitespace();
        var type = ParseWord("argument type");
        SkipWhitespace();
        return type switch {
            "plural" => ParsePlural(name, depth, ordinal: false),
            "selectordinal" => ParsePlural(name, depth, ordinal: true),
            "select" => ParseSelect(name, depth, inPlural),
            "number" => ParseSimpleTyped(name, static (style, skeleton) => new IcuNumberFormat(style, skeleton)),
            "date" => ParseSimpleTyped(name, static (style, skeleton) => new IcuDateFormat(style, skeleton)),
            "time" => ParseSimpleTyped(name, static (style, skeleton) => new IcuTimeFormat(style, skeleton)),
            _ => throw Fail($"Unknown argument type '{type}' for '{{{name}}}'. Supported types: plural, select, selectordinal, number, date, time."),
        };
    }

    /// <summary>
    /// Parses plural.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="ordinal">The ordinal position.</param>
    /// <returns>The plural result.</returns>
    private IcuArgumentNode ParsePlural(string name, int depth, bool ordinal) {
        if(TryConsume('}')) {
            throw Fail($"Argument '{{{name}}}' requires options (selectors with messages).");
        }

        Consume(',');
        var (options, offset) = ParseOptions(depth, inPlural: true, allowOffset: true, allowExplicit: true);
        Consume('}');
        ValidateOptions(name, options);
        _hasComplexArgument = true;
        return new IcuArgumentNode(name, new IcuPluralFormat(options, offset, ordinal));
    }

    /// <summary>
    /// Parses select.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="inPlural">The in plural.</param>
    /// <returns>The select result.</returns>
    private IcuArgumentNode ParseSelect(string name, int depth, bool inPlural) {
        if(TryConsume('}')) {
            throw Fail($"Argument '{{{name}}}' requires options (selectors with messages).");
        }

        Consume(',');
        var (options, _) = ParseOptions(depth, inPlural, allowOffset: false, allowExplicit: false);
        Consume('}');
        ValidateOptions(name, options);
        _hasComplexArgument = true;
        return new IcuArgumentNode(name, new IcuSelectFormat(options));
    }

    /// <summary>
    /// Parses simple is typed.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="create">The create.</param>
    /// <returns>The simple typed result.</returns>
    private IcuArgumentNode ParseSimpleTyped(string name, Func<string?, string?, IIcuArgumentFormat> create) {
        string? style = null;
        string? skeleton = null;
        if(!TryConsume('}')) {
            Consume(',');
            SkipWhitespace();
            if(Peek(0) == ':') {
                skeleton = ParseSkeleton();
            } else {
                style = ParseWord("style");
            }

            SkipWhitespace();
            Consume('}');
        }

        _hasComplexArgument = true;
        return new IcuArgumentNode(name, create(style, skeleton));
    }

    /// <summary>
    /// Parses skeleton.
    /// </summary>
    /// <returns>The skeleton text.</returns>
    private string ParseSkeleton() {
        // Current character is ':'.
        Consume(':');
        Consume(':');
        var start = _position;
        while(!IsAtEnd() && _pattern[_position] != '}') {
            var current = _pattern[_position];
            if(!IsSkeletonChar(current)) {
                throw Fail($"Invalid skeleton character '{current}': skeletons carry only letters, digits, '.', '/', '-', '+', '_', '#', and spaces.");
            }

            _position++;
        }

        var skeleton = _pattern[start.._position].Trim();
        if(skeleton.Length == 0) {
            throw Fail("Empty '::' skeleton.");
        }

        return skeleton;
    }

    /// <summary>
    /// Parses options.
    /// </summary>
    /// <param name="depth">The depth.</param>
    /// <param name="inPlural">The in plural.</param>
    /// <param name="allowOffset">The allow offset.</param>
    /// <param name="allowExplicit">The allow explicit.</param>
    /// <returns>The options result.</returns>
    private (List<IcuOption> Options, decimal? Offset) ParseOptions(int depth, bool inPlural, bool allowOffset, bool allowExplicit) {
        decimal? offset = null;
        SkipWhitespace();
        if(allowOffset && IsOffsetDirective()) {
            _position += "offset".Length;
            SkipWhitespace();
            Consume(':');
            SkipWhitespace();
            offset = ParseDecimal("offset value");
            SkipWhitespace();
        }

        var options = new List<IcuOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while(!IsAtEnd() && _pattern[_position] != '}') {
            var option = ParseOption(depth, inPlural, allowExplicit);
            var identity = option.ExplicitValue.HasValue
                ? "=" + option.ExplicitValue.Value.ToString(CultureInfo.InvariantCulture)
                : "keyword:" + option.Keyword;
            var display = option.ExplicitValue.HasValue ? identity : option.Keyword ?? string.Empty;
            if(!seen.Add(identity)) {
                throw Fail($"Duplicate selector '{display}' in one argument.");
            }

            options.Add(option);
            SkipWhitespace();
        }

        if(IsAtEnd()) {
            throw Fail("Unterminated argument: options are never closed.");
        }

        if(options.Count == 0) {
            throw Fail("An argument has no options; at least one selector with a message is required.");
        }

        return (options, offset);
    }

    /// <summary>
    /// Parses option.
    /// </summary>
    /// <param name="depth">The depth.</param>
    /// <param name="inPlural">The in plural.</param>
    /// <param name="allowExplicit">The allow explicit.</param>
    /// <returns>The option result.</returns>
    private IcuOption ParseOption(int depth, bool inPlural, bool allowExplicit) {
        SkipWhitespace();
        decimal? explicitValue = null;
        string? keyword = null;
        if(TryConsume('=')) {
            if(!allowExplicit) {
                throw Fail("Explicit '=number' selectors are only allowed in plural arguments.");
            }

            SkipWhitespace();
            explicitValue = ParseDecimal("explicit selector value");
        } else {
            keyword = ParseWord("selector");
        }

        SkipWhitespace();
        Consume('{');
        var message = ParseMessage(depth + 1, inPlural);
        Consume('}');
        return new IcuOption(explicitValue, keyword, message);
    }

    /// <summary>
    /// Validates options.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="options">The options instance.</param>
    private void ValidateOptions(string name, List<IcuOption> options) {
        if(!options.Any(static option => string.Equals(option.Keyword, "other", StringComparison.Ordinal))) {
            throw Fail($"Argument '{{{name}}}' requires an 'other' option.");
        }
    }

    /// <summary>
    /// Determines whether the value is offset directive.
    /// </summary>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private bool IsOffsetDirective() {
        const string directive = "offset";
        if(_position + directive.Length >= _pattern.Length) {
            return false;
        }

        if(!string.Equals(_pattern.Substring(_position, directive.Length), directive, StringComparison.Ordinal)) {
            return false;
        }

        var index = _position + directive.Length;
        while(index < _pattern.Length && IsWhitespace(_pattern[index])) {
            index++;
        }

        return index < _pattern.Length && _pattern[index] == ':';
    }

    /// <summary>
    /// Parses name.
    /// </summary>
    /// <returns>The name text.</returns>
    private string ParseName() {
        var start = _position;
        while(!IsAtEnd() && IsNameChar(_pattern[_position])) {
            _position++;
        }

        if(_position == start) {
            throw Fail("Expected an argument name.");
        }

        return _pattern[start.._position];
    }

    /// <summary>
    /// Parses word.
    /// </summary>
    /// <param name="what">The what.</param>
    /// <returns>The word text.</returns>
    private string ParseWord(string what) {
        var start = _position;
        while(!IsAtEnd() && IsWordChar(_pattern[_position])) {
            _position++;
        }

        if(_position == start) {
            throw Fail($"Expected {what}.");
        }

        return _pattern[start.._position];
    }

    /// <summary>
    /// Parses decimal.
    /// </summary>
    /// <param name="what">The what.</param>
    /// <returns>The decimal value.</returns>
    private decimal ParseDecimal(string what) {
        var start = _position;
        if(!IsAtEnd() && _pattern[_position] is '+' or '-') {
            _position++;
        }

        var hasDigits = false;
        while(!IsAtEnd() && char.IsAsciiDigit(_pattern[_position])) {
            _position++;
            hasDigits = true;
        }

        if(!IsAtEnd() && _pattern[_position] == '.') {
            _position++;
            while(!IsAtEnd() && char.IsAsciiDigit(_pattern[_position])) {
                _position++;
                hasDigits = true;
            }
        }

        var text = _pattern[start.._position];
        if(!hasDigits || !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) {
            throw Fail($"Expected {what} as a decimal number.");
        }

        return value;
    }

    /// <summary>
    /// Performs the skip whitespace operation.
    /// </summary>
    private void SkipWhitespace() {
        while(!IsAtEnd() && IsWhitespace(_pattern[_position])) {
            _position++;
        }
    }

    /// <summary>
    /// Performs the consume operation.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    private void Consume(char expected) {
        if(IsAtEnd() || _pattern[_position] != expected) {
            throw Fail($"Expected '{expected}'.");
        }

        _position++;
    }

    /// <summary>
    /// Attempts to consume.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private bool TryConsume(char expected) {
        if(!IsAtEnd() && _pattern[_position] == expected) {
            _position++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs the peek operation.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The peek result.</returns>
    private char Peek(int offset) {
        var index = _position + offset;
        return index < _pattern.Length ? _pattern[index] : '\0';
    }

    /// <summary>
    /// Determines whether the value is at end.
    /// </summary>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private bool IsAtEnd() => _position >= _pattern.Length;

    /// <summary>
    /// Determines whether the value is whitespace.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';

    /// <summary>
    /// Determines whether the value is skeleton char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsSkeletonChar(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '.' or '/' or '-' or '+' or '_' or '#' or ' ' or '\t';

    /// <summary>
    /// Determines whether the value is name char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsNameChar(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';

    /// <summary>
    /// Determines whether the value is word char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWordChar(char value) => char.IsAsciiLetter(value);

    /// <summary>
    /// Performs the fail operation.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The fail result.</returns>
    private FormatException Fail(string message) =>
        new($"Invalid ICU message pattern at offset {_position}: {message} Pattern: '{_pattern}'.");
}
