//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
#pragma warning disable S1694 // Abstract Node with inheritable Evaluate behavior is intentional — the expression-tree base for the plural-formula evaluator
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// The parsed gettext <c>Plural-Forms</c> header (<c>nplurals</c> plus the C-expression
/// <c>plural=</c> formula). Evaluates the form index for a count.
/// </summary>
internal sealed class PoPluralForms {
    /// <summary>
    /// Gets the number of plural forms.
    /// </summary>
    internal int NPlurals { get; }

    private readonly Node _root;

    /// <summary>
    /// Initializes a new instance of the PoPluralForms class.
    /// </summary>
    /// <param name="nplurals">The nplurals.</param>
    /// <param name="root">The root.</param>
    private PoPluralForms(int nplurals, Node root) {
        NPlurals = nplurals;
        _root = root;
    }

    /// <summary>
    /// Evaluates the form index for count <paramref name="n"/>.
    /// Returns <c>-1</c> when the formula cannot produce a usable index
    /// (division by zero or an out-of-range result); the caller treats that as a miss.
    /// </summary>
    /// <param name="n">The (non-negative) count.</param>
    /// <returns>The form index, or <c>-1</c> when unusable.</returns>
    internal int Evaluate(long n) {
        long result;
        try {
            result = _root.Evaluate(n);
        }
        catch(DivideByZeroException) {
            return -1;
        }

        return result >= 0 && result < NPlurals ? (int)result : -1;
    }

    /// <summary>
    /// Parses a <c>Plural-Forms</c> header value (for example
    /// <c>"nplurals=2; plural=(n != 1);"</c>).
    /// </summary>
    /// <param name="headerValue">The header value, or <see langword="null"/> when absent.</param>
    /// <param name="filePath">The PO (Portable Object) file path (diagnostics).</param>
    /// <param name="lineNumber">The 1-based line number of the header entry.</param>
    /// <returns>The parsed forms, or <see langword="null"/> when <paramref name="headerValue"/> is absent.</returns>
    /// <exception cref="PoParseException">Thrown when the header is present but malformed.</exception>
    internal static PoPluralForms? Parse(string? headerValue, string filePath, int lineNumber) {
        if(string.IsNullOrWhiteSpace(headerValue)) {
            return null;
        }

        var npluralsText = Extract(headerValue, "nplurals", filePath, lineNumber);
        var pluralText = Extract(headerValue, "plural", filePath, lineNumber);

        if(!int.TryParse(npluralsText, NumberStyles.None, CultureInfo.InvariantCulture, out var nplurals) || nplurals < 1 || nplurals > 6) {
            throw new PoParseException(filePath, lineNumber, $"Invalid Plural-Forms nplurals '{npluralsText}': expected an integer 1-6");
        }

        if(string.IsNullOrWhiteSpace(pluralText)) {
            throw new PoParseException(filePath, lineNumber, "Invalid Plural-Forms header: missing plural expression");
        }

        var parser = new ExpressionParser(pluralText, filePath, lineNumber);
        var root = parser.ParseExpression();
        parser.EnsureEnd();

        return new PoPluralForms(nplurals, root);
    }

    /// <summary>
    /// Performs the extract operation.
    /// </summary>
    /// <param name="headerValue">The header value.</param>
    /// <param name="key">The message key.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <returns>The extract text.</returns>
    private static string Extract(string headerValue, string key, string filePath, int lineNumber) {
        var search = key + "=";
        var start = headerValue.IndexOf(search, StringComparison.Ordinal);
        if(start < 0) {
            throw new PoParseException(filePath, lineNumber, $"Invalid Plural-Forms header: missing '{key}=' in '{headerValue}'");
        }

        start += search.Length;
        var end = headerValue.IndexOf(';', start);
        var raw = (end < 0 ? headerValue[start..] : headerValue[start..end]).Trim();
        if(raw.Length == 0) {
            throw new PoParseException(filePath, lineNumber, $"Invalid Plural-Forms header: empty '{key}=' in '{headerValue}'");
        }

        return raw;
    }

