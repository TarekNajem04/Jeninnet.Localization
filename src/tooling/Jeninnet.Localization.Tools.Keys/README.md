# Jeninnet.Localization.Tools.Keys

`Jeninnet.Localization.Tools.Keys` is the `jeninnet-localization-keys` .NET tool: key extraction plus catalog parity checking. It scans source for localization key references, reads JSON, XML, ResX, and PO (Portable Object) catalogs, and reports missing keys, unused keys, template drift, format gaps, placeholder drift, and reference hygiene with file/line precision. The tool is report-only and exits non-zero when findings exist, so it fits a CI quality gate. See [Parity and quality gates](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/13-parity-and-quality-gates.html) for the workflow around it.

## Compatibility

Target frameworks: `net10.0` (.NET tool). See [compatibility and versions](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/07-compatibility-and-versions.html) for the supported matrix.

## Installation

```bash
dotnet tool install --global Jeninnet.Localization.Tools.Keys --version 1.0.0-preview.2
```

The package is a `net10.0` console application packaged as a .NET tool. Its command name is `jeninnet-localization-keys`. To run the current source tree instead, use:

```bash
dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys -- \
  --catalog ./Localization --source ./src --report artifacts/parity.json
```

## Registration

No services are registered. Installation registers the tool command; alternatively, invoke the project with `dotnet run` as shown above. In CI, install once and run the command as a step:

```bash
dotnet tool install --global Jeninnet.Localization.Tools.Keys
jeninnet-localization-keys --catalog ./Localization --source ./src --default-culture en --report artifacts/parity.json
```

## Configuration

The tool has no options file. Everything is passed on the command line:

| Option | Required | Repeatable | Meaning |
|----|----|----|----|
| `--catalog <dir>` | Yes | Yes | Catalog root scanned recursively for `.json`, `.xml`, `.resx`, and `.po`. |
| `--source <dir>` | Yes | Yes | Source root scanned recursively for `.cs`, `.razor`, and `.cshtml`. |
| `--keys-file <file>` | No | Yes | Extra C# file parsed for `const string` key definitions. |
| `--exclude <key>` | No | Yes | Exclude a key everywhere, or a culture-scoped `culture:key`. |
| `--default-culture <tag>` | No | No | Default culture closing every fallback chain. Default `en`. |
| `--report <path>` | No | No | Write the versioned JSON report to this path. |
| `--sarif <path>` | No | No | Write the SARIF 2.1.0 report to this path. |
| `--baseline <path>` | No | No | Compare against the committed baseline at this path; only new findings fail. |
| `--write-baseline <path>` | No | No | Write the current findings as a new baseline (always exits 0). |
| `--help`, `-h`, `-?` | No | No | Print usage. |
| `--version` | No | No | Print the tool assembly version. |

At least one `--catalog` root and one `--source` root are required. Directories, `--keys-file` inputs, and the default culture are validated before scanning. A missing root, an unknown culture, or an unreadable input root is a usage error.

## Usage

```bash
jeninnet-localization-keys \
  --catalog ./Localization \
  --source ./src \
  --default-culture en \
  --report artifacts/parity.json
```

The console output is a deterministic table of findings. The JSON report is written only when `--report` is supplied. A clean run prints the summary and exits `0`:

```text
jeninnet-localization-keys 1.0.0.0
catalogs: 4 file(s), 2 culture(s), 3 format(s)
sources: 12 file(s), 32 reference(s), 32 distinct key(s)
definitions: 32 constant(s)
excluded: 0 key(s)
findings: 0
summary: missing-key=0, unused-key=0, template-drift=0, format-gap=0, placeholder-drift=0, unresolved-reference=0, definition-conflict=0, catalog-error=0
result: PASS (exit 0)
```

Exclusions are explicit and reviewable:

```bash
jeninnet-localization-keys --catalog ./Localization --source ./src \
  --exclude Demo.MissingEverywhere \
  --exclude ar:Demo.Partial
```

Adopt the gate incrementally with a committed baseline. Record the current findings once, then fail only on new ones:

```bash
jeninnet-localization-keys --catalog ./Localization --source ./src --write-baseline parity-baseline.json
git add parity-baseline.json
jeninnet-localization-keys --catalog ./Localization --source ./src --baseline parity-baseline.json
```

The comparison matches on (kind, key, culture) with the file path as tiebreaker for file-level findings. Fixed findings are reported informationally (`N fixed since baseline`) and never fail the run; a missing baseline file is a usage error. The reusable GitHub Action (`.github/actions/parity`) wraps this flow and uploads SARIF to code scanning.

