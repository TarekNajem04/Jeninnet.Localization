# Consumers (package validation)

This folder validates the solution **as a consumer sees it**:

- [`Jeninnet.Localization.Consumer`](Jeninnet.Localization.Consumer/README.md) resolves the libraries from the **packed packages** in the local feed (`../artifacts`, populated by `dotnet pack`) through `PackageReference`, never `ProjectReference`. That is the point: a broken package — missing payload, missing dependency, wrong analyzer/generator/tool layout — fails here even though the source build is green.
- [`Jeninnet.Localization.Consumer.Tests`](Jeninnet.Localization.Consumer.Tests/README.md) drives the consumer scenario end to end and validates the package set itself: inventory against the committed public-API baselines, payload and metadata contents, analyzer/generator/tool asset layout, and the dependency closure inside the feed.

## Why this folder has its own solution

[`Jeninnet.Localization.Consumers.slnx`](Jeninnet.Localization.Consumers.slnx) is intentionally separate from `Jeninnet.Localization.slnx`: the consumers restore from the local feed, so any *solution-wide* restore requires the packages to exist first. Keeping them out of the repository solution means a fresh clone builds, tests, and formats without a pack step, while CI performs the pack → restore → build → test sequence explicitly (the `consumer-validation` job in `.github/workflows/ci.yml`). The tag-driven NuGet workflow runs the same validation before uploading or pushing packages, so a packaging regression blocks publication.

## Prerequisites

- .NET 10 SDK.
- The packages produced by the main solution. The consumers restore through [`nuget.config`](nuget.config), which points at `../artifacts` and uses an isolated `../artifacts/consumer-packages` global-packages folder.

## Run it locally

```powershell
# 1) Pack the solution into the local feed. The Directory.Build.targets pack hook copies every
#    produced package into artifacts/, which consumers/nuget.config uses as a source.
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet pack Jeninnet.Localization.Windows.slnx -c Release

# 2) Build and test the consumers against the packed packages.
dotnet test consumers/Jeninnet.Localization.Consumers.slnx -c Release

# 3) Run the consumer application.
dotnet run --project consumers/Jeninnet.Localization.Consumer -c Release
```

The pack hook invalidates the isolated consumer package cache after every pack, so the next restore always sees the freshly produced packages instead of a machine-wide copy.

## What can fail here

| Failure | Meaning |
|----|----|
| A package is missing from the feed | `dotnet pack` did not produce it, or the version does not match the solution version |
| A package lacks `README.md`, `LICENSE`, or a payload | The package metadata or build output is incomplete |
| The analyzer package lacks the code-fix DLL | The analyzer/code-fix asset layout regressed |
| The generator or tool package lacks its asset | The package asset path regressed |
| A dependency is not in the feed | The package dependency closure is incomplete |
| The scenario fails to resolve a key | A runtime payload, catalog, or generator constant is missing |

## Related documentation

- [Consumer application](Jeninnet.Localization.Consumer/README.md)
- [Consumer package-validation tests](Jeninnet.Localization.Consumer.Tests/README.md)
- [Repository README](../README.md)
- [Package map](../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../LICENSE).
