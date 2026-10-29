# Interchange Formats

Interchange packages move catalog content to and from translator tooling. They are build-time and
review-time utilities. None of them is a runtime provider, and none of them reads a catalog at
lookup time.

All formats share one conflict vocabulary and one apply philosophy:

- The whole document validates before anything is computed.
- The dictionary-level import returns a change-set; the file-level import rewrites the target catalog
  at most once.
- A dry run changes nothing.
- A no-change round trip is byte-identical because the shared file codec preserves key order, newline
  style, and BOM (Byte Order Mark).
- Translator notes are surfaced in the report and never written into JSON or XML catalogs.

## Shared status vocabulary

`CatalogKeyStatus` lives in `Jeninnet.Localization.Interchange.Abstractions`:

| Status | Meaning |
|---|---|
| `Unchanged` | The target already matches; nothing applied. |
| `Updated` | The target differs and was applied (or would be, in a dry run). |
| `Skipped` | Empty or missing target, fuzzy entry, or `translate="no"`. Existing templates are never cleared. |
| `UnknownKey` | The key does not exist in the source catalog. Interchange never invents keys. |
| `StaleSource` | The document's source template differs from the current source. Reported, never applied. |
| `UnsupportedKey` | The key cannot be represented in the target format. Reported, never applied. |

The set of statuses a format can produce is narrower than the vocabulary. PO (Portable Object) and ResX cannot
represent `StaleSource` because their documents carry no source-template snapshot. CSV and XLIFF (XML Localization Interchange File Format) can.

## Public surface

Each format is a static class with the same schematic shape (`TDocument` and `TReport` stand for the
format-specific types):

```csharp
public static string ExportCatalog(...);
public static void ExportCatalogFiles(...);
public static TDocument ParseDocument(string text, string filePath);
public static TCatalogImportResult ImportDocument(...);
public static TReport ImportCatalogFiles(...);
```

Import options are a single `DryRun` flag. The dictionary-level result carries the per-key report and
the updated target maps; `null` means "no changes in that dimension". The file-level report carries
the per-key outcomes and the notes parsed from the document.

## CSV

`CsvCatalogInterchange` uses RFC 4180 with a fixed schema:

```text
key,culture,value,note
App.Title,en,Bookstore,Product name
App.Title,ar,مكتبة,Product name
```

- **Cultures:** one source and one target per document, supplied as API parameters because CSV has no
  document metadata. Rows for any other culture fail validation.
- **Dialect:** UTF-8 (Unicode Transformation Format, 8-bit) with BOM on write (spreadsheets detect UTF-8 reliably); CRLF (Carriage Return and Line Feed) records on write;
  BOM, CRLF, LF, lone CR, and blank lines tolerated on read. Minimal quoting on write; strict
  RFC 4180 parsing on read.
- **Plural forms:** outside the flat subset. Contextual keys travel as ordinary cell text, but a
  contextual key against an XML target is `UnsupportedKey` because XML names cannot carry the EOT (End of Transmission)
  separator.
- **Notes:** written on every row of the key, validated to agree when non-empty, surfaced per key.
- **Stale source:** representable. The source row is compared with the current source catalog.
- **Lossy edges:** no plural tables, no formatting metadata, context is indistinguishable from key
  text.

```csharp
CsvCatalogInterchange.ExportCatalogFiles("en.json", "ar.json", "ar.csv", "en", "ar");
var report = CsvCatalogInterchange.ImportCatalogFiles(
    "ar.csv", "en.json", "ar.json", "en", "ar",
    new CsvImportOptions { DryRun = true });
```

## PO

`PoCatalogInterchange` reads and writes standard gettext PO text for one culture.

```po
msgctxt "Views.Catalog.Index"
msgid "Heading"
msgstr "الفهرس"
```

- **Entries:** `msgid` is the key; `msgstr` is the culture's template; the header entry (`msgid ""`)
  carries `Content-Type: text/plain; charset=UTF-8` and `Language:`.
- **Contexts:** `msgctxt` entries join with EOT (`'\u0004'`) on import, matching the runtime
  `LocalizeWithContextAsync` key.
- **Plurals:** `msgid_plural` plus contiguous `msgstr[n]`. A plural group reports once; updated
  tables are returned when current tables are supplied.
- **Notes and flags:** `#.` extracted comments become `TranslatorNote`; `#, fuzzy` is retained and
  fuzzy entries are `Skipped`.
- **Ordering and encoding:** deterministic ordinal ordering; single-line strings with C-style escapes;
  UTF-8 without BOM on write; BOM tolerated on read.
- **Plural-Forms policy:** the export accepts an optional caller-supplied `Plural-Forms` header
  written verbatim. The framework has no gettext expression table. Without a header, the provider's
  pair fallback applies (`one` to form 0, otherwise form 1 when present).
- **Stale source:** not representable. The PO file carries keys, not source templates.
- **Lossy edges:** no source snapshot, no reliable detection of a moved source; plural semantics
  depend on the header supplied.

## ResX

`ResXCatalogInterchange` reads and writes canonical ResX 2.0 strings and comments.

- **Entries:** `data` elements with a `name`, `xml:space="preserve"`, at most one `value`, and at most
  one `comment`. The entry name is the key.
- **Culture:** one culture per document. ResX has no culture metadata; the file name or the caller
  supplies the culture. Pair-file neutral and satellite workflows are the caller's composition.
