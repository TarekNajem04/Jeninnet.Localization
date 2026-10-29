# Jeninnet.Localization.Reference.Hosts.Tests

Composition tests for the three web reference hosts: the ASP.NET Core API, ASP.NET Core MVC, and Blazor Server applications. The suite verifies that each host's single composition root produces the same canonical stack, that Provider selection changes the implementation without changing the application, and that invalid or duplicated configurations fail eagerly.

The project is a non-packable MSTest project (`IsPackable=false`, `IsTestProject=true`) with 13 test methods. It references the three host projects directly and runs on every CI operating system without external services.

## What the suite covers

| Test file | Scope |
|----|----|
| `HostCompositionTests.cs` | The full web-host composition matrix and culture behavior |
| `HostTestPaths.cs` | Temporary content staging and repository-root discovery |

The test matrix:

- **Exactly one inner Provider** for every build-safe Provider (`Memory`, `Json`, `Xml`, `ResX`, `SQLite`) across the `Api`, `Mvc`, and `Blazor` compositions.
- **Provider selection changes the implementation**: `CultureAwareMessageProvider` for JSON/XML, `MemoryMessageProvider` for Memory, `ResXMessageProvider` for RESX, and `SqliteMessageProvider` for SQLite.
- **Canonical services resolve** from every host: `IMessageLocalizer`, `IMessageFormatter`, `ILocalizationStore`, and `IStringLocalizer<CreateOrderRequest>`.
- **MVC view localization** registers the Jeninnet `ViewMessageLocalizer` as `IViewLocalizer`.
- **Blazor Server culture composition** registers a scoped `BlazorCultureService`, the `BlazorCircuitCultureHandler` in the `CircuitHandler` set, and the scoped `BlazorCookieCultureStore`.
- **Bridge culture awareness**: `IStringLocalizer<T>` resolves the current UI culture through every host composition.
- **Memory stays culture-agnostic** in every host, as designed for the flat in-memory table.
- **Gating and failure modes**: unknown Providers are rejected, translation Providers require an API key, server Providers require a connection string, and a second composition or provider is rejected.
- **SQLite selection** registers only the SQLite inner Provider across all web hosts.

## Prerequisites

- .NET 10 SDK.
- No external service, credential, or database. The tests use the build-safe Providers and temporary content directories.

## Run

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Tests \
    --configuration Release
```

Run a focused test:

```bash
dotnet test samples/Jeninnet.Localization.Reference.Hosts.Tests \
    --configuration Release \
    --filter "FullyQualifiedName~BlazorServerComposition"
```

The project is part of the main solution, so a normal solution-wide `dotnet test` also runs it.

## Test infrastructure

- `HostTestPaths.CreateLocalizationDirectory` copies the committed JSON/XML content into a temporary `Localization/` directory so file-based Providers can be composed without touching the working tree.
- The repository root is discovered by walking upward to `Directory.Packages.props`.
- Tests compose services explicitly rather than starting Kestrel, so they are fast, deterministic, and platform-independent.

## Related documentation

- [Reference application overview](../README.md)
- [API host](../Jeninnet.Localization.Reference.Api/README.md)
- [MVC host](../Jeninnet.Localization.Reference.Mvc/README.md)
- [Blazor Server host](../Jeninnet.Localization.Reference.BlazorServer/README.md)
- [Shared reference project](../Jeninnet.Localization.Reference.Shared/README.md)
- [Testing your localization](../../docs/en/02-guides/15-testing-your-localization.md)
- [Provider reference](../../docs/en/03-reference/03-provider-reference.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