## Behavior and limits

- **Extraction.** C# is parsed with Roslyn syntax only; no compilation is required, so the check works even when the solution does not build. The extractor recognizes `*Keys.Member` accesses whose owner name ends in `Keys`, string literals in key positions of the canonical lookup methods (`GetMessageTemplateAsync`, `GetMessageTemplatesAsync`, `GetPluralTemplateAsync`, `GetPluralTemplateWithContextAsync`, `LocalizeAsync`, `Localize`, `LocalizePluralAsync`, `LocalizeWithContextAsync`, `GetString`), single-string indexers on receivers whose name ends in `Localizer`, and `const string` definitions in `*Keys` classes and `--keys-file` inputs. Razor views are scanned textually. Generated files (`*.g.cs`, `*.Designer.cs`) and `bin`/`obj` are skipped.
- **Completeness matrix.** For each referenced key and each culture with a readable catalog the matrix records `served`, `fallback`, or `missing`. A key absent locally but served by a parent culture or the default culture is `fallback`, not `missing`, mirroring the runtime chain.
- **Findings.** `missing-key`, `unused-key`, `template-drift` (one culture/key served with differing templates across formats), `format-gap` (a key absent from one serving format's existing catalog), `placeholder-drift` (placeholder identifier set differs from the reference culture), `unresolved-reference` (`*Keys.Name` with no definition), `definition-conflict` (one constant with differing values), and `catalog-error` (unreadable catalog, with a line number).
- **Placeholders.** The scanner is balanced-brace and ICU-aware. An ICU (International Components for Unicode) construct such as `{count, plural, ...}` contributes only its outer argument name; other `{...}` spans contribute their content verbatim. Sets are ordinal, so named-argument reordering is not drift, while adding or dropping a positional `{0}`/`{1}` is. Empty templates are skipped as untranslated.
- **Fallback chains.** ResX neutral files serve the default culture; satellite-style names carry the culture after the first dot (`Messages.ar.resx` serves `ar`). Unrecognized file names are listed as ignored files rather than silently skipped.
- **Report only.** The tool never writes catalogs, applies translations, or modifies source. The console table, the JSON report, and the SARIF report are the only formats.
- **Determinism.** Findings are ordered by kind, key, culture, path, and line. Report paths are relative to the working directory with forward slashes.
- **Exit codes.** `0` clean (or a clean baseline comparison / a written baseline), `1` one or more parity findings (or new findings versus the baseline), `2` usage or environment error (bad arguments, missing directory, unreadable input root or baseline).
- **JSON report.** `--report` writes `schemaVersion: 2` with `tool`, `toolVersion`, `defaultCulture`, `catalogs`, `ignoredFiles`, `sources`, `keyDefinitions`, `excludedKeys`, `matrix`, `findings`, and `summary`. Version 2 adds the `placeholder-drift` kind and the `placeholderDrifts` counter.
- **SARIF report.** `--sarif` writes SARIF 2.1.0 with one rule per finding kind (stable ids) and one result per finding with file/line locations and a `parity/v1` fingerprint. Levels: `error` for `catalog-error`, `missing-key`, `unresolved-reference`, and `definition-conflict`; `warning` for the rest.
- **Baseline.** `--write-baseline` writes `version: 1` with the current findings and always exits `0`. `--baseline` fails (`1`) only on findings absent from the baseline; fixed findings are informational.

## AOT and trimming

**Build-time tooling.** The package builds with `IsAotCompatible=true` and is distributed as a .NET tool; it is not part of the application's runtime graph, so the runtime AOT (Ahead-of-Time) contract does not apply to consumers. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract and exclusions.

## Related packages

- [Jeninnet.Localization.Generators.Keys](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) - the compile-time keys this tool harvests.
- [Jeninnet.Localization.Analyzers.Localizability](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/analyzers/Jeninnet.Localization.Analyzers.Localizability/README.md) - authoring-time detection of hardcoded strings.
- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) - the runtime lookup methods whose argument positions the extractor recognizes.

## Documentation

- [Parity and quality gates](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/13-parity-and-quality-gates.html)
- [Tooling reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/05-tooling-reference.html)
- [Diagnostics reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/06-diagnostics-reference.html)
- [Translator workflow](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/11-translator-workflow.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)

## License

MIT. See [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
