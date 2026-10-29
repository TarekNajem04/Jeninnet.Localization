# Parity and Quality Gates

A localization build can be green while the product is untranslated. Compile-time keys prove that a
key exists in a declared catalog; they do not prove that every served culture has it, that formats
agree, or that a translator preserved the placeholders. Parity is the check that closes that gap.

The parity tool is `jeninnet-localization-keys`. It extracts key references from source, reads every
catalog it can find, and lets the internal `ParityAnalyzer` build a per-culture completeness matrix
and deterministic findings with file and line locations. It exits non-zero on disagreement.

## What the check is, and is not

It is a report-only static check. It does not create, rename, or delete catalog entries; apply
translations; modify source; or validate runtime formatting. It does:

- harvest `const string` definitions from `*Keys` classes, including generated files;
- find literals in key positions such as `localizer["Key"]`, `GetMessageTemplateAsync("Key", ...)`,
  `LocalizeAsync`, `LocalizePluralAsync`, `LocalizeWithContextAsync`, and `GetString`;
- scan Razor views textually;
- read JSON, XML, ResX, and PO (Portable Object) catalogs;
- compare placeholder identifiers across cultures;
- report unusable catalogs with a line number.

C# is parsed with Roslyn syntax only; no compilation is required, so the check works even when the
solution does not build. The trade-off is that a key composed at runtime, or referenced through an
unrecognized wrapper, can be reported as unused or missing incorrectly. The exclusion list exists for
exactly those cases.

## Running the check

Before the tool is published to a feed:

```bash
dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys -- \
  --catalog ./Localization --source ./src \
  --default-culture en --report artifacts/parity.json
```

After publication, replace the run step with `dotnet tool install --global
Jeninnet.Localization.Tools.Keys` and `jeninnet-localization-keys ...`.

| Option | Meaning |
|---|---|
| `--catalog <dir>` | Catalog root scanned recursively. Repeatable and required. |
| `--source <dir>` | Source root for `.cs`, `.razor`, `.cshtml`. Repeatable and required. |
| `--keys-file <file>` | Extra C# file parsed for key-constant definitions. Repeatable. |
| `--exclude <key>` | Exclude a key everywhere, or a culture-scoped `ar:Key.Name`. Repeatable. |
| `--default-culture <tag>` | Default culture closing every fallback chain. Default `en`. |
| `--report <path>` | Write the versioned JSON report. |
| `--help`, `--version` | Usage or tool version. |

Exit codes are part of the contract: `0` clean, `1` parity findings, `2` usage or environment error
(missing directory, unreadable input root). The tool does not emit SARIF. Its only machine-readable
output is the versioned JSON report (`schemaVersion: 2`); upload that as an artifact or convert it in
your own step. The console output is deterministic and suitable for a golden test.

## The completeness matrix

For every referenced key and every culture with at least one readable catalog, the matrix records one
state:

- **`served`** — the key exists in that culture's own catalogs.
- **`fallback`** — the key is absent locally but exists in a parent culture or the default culture.
- **`missing`** — the key is absent from the whole chain.

The chain matches the runtime: requested culture, parents, invariant excluded, then the default
culture. A `fallback` cell is not a `missing-key` finding, but it still shows that the culture is not
translated for that key. Fallback keeps the application usable; the matrix tells you where coverage
is only nominal.

ResX needs a naming convention. The neutral file serves the default culture; satellite-style names
carry the culture after the first dot (`Messages.ar.resx` serves `ar`). Unrecognized names are listed
under `ignoredFiles` rather than silently skipped.

## Finding kinds

| Finding | Meaning | Typical fix |
|---|---|---|
| `missing-key` | A referenced key is missing in a culture and its fallback chain. | Add the entry or correct the reference. |
| `unused-key` | A catalog key is never referenced in source. | Remove it, mark it reserved, or exclude it with a reason. |
| `template-drift` | One culture/key is served with different templates across formats. | Make one format authoritative and regenerate the other. |
| `format-gap` | A culture serves the key in one format but not another serving format. | Add the entry or stop serving that format. |
| `placeholder-drift` | A culture's placeholder set differs from the reference culture's. | Restore or remove the placeholder. |
| `unresolved-reference` | A `*Keys.Name` reference has no constant definition. | Add the constant or fix the typo. |
| `definition-conflict` | One constant name is defined with different values. | Keep one definition. |
| `catalog-error` | A catalog cannot be read or parsed. | Fix the syntax; the finding carries a line. |

Findings are sorted by kind, key, culture, path, and line. All paths are relative to the working
directory with forward slashes.

