//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core;

/// <summary>
/// An <see cref="IMessageFormatter"/> that renders a documented CommonMark subset in
/// trusted message templates and delegates placeholder substitution to an inner
/// formatter.
/// </summary>
/// <remarks>
/// <para>
/// Pipeline order: the template is rendered first — placeholders
/// (<c>{name}</c>) survive rendering as literal text in the supported subset — and the
/// rendered HTML is then handed to <see cref="IMessageFormatter.Format(string, IReadOnlyDictionary{string, object?})"/>
/// of the inner formatter for substitution. Because argument values are inserted
/// <em>after</em> rendering, argument content can never act as Markdown or HTML. The
/// default inner is <see cref="HtmlSafeMessageFormatter"/> (itself defaulting to
/// <see cref="DefaultMessageFormatter"/>), so <c>new MarkdownMessageFormatter()</c>
/// carries the  already-encoded guarantee by default; compose with
/// <c>new MarkdownMessageFormatter(new HtmlSafeMessageFormatter(new IcuMessageFormatter()))</c>
/// to keep ICU (International Components for Unicode) selection, and with <c>new MarkdownMessageFormatter(new DefaultMessageFormatter())</c>
/// when arguments were already encoded upstream (for example the MVC
/// <c>IHtmlLocalizer</c> bridge, which encodes through
/// <see cref="HtmlSafeMessageFormatter.EncodeArgument(object?)"/>).
/// </para>
/// <para>
/// Trust model: Markdown syntax in templates is trusted content.
/// Raw HTML is disabled by default — text nodes are encoded with
/// <see cref="HtmlEncoder.Default"/>, so <c>&lt;b&gt;</c> renders as literal text.
/// The <c>allowRawHtml</c> constructor opt-in emits text nodes verbatim for
/// fully trusted HTML-bearing Markdown. Code-span content is always HTML-encoded; link
/// destinations are always attribute-encoded (no attribute breakout). Scheme vetting of
/// destinations is the template author's responsibility, identical to  trusted
/// markup. The output is HTML content and must be emitted raw (a normal HTML-encoding
/// sink would double-encode it).
/// </para>
/// <para>
/// Documented subset. Blocks: paragraphs (soft line breaks emit
/// <c>\n</c>), unordered lists (<c>- </c>/<c>* </c>/<c>+ </c> at column 0; mixed
/// unordered markers merge into one list), ordered lists (<c>N. </c> at column 0; a
/// first marker other than <c>1</c> emits <c>start="N"</c> so numbers are never silently
/// renumbered), blank-line block separation; list items are single-line. Inlines: backslash escapes for <c>\ ` * _ [ ] ( )</c>; code spans
/// (<c>`x`</c>, single-backtick runs only — runs of two or more are literal — literal
/// content, no nesting); links <c>[label](destination)</c> (label
/// inline-processed and may not contain <c>[</c>/<c>]</c>; destination runs to the first
/// <c>)</c>); strong <c>**x**</c>/<c>__x__</c>; emphasis <c>*x*</c>/<c>_x_</c>
/// (delimiters must hug non-whitespace inner sides, must match in run length, and
/// <c>_</c> never opens or closes intraword). Constructs outside the subset render
/// literally and are never silently mangled: headings, blockquotes, code blocks/fences,
/// thematic breaks, tables, images (<c>![</c> is never a link), autolinks, reference
/// links, link titles, nested/continuation/indented lists, HTML entities (<c>&amp;amp;</c>
/// stays literal), and raw HTML (inert by default). Rendering is deterministic and
/// culture-independent (culture-sensitive substitution stays with the inner formatter).
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="MarkdownMessageFormatter"/> class.
/// </remarks>
/// <param name="inner">The formatter performing substitution after rendering. Defaults to <see cref="HtmlSafeMessageFormatter"/> when null.</param>
/// <param name="allowRawHtml">Whether raw HTML in the Markdown source passes through verbatim (fully trusted templates). Defaults to <see langword="false"/>: text is HTML-encoded and raw HTML renders inert.</param>
public sealed class MarkdownMessageFormatter(IMessageFormatter? inner = null, bool allowRawHtml = false) : IMessageFormatter {
    private readonly IMessageFormatter _inner = inner ?? new HtmlSafeMessageFormatter();
    private readonly bool _allowRawHtml = allowRawHtml;

