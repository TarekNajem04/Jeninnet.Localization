# Jeninnet.Localization.Docs

Documentation snippet verification. This non-packable console project compiles the C# examples that are most likely to be copied from the book, and it executes the quick-start path against the real packages so the examples cannot silently drift.

It is not a reference application and it is not a package. It has no external service, credential, or database dependency; the compile-only registration methods are never invoked.

## What is verified

| Example | Source | Verification |
|----|----|----|
| Quick start: per-culture JSON, exact lookup, fallback, missing key, plural | `docs/en/01-getting-started/03-quick-start.md` | Executed on every run |
| Memory provider registration | `docs/en/01-getting-started/02-installation.md` | Compile-only |
| JSON, XML, ResX, PO (Portable Object) provider registration | `docs/en/02-guides/01-choosing-a-provider.md` | Compile-only |
| SQLite provider registration | `docs/en/02-guides/01-choosing-a-provider.md` | Compile-only |
| EF Core store registration and `IStringLocalizer` bridge | `docs/en/02-guides/06-resource-sets-and-store.md`, `docs/en/03-reference/01-core-api.md` | Compile-only |
| Localized DataAnnotations attribute | `integrations/Jeninnet.Localization.Integrations.DataAnnotations/README.md` | Compile-only |

The quick-start data lives in `Localization/en.json` and `Localization/ar.json` and is copied to the output directory, matching the path used by `AddJsonLocalizationProviders(AppContext.BaseDirectory, ...)`.

## Run

From the repository root:

```bash
dotnet run --project samples/Jeninnet.Localization.Docs -c Release
```

Expected output:

```text
DOCS SNIPPETS OK
```

A failed assertion prints `DOCS SNIPPETS FAILED: ...` and exits with a non-zero code. The project is part of the main solution, so the normal Release build compiles it. CI runs the executable after the solution build.

## Add a snippet

1.  Add the example to the relevant Markdown chapter.
2.  Add a compile-only method to `CompileOnlySnippets.cs`, or extend the executable quick-start path in `Program.cs` if the example is safe to run.
3.  Name the documentation file in the method comment so the source pair is discoverable.
4.  Run the project and the documentation checks.

Do not add snippets that require network access, credentials, a GUI, a real database, or a specific working directory. Registration-only examples belong in `CompileOnlySnippets`; runnable examples must be hermetic and deterministic.

## Related documentation

- [Repository README](../../README.md)
- [Reference book](../../docs/en/README.md)
- [Quick start](../../docs/en/01-getting-started/03-quick-start.md)
- [Installation](../../docs/en/01-getting-started/02-installation.md)
- [Choosing a provider](../../docs/en/02-guides/01-choosing-a-provider.md)
- [Documentation contribution guide](../../docs/CONTRIBUTING.md)

## License

MIT — see [`LICENSE`](../../LICENSE).
