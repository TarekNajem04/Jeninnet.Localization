# Tooling Reference

The repository ships three build-time tools: a key generator, a localizability analyzer with a code
fix, and the parity command-line tool. None of them is a runtime dependency. The generator and the
analyzer target `netstandard2.0` for Roslyn host compatibility; the parity tool targets `net8.0`
and `net10.0` and is packaged as a .NET tool.

## The parity tool

Package: `Jeninnet.Localization.Tools.Keys`. Command:
`jeninnet-localization-keys`. There are no subcommands. The tool has one check command and exits with
a result code.

```bash
dotnet tool install --global Jeninnet.Localization.Tools.Keys
jeninnet-localization-keys --catalog ./Localization --source ./src --report parity.json
```

From a repository checkout before publication:

```bash
dotnet run --project src/tooling/Jeninnet.Localization.Tools.Keys -- \
  --catalog ./Localization --source ./src --report parity.json
```

### Options

| Option | Required | Repeatable | Meaning |
|---|---|---|---|
| `--catalog <dir>` | Yes | Yes | Catalog root scanned recursively for `.json`, `.xml`, `.resx`, `.po`. |
| `--source <dir>` | Yes | Yes | Source root scanned recursively for `.cs`, `.razor`, `.cshtml`. |
| `--keys-file <file>` | No | Yes | Extra C# file parsed for `const string` definitions. |
| `--exclude <key>` | No | Yes | Exclude a key everywhere, or a culture-scoped `culture:key`. |
| `--default-culture <tag>` | No | No | Default culture closing fallback chains. Default `en`. |
| `--report <path>` | No | No | Write the JSON report to this path. |
| `--help`, `-h`, `-?` | No | No | Print usage. |
| `--version` | No | No | Print the tool assembly version. |

At least one `--catalog` root and one `--source` root are required. Directories must exist. A missing
keys file or an unknown default culture is a usage error.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Clean: no findings. |
| `1` | One or more parity findings. |
| `2` | Usage or environment error: bad arguments, missing directory, unreadable input root. |

The tool is report-only. It never writes catalogs, never applies translations, and never modifies
source. Write-back is out of scope; the governed store owns writes.

### Extraction rules

C# is parsed syntactically with Roslyn; no compilation is needed. The extractor recognizes:

- `*Keys.Member` accesses whose owner identifier ends in `Keys` and whose member is not invoked.
- String literals in the first argument of `GetMessageTemplateAsync`, `GetMessageTemplatesAsync`,
  `GetPluralTemplateAsync`, `GetPluralTemplateWithContextAsync`, `LocalizeAsync`, `Localize`,
  `LocalizePluralAsync`, `LocalizeWithContextAsync`, and `GetString`.
- Single-string indexers on receivers whose name ends in `Localizer` (`localizer["Key"]`).
- `const string` definitions in `*Keys` classes and in `--keys-file` inputs.

Razor views are scanned textually for member accesses and key-position literals. Generated files
(`*.g.cs`, `*.Designer.cs`) and `bin`/`obj` are skipped. Generated constants on disk are harvested as
definitions.

### Report schema

`--report` writes JSON with `schemaVersion: 2`, `tool`, `toolVersion`, `defaultCulture`, `catalogs`,
`ignoredFiles`, `sources`, `keyDefinitions`, `excludedKeys`, `matrix`, `findings`, and `summary`.
A finding is `{ kind, key, culture, message, locations[] }`; the summary has `total` plus one counter
per kind (`missingKeys`, `unusedKeys`, `templateDrifts`, `formatGaps`, `placeholderDrifts`,
`unresolvedReferences`, `definitionConflicts`, `catalogErrors`).

`matrix[].cultures[].state` is `served`, `fallback`, or `missing`. `schemaVersion` bumps on a
breaking shape change; version 2 added `placeholder-drift` and `placeholderDrifts`. All paths are
relative to the working directory with forward slashes. Findings are ordered by kind, key, culture,
path, and line.

### Finding kinds

- `missing-key` / `unused-key` — referenced but unserved, or served but unreferenced.
- `template-drift` / `format-gap` — formats disagree, or one format omits a served key.
- `placeholder-drift` — the placeholder set differs from the reference culture.
- `unresolved-reference` / `definition-conflict` — a `*Keys` name has no definition or conflicting
  values.
- `catalog-error` — unreadable or malformed catalog, with a line number.

There is no SARIF output. The JSON report and the deterministic console output are the only formats.

## Key generator

Package: `Jeninnet.Localization.Generators.Keys`. It is a Roslyn incremental source generator shipped
as an analyzer asset with `DevelopmentDependency=true`. It never becomes a runtime dependency.

### Inputs

Catalog files are declared as additional files, and only names ending in `.loc.json` are considered:

```xml
<ItemGroup>
  <AdditionalFiles Include="Localization\en.loc.json" />
  <AdditionalFiles Include="Localization\ar.loc.json" />
</ItemGroup>
```