    /// <inheritdoc/>
    public string Format(string template, IReadOnlyDictionary<string, object?> parameters) {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        return _inner.Format(RenderMarkdown(template, _allowRawHtml), parameters);
    }

    /// <summary>
    /// Renders markdown.
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <param name="allowRawHtml">The allow raw html.</param>
    /// <returns>The markdown text.</returns>
    private static string RenderMarkdown(string markdown, bool allowRawHtml) {
        var output = new StringBuilder(markdown.Length + 16);
        var lines = SplitLines(markdown);
        var index = 0;
        var firstBlock = true;

        while(index < lines.Length) {
            var line = lines[index];
            if(string.IsNullOrWhiteSpace(line)) {
                index++;
                continue;
            }

            if(TryParseListItem(line, out var listKind, out var listNumber, out _)) {
                AppendListBlock(output, lines, ref index, listKind, listNumber, allowRawHtml, ref firstBlock);
                continue;
            }

            AppendParagraphBlock(output, lines, ref index, allowRawHtml, ref firstBlock);
        }

        return output.ToString();
    }

    /// <summary>
    /// Appends one unordered or ordered list block starting at the current line.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="lines">The template lines.</param>
    /// <param name="index">The zero-based line cursor; advanced past the consumed list items.</param>
    /// <param name="listKind">The kind of list to render.</param>
    /// <param name="listNumber">The first ordered-list marker.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <param name="firstBlock">Whether no block has been emitted yet; updated after emission.</param>
    private static void AppendListBlock(StringBuilder output, string[] lines, ref int index, ListKind listKind, int listNumber, bool allowRawHtml, ref bool firstBlock) {
        var tag = listKind == ListKind.Unordered ? "ul" : "ol";
        AppendBlockSeparator(output, ref firstBlock);
        output.Append('<').Append(tag);
        if(listKind == ListKind.Ordered && listNumber != 1) {
            output.Append(" start=\"").Append(listNumber.ToString(CultureInfo.InvariantCulture)).Append('"');
        }

        output.Append('>');
        while(index < lines.Length
            && TryParseListItem(lines[index], out var currentKind, out _, out var currentText)
            && currentKind == listKind) {
            output.Append("\n<li>");
            AppendInline(output, currentText, allowRawHtml);
            output.Append("</li>");
            index++;
        }

        output.Append("\n</").Append(tag).Append('>');
    }

    /// <summary>
    /// Appends one paragraph block starting at the current line.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="lines">The template lines.</param>
    /// <param name="index">The zero-based line cursor; advanced past the consumed paragraph lines.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <param name="firstBlock">Whether no block has been emitted yet; updated after emission.</param>
    private static void AppendParagraphBlock(StringBuilder output, string[] lines, ref int index, bool allowRawHtml, ref bool firstBlock) {
        AppendBlockSeparator(output, ref firstBlock);
        output.Append("<p>");
        var firstLine = true;
        while(index < lines.Length
            && !string.IsNullOrWhiteSpace(lines[index])
            && !TryParseListItem(lines[index], out _, out _, out _)) {
            if(!firstLine) {
                output.Append('\n');
            }

            AppendInline(output, lines[index], allowRawHtml);
            firstLine = false;
            index++;
        }

        output.Append("</p>");
    }

    /// <summary>
    /// Appends block separator.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="firstBlock">The first block.</param>
    private static void AppendBlockSeparator(StringBuilder output, ref bool firstBlock) {
        if(!firstBlock) {
            output.Append('\n');
        }

        firstBlock = false;
    }

    /// <summary>
    /// Splits lines.
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <returns>The split lines sequence.</returns>
    private static string[] SplitLines(string markdown) {
        var lines = markdown.Split('\n');
        for(var i = 0; i < lines.Length; i++) {
            if(lines[i].Length > 0 && lines[i][^1] == '\r') {
                lines[i] = lines[i][..^1];
            }
        }

        return lines;
    }

    /// <summary>
    /// Attempts to parse list item.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="number">The number.</param>
    /// <param name="text">The text.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private static bool TryParseListItem(string line, out ListKind kind, out int number, out string text) {
        kind = ListKind.Unordered;
        number = 0;
        text = string.Empty;
        if(line.Length == 0) {
            return false;
        }

        if(line[0] is '-' or '*' or '+') {
            if(line.Length < 2 || line[1] != ' ') {
                return false;
            }

            kind = ListKind.Unordered;
            text = line[2..].TrimStart();
            return true;
        }