    /// <summary>
    /// Represents a node in the parsed model.
    /// </summary>
    private abstract class Node {
        /// <summary>
        /// Performs the evaluate operation.
        /// </summary>
        /// <param name="n">The n.</param>
        /// <returns>The evaluate value.</returns>
        internal abstract long Evaluate(long n);
    }

    /// <summary>
    /// Represents a constant node in the parsed model.
    /// </summary>
    /// <param name="value">The value.</param>
    private sealed class ConstantNode(long value) : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) => value;
    }

    /// <summary>
    /// Represents a variable node in the parsed model.
    /// </summary>
    private sealed class VariableNode : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) => n;
    }

    /// <summary>
    /// Represents a not node in the parsed model.
    /// </summary>
    /// <param name="operand">The operand.</param>
    private sealed class NotNode(Node operand) : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) => operand.Evaluate(n) == 0 ? 1 : 0;
    }

    /// <summary>
    /// Represents a negate node in the parsed model.
    /// </summary>
    /// <param name="operand">The operand.</param>
    private sealed class NegateNode(Node operand) : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) => -operand.Evaluate(n);
    }

    /// <summary>
    /// Specifies the binary operator values.
    /// </summary>
    private enum BinaryOperator {
        Or = 0,
        And = 1,
        Equal = 2,
        NotEqual = 3,
        Less = 4,
        Greater = 5,
        LessOrEqual = 6,
        GreaterOrEqual = 7,
        Add = 8,
        Subtract = 9,
        Multiply = 10,
        Divide = 11,
        Modulo = 12
    }

    /// <summary>
    /// Represents a binary node in the parsed model.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="op">The op.</param>
    /// <param name="right">The right.</param>
    private sealed class BinaryNode(Node left, BinaryOperator op, Node right) : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) {
            if(op is BinaryOperator.Or) {
                return left.Evaluate(n) != 0 || right.Evaluate(n) != 0 ? 1 : 0;
            }

            if(op is BinaryOperator.And) {
                return left.Evaluate(n) != 0 && right.Evaluate(n) != 0 ? 1 : 0;
            }

            var l = left.Evaluate(n);
            var r = right.Evaluate(n);
            return op switch {
                BinaryOperator.Equal => ToLong(l == r),
                BinaryOperator.NotEqual => ToLong(l != r),
                BinaryOperator.Less => ToLong(l < r),
                BinaryOperator.Greater => ToLong(l > r),
                BinaryOperator.LessOrEqual => ToLong(l <= r),
                BinaryOperator.GreaterOrEqual => ToLong(l >= r),
                BinaryOperator.Add => l + r,
                BinaryOperator.Subtract => l - r,
                BinaryOperator.Multiply => l * r,
                BinaryOperator.Divide => Divide(l, r),
                BinaryOperator.Modulo => Modulo(l, r),
                _ => throw new InvalidOperationException($"Unknown operator {op}"),
            };
        }

        /// <summary>
        /// Maps a boolean expression result to the formula's <c>0</c>/<c>1</c> shape.
        /// </summary>
        /// <param name="value">The expression result.</param>
        /// <returns><c>1</c> when true; otherwise <c>0</c>.</returns>
        private static long ToLong(bool value) => value ? 1 : 0;

        /// <summary>
        /// Divides, rejecting a zero divisor.
        /// </summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The quotient.</returns>
        /// <exception cref="DivideByZeroException">Thrown when the divisor is zero.</exception>
        private static long Divide(long left, long right) =>
            right == 0 ? throw new DivideByZeroException() : left / right;

        /// <summary>
        /// Computes the remainder, rejecting a zero divisor.
        /// </summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The remainder.</returns>
        /// <exception cref="DivideByZeroException">Thrown when the divisor is zero.</exception>
        private static long Modulo(long left, long right) =>
            right == 0 ? throw new DivideByZeroException() : left % right;
    }

    /// <summary>
    /// Represents a conditional node in the parsed model.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="whenTrue">The when true.</param>
    /// <param name="whenFalse">The when false.</param>
    private sealed class ConditionalNode(Node condition, Node whenTrue, Node whenFalse) : Node {
        /// <inheritdoc/>
        internal override long Evaluate(long n) =>
            condition.Evaluate(n) != 0 ? whenTrue.Evaluate(n) : whenFalse.Evaluate(n);
    }

    /// <summary>
    /// Represents an expression parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="lineNumber">The line number.</param>
    private sealed class ExpressionParser(string text, string filePath, int lineNumber) {
        private readonly string _text = text;
        private int _position;

        /// <summary>
        /// Parses expression.
        /// </summary>
        /// <returns>The expression result.</returns>
        internal Node ParseExpression() => ParseConditional();

        /// <summary>
        /// Ensures end.
        /// </summary>
        internal void EnsureEnd() {
            SkipWhitespace();
            if(_position != _text.Length) {
                throw new PoParseException(filePath, lineNumber, $"Invalid plural expression '{_text}': unexpected '{_text[_position]}'");
            }
        }

        /// <summary>
        /// Parses conditional.
        /// </summary>
        /// <returns>The conditional result.</returns>
        private Node ParseConditional() {
            var condition = ParseOr();
            SkipWhitespace();
            if(!TryConsume('?')) {
                return condition;
            }

            var whenTrue = ParseConditional();
            SkipWhitespace();
            if(!TryConsume(':')) {
                throw new PoParseException(filePath, lineNumber, $"Invalid plural expression '{_text}': expected ':' in conditional");
            }

            var whenFalse = ParseConditional();
            return new ConditionalNode(condition, whenTrue, whenFalse);
        }

        /// <summary>
        /// Parses or.
        /// </summary>
        /// <returns>The or result.</returns>
        private Node ParseOr() {
            var left = ParseAnd();
            while(true) {
                SkipWhitespace();
                if(!TryConsumeOr()) {
                    return left;
                }

                var right = ParseAnd();
                left = new BinaryNode(left, BinaryOperator.Or, right);
            }
        }

        /// <summary>
        /// Parses and.
        /// </summary>
        /// <returns>The and result.</returns>
        private Node ParseAnd() {
            var left = ParseEquality();
            while(true) {
                SkipWhitespace();
                if(!TryConsumeAnd()) {
                    return left;
                }

                var right = ParseEquality();
                left = new BinaryNode(left, BinaryOperator.And, right);
            }
        }

        /// <summary>
        /// Parses equality.
        /// </summary>
        /// <returns>The equality result.</returns>
        private Node ParseEquality() {
            var left = ParseRelational();
            while(true) {
                SkipWhitespace();
                if(TryConsume("==")) {
                    left = new BinaryNode(left, BinaryOperator.Equal, ParseRelational());
                } else if(TryConsume("!=")) {
                    left = new BinaryNode(left, BinaryOperator.NotEqual, ParseRelational());
                } else {
                    return left;
                }
            }
        }

        /// <summary>
        /// Parses relational.
        /// </summary>
        /// <returns>The relational result.</returns>
        private Node ParseRelational() {
            var left = ParseAdditive();
            while(true) {
                SkipWhitespace();
                if(TryConsume("<=")) {
                    left = new BinaryNode(left, BinaryOperator.LessOrEqual, ParseAdditive());
                } else if(TryConsume(">=")) {
                    left = new BinaryNode(left, BinaryOperator.GreaterOrEqual, ParseAdditive());
                } else if(TryConsume("<")) {
                    left = new BinaryNode(left, BinaryOperator.Less, ParseAdditive());
                } else if(TryConsume(">")) {
                    left = new BinaryNode(left, BinaryOperator.Greater, ParseAdditive());
                } else {
                    return left;
                }
            }
        }

        /// <summary>
        /// Parses additive.
        /// </summary>
        /// <returns>The additive result.</returns>
        private Node ParseAdditive() {
            var left = ParseMultiplicative();
            while(true) {
                SkipWhitespace();
                if(TryConsume("+")) {
                    left = new BinaryNode(left, BinaryOperator.Add, ParseMultiplicative());
                } else if(TryConsume("-")) {
                    left = new BinaryNode(left, BinaryOperator.Subtract, ParseMultiplicative());
                } else {
                    return left;
                }
            }
        }

        /// <summary>
        /// Parses multiplicative.
        /// </summary>
        /// <returns>The multiplicative result.</returns>
        private Node ParseMultiplicative() {
            var left = ParseUnary();
            while(true) {
                SkipWhitespace();
                if(TryConsume("*")) {
                    left = new BinaryNode(left, BinaryOperator.Multiply, ParseUnary());
                } else if(TryConsume("/")) {
                    left = new BinaryNode(left, BinaryOperator.Divide, ParseUnary());
                } else if(TryConsume("%")) {
                    left = new BinaryNode(left, BinaryOperator.Modulo, ParseUnary());
                } else {
                    return left;
                }
            }
        }

        /// <summary>
        /// Parses unary.
        /// </summary>
        /// <returns>The unary result.</returns>
        private Node ParseUnary() {
            SkipWhitespace();
            if(TryConsume("!")) {
                return new NotNode(ParseUnary());
            }

            if(TryConsume("-")) {
                return new NegateNode(ParseUnary());
            }

            return ParsePrimary();
        }

        /// <summary>
        /// Parses primary.
        /// </summary>
        /// <returns>The primary result.</returns>
        private Node ParsePrimary() {
            SkipWhitespace();
            if(TryConsume("(")) {
                var inner = ParseConditional();
                SkipWhitespace();
                if(!TryConsume(")")) {
                    throw new PoParseException(filePath, lineNumber, $"Invalid plural expression '{_text}': expected ')'");
                }

                return inner;
            }

            if(_position < _text.Length && _text[_position] == 'n') {
                _position++;
                return new VariableNode();
            }

            var start = _position;
            while(_position < _text.Length && char.IsAsciiDigit(_text[_position])) {
                _position++;
            }

            if(start == _position) {
                var found = _position < _text.Length ? $"'{_text[_position]}'" : "end of expression";
                throw new PoParseException(filePath, lineNumber, $"Invalid plural expression '{_text}': unexpected {found}");
            }

            var digits = _text[start.._position];
            if(!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) {
                throw new PoParseException(filePath, lineNumber, $"Invalid plural expression '{_text}': bad number '{digits}'");
            }

            return new ConstantNode(value);
        }

        /// <summary>
        /// Performs the skip whitespace operation.
        /// </summary>
        private void SkipWhitespace() {
            while(_position < _text.Length && char.IsWhiteSpace(_text[_position])) {
                _position++;
            }
        }

        /// <summary>
        /// Attempts to consume.
        /// </summary>
        /// <param name="c">The c.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryConsume(char c) {
            if(_position < _text.Length && _text[_position] == c) {
                _position++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to consume.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryConsume(string s) {
            if(_text.AsSpan(_position).StartsWith(s.AsSpan(), StringComparison.Ordinal)) {
                _position += s.Length;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to consume or.
        /// </summary>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryConsumeOr() {
            var saved = _position;
            if(TryConsume("||")) {
                return true;
            }

            _position = saved;
            return false;
        }

        /// <summary>
        /// Attempts to consume and.
        /// </summary>
        /// <returns>true when the operation succeeds; otherwise, false.</returns>
        private bool TryConsumeAnd() {
            var saved = _position;
            if(TryConsume("&&")) {
                return true;
            }

            _position = saved;
            return false;
        }
    }
}