- **Dialect:** canonical VS header (`resmimetype` `text/microsoft-resx`, version `2.0`), 2-space
  indent, LF newlines, UTF-8 with BOM on write. Reading tolerates a BOM and ignores `assembly` and
  `metadata` root elements.
- **Plural forms:** outside the subset.
- **Notes:** entry comments. Parsed back and surfaced; never applied to a catalog file.
- **Unsupported entries:** typed, `mimetype`, or valueless entries are `UnsupportedKey`.
- **Stale source:** not representable.
- **Lossy edges:** XML 1.0 representability. The EOT context separator is illegal and is rejected at
  export.

The writer and reader are hand-rolled because the classic BCL (Base Class Library) `ResXResourceWriter` and
`ResXResourceReader` are Windows-Desktop-only on the supported .NET versions. The exported shape is pinned by tests and by
the BCL XML stack.

## XLIFF 2.0

`XliffCatalogInterchange` targets XLIFF 2.0 with one `file`, one source language, one target
language, and one `segment` per unit.

- **Units:** unique `<unit id>` elements, required plain-text `<source>`, optional plain-text
  `<target>`. Segment states are `initial`, `translated`, `reviewed`, and `final`.
- **Context:** a `<note category="context">`. In unit ids the EOT separator is encoded as U+2404
  (␄), which is legal in XML 1.0.
- **Plurals:** each form travels as its own unit (`{key}[{index}]`) with `plural-singular`,
  `plural-other`, `plural-index`, and `plural-count` notes.
- **Translator notes:** `<note category="translator">` on flat units only; plural units never carry
  them in this subset. Conflicting notes on one unit fail validation.
- **Translate flag:** `translate="no"` is `Skipped` on import.
- **Dialect:** vendor namespaced elements are ignored with their subtrees. Anything else outside the
  subset is rejected. There is no full-XSD pass; code-based subset validation is the conformance
  check.
- **Stale source:** representable. The unit source is compared with the current source catalog.
- **Lossy edges:** contextual keys or plural forms beyond the pair slots against a flat XML target
  are `UnsupportedKey`. Translator notes on plural units are not represented.

```csharp
XliffCatalogInterchange.ExportCatalogFiles(
    "en.json", "ar.json", "ar.xliff", "en", "ar", fileId: "bookstore");

var report = XliffCatalogInterchange.ImportCatalogFiles(
    "ar.xliff", "en.json", "ar.json",
    new XliffImportOptions { DryRun = true });
```

## Constructs at a glance

| Construct | CSV | PO | ResX | XLIFF |
|---|---|---|---|---|
| Flat key/template | Yes | Yes | Yes | Yes |
| Context | As key text | `msgctxt` | No | Context note + encoded id |
| Plural forms | No | Yes | No | Yes, one unit per form |
| Translator notes | `note` column | `#.` comments | `comment` | Translator note |
| Source snapshot | Yes | No | No | Yes |
| Segment state | No | Fuzzy flag | No | Yes |
| Source/target languages in document | API parameters | `Language:` header | No | `srcLang` / `trgLang` |
| Encoding on write | UTF-8 + BOM, CRLF | UTF-8, no BOM | UTF-8 + BOM, LF | UTF-8, no BOM |

## Round-trip guarantees

The guarantees are precise, and the boundaries matter:

- **Catalog rewrite:** key order, newline style, and BOM are preserved. A no-change import does not
  rewrite the target. When changes exist, the target is written once.
- **Interchange text:** export is deterministic for a fixed input. PO orders entries ordinally, CSV
  orders keys ordinally, ResX orders names ordinally, and XLIFF preserves the source order.
- **Notes:** never applied to catalog files. Apply them through `IResourceAdminStore.SetNoteAsync`.
- **Dry run:** computes the same report as an applied run but mutates nothing.
- **Source changes:** CSV and XLIFF detect them as `StaleSource`. PO and ResX cannot.

## Dialect settings

There is no configurable delimiter, quote character, newline, or encoding for CSV; the dialect is
fixed. PO accepts only a caller-supplied `Plural-Forms` header. XLIFF accepts a `fileId` on export and
enforces the subset on read. ResX accepts no dialect settings at all.

That is a deliberate trade-off. A fixed dialect is easier to test and harder to corrupt; a
translator's tool may need a conversion step, but the resulting catalog is predictable.

## Worked example: one round trip

Export `en.json` and `ar.json` to `ar.po` with a supplied `Plural-Forms` header and translator notes.
After the translator edits the file, import with `DryRun = true` and review `Updated`, `Skipped`, and
conflict entries. Apply the changed targets, apply the parsed notes through `SetNoteAsync`, then run
the parity check. The runtime provider still reads the JSON catalogs.

## Pitfalls

**Pitfall:** expecting translator notes to be written into JSON or XML. They are report data.

**Pitfall:** using CSV for a language that needs plural forms. The flat schema cannot carry them.

**Pitfall:** expecting `StaleSource` from PO or ResX. Those formats carry no source snapshot.

## Where to go next

- [Translator workflow](../02-guides/11-translator-workflow.md) for the round-trip process.
- [Tooling reference](05-tooling-reference.md) for the parity check that complements imports.
- [Provider reference](03-provider-reference.md) for runtime PO, ResX, and satellite behavior.
- [Core API](01-core-api.md) for the context-key encoding used by PO and XLIFF.
