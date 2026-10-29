# Jeninnet.Localization.Reference.Shared.Tests

Behavior and content-parity tests for the shared reference application. The suite proves that the domain, the canonical content, the Provider selections, and the sample-only store all agree before any host renders a page, and it pins the culture and fallback behavior the reference application teaches.

The project is a non-packable MSTest project (`IsPackable=false`, `IsTestProject=true`) with 66 test methods. The whole assembly is marked `[assembly: DoNotParallelize]` because several tests mutate process-wide culture and the sample database.

## What the suite covers

| Area | Test file | What is pinned |
|----|----|----|
| Domain | `CatalogTests.cs` | Four deterministic books, stable order and prices, localized key references, unknown ids |
| Domain | `OrderTests.cs` | Deterministic order shape, monotonic ids, business error keys, boundary quantities, localized validation attributes |
| Canonical content | `LocalizationContentTests.cs` | Supported cultures, required-key coverage, intentional partial-culture semantics, unknown culture rejection |
| Content parity | `ContentFileConsistencyTests.cs` | Committed JSON and XML files match the embedded canonical content exactly |
| RESX parity | `ResXContentParityTests.cs` | Committed neutral and satellite resources match the canonical content; the shipping RESX Provider resolves every key exactly per culture |
| SQLite parity | `SqliteContentParityTests.cs` | The sample database mirrors canonical content; exact-culture reads; deterministic, idempotent, resettable initialization; SQL-significant values are preserved |
| Server parity | `ServerDatabaseParityTests.cs` | SQL Server, PostgreSQL, and MySQL match canonical content when a real connection is supplied; SQL-significant keys stay parameterized; reset/reinitialize is equivalent |
| Composition | `CompositionTests.cs` | JSON, XML, RESX, SQLite, and Memory behavior; full fallback chain; bridge; validation formatting; store opt-out; Provider partition and rejection rules; translation and server gating |
| Sample store | `ReferenceResourceSetStoreTests.cs` | Supported cultures, deterministic entry ids, missing-key behavior, enumeration, read-only mutation rules, reload semantics |

## Prerequisites

- .NET 10 SDK.
- No external service for the default run. SQLite parity uses a local file created by the sample.
- A reachable database only for the gated server parity tests.

## Run

```bash
dotnet test samples/Jeninnet.Localization.Reference.Shared.Tests \
    --configuration Release
```

Run a focused class or test:

```bash
dotnet test samples/Jeninnet.Localization.Reference.Shared.Tests \
    --configuration Release \
    --filter "FullyQualifiedName~CompositionTests"
```

The tests live under the repository solution, so a normal `dotnet test Jeninnet.Localization.slnx` also runs them.

## Gated server parity tests

The three server tests are real-engine checks. They read a connection string from the environment and report `Inconclusive` when the variable is absent, so ordinary CI stays green without infrastructure:

| Provider   | Environment variable                  |
|------------|---------------------------------------|
| SQL Server | `JENINNET_TEST_SQLSERVER_CONNECTION`  |
| PostgreSQL | `JENINNET_TEST_POSTGRESQL_CONNECTION` |
| MySQL      | `JENINNET_TEST_MYSQL_CONNECTION`      |

```powershell
$env:JENINNET_TEST_SQLSERVER_CONNECTION = "Server=localhost;Integrated Security=True;TrustServerCertificate=True;"
dotnet test samples/Jeninnet.Localization.Reference.Shared.Tests --configuration Release
```

The tests create and seed the sample database `JeninnetLocalizationReference` (or the equivalent vendor-specific database) and reset it on the success path. Do not point these variables at a database you care about: the initializer only accepts the sample database name, but it does write and drop the sample table.

## Test infrastructure

- `TestPaths.cs` locates the repository root by walking upward to `Directory.Packages.props`, then copies `samples/Content/Localization/*.json` and `*.xml` into a temporary directory so file Providers can be composed without touching the working tree.
- `MSTestSettings.cs` disables parallelization for the assembly.
- All tests use the canonical `LocalizationContent` as the expected source of truth, so the suite fails when content, resources, or Provider behavior drift apart.

## Conventions

- MSTest with `[TestClass]` and `[TestMethod]`.
- Resource files are embedded by the shared project; the tests inspect the compiled identity as well as the committed files.
- No network calls in the default run; translation tests exercise configuration and deterministic behavior without contacting a service.
- No timing thresholds; the suite verifies behavior, not performance.

## Related documentation

- [Reference application overview](../README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Provider reference](../../docs/en/03-reference/03-provider-reference.md)
- [Troubleshooting](../../docs/en/04-operations/04-troubleshooting.md)
- [Package map](../../docs/en/appendix/02-package-map.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
