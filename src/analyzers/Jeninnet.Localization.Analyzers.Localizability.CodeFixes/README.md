# Jeninnet.Localization.Analyzers.Localizability.CodeFixes

The code-fix companion for the Jeninnet localizability analyzer (`JLNLOC001`). When the analyzer flags a hardcoded user-facing string and that exact string already exists as a `const string` in a type whose name ends in `Keys`, the fix replaces the literal with a fully qualified constant reference.

The project is **not a package** and is never referenced by application code. It is a companion assembly that ships inside the [`Jeninnet.Localization.Analyzers.Localizability`](../Jeninnet.Localization.Analyzers.Localizability/README.md) package under `analyzers/dotnet/cs`.

## What the fix does

1.  The analyzer reports `JLNLOC001` for a curated set of user-facing string shapes (UI text, exceptions, validation messages, `ViewData`/`ViewBag`, `Display` attributes, `Results.Problem`, and similar).
2.  The code fix searches all source types whose name ends in `Keys` for a `const string` field whose value equals the flagged literal.
3.  If a match exists, it offers the action `Use <Type>.<Field>` and replaces the literal with the fully qualified constant expression, preserving the original trivia.
4.  If several constants share the value, it selects the ordinally smallest qualified name so the result is deterministic.
5.  If no constant matches, it offers no fix. The diagnostic remains until the author moves the text into a catalog; the fix never extracts text automatically.

```csharp
// Before — JLNLOC001
throw new InvalidOperationException("The order is already closed.");

// After — the literal now references the matching constant
throw new InvalidOperationException(ReferenceKeys.ORDER_ALREADY_CLOSED);
```

## Why this is a separate project

Code fixes require `Microsoft.CodeAnalysis.Workspaces`, and Roslyn rule RS1038 forbids workspace references in analyzer assemblies because they can change command-line compiler behavior. Splitting the fix into its own project keeps the analyzer clean while still producing one package for consumers.

The project is `IsPackable=false`; the analyzer package includes the compiled fix assembly. Consumers add only the analyzer package, never this project.

## Behavior and limits

- **Diagnostic ID:** `JLNLOC001` only.
- **Matching scope:** source types whose names end in `Keys`; both hand-written contracts (for example `ReferenceKeys`) and generator-produced types (for example `LocalizationKeys`) qualify.
- **Exact value match:** the comparison is ordinal and value-based; there is no fuzzy or case-insensitive matching.
- **Fully qualified replacement:** no `using` directive is added or changed.
- **Single fix only:** `GetFixAllProvider()` returns `null`; there is no fix-all or solution-wide apply.
- **No extraction:** the fix never writes catalogs or creates constants.
- **Generated code:** generated documents are not analyzed, so generated literals are not offered a fix.
- **Platform:** the assembly targets `netstandard2.0`, so it loads in the .NET CLI, test hosts, and Visual Studio.

## Build and test

Build the project from the repository root:

```bash
dotnet build src/analyzers/Jeninnet.Localization.Analyzers.Localizability.CodeFixes \
    --configuration Release
```

The fix is covered by the main test suite through `LocalizabilityCodeFixTests` in [`tests/Jeninnet.Localization.Tests`](../../../tests/Jeninnet.Localization.Tests/README.md), including end-to-end `AdhocWorkspace` applications. The analyzer package layout — the analyzer DLL and this fix DLL under `analyzers/dotnet/cs` — is verified by the consumer package-validation tests:

```bash
dotnet test consumers/Jeninnet.Localization.Consumers.slnx --configuration Release
```

## AOT and trimming

**Build-time only.** The project targets `netstandard2.0` and runs in the compiler/Roslyn host, not in the published application, so the runtime AOT (Ahead-of-Time)/trimming contract does not apply. See [AOT and trimming](../../../docs/en/aot-and-trimming.md) for the contract and exclusions.

## Related packages

- [`Jeninnet.Localization.Analyzers.Localizability`](../Jeninnet.Localization.Analyzers.Localizability/README.md) — the analyzer and package that carries this fix.
- [`Jeninnet.Localization.Generators.Keys`](../../generators/Jeninnet.Localization.Generators.Keys/README.md) — compile-time key constants the fix can reference.
- [`Jeninnet.Localization.Tools.Keys`](../../tooling/Jeninnet.Localization.Tools.Keys/README.md) — parity checks after literals move into catalogs.
- [`Jeninnet.Localization.Core`](../../Jeninnet.Localization.Core/README.md) — `IMessageLocalizer` and the formatting pipeline.

## Documentation

- [Strongly typed keys](../../../docs/en/02-guides/05-strongly-typed-keys.md)
- [Parity and quality gates](../../../docs/en/02-guides/13-parity-and-quality-gates.md)
- [Tooling reference](../../../docs/en/03-reference/05-tooling-reference.md)
- [Diagnostics reference](../../../docs/en/03-reference/06-diagnostics-reference.md)
- [Package map](../../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../../LICENSE).
