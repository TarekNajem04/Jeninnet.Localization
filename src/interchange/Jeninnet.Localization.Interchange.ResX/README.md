# Jeninnet.Localization.Interchange.ResX

ResX catalog interchange for `Jeninnet.Localization`. The package exports a culture's catalog to canonical ResX 2.0 text and imports an edited file with validation before anything is applied, so the installed base of ResX editors (Visual Studio, ResX Manager, and similar tools) can work on catalog content. It is tooling only: the ResX file is a handoff artifact, never a runtime source, and the runtime `Providers.ResX` package keeps using compiled `.resources` through `ResourceManager`.

See the [interchange formats reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/04-interchange-formats.html) for the family-wide subset and the [translator workflow guide](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html) for the handoff process.

## Compatibility

Target frameworks: `net10.0` and `net11.0`. See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet add package Jeninnet.Localization.Interchange.ResX
```

The package targets .NET 10 and references only `Jeninnet.Localization.Interchange.Abstractions`. It ships a hand-rolled canonical ResX reader/writer because the classic BCL (Base Class Library) `ResXResourceWriter` and `ResXResourceReader` are Windows-Desktop-only on the supported .NET versions.

## Registration

There is no dependency injection registration. `ResXCatalogInterchange` is a static API:

```csharp
using Jeninnet.Localization.Interchange.ResX;
```

The public entry points are:

- `ExportCatalog(...)` — writes canonical ResX 2.0 text from a catalog dictionary.
- `ExportCatalogFiles(...)` — reads a JSON or XML catalog and writes a `.resx` file.
- `ParseDocument(text, filePath)` — parses and validates ResX text into `ResXDocument`.
- `ImportDocument(...)` — computes a change-set without touching files.
- `ImportCatalogFiles(...)` — parses, validates, and applies to a target catalog file.

## Configuration

The only import option is the dry-run flag:

```csharp
var options = new ResXImportOptions {
    DryRun = true,
};
```

There are no dialect settings. Export always uses the canonical header, ordinal name order, 2-space indentation, LF newlines, and UTF-8 (Unicode Transformation Format, 8-bit) with BOM (Byte Order Mark). Reading tolerates a BOM and ignores `assembly` and `metadata` root elements.

## Usage

Export the target culture, let the translator edit the entries, preview the import, then apply it:

```csharp
using Jeninnet.Localization.Interchange.ResX;

ResXCatalogInterchange.ExportCatalogFiles(
    catalogPath: "Localization/ar.json",
    resxPath: "handoff/ar.resx",
    translatorNotes: notes);

// The translator edits ar.resx in Visual Studio, ResX Manager, or another resx editor.

var preview = ResXCatalogInterchange.ImportCatalogFiles(
    resxPath: "handoff/ar.resx",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    options: new ResXImportOptions { DryRun = true });

foreach (var key in preview.Keys) {
    Console.WriteLine($"{key.Key}: {key.Status} — {key.Detail}");
}

if (preview.ConflictCount == 0) {
    var applied = ResXCatalogInterchange.ImportCatalogFiles(
        "handoff/ar.resx", "Localization/en.json", "Localization/ar.json");
}
```

Translator notes are returned on `ResXKeyReport.TranslatorNote`; they are not written into the catalog. Apply them through the governed store path when their content is approved.

## Behavior and limits

**Document shape.** One culture per document. ResX carries no culture metadata; the export/import pair is source catalog to target-culture resx, and neutral/satellite pair-file workflows are the caller's composition. Entries are `data` elements with a `name`, `xml:space="preserve"`, at most one `value`, and at most one `comment`. The entry name is the catalog key.

**Dialect.** Export writes the VS-canonical ResX 2.0 header (`resmimetype` `text/microsoft-resx`, version `2.0`, classic reader/writer `resheader` values), ordinal key order, 2-space indent, LF newlines, and UTF-8 with BOM. Reading rejects foreign roots, missing or invalid resheaders, duplicate names, multiple values or comments, and unexpected elements with file and line diagnostics.

**XML representability.** Keys, values, and comments must be representable in XML 1.0. Illegal characters are rejected at export; the EOT (End of Transmission) context separator (`\u0004`) is illegal in XML 1.0, so contextual key storage is not representable in this format.

**Conflict policy.** Non-string entries (typed, `mimetype`, or valueless) are `UnsupportedKey` and are never applied. An empty value is `Skipped`; existing templates are never cleared. A key absent from the source catalog is `UnknownKey`. An equal target is `Unchanged`; a changed target is `Updated`. `StaleSource` is not representable because ResX carries no source-template snapshot. `ConflictCount` counts `UnknownKey` and `UnsupportedKey`; read the per-key report rather than treating zero as proof that every entry was applied.

**Validation and apply.** The whole document validates before anything is computed. The dictionary-level import returns the per-key report plus the updated target map, or `null` when nothing changed. The file-level import rewrites the target catalog at most once, never on a dry run, and never when unchanged. Key order, newline style, and BOM are preserved by the shared codec.

**Notes.** Entry comments carry translator notes, are parsed back, and are surfaced in the report. They are never applied to catalog files; use the governed note API for that.

**Security.** ResX files are untrusted input. The hand-rolled reader reads only `data` entries and never instantiates types, which avoids the type-activation surface of a general-purpose resource reader; typed entries are reported as `UnsupportedKey`. Parsing uses hardened reader settings: external entities are never resolved, entity expansion is capped, and documents are capped at 16 MiB of text; file reads are additionally capped at 64 MiB. DTDs are not rejected. Enforce upload size and processing time before parsing, and keep untrusted ResX away from readers that deserialize arbitrary types. See the [security chapter](https://tareknajem04.github.io/Jeninnet.Localization/en/04-operations/02-security.html).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Interchange.Abstractions](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Abstractions/README.md)
- [Jeninnet.Localization.Interchange.Csv](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Csv/README.md)
- [Jeninnet.Localization.Interchange.Po](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Po/README.md)
- [Jeninnet.Localization.Interchange.Xliff](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/README.md)

## Documentation

- [Interchange formats](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/04-interchange-formats.html)
- [Translator workflow](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html)
- [Provider reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/03-provider-reference.html)
- [Security](https://tareknajem04.github.io/Jeninnet.Localization/en/04-operations/02-security.html)

## License

MIT — see [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
