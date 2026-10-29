# Translator Workflow

A catalog is a data structure. A translation workflow is a sequence of handoffs between people and
tools. The interchange packages exist to make those handoffs lossless enough to be safe and explicit
enough to be reviewable.

The workflow has five stages: extract and verify the key set, export a translator-friendly format,
translate outside the repository, import with validation, and run the parity check. The runtime
provider never reads the interchange file; the canonical catalog stays JSON or XML.

## Stage 1: extract and verify

Before exporting anything, establish what the source actually references and what each culture
serves. The `jeninnet-localization-keys` tool does both. It parses C# syntax and Razor views, resolves
`*Keys` constants, and compares the result against every catalog file it can read.

```bash
jeninnet-localization-keys \
  --catalog ./Localization \
  --source ./src \
  --default-culture en \
  --report parity.json
```

The report contains a completeness matrix with three states per culture and key: `served` (present
locally), `fallback` (present in a parent or the default culture), and `missing` (absent from the
whole chain).

`fallback` is not a translation task failure, but it is a warning. A culture that only ever resolves
through fallback is not translated, even if the application appears complete. Use the matrix to
choose the keys and cultures worth sending to a translator.

The tool is report-only. It does not create catalog entries, write keys into files, or apply
translations. That boundary is deliberate; catalog writes belong to the governed store or to an
explicit reviewed file change.

## Stage 2: export

Choose the format that matches the translator's tools, not the runtime format. The interchange
packages read JSON and XML catalogs and write PO (Portable Object), CSV, ResX, or XLIFF (XML Localization Interchange File Format). All of them validate the whole
document before applying anything.

| Format | Best for | Plural forms | Context | Translator notes |
|---|---|---|---|---|
| PO | Poedit, Weblate, Crowdin, gettext pipelines | Yes, `msgstr[n]` | `msgctxt` | `#.` comments |
| XLIFF 2.0 | Professional vendors and CAT tools | One unit per form | `<note category="context">` | `<note category="translator">` |
| CSV | Spreadsheet review, quick handoffs | No, flat only | Key text only | `note` column |
| ResX | Visual Studio, ResX Manager | No, flat only | Key text only | `comment` element |

A PO export for one culture:

```csharp
PoCatalogInterchange.ExportCatalogFiles(
    catalogPath: "Localization/ar.json",
    poPath: "handoff/ar.po",
    culture: "ar",
    translatorNotes: notes,
    pluralForms: "nplurals=6; plural=n==0 ? 0 : n==1 ? 1 : n==2 ? 2 : n%100>=3 && n%100<=10 ? 3 : n%100>=11 ? 4 : 5;");
```

The `pluralForms` header is optional and written verbatim. The framework has no gettext expression
table; `PluralRules` exposes CLDR (Common Locale Data Repository) categorization only. If the translator's tool needs a
`Plural-Forms` header, the caller supplies one. If it is omitted, the PO provider's documented pair
fallback applies: `one` selects form 0, every other category selects form 1 when present.

An XLIFF export for a vendor:

```csharp
XliffCatalogInterchange.ExportCatalogFiles(
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    xliffPath: "handoff/ar.xliff",
    srcLang: "en",
    trgLang: "ar",
    translatorNotes: notes);
```

CSV and ResX are flat formats. They are good for review and terminology work, but a language with
more than two plural forms needs PO or XLIFF if the translator must edit each form. Otherwise the
pair API degrades to singular versus everything else.

## Stage 3: translate outside the repository

The translator works on the exported file. What travels with the text matters as much as the text:

- **Notes** are key-level, culture-agnostic guidance. PO writes `#.` comments, XLIFF
  `<note category="translator">`, CSV the `note` column, ResX the `comment` element.
- **Contexts** disambiguate identical keys. The runtime key is `context + '\u0004' + key`. PO uses
  `msgctxt`; XLIFF puts context in a note and encodes the separator as U+2404 in the unit id; CSV
  carries the composite key as cell text; ResX cannot represent it because the separator is illegal
  in XML 1.0.
- **Plural groups** are one logical entry with several forms. PO writes `msgid_plural` and contiguous
  `msgstr[n]`; XLIFF writes one unit per form with `plural-singular`, `plural-other`,
  `plural-index`, and `plural-count` notes.

Translators should preserve every placeholder and every ICU (International Components for Unicode) construct exactly. A dropped `{0}` or a
renamed `{count}` is a runtime defect, and the placeholder scanner in the parity tool is designed to
catch it. See [Formatting and plurals](04-formatting-and-plurals.md).

**Pitfall:** editing the source catalog while a translation is in flight. The import compares against
the current source where the format carries a source snapshot (CSV and XLIFF), and reports
`StaleSource`. PO and ResX carry no source template, so a stale handoff is not detectable. Freeze the
source for the duration of the round trip, or accept that late changes need a new export.

## Stage 4: import with validation

