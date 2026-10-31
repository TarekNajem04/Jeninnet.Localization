# Compatibility and Versions

This chapter states the support posture the repository actually enforces. It is deliberately
conservative: the project is unreleased, the version number is a development placeholder, and the
compatibility promises are the ones encoded in the build, the API baselines, and the tests.

## Target framework and language

- **Runtime libraries:** `net8.0`, `net9.0`, and `net10.0`.
- **Hosts and samples:** `net10.0` (desktop hosts use `net10.0-windows`; MAUI uses `net10.0`, WinUI uses `net10.0-windows10.0.19041.0`).
- **Platform integrations:** MAUI targets `net10.0`; WinUI targets `net10.0-windows10.0.19041.0`; Avalonia follows the `net8.0`/`net9.0`/`net10.0` matrix.
- **C# language version:** 14.0. The compiler is provided by the .NET 10 SDK, while the generated
  assemblies remain compatible with every supported runtime target.
- **Nullable reference types:** enabled in every project.
- **Warnings:** treated as errors, with .NET analyzers, SonarAnalyzer, and code-style enforcement at
  build time.
- **Build-time Roslyn tooling:** the key generator, the localizability analyzer, and the code-fix
  assembly target `netstandard2.0` so they load in every Roslyn host. They never execute in the
  published application.
- **Parity tool:** `net8.0` and `net10.0`, packaged as a .NET tool with command name
  `jeninnet-localization-keys`.

The runtime libraries target .NET 8, .NET 9, and .NET 10. The compatibility boundary is enforced by
building, testing, packing, and restoring every supported target; the same public API baseline is
checked under each target for the cross-platform assemblies, and the Windows host suite covers the
platform-specific baselines. There is no support claim for older .NET versions.

> Support windows: .NET 9 is out of support and .NET 8 reaches end of support in November 2026. The
> targets remain in the build matrix to prove there is no .NET 10-only API dependency; consumers
> should plan their own upgrade path.

## Versioning

- There is one solution-wide version, `1.0.0-preview.1`, declared in `Directory.Build.props`.
- Project name, assembly name, root namespace, and package id are unified. Projects must not declare
  their own `<Version>`.
- The repository is **unreleased**. No NuGet packages have been published. The version number is a
  development placeholder, not a release declaration.
- `CHANGELOG.md` is the release-notes source. It records breaking changes under `### Changed` with a
  **Breaking:** prefix.
- There are no `[Obsolete]` members. An architecture test fails if one appears. The unreleased API
  contains canonical names only; renames happen directly rather than through obsolete shims.

Once packages are published, the committed API baselines are the compatibility gate. A change that
alters a public signature is a deliberate, reviewed event, not an accident that slips into a build.

## API baselines

Every packable assembly has a committed baseline under
`tests/Jeninnet.Localization.Tests/ApiBaselines/*.Api.txt`. The test
`PublicApiSurface_MatchesCommittedBaselines` loads each assembly, dumps its exported surface, and
compares it line by line. An accidental public addition, removal, or signature change fails with a
diff.

- The inventory is 35 packable assemblies. The 33 cross-platform assemblies share one baseline under each supported runtime target; MAUI targets `net10.0` and WinUI targets `net10.0-windows10.0.19041.0`, and both are validated by the Windows host suite.
- Regeneration is explicit: set `JENINNET_API_REGEN=1` and run the regeneration test. Without the
  variable, the test is inconclusive, so a normal test run cannot silently rewrite the baselines.
- Baselines are per assembly, deterministic, and offline. No third-party API-comparison tool is used.
- Package validation runs in CI with `EnablePackageValidation=true`, and the consumer solution
  validates the packed packages from the local feed.

The practical rule for contributors: if the baseline test fails, decide whether the API change is
intended. If it is, regenerate the baseline in the same change and explain why. If it is not, fix the
code. Do not regenerate to make a red build green.

## Breaking-change process

The repository records architecture decisions (ADs) before implementation. The process summary, without
private links:

1. **Proposal.** A change that affects a public contract, dependency direction, or package identity
   starts as a design proposal and is recorded as an architecture decision.
2. **Review.** The decision records the contract, the alternatives, and the consequences. This is
   where "should this be a new API or a rename?" is settled.
3. **Implementation.** The change lands with tests and updated documentation.
4. **Baseline update.** The affected `.Api.txt` files are regenerated deliberately.
5. **Changelog.** A breaking change is listed under `### Changed` with the **Breaking:** prefix and a
   migration note.
6. **Package validation.** The packed packages and the consumer solution must still validate.

While the project is unreleased, breaking changes are still allowed, but they follow the same
visibility rules. The changelog contains a concrete example: the XLIFF (XML Localization Interchange File Format) status type was renamed to the
shared `CatalogKeyStatus` across the interchange family, and the change is recorded as a canonical
rename in an unreleased repository.

After a public release, the same mechanism becomes the compatibility promise: a breaking change is
expected to require a major-version decision, a baseline update, a migration note, and a review of the
package dependency graph. The repository has not published a release cadence or a long-term support
window, so no such promise is claimed here.

## AOT and trimming

Every packable runtime library that targets a supported TFM (`net8.0`, `net9.0`, or `net10.0`) builds with `IsAotCompatible=true` unless it is
explicitly excluded. That enables the trim/AOT (Ahead-of-Time) analyzers and stamps trimmable metadata; warnings are
errors.

The exclusions are documented in [AOT and trimming](../aot-and-trimming.md):

