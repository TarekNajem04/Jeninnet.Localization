# Jeninnet.Localization.Interchange.Po

PO (Portable Object, the gettext text catalog format) catalog interchange for `Jeninnet.Localization`. The package exports one culture's catalog to standard PO text and imports an edited file with validation before anything is applied, so catalogs can move through Poedit, Weblate, msgcat, and similar gettext workflows. It is tooling only: the PO file is not a runtime source, and the runtime reader remains the `Providers.Po` package.

The interchange package shares the PO text grammar with the runtime provider but keeps an independent reader/writer surface, so tooling does not take a dependency on runtime provider internals. See the [interchange formats reference](../../../docs/en/03-reference/04-interchange-formats.md) for the format subset and the [translator workflow guide](../../../docs/en/02-guides/11-translator-workflow.md) for the handoff process.

## Installation

```bash
dotnet add package Jeninnet.Localization.Interchange.Po
```

The package targets .NET 8, .NET 9, and .NET 10 and references only `Jeninnet.Localization.Interchange.Abstractions`.

## Registration

There is no dependency injection registration. `PoCatalogInterchange` is a static API:

```csharp
using Jeninnet.Localization.Interchange.Po;
```

The public entry points are:

- `ExportCatalog(...)` — writes PO text for one culture, with optional plural tables, translator notes, and a verbatim `Plural-Forms` header.
- `ExportCatalogFiles(...)` — reads a JSON or XML catalog and writes a PO file.
- `ParseDocument(text, filePath)` — parses and validates PO text into `PoDocument`.
- `ImportDocument(...)` — computes a change-set without touching files.
- `ImportCatalogFiles(...)` — parses, validates, and applies to a target catalog file.

## Configuration

The only import option is the dry-run flag:

```csharp
var options = new PoImportOptions {
    DryRun = true,
};
```

Export-side settings are method parameters, not an options object:

- `pluralEntries` — optional `PoPluralEntry` tables. A plural group is exported as `msgid_plural` plus contiguous `msgstr[n]` forms, and its pair keys are excluded from the flat entries.
- `translatorNotes` — optional key-level notes written as `#.` extracted comments.
- `pluralForms` — an optional caller-supplied `Plural-Forms` header, written verbatim. It must contain an integer `nplurals` from 1 to 6 and a non-empty `plural=` expression.

The framework has no gettext expression table; `PluralRules` exposes CLDR (Common Locale Data Repository) categorization only. When no header is supplied, no header is written and the runtime provider's documented pair fallback applies (`one` selects form 0; other categories select form 1 when present).

## Usage

Export one culture, let the translator work in a gettext tool, preview the import, then apply it:

```csharp
using Jeninnet.Localization.Interchange.Po;

PoCatalogInterchange.ExportCatalogFiles(
    catalogPath: "Localization/ar.json",
    poPath: "handoff/ar.po",
    culture: "ar",
    translatorNotes: notes,
    pluralForms: "nplurals=6; plural=n==0 ? 0 : n==1 ? 1 : n==2 ? 2 : n%100>=3 && n%100<=10 ? 3 : n%100>=11 ? 4 : 5;");

// The translator edits ar.po in Poedit, Weblate, or msgcat and returns it.

var preview = PoCatalogInterchange.ImportCatalogFiles(
    poPath: "handoff/ar.po",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    options: new PoImportOptions { DryRun = true });

foreach (var entry in preview.Keys) {
    Console.WriteLine($"{entry.Key}: {entry.Status} — {entry.Detail}");
}

if (preview.ConflictCount == 0) {
    var applied = PoCatalogInterchange.ImportCatalogFiles(
        "handoff/ar.po", "Localization/en.json", "Localization/ar.json");
}
```

When the target keeps plural tables, pass the current tables to the import so form counts and updates are checked against them:

```csharp
var result = PoCatalogInterchange.ImportDocument(
    document: PoCatalogInterchange.ParseDocument(poText, "ar.po"),
    source: sourceCatalog,
    currentTarget: targetCatalog,
    currentPlurals: currentPluralTables,
    xmlTarget: false);
```

## Behavior and limits

**Document shape.** A PO document is single-culture. `msgid` is the catalog key and `msgstr` is the culture's template. The header entry (`msgid ""`) carries `Content-Type: text/plain; charset=UTF-8` and `Language:`. Contexts travel as `msgctxt`; on import the context and key are joined with EOT (End of Transmission) (`\u0004`), matching the runtime context-key encoding. Plural groups use `msgid_plural` plus contiguous `msgstr[n]`. Translator notes use `#.` extracted comments. `#, fuzzy` is retained, and a fuzzy entry is reported `Skipped` and never applied.

**Ordering and encoding.** Export is deterministic: entries are ordered ordinally and strings are single-line with C-style escapes (`\"`, `\\`, `\n`, `\t`, `\r`, `\b`, `\f`, `\v`, `\0`). Files are UTF-8 (Unicode Transformation Format, 8-bit) without BOM (Byte Order Mark); a BOM is tolerated on read. Malformed input fails with file and line diagnostics before anything is computed.

**Conflict policy.** `Skipped` covers an empty `msgstr` and a fuzzy entry. `UnknownKey` means the key or plural pair is absent from the source catalog. `Unchanged` means the target already matches. `Updated` means a changed target was applied. `UnsupportedKey` covers a contextual key against an XML target, a plural form count that differs from the current table, and forms beyond the singular/plural pair slots when no plural tables are supplied. `StaleSource` is not representable: a PO file carries keys, not source templates. Plural groups report once; `UpdatedPlurals` carries the full updated tables when current tables are supplied.

**Validation and apply.** The whole document validates before anything is computed. The dictionary-level import returns the per-entry report plus the updated flat map and plural tables; `null` means that dimension is unchanged. The file-level import rewrites the target catalog at most once, never on a dry run, and never when unchanged. Key order, newline style, and BOM are preserved by the shared codec.

**Notes.** Translator notes are exported as `#.` comments, parsed back, surfaced per key in the report, and never applied to catalog files. JSON and XML catalogs have no note storage; apply notes through `IResourceAdminStore.SetNoteAsync`.

**Security.** PO files are untrusted input. The reader loads the whole file as text and applies no explicit size, line-count, or processing-time limit. Enforce those limits at the ingestion boundary, use `DryRun` first, and never pass a user-controlled path directly to `ImportCatalogFiles`. See the [security chapter](../../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Interchange.Abstractions](../Jeninnet.Localization.Interchange.Abstractions/README.md)
- [Jeninnet.Localization.Interchange.Csv](../Jeninnet.Localization.Interchange.Csv/README.md)
- [Jeninnet.Localization.Interchange.ResX](../Jeninnet.Localization.Interchange.ResX/README.md)
- [Jeninnet.Localization.Interchange.Xliff](../Jeninnet.Localization.Interchange.Xliff/README.md)

## Documentation

- [Interchange formats](../../../docs/en/03-reference/04-interchange-formats.md)
- [Translator workflow](../../../docs/en/02-guides/11-translator-workflow.md)
- [Formatting and plurals](../../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Security](../../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../../LICENSE).
