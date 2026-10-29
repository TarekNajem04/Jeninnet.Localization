# Jeninnet.Localization.Consumer.Tests

Package-validation tests for the packed solution, written from the consumer's side. The suite verifies that the local feed contains every expected package with the right version, metadata, payload, and dependency closure, and it drives the consumer scenario against those packages.

The project is non-packable (`IsPackable=false`, `IsTestProject=true`), contains 11 test methods, and references the consumer application as a project. It belongs to the separate [`Jeninnet.Localization.Consumers.slnx`](../Jeninnet.Localization.Consumers.slnx) solution.

## What the suite covers

| Test file | Scope |
|----|----|
| `PackageValidationTests.cs` | Feed inventory, package metadata, payload layout, analyzer/generator/tool assets, dependency closure |
| `CheckoutScenarioTests.cs` | End-to-end scenario through the packed packages, CSV schema, generator constants, argument validation |
| `MSTestSettings.cs` | MSTest configuration |

### Package validation

- **Inventory:** the feed carries every packable package at the solution version. The expected set is owned by the 35 committed API baselines.
- **Metadata and payload:** every package ships `LICENSE`, `README.md`, a `lib/`, `analyzers/`, or `tools/` payload, the MIT license expression, source-repository metadata, and the solution version.
- **Analyzer package:** ships both the analyzer DLL and the code-fix DLL under `analyzers/dotnet/cs`.
- **Generator package:** ships the incremental generator assembly under `analyzers/dotnet/cs`.
- **Tool package:** ships `DotnetToolSettings.xml` and the tool assembly under `tools/net10.0/any`, with the documented command name `jeninnet-localization-keys`.
- **Dependency closure:** every `Jeninnet.Localization.*` dependency of every expected package resolves inside the feed.

### Scenario validation

- The Memory pipeline resolves generator-emitted keys with formatted arguments.
- The `ar` JSON catalog resolves the same key through the canonical pipeline.
- The CSV export contains the canonical header, key, source value, and Arabic target value.
- The generated constants match the catalog contract (`Checkout.ThankYou`, `Checkout.Total`, and the `All` table).
- The scenario rejects an empty base directory with `ArgumentException`.

## Prerequisites

- .NET 10 SDK.
- The packed solution in `artifacts/`. Run `dotnet pack Jeninnet.Localization.slnx -c Release` before restoring or testing these projects.
- No external service, credential, or database.

## Run

From the repository root:

```powershell
# 1) Pack the solution into the local feed.
dotnet pack Jeninnet.Localization.slnx -c Release

# 2) Build and test the consumers solution.
dotnet test consumers/Jeninnet.Localization.Consumers.slnx -c Release
```

The test project restores through [`consumers/nuget.config`](../nuget.config), which points at `../artifacts` and uses the isolated `../artifacts/consumer-packages` global-packages folder. The pack hook invalidates that folder after every pack, so the tests always inspect the freshly packed output.

## Why this is a separate solution

The consumer restore depends on packages that do not exist in a fresh clone. Keeping the consumers in their own solution means a normal clone can build, test, and format the repository without a pack step, while CI runs the explicit pack → restore → build → test sequence in the `consumer-validation` job.

## Related documentation

- [Consumers overview](../README.md)
- [Consumer application](../Jeninnet.Localization.Consumer/README.md)
- [Repository packaging notes](../../README.md)
- [Package map](../../docs/en/appendix/02-package-map.md)
- [API baselines](../../tests/Jeninnet.Localization.Tests/README.md)
- [Repository README](../../README.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
