# Jeninnet.Localization.Analyzers.Localizability

`Jeninnet.Localization.Analyzers.Localizability` is a Roslyn analyzer and code fix that flags hardcoded user-facing strings at authoring time. Its suggestion rule, `JLNLOC001`, points UI text, exception messages, validation messages, and view-dictionary entries toward the message pipeline; the warning rules cover composite shapes (`JLNLOC002` concatenated fragments, `JLNLOC003` uninterpolated holes) and catalog-backed findings (`JLNLOC004` placeholder mismatches, `JLNLOC005` missing keys, `JLNLOC006` unused keys). The package is build-time only and sits alongside the key generator in the [strongly typed keys](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/05-strongly-typed-keys.html) workflow.

## Installation

```bash
dotnet add package Jeninnet.Localization.Analyzers.Localizability
```

Reference it as a build-time analyzer so it is not treated as a runtime library:

```xml
<ItemGroup>
  <PackageReference Include="Jeninnet.Localization.Analyzers.Localizability"
                    Version="1.0.0-preview.1"
                    PrivateAssets="all" />
</ItemGroup>
```

The package ships `analyzers/dotnet/cs` assets with `DevelopmentDependency=true`. The analyzer and its code fix travel in one package; the sibling `...Localizability.CodeFixes` project is not packable and is never referenced by application code.

## Registration

No services are registered. Adding the package reference is the entire integration; the Roslyn host loads the analyzer and the code fix automatically.

To raise the severity through MSBuild, declare the property visible to the compiler:

```xml
<PropertyGroup>
  <LocalizabilitySeverity>warning</LocalizabilitySeverity>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="LocalizabilitySeverity" />
</ItemGroup>
```

Without `CompilerVisibleProperty`, the property does not reach the analyzer and the default severity applies.

## Configuration

There is no options type. Severity and suppression use standard Roslyn mechanisms:

| Mechanism | Effect |
|----|----|
| `LocalizabilitySeverity` | `suggestion` or `hidden` maps to `Hidden`, `warning` to `Warning`, `error` to `Error`. Default is `Hidden` for `JLNLOC001` and `Warning` for `JLNLOC002`–`JLNLOC006`. |
| `dotnet_diagnostic.JLNLOC001.severity` | Per-project or per-folder `.editorconfig` severity. `.editorconfig` wins on conflict. |
| `dotnet_diagnostic.JLNLOC002.severity` (and so on per rule ID) | Same mechanism for the newer rules; each rule falls back to its own default. |
| `#pragma warning disable JLNLOC001` | Line or region suppression; substitute any rule ID. |
| `[SuppressMessage("Localizability", "JLNLOC001", Justification = "...")]` | Attribute suppression with a reviewable justification. |

```ini
[*.{cs,vb}]
dotnet_diagnostic.JLNLOC001.severity = warning

[tests/**/*.cs]
dotnet_diagnostic.JLNLOC001.severity = none
```

## Usage

The rule flags curated shapes rather than every string literal:

- UI text assignments to `Text`, `Title`, `Header`, `Content`, `Caption`, `ToolTip`, and `PlaceholderText` on WinForms, WPF, Web Forms, and Blazor `ComponentBase` receivers.
- `ViewData["..."]` and `ViewBag....` entries.
- `MessageBox.Show("...")`.
- Exception constructors, including `ValidationResult`.
- Validation messages in `AddModelError(key, "...")`, validation-attribute `ErrorMessage` arguments, and `[Display(Name = "...")]` / `[DisplayName("...")]`.
- `Results.Problem("...")` and `TypedResults.Problem("...")`.

```csharp
[Required(ErrorMessage = "This field is required.")]
public string Name { get; set; } = string.Empty;

throw new InvalidOperationException("The order is already closed.");
```

`JLNLOC001` reports on both literals with a suggestion-level diagnostic. Move the text to a catalog and resolve it through `IMessageLocalizer`, or suppress with a justification when the literal is intentional.

