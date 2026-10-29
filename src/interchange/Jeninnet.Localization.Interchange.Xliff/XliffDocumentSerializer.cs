//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Interchange.Xliff;

/// <summary>
/// Builds and serializes <see cref="XliffDocument"/> instances.
/// </summary>
internal static class XliffDocumentSerializer {
    /// <summary>
    /// Builds a document from flat catalogs plus optional plural tables. Flat pair keys
    /// covered by a plural group are emitted as form units only (tables win, no duplication).
    /// Optional translator notes attach to flat units by catalog key; null/empty
    /// note values are skipped (they are equivalent to no note).
    /// </summary>
    /// <param name="source">The source catalog.</param>
    /// <param name="target">The optional target catalog.</param>
    /// <param name="srcLang">The source language tag.</param>
    /// <param name="trgLang">The target language tag.</param>
    /// <param name="fileId">The XLIFF (XML Localization Interchange File Format) file id.</param>
    /// <param name="pluralEntries">The optional plural tables.</param>
    /// <param name="translatorNotes">The optional translator notes by catalog key.</param>
    /// <returns>The built document.</returns>
    /// <exception cref="ArgumentException">Thrown when a language tag or file id is empty, a catalog key is empty, or a source template is <see langword="null"/>.</exception>
    internal static XliffDocument BuildDocument(
        IReadOnlyDictionary<string, string?> source,
        IReadOnlyDictionary<string, string?>? target,
        string srcLang,
        string trgLang,
        string fileId,
        IReadOnlyList<XliffPluralEntry>? pluralEntries,
        IReadOnlyDictionary<string, string>? translatorNotes) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(srcLang);
        ArgumentException.ThrowIfNullOrEmpty(trgLang);
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var units = new List<XliffUnit>();
        var emittedIds = new HashSet<string>(StringComparer.Ordinal);
        var groupedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach(var group in pluralEntries ?? []) {
            ValidatePluralEntry(group);
            groupedKeys.Add(FlatKey(group.Context, group.SingularKey));
            groupedKeys.Add(FlatKey(group.Context, group.PluralKey));
        }

        foreach(var key in source.Keys.Order(StringComparer.Ordinal)) {
            if(string.IsNullOrEmpty(key)) {
                throw new ArgumentException("Catalog keys must not be empty.", nameof(source));
            }

            if(groupedKeys.Contains(key)) {
                continue;
            }

            if(source[key] is null) {
                throw new ArgumentException($"Source template for key '{key}' must not be null.", nameof(source));
            }

            string? targetTemplate = null;
            target?.TryGetValue(key, out targetTemplate);
            units.Add(new XliffUnit {
                Id = NextId(emittedIds, XliffConstants.EncodeId(key)),
                Source = source[key]!,
                Target = string.IsNullOrEmpty(targetTemplate) ? null : targetTemplate,
                State = string.IsNullOrEmpty(targetTemplate) ? "initial" : "translated",
                Context = SplitContext(key).Context,
                TranslatorNote = NoteFor(translatorNotes, key),
            });
        }

        foreach(var group in pluralEntries ?? []) {
            AddPluralUnits(units, emittedIds, source, group);
        }