The placeholder scanner is balanced-brace and ICU-aware. `{name}` contributes `name`; `{0}`
contributes `0`; `{count, plural, one {# item} other {# items}}` contributes `count` only because
branch-internal placeables are conditional; `{amount, number, ::.00}` contributes `amount`. Unbalanced
braces and `{}` are literal text, and empty templates are skipped as untranslated.

Sets are ordinal, so reordering named placeholders is not drift. Dropping or adding a positional
`{0}` or `{1}` is. The reference set comes from the default culture when it serves the key locally,
otherwise from the ordinal-first culture that serves it. Only cultures serving the key locally are
compared; fallback is the matrix's concern. This catches the most common translation defect: a
rewritten sentence loses a placeholder, and the runtime either leaves it literal or throws in the ICU (International Components for Unicode)
formatter.

## Unused keys and false positives

`unused-key` is intentionally noisy. The extractor sees static references, not keys built with string
concatenation, selected by an external consumer, or shared with another repository. Make the exception
explicit:

```bash
jeninnet-localization-keys --catalog ./Localization --source ./src \
  --exclude Demo.MissingEverywhere \
  --exclude ar:Demo.Partial \
  --exclude Public.Api.Key
```

Every exclusion should have a reason in review. A long exclusion list signals a key-strategy or
extraction-boundary problem, not a tool defect.

`missing-key` has a similar dynamic boundary. A wrapper method that takes a key and calls
`IMessageLocalizer` internally is not recognized unless its name matches the extractor's curated
list. Call the canonical methods directly, or add the reference to a `--keys-file`.

`placeholder-drift` can be a false positive when two languages legitimately need different
placeholders. That is rare and should be reviewed; exclude the specific key and culture with a
comment rather than blanket-excluding the key.

## Baseline discipline

1. **Before translation.** Run on the source catalog. Fix missing source keys and unresolved
   references. This is the baseline translators receive.
2. **After import.** Run again. Expect new `served` cells and no new `missing` cells. Review every
   `placeholder-drift` and `StaleSource`.
3. **Before release.** Run on the exact catalog files that will ship. A green build with a stale
   catalog is not evidence.
4. **After key renames.** The old key becomes unused and the new key may be missing. Run the check in
   the same change set.

Keep the JSON report as a CI artifact. Diffing two reports shows what the round actually changed. If
you commit a golden report, pin the tool version and expect it to change with the catalog; do not use
a golden console snapshot as the only gate.

## CI wiring

```yaml
name: localization-parity

on:
  pull_request:
    paths:
      - "src/**"
      - "Localization/**"
      - "tests/**"

jobs:
  parity:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7.0.1
      - uses: actions/setup-dotnet@v6.0.0
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - name: Parity check
        run: >
          dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys --
          --catalog ./Localization
          --source ./src
          --default-culture en
          --report artifacts/parity.json
      - name: Upload parity report
        if: always()
        uses: actions/upload-artifact@v7.0.2
        with:
          name: parity-report
          path: artifacts/parity.json
```

The step fails when the tool exits `1`. Create the report directory first. For a published tool,
replace the run step with the install command and `jeninnet-localization-keys`.

## Worked example: catching a bad import

A German import changes `Checkout.Summary` from `Your order contains {count} items.` to
`Ihre Bestellung enthält Artikel.` The placeholder is gone.

```text
[placeholder-drift] Checkout.Summary [de] Localization/de.json:42
  Key 'Checkout.Summary' has placeholder drift for culture 'de':
  missing {count} (reference culture 'en' uses placeholders {count}).
```

The build fails at exit `1`. The reviewer restores `{count}`, re-imports, and the next run is clean.
Without the check, the defect would surface only when a German user opened the checkout page and the
template rendered `{count}` literally or the ICU formatter threw.

## Pitfalls

**Pitfall:** treating a `fallback` cell as translated. It is covered by another culture, not by the
requested one.

**Pitfall:** using `--exclude` to silence a finding without recording why. The exclusion list is part
of the quality contract and should be reviewed like code.

**Pitfall:** assuming the tool sees dynamically composed keys. It does not. Use constants or extend
the extraction surface.

**Pitfall:** expecting SARIF. The tool writes console output and a versioned JSON report only.

**Pitfall:** running the check against a different working directory than the one used to produce the
report. Paths are relative to the working directory; CI and local runs should use the same root.

**Pitfall:** letting an unreadable catalog pass because the console was truncated. `catalog-error`
exits `1`; read the JSON report when the console output is long.

## Where to go next

- [Tooling reference](../03-reference/05-tooling-reference.md) for the CLI, report schema, generator,
  and analyzer details.
- [Strongly typed keys](05-strongly-typed-keys.md) for the constants the extractor harvests.
- [Translator workflow](11-translator-workflow.md) for the round trip this gate protects.
