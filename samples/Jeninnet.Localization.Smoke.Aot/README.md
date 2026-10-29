# Jeninnet.Localization.Smoke.Aot

A native-AOT and trimming smoke consumer for the canonical localization pipeline. It publishes as a self-contained native executable and runs a linear set of checks that would fail at publish time or at runtime if the exercised surface leaked reflection or trim-incompatible dependencies.

The project is a sample, not a package: `IsPackable=false`. It is validated in CI on `ubuntu-latest`, where the native toolchain is provisioned.

## What it exercises

| Check | What it proves |
|----|----|
| **Localize and format** | The Core pipeline over the in-memory Provider resolves a key and formats named template arguments |
| **ICU (International Components for Unicode) formatting** | The shipped opt-in `IcuMessageFormatter` selects plurals correctly |
| **Store caching** | `CachedLocalizationStoreDecorator` collapses repeated lookups into one backend call |
| **Translation with fakes** | The Azure translation Provider serializes requests and parses responses through the source-generated JSON contract, without network access |

The program prints `SMOKE OK: localize/format/cache/translate under AOT/trimming` and exits `0` when every check passes. A failed check prints `SMOKE FAILED: ...` and exits `1`.

## Prerequisites

- .NET 10 SDK.
- Native AOT (Ahead-of-Time) platform toolchain:
  - Windows: MSVC, installed through the Visual Studio C++ workload;
  - Linux: `clang` and `zlib1g-dev`.
- The relevant runtime identifier (`win-x64`, `linux-x64`, and so on).

Without the toolchain the publish fails loudly at the native link stage. The smoke is not a managed-only test; it must be published with `PublishAot=true`.

## Publish and run

From the repository root:

```powershell
# Windows
dotnet publish samples/Jeninnet.Localization.Smoke.Aot -c Release -r win-x64
./samples/Jeninnet.Localization.Smoke.Aot/bin/Release/win-x64/publish/Jeninnet.Localization.Smoke.Aot
```

```bash
# Linux (the CI path)
sudo apt-get update
sudo apt-get install -y clang zlib1g-dev
dotnet publish samples/Jeninnet.Localization.Smoke.Aot -c Release -r linux-x64
./samples/Jeninnet.Localization.Smoke.Aot/bin/Release/linux-x64/publish/Jeninnet.Localization.Smoke.Aot
```

The project pins `PublishAot=true` and sets `SelfContained=true` whenever a runtime identifier is supplied, so the publish command above is the complete surface. No extra properties are required.

## Project settings

| Setting | Purpose |
|----|----|
| `PublishAot=true` | Produces a native executable and enables the AOT/trim analyzers |
| `IsAotCompatible=true` | Declares AOT compatibility for the project |
| `SelfContained=true` (per RID) | Native AOT requires a self-contained publish on .NET 10 |
| `InvariantGlobalization=false` | Keeps culture-aware formatting and ICU behavior available |
| `GenerateDocumentationFile=false` | The RID-specific publish layout does not carry the XML doc file |

The repository treats warnings as errors, so the compile itself enforces the zero trim/AOT analyzer warning bar for the exercised surface.

## CI

The `build-and-test` job runs the smoke on `ubuntu-latest`:

```text
sudo apt-get install -y clang zlib1g-dev
dotnet publish samples/Jeninnet.Localization.Smoke.Aot/... -c Release -r linux-x64
./.../publish/Jeninnet.Localization.Smoke.Aot
```

The Windows and macOS matrix legs build and test the repository but do not run the native AOT smoke. The Linux leg is the authoritative gate.

## Troubleshooting

| Symptom | Cause | Remedy |
|----|----|----|
| Publish fails at the native link stage | Missing MSVC, `clang`, or `zlib1g-dev` | Install the platform toolchain listed above |
| Publish fails with a trim/AOT analyzer warning | A newly used API is not trim-compatible | Fix the API usage or update the smoke surface; warnings are errors |
| `SMOKE FAILED` with a lookup mismatch | Pipeline, Provider, cache, or formatter behavior changed | Inspect the printed failure and run the corresponding unit suite |
| The binary cannot find culture data | `InvariantGlobalization` was changed to `true` | Restore `InvariantGlobalization=false` |
| Publish emits no XML documentation file | Expected; `GenerateDocumentationFile=false` for the RID layout | No action required |

## Related documentation

- [Samples overview](../README.md)
- [AOT and trimming notes](../../docs/en/aot-and-trimming.md)
- [Core package](../../src/Jeninnet.Localization.Core/README.md)
- [Memory Provider](../../src/providers/Jeninnet.Localization.Providers.Memory/README.md)
- [Azure translation Provider](../../src/translation/Jeninnet.Localization.Translation.Azure/README.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
