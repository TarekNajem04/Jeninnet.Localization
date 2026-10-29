# Jeninnet.Localization.Consumer

A runnable consumer application that validates the solution exactly as a NuGet consumer sees it. It resolves the libraries from the packed packages in the local feed through `PackageReference` — never `ProjectReference` — and drives an end-to-end checkout scenario that would fail if a package payload, dependency, generator asset, or analyzer asset were missing.

The project is non-packable (`IsPackable=false`) and belongs to the separate [`Jeninnet.Localization.Consumers.slnx`](../Jeninnet.Localization.Consumers.slnx) solution because it can only restore after the main solution has been packed.

## What the scenario proves

`CheckoutScenario.RunAsync` exercises three independent package surfaces in one run:

1.  **Canonical pipeline over the Memory Provider** using constants emitted by the packed key generator (`LocalizationKeys.CHECKOUT_THANK_YOU`, `LocalizationKeys.CHECKOUT_TOTAL`).
2.  **The same pipeline over per-culture JSON catalogs** for `en` and `ar`, proving the packed JSON Provider and its runtime content assets work.
3.  **A translator hand-off through the CSV interchange package**, exporting the resolved source/target pair with the canonical `key,culture,value,note` schema.

The app prints the English confirmation, the English total, the Arabic confirmation, and the CSV:

```text
Thank you, Lina!
Total: 133.38
شكراً لك يا Lina!
CSV:
key,culture,value,note
...
```

A missing package, a missing `lib/` payload, a missing analyzer/generator asset, or a dependency that is not in the feed fails this run.

## Prerequisites

- .NET 10 SDK.
- The packed solution in `artifacts/`. The consumer restores from `../artifacts` through [`consumers/nuget.config`](../nuget.config), so `dotnet pack` must run first.
- No external service, credential, or database.

## Run

From the repository root:

```powershell
# 1) Pack the solution into the local feed. The pack hook copies every package to artifacts/.
dotnet pack Jeninnet.Localization.slnx -c Release

# 2) Run the consumer app against the packed packages.
dotnet run --project consumers/Jeninnet.Localization.Consumer -c Release
```

Build and test the whole consumers solution:

```bash
dotnet test consumers/Jeninnet.Localization.Consumers.slnx -c Release
```

The consumer restore uses an isolated global-packages folder (`../artifacts/consumer-packages`) so a cached machine-wide package cannot mask a stale pack. The pack hook invalidates that folder after every pack.

## Project layout

| Path | Purpose |
|----|----|
| `Program.cs` | Runs the scenario and prints the localized output and CSV |
| `CheckoutScenario.cs` | The three-step end-to-end consumer scenario |
| `CheckoutResult.cs` | Immutable result record returned by the scenario |
| `Localization/consumer.loc.json` | Generator additional file; source of the emitted key constants |
| `Localization/en.json`, `ar.json` | Runtime catalogs for the packed JSON Provider |
| `Jeninnet.Localization.Consumer.csproj` | Package references, analyzer assets, and content staging |

## Package references and versioning

The project references the packaged libraries with `VersionOverride="$(Version)"` so `Directory.Build.props` remains the single source of truth for the solution version. The generator and analyzer are referenced as analyzer assets:

```xml
<PackageReference Include="Jeninnet.Localization.Generators.Keys"
                  VersionOverride="$(Version)"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
<PackageReference Include="Jeninnet.Localization.Analyzers.Localizability"
                  VersionOverride="$(Version)"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

The generator consumes `Localization\consumer.loc.json` as an `AdditionalFile`; the JSON catalogs are copied to the output directory for the Provider.

## Related documentation

- [Consumers overview](../README.md)
- [Consumer package-validation tests](../Jeninnet.Localization.Consumer.Tests/README.md)
- [Core package](../../src/Jeninnet.Localization.Core/README.md)
- [Memory Provider](../../src/providers/Jeninnet.Localization.Providers.Memory/README.md)
- [JSON Provider](../../src/providers/Jeninnet.Localization.Providers.Json/README.md)
- [CSV interchange](../../src/interchange/Jeninnet.Localization.Interchange.Csv/README.md)
- [Key generator](../../src/generators/Jeninnet.Localization.Generators.Keys/README.md)
- [Repository README](../../README.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
