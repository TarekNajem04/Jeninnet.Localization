# Jeninnet.Localization.Tests

The main verification suite for the Jeninnet.Localization repository. It is the largest project in the solution and the gate for architecture rules, the public API surface, the localization pipeline, every Provider, translations, integrations, interchange formats, tooling, and analyzer behavior.

The project is non-packable (`IsPackable=false`, `IsTestProject=true`) and contains 90 source files, 85 test classes, and roughly 1,228 test methods organized by concern. It references every production project directly so the tests exercise the real implementations rather than mocks.

## What the suite covers

| Area | Representative files | What is pinned |
|----|----|----|
| Architecture | `ArchitectureTests.cs` | Assembly identity, dependency direction, forbidden references, namespace and package invariants |
| Public API | `PublicApiSurfaceTests.cs` | Every packable assembly matches its committed baseline under `ApiBaselines` |
| Pipeline and contracts | `DefaultMessageLocalizerContractTests.cs`, `FormatterAndLocalizerTests.cs`, `IcuMessageFormatTests.cs`, `PluralRulesTests.cs`, `CancellationBehaviorTests.cs` | Lookup, fallback, formatting, ICU (International Components for Unicode), plurals, cancellation, and misses versus failures |
| Providers | `MemoryMessageProviderContractTests.cs`, `JsonMessageProviderContractTests.cs`, `XmlMessageProviderContractTests.cs`, `PoMessageProviderTests.cs`, `ResXMessageProviderContractTests.cs`, `SatelliteMessageProviderTests.cs`, `Providers.Database.*` tests | Each Provider's exact-match semantics, configuration, file behavior, and contract |
| Database runtime | `DatabaseRuntimeTests.cs`, `EntityFrameworkCoreRuntimeTests.cs` | Real SQL Server, PostgreSQL, and MySQL behavior when connections are supplied, plus EF Core store behavior on SQLite |
| Translation | `AzureTranslationProviderTests.cs`, `AzureTranslationResilienceTests.cs`, `AzureTranslationBatchTests.cs`, `GoogleTranslationProviderTests.cs`, `TranslationMemoryTests.cs` | Request shapes, source-generated JSON, batching, glossary/memory, resilience, observability |
| Integrations | `BlazorLocalizationTests.cs`, `LocalizedDataAnnotationsIntegrationTests.cs`, `MvcValidationMetadataTests.cs`, `HtmlLocalizerFactoryTests.cs`, `JavaScriptLocalizationTests.cs`, `DiagnosticsObservabilityTests.cs` | Framework-specific behavior of every integration package |
| Interchange | `CsvInterchangeTests.cs`, `PoInterchangeTests.cs`, `ResXInterchangeTests.cs`, `XliffInterchangeTests.cs` | Round trips, schema fidelity, translator notes, and culture handling |
| Tooling | `LocalizationKeysGeneratorTests.cs`, `LocalizabilityAnalyzerTests.cs`, `LocalizabilityCodeFixTests.cs`, `ParityToolTests.cs` | Source generator output, analyzer diagnostics, code-fix application, and parity CLI behavior |
| Store and admin | `ResourceAdminApiTests.cs`, `ResourceHistoryTests.cs`, `ResourceSynchronizerTests.cs`, `EmptyLocalizationResourceSetTests.cs` | Store contracts, audit/history, synchronization, and read-only surfaces |
| Performance evidence | `Performance/PerformanceEvidenceTests.cs` | Reproducible throughput and allocation measurements; no timing assertions |

## Public API baselines

`ApiBaselines/` contains 35 committed `.Api.txt` files, one per packable assembly. The public-surface test dumps the built assembly and compares it with its baseline. Any accidental public addition, removal, or signature change fails with a diff. The Windows host test project validates the MAUI and WinUI baselines separately.

If a public API change is intentional:

1.  update the production code;
2.  update the corresponding `ApiBaselines/<Assembly>.Api.txt`;
3.  run the test again and review the diff in the pull request.

The same inventory drives the consumer package-validation tests, so a baseline change also changes the expected package set.

## Prerequisites

- .NET 10 SDK.
- No external service for the default run. Translation tests use fakes and source-generated contracts.
- Real database engines only if you want the gated runtime tests to execute.

## Run

Run the whole suite from the repository root:

```bash
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj \
    --configuration Release
```

Run a focused class or namespace:

```bash
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj \
    --configuration Release \
    --filter "FullyQualifiedName~ArchitectureTests"
```

Skip the non-gating performance measurements for a faster contract-only run:

```bash
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj \
    --configuration Release \
    --filter "TestCategory!=Performance"
```

Collect coverage with the repository settings:

```bash
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj \
    --configuration Release \
    --settings tests/Jeninnet.Localization.Tests/.runsettings \
    --collect:"XPlat Code Coverage"
```

The repository's [`scripts/coverage`](../../scripts/coverage/README.md) script wraps the same flow and generates an HTML report.

## Gated database tests

The real-engine database tests read connection strings from the environment and report `Inconclusive` when a variable is absent, so the default build stays green:

| Provider   | Environment variable                  |
|------------|---------------------------------------|
| SQL Server | `JENINNET_TEST_SQLSERVER_CONNECTION`  |
| PostgreSQL | `JENINNET_TEST_POSTGRESQL_CONNECTION` |
| MySQL      | `JENINNET_TEST_MYSQL_CONNECTION`      |

```powershell
$env:JENINNET_TEST_SQLSERVER_CONNECTION = "Server=localhost;Integrated Security=True;TrustServerCertificate=True;"
dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj --configuration Release
```

These tests create isolated schemas or databases and seed rows directly as test setup; the production Providers remain read-only during lookups.

## Test infrastructure and conventions

- MSTest with method-level parallelization (`[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`); suites that mutate process-wide state opt out with `[DoNotParallelize]`.
- `MSTestSettings.cs` holds the assembly-level parallelization setting.
- `.runsettings` configures XPlat Code Coverage in OpenCover format.
- Test-only packages include Roslyn workspaces (for analyzer/code-fix tests), EF Core SQLite (for store tests), `Jeninnet.Assertions` (fluent assertions), and `coverlet.collector`.
- No timing assertions: performance tests record measurements and environment details only.
- No outbound network calls in the default run; translation tests use fake HTTP transports.

## Related documentation

- [Repository README](../../README.md)
- [Core API reference](../../docs/en/03-reference/01-core-api.md)
- [Storage API reference](../../docs/en/03-reference/02-storage-api.md)
- [Provider reference](../../docs/en/03-reference/03-provider-reference.md)
- [Interchange formats](../../docs/en/03-reference/04-interchange-formats.md)
- [Tooling reference](../../docs/en/03-reference/05-tooling-reference.md)
- [Diagnostics reference](../../docs/en/03-reference/06-diagnostics-reference.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
