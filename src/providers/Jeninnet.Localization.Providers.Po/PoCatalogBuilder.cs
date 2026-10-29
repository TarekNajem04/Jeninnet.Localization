//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Providers.Po;

/// <summary>
/// The single file→catalog mapping owner: both carriers — the PO (Portable Object) text
/// parser and the MO (Machine Object) binary reader — feed parsed <see cref="PoEntry"/> values here,
/// so flat keys, contexts (<c>msgctxt</c> + EOT (End of Transmission),), plural pair keys and
/// pair slots, duplicate claims, header parsing, and <c>Plural-Forms</c> validation
/// exist once. Produces the same <see cref="PoCatalog"/> the plural-selection path
/// consumes.
/// </summary>
internal sealed class PoCatalogBuilder {
    private readonly Dictionary<string, string> _headers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _plurals = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedExactKeys = new(StringComparer.Ordinal);
    private string? _pluralFormsValue;
    private int _pluralFormsLine;
    private bool _headerSeen;

    /// <summary>
    /// Adds the header entry (<c>msgid ""</c>) fields.
    /// </summary>
    /// <param name="headerBlock">The header block (the header entry's translation).</param>
    /// <param name="filePath">The catalog file path (diagnostics).</param>
    /// <param name="lineNumber">The 1-based line number of the header entry (diagnostics).</param>
    /// <exception cref="PoParseException">Thrown when a second header entry is added.</exception>
    private void AddHeader(string headerBlock, string filePath, int lineNumber) {
        if(_headerSeen) {
            throw new PoParseException(filePath, lineNumber, "Duplicate header entry (msgid \"\")");
        }

        _headerSeen = true;
        _pluralFormsLine = lineNumber;
        foreach(var pair in PoTextParsing.SplitHeaders(headerBlock)) {
            _headers[pair.Key] = pair.Value;
            if(pair.Key.Equals("Plural-Forms", StringComparison.OrdinalIgnoreCase)) {
                _pluralFormsValue = pair.Value;
            }
        }
    }

    /// <summary>
    /// Adds one parsed entry: the header entry (<c>msgid ""</c>) contributes the
    /// header fields; every other entry contributes flat/contextual templates,
    /// plural tables, and their pair-slot projections, with duplicate-key claims.
    /// </summary>
    /// <param name="entry">The parsed entry.</param>
    /// <param name="filePath">The catalog file path (diagnostics).</param>
    /// <exception cref="PoParseException">Thrown on duplicate header entries, duplicate keys, or pair collisions.</exception>
    internal void AddEntry(PoEntry entry, string filePath) {
        var context = entry.Context is { Length: 0 } ? null : entry.Context;

        if(entry.MsgId.Length == 0 && context is null && entry.MsgIdPlural is null) {
            AddHeader(entry.Forms[0], filePath, entry.LineNumber);
            return;
        }

        if(entry.MsgIdPlural is not { } msgIdPlural) {
            AddSingular(entry, context, filePath);
            return;
        }

        AddPlural(entry, context, msgIdPlural, filePath);
    }

    /// <summary>
    /// Adds one singular entry: the flat/contextual template with its duplicate-key claim.
    /// </summary>
    /// <param name="entry">The parsed entry.</param>
    /// <param name="context">The normalized context, or <see langword="null"/>.</param>
    /// <param name="filePath">The catalog file path (diagnostics).</param>
    /// <exception cref="PoParseException">Thrown on a duplicate key.</exception>
    private void AddSingular(PoEntry entry, string? context, string filePath) {
        var key = context is null ? entry.MsgId : context + PoCatalog.CONTEXT_SEPARATOR + entry.MsgId;
        if(!_claimedExactKeys.Add(key)) {
            throw new PoParseException(filePath, entry.LineNumber, $"Duplicate message key '{entry.MsgId}'");
        }

        if(entry.Forms[0].Length != 0) {
            _messages[key] = entry.Forms[0];
        }
    }

    /// <summary>
    /// Adds one plural entry: the plural table and the pair-slot projections, with
    /// duplicate claims for the table and both exact keys.
    /// </summary>
    /// <param name="entry">The parsed entry.</param>
    /// <param name="context">The normalized context, or <see langword="null"/>.</param>
    /// <param name="msgIdPlural">The entry's <c>msgid_plural</c>.</param>
    /// <param name="filePath">The catalog file path (diagnostics).</param>
    /// <exception cref="PoParseException">Thrown on a duplicate plural entry or key.</exception>
    private void AddPlural(PoEntry entry, string? context, string msgIdPlural, string filePath) {
        var pluralKey = context is null
            ? entry.MsgId + '\0' + msgIdPlural
            : context + '\0' + entry.MsgId + '\0' + msgIdPlural;

        if(_plurals.ContainsKey(pluralKey)) {
            throw new PoParseException(filePath, entry.LineNumber, $"Duplicate plural entry for msgid '{entry.MsgId}'");
        }

        _plurals[pluralKey] = entry.Forms;

        var singularExact = context is null ? entry.MsgId : context + PoCatalog.CONTEXT_SEPARATOR + entry.MsgId;
        var pluralExact = context is null ? msgIdPlural : context + PoCatalog.CONTEXT_SEPARATOR + msgIdPlural;
        if(!_claimedExactKeys.Add(singularExact)) {
            throw new PoParseException(filePath, entry.LineNumber, $"Duplicate message key '{entry.MsgId}'");
        }

        if(!_claimedExactKeys.Add(pluralExact)) {
            throw new PoParseException(filePath, entry.LineNumber, $"Duplicate message key '{msgIdPlural}'");
        }

        if(entry.Forms.Length > 0 && entry.Forms[0].Length != 0) {
            _messages[singularExact] = entry.Forms[0];
        }

        if(entry.Forms.Length > 1) {
            if(entry.Forms[1].Length != 0) {
                _messages[pluralExact] = entry.Forms[1];
            }
        } else if(entry.Forms.Length == 1 && entry.Forms[0].Length != 0) {
            _messages[pluralExact] = entry.Forms[0];
        }
    }

    /// <summary>
    /// Builds the catalog: parses the collected <c>Plural-Forms</c> header (if any)
    /// and validates every plural table against its nplurals.
    /// </summary>
    /// <param name="filePath">The catalog file path (diagnostics).</param>
    /// <returns>The parsed catalog.</returns>
    /// <exception cref="PoParseException">Thrown when the header is malformed or a table mismatches nplurals.</exception>
    internal PoCatalog Build(string filePath) {
        PoPluralForms? pluralForms = null;
        if(_pluralFormsValue is not null) {
            pluralForms = PoPluralForms.Parse(_pluralFormsValue, filePath, _pluralFormsLine);
        }

        if(pluralForms is not null) {
            var mismatch = _plurals.Values.FirstOrDefault(forms => forms.Length != pluralForms.NPlurals);
            if(mismatch is not null) {
                throw new PoParseException(
                    filePath,
                    0,
                    $"Plural entry has {mismatch.Length.ToString(CultureInfo.InvariantCulture)} forms but Plural-Forms declares nplurals={pluralForms.NPlurals.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        return new PoCatalog(_headers, pluralForms, _messages, _plurals);
    }
}