        units.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
        return new XliffDocument {
            SourceLanguage = srcLang,
            TargetLanguage = trgLang,
            FileId = fileId,
            Units = units,
        };
    }

    /// <summary>
    /// Serializes a document to deterministic XLIFF 2.0 text (LF newlines, 2-space indent).
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>The XLIFF text.</returns>
    internal static string Serialize(XliffDocument document) {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")
            .Append(CultureInfo.InvariantCulture, $"<xliff version=\"2.0\" srcLang=\"{EscapeAttribute(document.SourceLanguage)}\" trgLang=\"{EscapeAttribute(document.TargetLanguage)}\">\n")
            .Append(CultureInfo.InvariantCulture, $"  <file id=\"{EscapeAttribute(document.FileId)}\">\n");

        foreach(var unit in document.Units) {
            builder.Append(CultureInfo.InvariantCulture, $"    <unit id=\"{EscapeAttribute(unit.Id)}\" xml:space=\"preserve\">\n");
            AppendNotes(builder, unit);

            builder.Append(CultureInfo.InvariantCulture, $"      <segment state=\"{EscapeAttribute(unit.State)}\"");
            if(!unit.Translate) {
                builder.Append(" translate=\"no\"");
            }

            builder.Append(">\n")
                .Append(CultureInfo.InvariantCulture, $"        <source>{EscapeText(unit.Source)}</source>\n");
            if(unit.Target is not null) {
                builder.Append(CultureInfo.InvariantCulture, $"        <target>{EscapeText(unit.Target)}</target>\n");
            }

            builder.Append("      </segment>\n")
                .Append("    </unit>\n");
        }

        builder.Append("  </file>\n")
            .Append("</xliff>");
        return builder.ToString();
    }

    /// <summary>
    /// Appends the unit's notes block when the unit carries a context, a translator note, or
    /// plural metadata.
    /// </summary>
    /// <param name="builder">The output builder.</param>
    /// <param name="unit">The unit to serialize.</param>
    private static void AppendNotes(StringBuilder builder, XliffUnit unit) {
        if(unit.Context is null && unit.TranslatorNote is null && unit.Plural is null) {
            return;
        }

        builder.Append("      <notes>\n");
        if(unit.Context is not null) {
            builder.Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_CONTEXT}\">{EscapeText(unit.Context)}</note>\n");
        }

        if(unit.TranslatorNote is not null) {
            builder.Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_TRANSLATOR}\">{EscapeText(unit.TranslatorNote)}</note>\n");
        }

        if(unit.Plural is not null) {
            builder.Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_PLURAL_SINGULAR}\">{EscapeText(unit.Plural.SingularKey)}</note>\n")
                .Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_PLURAL_OTHER}\">{EscapeText(unit.Plural.PluralKey)}</note>\n")
                .Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_PLURAL_INDEX}\">{unit.Plural.Index.ToString(CultureInfo.InvariantCulture)}</note>\n")
                .Append(CultureInfo.InvariantCulture, $"        <note category=\"{XliffConstants.NOTE_PLURAL_COUNT}\">{unit.Plural.Count.ToString(CultureInfo.InvariantCulture)}</note>\n");
        }

        builder.Append("      </notes>\n");
    }

    /// <summary>
    /// Adds plural units.
    /// </summary>
    /// <param name="units">The units.</param>
    /// <param name="emittedIds">The emitted ids.</param>
    /// <param name="source">The source.</param>
    /// <param name="group">The group.</param>
    private static void AddPluralUnits(
        List<XliffUnit> units,
        HashSet<string> emittedIds,
        IReadOnlyDictionary<string, string?> source,
        XliffPluralEntry group) {
        var singularFlat = FlatKey(group.Context, group.SingularKey);
        var pluralFlat = FlatKey(group.Context, group.PluralKey);
        var baseId = group.Context is null
            ? XliffConstants.EncodeId(group.SingularKey)
            : XliffConstants.EncodeId(group.Context) + XliffConstants.ENCODED_CONTEXT_SEPARATOR + XliffConstants.EncodeId(group.SingularKey);

        var sourceForms = group.SourceForms ?? ClampedSourceForms(source, singularFlat, pluralFlat, group);
        for(var i = 0; i < group.TargetForms.Count; i++) {
            var targetForm = group.TargetForms[i];
            units.Add(new XliffUnit {
                Id = NextId(emittedIds, XliffConstants.EncodePluralId(baseId, i)),
                Source = sourceForms[Math.Min(i, sourceForms.Count - 1)] ?? string.Empty,
                Target = string.IsNullOrEmpty(targetForm) ? null : targetForm,
                State = string.IsNullOrEmpty(targetForm) ? "initial" : "translated",
                Context = group.Context,
                Plural = new XliffPluralInfo {
                    SingularKey = group.SingularKey,
                    PluralKey = group.PluralKey,
                    Index = i,
                    Count = group.TargetForms.Count,
                },
            });
        }
    }

    /// <summary>
    /// Performs the clamped source forms operation.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="singularFlat">The singular flat.</param>
    /// <param name="pluralFlat">The plural flat.</param>
    /// <param name="group">The group.</param>
    /// <returns>The clamped source forms sequence.</returns>
    private static List<string> ClampedSourceForms(
        IReadOnlyDictionary<string, string?> source,
        string singularFlat,
        string pluralFlat,
        XliffPluralEntry group) {
        if(!source.TryGetValue(singularFlat, out var singular) || singular is null) {
            throw new ArgumentException($"Source catalog lacks the singular key '{group.SingularKey}' for a plural group.", nameof(source));
        }

        if(!source.TryGetValue(pluralFlat, out var plural) || string.IsNullOrEmpty(plural)) {
            plural = singular;
        }

        return [singular, plural];
    }

    /// <summary>
    /// Validates plural entry.
    /// </summary>
    /// <param name="group">The group.</param>
    private static void ValidatePluralEntry(XliffPluralEntry group) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrEmpty(group.SingularKey);
        ArgumentException.ThrowIfNullOrEmpty(group.PluralKey);
        ArgumentNullException.ThrowIfNull(group.TargetForms);
        if(group.TargetForms.Count == 0) {
            throw new ArgumentException("Plural groups must carry at least one form.", nameof(group));
        }

        if(group.SourceForms?.Count == 0) {
            throw new ArgumentException("Provided source forms must not be empty.", nameof(group));
        }
    }

    /// <summary>
    /// Performs the flat key operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="key">The message key.</param>
    /// <returns>The flat key text.</returns>
    private static string FlatKey(string? context, string key) =>
        context is null ? key : context + XliffConstants.CONTEXT_SEPARATOR + key;

    /// <summary>
    /// Performs the note for operation.
    /// </summary>
    /// <param name="translatorNotes">The translator notes.</param>
    /// <param name="key">The message key.</param>
    /// <returns>The note for result.</returns>
    private static string? NoteFor(IReadOnlyDictionary<string, string>? translatorNotes, string key) =>
        translatorNotes is not null && translatorNotes.TryGetValue(key, out var note) && !string.IsNullOrEmpty(note)
            ? note
            : null;

    /// <summary>
    /// Splits context.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <returns>The split context result.</returns>
    private static (string? Context, string MsgId) SplitContext(string key) {
        var separator = key.IndexOf(XliffConstants.CONTEXT_SEPARATOR);
        return separator < 0 ? (null, key) : (key[..separator], key[(separator + 1)..]);
    }

    /// <summary>
    /// Performs the next id operation.
    /// </summary>
    /// <param name="emittedIds">The emitted ids.</param>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The next id text.</returns>
    private static string NextId(HashSet<string> emittedIds, string candidate) {
        if(!emittedIds.Add(candidate)) {
            throw new ArgumentException($"Duplicate XLIFF unit id '{candidate}': keys must encode to unique ids.", nameof(candidate));
        }

        return candidate;
    }

    /// <summary>
    /// Escapes text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape text.</returns>
    private static string EscapeText(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// Escapes attribute.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The escape attribute text.</returns>
    private static string EscapeAttribute(string value) =>
        EscapeText(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}
