# Jeninnet.Localization.Interchange.Csv

CSV catalog interchange for `Jeninnet.Localization`. The package exports a source/target catalog pair to an RFC 4180 spreadsheet document and imports the edited document back with validation before any change is applied. It is tooling only: the CSV file is a translator handoff artifact, never a runtime message source, and no provider lives here.

Use it when a translator or reviewer works in a spreadsheet. The runtime catalog stays JSON or XML. See the [interchange formats reference](../../../docs/en/03-reference/04-interchange-formats.md) for the family-wide contract and the [translator workflow guide](../../../docs/en/02-guides/11-translator-workflow.md) for the full round trip.

## Installation

```bash
dotnet add package Jeninnet.Localization.Interchange.Csv
```

The package targets .NET 8, .NET 9, and .NET 10 and references only `Jeninnet.Localization.Interchange.Abstractions`; it has no third-party CSV parser.

## Registration

There is no dependency injection registration. `CsvCatalogInterchange` is a static API:

```csharp
using Jeninnet.Localization.Interchange.Csv;
```

The public entry points are:

- `ExportCatalog(...)` — builds CSV text from source and target dictionaries.
- `ExportCatalogFiles(...)` — reads JSON or XML catalogs and writes a CSV file.
- `ParseDocument(text, filePath)` — parses and validates CSV text into `CsvDocument`.
- `ImportDocument(...)` — computes a change-set without touching files.
- `ImportCatalogFiles(...)` — parses, validates, and applies to a target catalog file.

## Configuration

The only import option is the dry-run flag:

```csharp
var options = new CsvImportOptions {
    DryRun = true,
};
```

The CSV dialect is fixed; there is no delimiter, quote, newline, or encoding option.

## Usage

Export the pair, let the translator edit the target cells, preview the import, then apply it:

```csharp
using Jeninnet.Localization.Interchange.Csv;

// One CSV per target culture: source row and target row per key.
CsvCatalogInterchange.ExportCatalogFiles(
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    csvPath: "handoff/ar.csv",
    srcLang: "en",
    trgLang: "ar",
    translatorNotes: notes);

// The translator edits the value cells in a spreadsheet and returns ar.csv.

var preview = CsvCatalogInterchange.ImportCatalogFiles(
    csvPath: "handoff/ar.csv",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    srcLang: "en",
    trgLang: "ar",
    options: new CsvImportOptions { DryRun = true });

foreach (var key in preview.Keys) {
    Console.WriteLine($"{key.Key}: {key.Status} — {key.Detail}");
}

if (preview.ConflictCount == 0) {
    var applied = CsvCatalogInterchange.ImportCatalogFiles(
        "handoff/ar.csv", "Localization/en.json", "Localization/ar.json", "en", "ar");
}
```

Translator notes returned in the report are not written into the catalog. Apply them through the governed store:

```csharp
foreach (var key in preview.Keys.Where(k => k.TranslatorNote is not null)) {
    await admin.SetNoteAsync("Bookstore", key.Key, key.TranslatorNote!);
}
```

## Behavior and limits

**Document shape.** The header row is exactly `key,culture,value,note`. Each row is one `(key, culture)` pair. One source culture and one target culture apply per document and are supplied as API parameters because CSV carries no metadata. Export writes keys in ordinal order, the source row then the target row for each key, an empty target cell when untranslated, and the key-level note on every row of the key. Rows for any other culture fail validation.

**Dialect.** Written files are UTF-8 (Unicode Transformation Format, 8-bit) with BOM (Byte Order Mark) and CRLF (Carriage Return and Line Feed) records. Reading tolerates a BOM, CRLF, LF, lone CR, and blank lines. Quoting is minimal on write and strict RFC 4180 on read; unterminated quotes, stray quotes, wrong field counts, a wrong header, duplicate `(key, culture)` rows, and conflicting non-empty notes for one key fail with file and line diagnostics before anything is computed.

**Conflict policy.** Per target row: `Unchanged` when the target already matches, `Updated` when a changed target is applied, `Skipped` for an empty value (existing templates are never cleared), `UnknownKey` when the key is absent from the source catalog, `StaleSource` when the source row is missing or no longer matches the source catalog, and `UnsupportedKey` when a contextual key cannot be an XML element name. `ConflictCount` counts the three conflict statuses, not skipped rows. State is reported per key and never gates application of other keys.

**Lossy edges.** Plural tables are outside the flat CSV subset. Contextual keys travel as ordinary cell text; the EOT (End of Transmission) separator (`\u0004`) cannot appear in an XML element name, so a contextual key against an XML target is reported `UnsupportedKey`. There is no formatting metadata in the document.

**Validation and apply.** The whole document validates before anything is computed. The dictionary-level import returns the per-key report plus the updated target map, or `null` when nothing changed. The file-level import rewrites the target catalog at most once, never on a dry run, and never when unchanged. Key order, newline style, and BOM are preserved by the shared codec.

**Security.** CSV documents are untrusted input. The reader loads the whole file as text; no explicit size, row-count, or processing-time limit is configured, so enforce those limits at the ingestion boundary. Use `DryRun` and review the report before applying, and never pass a user-controlled path directly to `ImportCatalogFiles`. The importer never invents keys or clears existing templates. See the [security chapter](../../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Interchange.Abstractions](../Jeninnet.Localization.Interchange.Abstractions/README.md)
- [Jeninnet.Localization.Interchange.Po](../Jeninnet.Localization.Interchange.Po/README.md)
- [Jeninnet.Localization.Interchange.ResX](../Jeninnet.Localization.Interchange.ResX/README.md)
- [Jeninnet.Localization.Interchange.Xliff](../Jeninnet.Localization.Interchange.Xliff/README.md)

## Documentation

- [Interchange formats](../../../docs/en/03-reference/04-interchange-formats.md)
- [Translator workflow](../../../docs/en/02-guides/11-translator-workflow.md)
- [Parity and quality gates](../../../docs/en/02-guides/13-parity-and-quality-gates.md)
- [Security](../../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../../LICENSE).