The warning rules subdivide the composite shapes. `JLNLOC002` fires on `+` concatenations containing a string literal in the same sinks; the code fix converts the chain to an interpolated string. `JLNLOC003` fires on interpolated strings in flagged sinks. `JLNLOC004` fires on `string.Format` whose first argument resolves to a catalog key whose template the remaining arguments cannot fill. `JLNLOC005` fires on generated `*Keys` constant references and key-position literals (`localizer["..."]`, `GetMessageTemplateAsync("...", ...)`) whose value no visible catalog serves. `JLNLOC006` fires once per catalog key never referenced in the compilation, on the key's catalog line. The catalog-backed rules read the generator's catalog inputs (`*.loc.json`, `*.resx`, `*.po`, `*.xliff`/`.xlf`) registered as `AdditionalFiles` and stay silent when no catalog is visible; malformed catalogs contribute no findings.

Everything outside the curated shapes is quiet by construction: identifiers, logging calls, test assertions, local initializers, empty strings, and compositions outside key positions. Assignments through `string.Format` or `string.Concat` with a literal format are flagged; other compositions are a documented boundary.

## Behavior and limits

- **Diagnostic.** `JLNLOC001` is enabled by default at `Hidden` severity in the `Localizability` category. It is a suggestion, not a build blocker, unless the host raises it. `JLNLOC002`–`JLNLOC006` default to `Warning`.
- **Generated code.** Generated code is not analyzed (`GeneratedCodeAnalysisFlags.None`).
- **Code fix.** When the flagged literal exactly equals a `const string` in a type whose name ends in `Keys` - hand-written like `ReferenceKeys` or generator-produced `LocalizationKeys` - the fix replaces the literal with a fully qualified constant reference. The replacement needs no new `using`. A `JLNLOC002` concatenation offers a second fix converting the `+` chain to an interpolated string (multiline text is excluded).
- **No automatic extraction.** Literals with no matching constant get no fix. The analyzer never writes catalogs; moving text into a catalog remains an explicit authoring decision. The parity tool reports the resulting gaps.
- **No fix-all.** The code fix provides a single-diagnostic fix only.
- **Platform.** The analyzer and code fix target `netstandard2.0`, so the assets load in the dotnet CLI, test harness, and Visual Studio.
- **No runtime dependency.** `IncludeBuildOutput=false`, `DevelopmentDependency=true`, and `SuppressDependenciesWhenPacking=true` keep the package out of the application's runtime graph.

## AOT and trimming

**Build-time only.** The project targets `netstandard2.0` and runs in the compiler/Roslyn host, not in the published application, so the runtime AOT (Ahead-of-Time)/trimming contract does not apply. See [AOT and trimming](https://tareknajem04.github.io/Jeninnet.Localization/en/aot-and-trimming.html) for the contract and exclusions.

## Related packages

- [Jeninnet.Localization.Generators.Keys](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/generators/Jeninnet.Localization.Generators.Keys/README.md) - compile-time key constants the code fix can reference.
- [Jeninnet.Localization.Tools.Keys](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/tooling/Jeninnet.Localization.Tools.Keys/README.md) - parity checks after literals are moved into catalogs.
- [Jeninnet.Localization.Core](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/Jeninnet.Localization.Core/README.md) - `IMessageLocalizer` and the formatting pipeline.

## Documentation

- [Strongly typed keys](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/05-strongly-typed-keys.html)
- [Parity and quality gates](https://tareknajem04.github.io/Jeninnet.Localization/en/02-guides/13-parity-and-quality-gates.html)
- [Tooling reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/05-tooling-reference.html)
- [Diagnostics reference](https://tareknajem04.github.io/Jeninnet.Localization/en/03-reference/06-diagnostics-reference.html)
- [Package map](https://tareknajem04.github.io/Jeninnet.Localization/en/appendix/02-package-map.html)

## License

MIT. See [LICENSE](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE).
