# Jeninnet.Localization.Interchange.Abstractions

Shared contracts for the `Jeninnet.Localization` interchange family. The package owns the single per-key outcome vocabulary every interchange format reports through, plus the internal catalog file codec that keeps JSON and XML catalog rewrites faithful. It is build-time and review-time tooling only: an interchange file is never a runtime message source, and no provider lives here.

The abstraction exists so a key's result means the same thing in CSV, PO (Portable Object), ResX, and XLIFF (XML Localization Interchange File Format). Import a translator file with any format package, and the same `CatalogKeyStatus` values describe what happened. See the [interchange formats reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/04-interchange-formats.html) for the family-wide model and the [translator workflow guide](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html) for the handoff process.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Interchange.Abstractions
```

Format packages reference this package transitively. Add it directly only when a custom tool needs the shared status type or the public exception contract.

## Registration

There is no dependency injection registration and no service collection extension. The package is a contract library, not a runtime component.

The format packages consume the internal `CatalogFileCodec` through `InternalsVisibleTo` (CSV, PO, ResX, and XLIFF). The public surface is deliberately small:

- `CatalogKeyStatus` — the shared per-key outcome enum.
- `CatalogInterchangeException` — the internal codec failure surface, carrying `FilePath`, `LineNumber`, and a bare message. Format packages catch it and wrap it in their own public exception with the file and line preserved.
- `CatalogFileCodec` and its nested model types are internal and are not part of the supported consumer API.

## Configuration

This package exposes no options and reads no configuration. Every interchange package uses the same fixed catalog behavior:

- Only JSON object catalogs (`.json`) and XML message documents (`.xml`) are readable as catalogs.
- Key order, newline style, and a UTF-8 (Unicode Transformation Format, 8-bit) BOM (Byte Order Mark) are preserved when a catalog is rewritten.
- Untouched files are never rewritten, so a no-change round trip is byte-identical.
- A missing file, malformed JSON, a non-object JSON root, a non-string JSON value, a malformed XML document, or a duplicate XML element name fails with file and line diagnostics where the parser supplies them.

There is nothing to tune: the family trades configurable dialects for predictable, testable output.

## Usage

A format package performs the export and import; this package supplies the vocabulary for the result. The example below uses the CSV package and inspects the report that any format returns in its own report shape:

```csharp
using Jeninnet.Localization.Interchange.Abstractions;
using Jeninnet.Localization.Interchange.Csv;

var report = CsvCatalogInterchange.ImportCatalogFiles(
    "ar.csv", "en.json", "ar.json", "en", "ar",
    new CsvImportOptions { DryRun = true });

foreach (var key in report.Keys) {
    var line = key.Status switch {
        CatalogKeyStatus.Unchanged => "already current",
        CatalogKeyStatus.Updated => "would apply",
        CatalogKeyStatus.Skipped => "left untouched",
        CatalogKeyStatus.UnknownKey => "key not in the source catalog",
        CatalogKeyStatus.StaleSource => "translator worked from a stale source",
        CatalogKeyStatus.UnsupportedKey => "not representable in the target",
        _ => "unexpected status",
    };

    Console.WriteLine($"{key.Key}: {line} ({key.Detail})");
}
```

The exception contract is useful when a tool wants structured diagnostics rather than the formatted `Message`:

```csharp
try {
    CsvCatalogInterchange.ImportCatalogFiles("ar.csv", "en.json", "ar.json", "en", "ar");
}
catch (CsvInterchangeException ex) {
    Console.Error.WriteLine($"{ex.FilePath}:{ex.LineNumber}: {ex.Message}");
}
```

## Behavior and limits

The status vocabulary is the conflict policy, not a merge algorithm:

| Status | Meaning |
|----|----|
| `Unchanged` | The target already matches; nothing applied. |
| `Updated` | The target differs and was applied, or would be in a dry run. |
| `Skipped` | Empty or missing target, fuzzy entry, or `translate="no"`. Existing templates are never cleared. |
| `UnknownKey` | The key is absent from the source catalog. Interchange never invents keys. |
| `StaleSource` | The document's source template differs from the current source. Reported, never applied. |
| `UnsupportedKey` | The key cannot be represented in the target format. Reported, never applied. |

Not every format can produce every status. CSV and XLIFF carry a source snapshot and can report `StaleSource`; PO and ResX carry no source snapshot and cannot. `Unchanged` and `Updated` are always per key. Conflict statuses are reported, not thrown, and state is never used to gate application of other keys.

The shared codec is intentionally permissive about catalog formatting and strict about structure. It preserves formatting facts it can observe, and it rejects values the target cannot represent (for example an XML-invalid key when writing XML). Keys are matched ordinally and exactly.

**Security.** Catalog files and interchange documents are untrusted inputs when they come from outside the deployment team. The shared XML codec parses with hardened settings: external entities are never resolved (`XmlResolver` is always `null`), entity expansion is capped at 4 MiB, and documents are capped at 16 MiB of text. DTDs themselves are accepted, and schema validation is not resource control. Catalog file reads are additionally capped at 64 MiB before parsing. Enforce upload size, row count, and processing time at the ingestion boundary before calling these APIs. See the [security chapter](https://tareknajem04.github.io/Jeninnet.Localization/en/04-operations/02-security.html) for the complete threat model.

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Interchange.Csv](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md)
- [Jeninnet.Localization.Interchange.Po](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md)
- [Jeninnet.Localization.Interchange.ResX](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/README.md)
- [Jeninnet.Localization.Interchange.Xliff](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md)

## Documentation

- [Interchange formats](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/04-interchange-formats.html)
- [Translator workflow](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html)
- [Security](https://tareknajem04.github.io/Jeninnet.Localization/en/04-operations/02-security.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)

## License

MIT — see [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