Each file is a flat JSON object mapping string keys to string templates. The generator rejects:

- malformed JSON or a non-object root;
- non-string keys or values;
- duplicate keys in one file;
- two keys that sanitize to the same identifier;
- a key with no identifier characters;
- an invalid configured namespace.

### Namespace configuration

The default namespace is `{AssemblyName}.Generated`. Override it with an MSBuild property that is
visible to the compiler:

```xml
<PropertyGroup><LocalizationKeysNamespace>MyApp.Localization</LocalizationKeysNamespace></PropertyGroup>
<ItemGroup><CompilerVisibleProperty Include="LocalizationKeysNamespace" /></ItemGroup>
```

Without `CompilerVisibleProperty`, the generator silently uses the default namespace. The build still
succeeds, so the wrong namespace is easy to miss.

### Output

The generator emits one compilation unit containing:

```csharp
public static partial class LocalizationKeys {
    public const string APP_TITLE = "App.Title";
    public const string CART_ITEMS = "Cart.Items";
    public static readonly string[] All = new string[] { "App.Title", "Cart.Items" };
}
```

The identifier is upper snake case. Separators and camel/acronym boundaries split words; a leading
digit gains an underscore. Constants and `All` are ordinal-sorted. Multiple catalogs contribute to
one class. The output is deterministic and byte-identical for fixed input.

The generator emits keys only, never message text. It follows every declared catalog, so a key
present only in a translation file still generates a constant.

### Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| `JLNKEY001` | Error | The catalog is not well-formed JSON, or its root is not an object. |
| `JLNKEY002` | Error | An entry does not map a string key to a string template. |
| `JLNKEY003` | Error | The same key occurs more than once in one file. |
| `JLNKEY004` | Error | Two distinct keys sanitize to the same C# identifier. |
| `JLNKEY005` | Error | A key contains no identifier characters. |
| `JLNKEY006` | Error | The configured namespace is not a legal dotted C# identifier. |

Every failure is a build error with a file and line where the content supplies one. Generation is
incremental; unchanged catalogs reuse cached parse results.

## Localizability analyzer

Package: `Jeninnet.Localization.Analyzers.Localizability`; the package also carries the code-fix
assembly. The single rule is `JLNLOC001`.

### What is flagged

- UI text assignments on UI receivers: `Text`, `Title`, `Header`, `Content`, `Caption`, `ToolTip`,
  and `PlaceholderText` on WinForms, WPF, Web Forms, and `ComponentBase` types.
- `ViewData["..."]` and `ViewBag....` entries.
- `MessageBox.Show("...")`.
- Exception constructors, including `ValidationResult`.
- Validation messages in `AddModelError`, `ErrorMessage` arguments, and `Display`/`DisplayName`
  attributes.
- `Results.Problem("...")` and `TypedResults.Problem("...")`.

Identifiers, logging calls, test assertions, local initializers, empty strings, and non-key
compositions are quiet by construction. Assignments through `string.Format` or `string.Concat` with a
literal format are flagged; other compositions are a documented boundary.

### Severity and suppression

The descriptor defaults to `Hidden`. Opt into a higher severity with the MSBuild property
`LocalizabilitySeverity` (`suggestion`, `warning`, or `error`) declared visible via
`<CompilerVisibleProperty Include="LocalizabilitySeverity" />`, or with
`dotnet_diagnostic.JLNLOC001.severity = warning` in `.editorconfig`. `.editorconfig` wins on
conflict. Standard suppression works: `#pragma warning disable JLNLOC001`, `[SuppressMessage]`, or a
per-folder section.

`suggestion` and `hidden` both map to `DiagnosticSeverity.Hidden`. `warning` and `error` map to their
namesake severities.

### Code fix

When the flagged literal exactly equals a `const string` in a type whose name ends in `Keys` — hand
written or generated — the fix replaces the literal with a fully qualified reference to that
constant. The replacement preserves the literal's trivia and requires no new `using`.

The fix has no fix-all provider. Literals without a matching constant get no fix. The analyzer never
extracts strings into catalogs; that remains an explicit authoring decision, and the parity tool
reports the resulting gaps.

## Worked example

Add `en.loc.json` as an `AdditionalFiles` item, set `LocalizationKeysNamespace`, add the matching
`CompilerVisibleProperty`, and build. The generated class exposes `CART_ITEMS` for call sites.

## Pitfalls

**Pitfall:** using `Content` instead of `AdditionalFiles`; no constants are generated.

**Pitfall:** omitting `CompilerVisibleProperty`; the namespace silently falls back.

## Where to go next

- [Strongly typed keys](../02-guides/05-strongly-typed-keys.md) for the workflow.
- [Parity and quality gates](../02-guides/13-parity-and-quality-gates.md) for the CI wiring.
- [Diagnostics reference](06-diagnostics-reference.md) for the IDs in one table.
