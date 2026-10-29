# Jeninnet.Localization.Interchange.Xliff

XLIFF (XML Localization Interchange File Format) 2.0 catalog interchange for `Jeninnet.Localization`. The package exports flat catalogs and optional plural tables to XLIFF text and imports a vendor-returned file with validation before anything is applied, so professional translation vendors and CAT tools can work on catalog content. It is tooling only: the XLIFF file is a handoff artifact, never a runtime source, and no provider lives here.

The subset is deliberately narrow and code-validated. See the [interchange formats reference](../../../docs/en/03-reference/04-interchange-formats.md) for the complete format contract and the [translator workflow guide](../../../docs/en/02-guides/11-translator-workflow.md) for the vendor round trip.

## Installation

```bash
dotnet add package Jeninnet.Localization.Interchange.Xliff
```

The package targets .NET 8, .NET 9, and .NET 10 and references only `Jeninnet.Localization.Interchange.Abstractions`. `System.Xml.Linq` and `System.Text.Json` ship with the framework.

## Registration

There is no dependency injection registration. `XliffCatalogInterchange` is a static API:

```csharp
using Jeninnet.Localization.Interchange.Xliff;
```

The public entry points are:

- `ExportCatalog(...)` — writes XLIFF 2.0 text from source/target dictionaries and optional plural tables.
- `ExportCatalogFiles(...)` — reads JSON or XML catalogs and writes an XLIFF file.
- `ParseDocument(text, filePath)` — parses and validates XLIFF text into `XliffDocument`.
- `ImportDocument(...)` — computes a change-set without touching files.
- `ImportCatalogFiles(...)` — parses, validates, and applies to a target catalog file.

## Configuration

The only import option is the dry-run flag:

```csharp
var options = new XliffImportOptions {
    DryRun = true,
};
```

Export accepts a `fileId` (typically the catalog name). The file-level export derives it from the target file name when omitted. The import side has no dialect switches: the schema subset is fixed and validation is code-based, with no full XSD pass.

## Usage

Export the catalogs for the vendor, import the returned document with a dry run, then apply it:

```csharp
using Jeninnet.Localization.Interchange.Xliff;

XliffCatalogInterchange.ExportCatalogFiles(
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    xliffPath: "handoff/ar.xliff",
    srcLang: "en",
    trgLang: "ar",
    fileId: "bookstore",
    translatorNotes: notes);

// The vendor translates the target segments and returns ar.xliff.

var preview = XliffCatalogInterchange.ImportCatalogFiles(
    xliffPath: "handoff/ar.xliff",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    options: new XliffImportOptions { DryRun = true });

foreach (var key in preview.Keys) {
    Console.WriteLine($"{key.Key}: {key.Status} — {key.Detail} (state {key.State})");
}

if (preview.ConflictCount == 0) {
    var applied = XliffCatalogInterchange.ImportCatalogFiles(
        "handoff/ar.xliff", "Localization/en.json", "Localization/ar.json");
}
```

For in-memory work, `ImportDocument` also accepts the current source and target plural tables:

```csharp
var result = XliffCatalogInterchange.ImportDocument(
    document: XliffCatalogInterchange.ParseDocument(xliffText, "ar.xliff"),
    source: sourceCatalog,
    currentTarget: targetCatalog,
    sourcePlurals: currentSourcePlurals,
    currentPlurals: currentTargetPlurals);

var updatedTarget = result.UpdatedTarget;   // null when unchanged
var updatedPlurals = result.UpdatedPlurals; // null when unchanged or not supplied
```

## Behavior and limits

**Schema subset.** XLIFF 2.0 with `version="2.0"`, a required `srcLang`, and an optional `trgLang`. The document has exactly one `<file id>`; unit ids are unique, and each unit carries exactly one `<segment>` with a required plain-text `<source>` and an optional plain-text `<target>`. Segment states are `initial`, `translated`, `reviewed`, and `final`. `translate="yes"` and `translate="no"` are honored. Namespaced vendor elements are ignored with their subtrees; anything else outside the subset is rejected.

**Context and plurals.** Context travels in `<note category="context">`, and the EOT (End of Transmission) separator is encoded as U+2404 (␄) in unit ids because the raw separator is illegal in XML 1.0. Each plural form travels as its own unit named `{key}[{index}]` with `plural-singular`, `plural-other`, `plural-index`, and `plural-count` notes. Translator notes travel in `<note category="translator">` on flat units only; a note on a plural unit is not represented by this subset.

**Conflict policy.** `Skipped` covers an empty or missing target and `translate="no"`. `UnknownKey` means the key is absent from the source catalog. `StaleSource` means the unit source differs from the current source catalog. `UnsupportedKey` covers plural forms beyond the pair slots and contextual keys against a flat XML target. `Unchanged` and `Updated` are the normal outcomes. `ConflictCount` counts `UnknownKey`, `StaleSource`, and `UnsupportedKey`. State is reported per key and never gates application of other keys.

**Validation and apply.** The whole document validates before anything is computed. The dictionary-level import returns the per-key report plus the updated flat map and plural tables; `null` means that dimension is unchanged. The file-level import rewrites the target catalog at most once, never on a dry run, and never when unchanged. Key order, newline style, and BOM (Byte Order Mark) are preserved by the shared codec.

**Notes.** Parsed translator notes are surfaced in the import report. Catalog files have no note storage, so notes are never applied to files; apply them through the governed `IResourceAdminStore.SetNoteAsync` step.

**Security.** XLIFF files are untrusted input even when the sender is trusted. Parsing uses `XDocument.Parse` without a custom `XmlResolver`, so external entity resolution is not enabled. DTDs are not rejected, and internal entities are expanded within the runtime's entity-expansion limit. No explicit size, character-count, or depth limit is configured, and schema validation is not resource control. Enforce upload size and processing time before parsing, use `DryRun` first, and never pass a user-controlled path directly to `ImportCatalogFiles`. See the [security chapter](../../../docs/en/04-operations/02-security.md).

## AOT and trimming

Compatible with Native AOT (Ahead-of-Time) and trimming. The project builds with `IsAotCompatible=true`, and the repository treats trim/AOT warnings as errors. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract, exclusions, and smoke consumer.

## Related packages

- [Jeninnet.Localization.Interchange.Abstractions](../Jeninnet.Localization.Interchange.Abstractions/README.md)
- [Jeninnet.Localization.Interchange.Csv](../Jeninnet.Localization.Interchange.Csv/README.md)
- [Jeninnet.Localization.Interchange.Po](../Jeninnet.Localization.Interchange.Po/README.md)
- [Jeninnet.Localization.Interchange.ResX](../Jeninnet.Localization.Interchange.ResX/README.md)

## Documentation

- [Interchange formats](../../../docs/en/03-reference/04-interchange-formats.md)
- [Translator workflow](../../../docs/en/02-guides/11-translator-workflow.md)
- [Formatting and plurals](../../../docs/en/02-guides/04-formatting-and-plurals.md)
- [Security](../../../docs/en/04-operations/02-security.md)

## License

MIT — see [LICENSE](../../../LICENSE).