        var position = 0;
        while(position < line.Length && char.IsAsciiDigit(line[position])) {
            position++;
        }

        if(position == 0 || position + 1 >= line.Length || line[position] != '.' || line[position + 1] != ' ') {
            return false;
        }

        if(!int.TryParse(line.AsSpan(0, position), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNumber)) {
            return false;
        }

        kind = ListKind.Ordered;
        number = parsedNumber;
        text = line[(position + 2)..].TrimStart();
        return true;
    }

    /// <summary>
    /// Appends inline.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="text">The text.</param>
    /// <param name="allowRawHtml">The allow raw html.</param>
    private static void AppendInline(StringBuilder output, string text, bool allowRawHtml) {
        var index = 0;
        while(index < text.Length) {
            var current = text[index];

            if(current == '\\') {
                index = AppendEscape(output, text, index, allowRawHtml);
                continue;
            }

            if(current == '`') {
                index = AppendCodeSpanOrLiteral(output, text, index, allowRawHtml);
                continue;
            }

            if(current == '[') {
                index = AppendLinkOrLiteral(output, text, index, allowRawHtml);
                continue;
            }

            if(current is '*' or '_') {
                index = AppendEmphasisOrLiteral(output, text, index, current, allowRawHtml);
                continue;
            }

            var next = FindNextSpecial(text, index);
            AppendText(output, text[index..next], allowRawHtml);
            index = next;
        }
    }

    /// <summary>
    /// Appends one backslash escape or a literal backslash and advances past it.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="text">The inline text being rendered.</param>
    /// <param name="index">The zero-based index of the backslash.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <returns>The index after the consumed escape or backslash.</returns>
    private static int AppendEscape(StringBuilder output, string text, int index, bool allowRawHtml) {
        if(index + 1 < text.Length && IsEscapable(text[index + 1])) {
            AppendText(output, text[index + 1].ToString(), allowRawHtml);
            return index + 2;
        }

        AppendText(output, "\\", allowRawHtml);
        return index + 1;
    }

    /// <summary>
    /// Appends one single-backtick code span, or the literal backtick run and its length when the
    /// span is not closed.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="text">The inline text being rendered.</param>
    /// <param name="index">The zero-based index of the backtick run.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <returns>The index after the consumed code span or literal run.</returns>
    private static int AppendCodeSpanOrLiteral(StringBuilder output, string text, int index, bool allowRawHtml) {
        var runLength = CountRun(text, index, '`');
        if(runLength == 1) {
            var close = FindClosingBacktick(text, index + 1);
            if(close >= 0) {
                output.Append("<code>").Append(HtmlEncoder.Default.Encode(text[(index + 1)..close])).Append("</code>");
                return close + 1;
            }
        }

        AppendText(output, text[index..(index + runLength)], allowRawHtml);
        return index + runLength;
    }

    /// <summary>
    /// Finds the next single-backtick run that can close a code span.
    /// </summary>
    /// <param name="text">The inline text being rendered.</param>
    /// <param name="search">The zero-based index to start searching from.</param>
    /// <returns>The index of the closing backtick, or <c>-1</c> when no single backtick follows.</returns>
    private static int FindClosingBacktick(string text, int search) {
        while(true) {
            var candidate = text.IndexOf('`', search);
            if(candidate < 0) {
                return -1;
            }

            var candidateRun = CountRun(text, candidate, '`');
            if(candidateRun == 1) {
                return candidate;
            }

            search = candidate + candidateRun;
        }
    }

    /// <summary>
    /// Appends one link, or a literal opening bracket and advances past it.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="text">The inline text being rendered.</param>
    /// <param name="index">The zero-based index of the opening bracket.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <returns>The index after the consumed link or bracket.</returns>
    private static int AppendLinkOrLiteral(StringBuilder output, string text, int index, bool allowRawHtml) {
        if((index == 0 || text[index - 1] != '!') && TryParseLink(text, index, out var label, out var destination, out var end)) {
            output.Append("<a href=\"").Append(HtmlEncoder.Default.Encode(destination)).Append("\">");
            AppendInline(output, label, allowRawHtml);
            output.Append("</a>");
            return end;
        }

        AppendText(output, "[", allowRawHtml);
        return index + 1;
    }

    /// <summary>
    /// Appends one strong or emphasis span, or the literal delimiter run and its length when the
    /// span cannot be parsed.
    /// </summary>
    /// <param name="output">The rendered HTML sink.</param>
    /// <param name="text">The inline text being rendered.</param>
    /// <param name="index">The zero-based index of the delimiter run.</param>
    /// <param name="delimiter">The delimiter character, <c>*</c> or <c>_</c>.</param>
    /// <param name="allowRawHtml">Whether raw HTML passes through verbatim.</param>
    /// <returns>The index after the consumed span or literal run.</returns>
    private static int AppendEmphasisOrLiteral(StringBuilder output, string text, int index, char delimiter, bool allowRawHtml) {
        var runLength = CountRun(text, index, delimiter);
        if(runLength <= 2 && TryParseEmphasis(text, index, runLength, delimiter, out var inner, out var end)) {
            output.Append(runLength == 2 ? "<strong>" : "<em>");
            AppendInline(output, inner, allowRawHtml);
            output.Append(runLength == 2 ? "</strong>" : "</em>");
            return end;
        }

        AppendText(output, text[index..(index + runLength)], allowRawHtml);
        return index + runLength;
    }

    /// <summary>
    /// Appends text.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="allowRawHtml">The allow raw html.</param>
    private static void AppendText(StringBuilder output, string segment, bool allowRawHtml) {
        if(allowRawHtml) {
            output.Append(segment);
        } else {
            output.Append(HtmlEncoder.Default.Encode(segment));
        }
    }

    /// <summary>
    /// Finds next special.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <returns>The next special value.</returns>
    private static int FindNextSpecial(string text, int start) {
        var index = start;
        while(index < text.Length && text[index] is not ('\\' or '`' or '[' or '*' or '_')) {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Attempts to parse link.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <param name="label">The label.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="end">The end.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private static bool TryParseLink(string text, int start, out string label, out string destination, out int end) {
        label = string.Empty;
        destination = string.Empty;
        end = 0;

        var close = text.IndexOf(']', start + 1);
        if(close < 0 || close + 1 >= text.Length || text[close + 1] != '(') {
            return false;
        }

        var destinationEnd = text.IndexOf(')', close + 2);
        if(destinationEnd < 0) {
            return false;
        }

        var candidateLabel = text[(start + 1)..close];
        if(candidateLabel.Contains('[')) {
            return false;
        }

        label = candidateLabel;
        destination = text[(close + 2)..destinationEnd];
        end = destinationEnd + 1;
        return true;
    }

    /// <summary>
    /// Attempts to parse emphasis.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <param name="runLength">The run length.</param>
    /// <param name="delimiter">The delimiter.</param>
    /// <param name="inner">The inner.</param>
    /// <param name="end">The end.</param>
    /// <returns>true when the operation succeeds; otherwise, false.</returns>
    private static bool TryParseEmphasis(string text, int start, int runLength, char delimiter, out string inner, out int end) {
        inner = string.Empty;
        end = 0;

        var contentStart = start + runLength;
        if(contentStart >= text.Length || char.IsWhiteSpace(text[contentStart])) {
            return false;
        }

        if(delimiter == '_' && start > 0 && IsWordChar(text[start - 1])) {
            return false;
        }

        var search = contentStart;
        while(true) {
            var candidate = text.IndexOf(delimiter, search);
            if(candidate < 0) {
                return false;
            }

            var runHere = CountRun(text, candidate, delimiter);
            var runEnd = candidate + runHere;
            if(candidate > contentStart
                && !char.IsWhiteSpace(text[candidate - 1])
                && runHere == runLength
                && (delimiter != '_' || runEnd >= text.Length || !IsWordChar(text[runEnd]))) {
                inner = text[contentStart..candidate];
                end = runEnd;
                return true;
            }

            search = candidate + runHere;
        }
    }

    /// <summary>
    /// Performs the count run operation.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <param name="delimiter">The delimiter.</param>
    /// <returns>The count run value.</returns>
    private static int CountRun(string text, int start, char delimiter) {
        var index = start;
        while(index < text.Length && text[index] == delimiter) {
            index++;
        }

        return index - start;
    }

    /// <summary>
    /// Determines whether the value is escapable.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsEscapable(char value) => value is '\\' or '`' or '*' or '_' or '[' or ']' or '(' or ')';

    /// <summary>
    /// Determines whether the value is word char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsWordChar(char value) => char.IsLetterOrDigit(value);

    /// <summary>
    /// Specifies the list kind values.
    /// </summary>
    private enum ListKind {
        Unordered = 0,
        Ordered = 1,
    }
}