Every interchange import is validate-then-apply. A malformed document fails before anything is
computed; the target file is rewritten at most once, never on a dry run, and never when nothing
changed.

```csharp
var preview = PoCatalogInterchange.ImportCatalogFiles(
    poPath: "handoff/ar.po",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json",
    options: new PoImportOptions { DryRun = true });

if (preview.ConflictCount > 0) {
    throw new InvalidOperationException($"Translation conflicts: {preview.ConflictCount}");
}

var applied = PoCatalogInterchange.ImportCatalogFiles(
    poPath: "handoff/ar.po",
    sourcePath: "Localization/en.json",
    targetPath: "Localization/ar.json");
```

The per-key status vocabulary is shared across formats: `Unchanged` (target matches), `Updated`
(changed target applied), `Skipped` (empty target, fuzzy entry, or `translate="no"`), `UnknownKey`
(key absent from the source catalog), `StaleSource` (source template differs; reported, never
applied), and `UnsupportedKey` (not representable in the target format).

`ConflictCount` counts the conflict statuses, not every skipped entry. Read the per-key report rather
than treating a zero as proof that every line was applied.

Translator notes are surfaced in the report, never applied to the catalog file. JSON and XML
catalogs have no note storage. Apply notes through the governed store:

```csharp
var admin = store.RequireAdmin();

foreach (var key in applied.Keys.Where(k => k.TranslatorNote is not null)) {
    await admin.SetNoteAsync("Bookstore", key.Key, key.TranslatorNote!, new ResourceAdminOptions {
        Actor = "translation-import"
    });
}
```

That step is a separate governed mutation with its own validation, conflict reporting, and audit
record. It is not part of the file import, and it should not be silently skipped because the file
looked fine.

## Stage 5: parity before merge

Run the parity tool again after the import. The important checks are:

- Every referenced key is now `served` or deliberately `fallback` in each culture.
- No catalog key is unused without a documented reason.
- Templates for one culture and key agree across formats.
- Placeholder sets agree with the reference culture.
- No catalog file became unreadable.

The tool exits `0` when clean, `1` when it finds parity problems, and `2` on a usage or environment
error. Wire exit `1` into CI as a failure. See [Parity and quality gates](13-parity-and-quality-gates.md).

## A realistic multi-culture round trip

A bookstore ships English as the source and Arabic, German, and Polish as targets. The team uses PO
for Arabic and Polish because both need more than two plural forms, XLIFF for the German vendor
because its CAT tool expects it, and CSV for an internal review pass.

1. The source key set is frozen at release `2026.10`. The parity tool reports 1,240 referenced keys
   and no missing English entries.
2. Arabic and Polish PO files are exported with explicit `Plural-Forms` headers supplied by the
   translation vendor. German XLIFF is exported with the vendor's file id. CSV is exported for the
   product owner's terminology review.
3. The vendor returns the files. The Arabic file has three `#, fuzzy` entries from a terminology
   change; the import marks them `Skipped`, and the report calls them out.
4. The German XLIFF was exported before a source edit. The import reports `StaleSource` for that key
   and leaves the old target in place. The team re-exports and repeats the import.
5. The German XLIFF includes two contextual keys that the target XML catalog cannot represent. They
   are reported `UnsupportedKey`; the runtime serves German from PO, so the team changes the handoff
   format for those keys rather than dropping them.
6. Notes returned in all three formats are applied through `SetNoteAsync` with actor
   `release-2026.10`. The file imports never touched notes.
7. The parity tool reports zero missing keys, zero placeholder drift, and three unused keys that are
   documented as reserved for the next release. The build passes.

The runtime provider registration does not change during any of this. The application keeps serving
the previous catalogs until the reviewed files are merged and deployed.

## Pitfalls

**Pitfall:** treating the export as a backup. It is a handoff artifact. The canonical catalog is the
JSON or XML file (or the store); regenerate the export from it after any source change.

**Pitfall:** importing a translation file over a catalog that has moved on. Check `StaleSource` and
the source key set before applying. The formats that cannot represent a source snapshot will not warn
you.

**Pitfall:** expecting notes to be written into JSON or XML. They are report data. Apply them through
the governed note API.

**Pitfall:** using CSV for plural-heavy languages. The flat schema has no plural table; only the pair
mapping applies. Use PO or XLIFF.

**Pitfall:** applying a translation with an empty target to "clear" a value. Empty targets are
`Skipped`; existing templates are never cleared. Delete or update the entry through the store if that
is the intent.

**Pitfall:** assuming a no-change import rewrites the file. It does not, which is what makes
byte-identical round trips possible.

## Where to go next

- [Interchange formats](../03-reference/04-interchange-formats.md) for the complete format subsets
  and lossy edges.
- [Audit and history](07-audit-and-history.md) for the governed note and audit path.
- [Parity and quality gates](13-parity-and-quality-gates.md) for the CI enforcement details.
- [Tooling reference](../03-reference/05-tooling-reference.md) for the CLI options and report schema.