- **EF Core (Entity Framework Core) store** — `IsAotCompatible=false` because EF Core is not fully trim- or AOT-safe.
- **Generator and analyzer assemblies** — `netstandard2.0` build-time tooling; the runtime contract
  does not apply.
- **Annotated reflection surfaces** — validation attributes that resolve members by reflection and
  `LocalizedResourceDiscovery` are annotated with `RequiresUnreferencedCode` /
  `DynamicallyAccessedMembers`. The annotations are honest propagation of BCL (Base Class Library) behavior.
- **Source-generated JSON** is used everywhere a serializer is used; the JavaScript endpoint maps a
  `RequestDelegate`; missing-key call-site capture uses `DiagnosticMethodInfo.Create`.

Plural rules are table-driven and compile no rules at runtime, so pluralization participates in AOT
without exclusions. The Native AOT smoke consumer publishes and runs localize/format/cache/translate
paths in CI.

## Package dependency rules

The dependency graph is intentionally shallow:

| Layer | May reference | Must not reference |
|---|---|---|
| `Abstractions` | BCL | Core, providers, integrations, storage implementations |
| `Core` | `Abstractions` | Providers, integrations, EF Core |
| `Storage` | BCL | Core, providers |
| Provider implementations | `Abstractions`; `Storage` for file models; `Database.Abstractions` for DB vendors | Core |
| EF Core store | `Abstractions`, EF Core, configuration abstractions | Core |
| Interchange packages | `Interchange.Abstractions` | Provider or Core runtime types |
| Translation providers | `Abstractions`, HTTP/Options infrastructure | Core |
| Avalonia integration | `Abstractions`, Avalonia, `Microsoft.Extensions.DependencyInjection.Abstractions` | Core |
| MAUI integration | `Abstractions`, `Microsoft.Maui.Core`, `Microsoft.Maui.Essentials`, `Microsoft.Extensions.DependencyInjection.Abstractions` | Core |
| WinUI integration | `Abstractions`, `Microsoft.WindowsAppSDK`, `Microsoft.Extensions.DependencyInjection.Abstractions` | Core |
| MVC validation integration | `Core` (the single documented `Integration → Core` edge), ASP.NET Core shared framework | Other integrations |
| Blazor, JavaScript, Diagnostics, RequestLocalization | `Abstractions`, ASP.NET Core or hosting framework references where required | Core |
| DataAnnotations integration | BCL, DataAnnotations, `Microsoft.Extensions.Localization.Abstractions` | Core, Abstractions |
| Tooling | BCL, Roslyn | Runtime libraries (the parity tool is standalone) |

The table lists library-to-library edges; approved BCL and `Microsoft.Extensions.*` packages are
additional edges documented by each project.

The **exactly-one provider** rule is a runtime rule, not a package-reference rule. A host may
reference several provider packages, but it must register exactly one. The selector resolves the
instances and throws when more than one is present.

```csharp
services.AddLocalization();
services.AddJsonLocalization(options => options.FilePath = "Localization/en.json");
// Do not add a second provider registration.
```

Integrations that need ASP.NET Core types declare a `FrameworkReference` on
`Microsoft.AspNetCore.App`. The Diagnostics integration depends only on
`Microsoft.Extensions.Diagnostics.HealthChecks` and Abstractions, so it works in any
`Microsoft.Extensions.Hosting` host.

There is no OpenTelemetry package dependency. Observability uses BCL `ActivitySource` and `Meter`
names; consumers wire the SDK side themselves.

## Compatibility checklist for a change

- Does the change alter a public signature? If yes, update the baseline deliberately.
- Does it change a provider's options, defaults, or validation? Update the provider reference and
  tests; options are construction snapshots.
- Does it change a diagnostic ID, activity name, metric name, or tag? Treat it as breaking for
  dashboards and suppressions.
- Does it add a project reference that violates the table above? The architecture tests will fail.
- Does it add a package that is not trim-safe? Annotate it honestly or exclude the project from the
  AOT contract.
- Does it change the catalog format or interchange shape? Bump the report schema or record the
  migration; byte-identical round trips are a test invariant.
- Does it add a new provider package? Add it to the package inventory, the API baselines, and the
  provider reference.

## Worked example: a deliberate API change

Renaming a public method is a breaking change. Update the call sites and tests, run the solution tests
to see the baseline diff, regenerate the affected `.Api.txt` files with `JENINNET_API_REGEN=1`, and
review the diff as part of the change. Add a **Breaking:** changelog entry with a migration note, then
run package validation and the consumer solution. The baseline test is the gate that keeps the rename
from being accidental.

## Pitfalls

**Pitfall:** adding a per-project `<Version>`. The solution-wide version is centralized; the
architecture test rejects the override.

**Pitfall:** adding a new dependency edge without updating the architecture model. The graph test
fails, and the package inventory becomes misleading.

**Pitfall:** assuming the EF store is AOT-safe. It is explicitly excluded; use a non-EF provider for
trimmed or Native AOT deployments.

## Where to go next

- [Release notes](09-release-notes.md) for the current publication status and planned 1.0 scope.
- [Known limitations](10-known-limitations.md) for the support boundaries behind these promises.
- [AOT and trimming](../aot-and-trimming.md) for the enforced contract and smoke consumer.
- [Package map](../appendix/02-package-map.md) for the complete package inventory.
- [Tooling reference](05-tooling-reference.md) for the build-time package behavior.
- [Migration from Microsoft.Extensions.Localization](08-migration-from-ms-extensions.md) for a
  compatibility-driven migration.
