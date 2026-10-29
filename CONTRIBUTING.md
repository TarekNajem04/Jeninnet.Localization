# Contributing to Jeninnet.Localization

Thank you for your interest in Jeninnet.Localization.

The project aims to provide a modular localization framework for .NET with a strong focus on
correctness, deterministic behavior, explicit contracts, trimming/Native AOT compatibility, and
long-term API stability.

## Current Project Status

Jeninnet.Localization is currently in an API stabilization phase. No NuGet packages have been
published yet.

During this stage, the project's internal architecture, CI/CD pipelines, quality gates, release
process, and public APIs are still being refined and validated.

To maintain consistency and avoid unnecessary breaking changes, source-code contributions are
temporarily limited to project maintainers.

## How You Can Contribute Today

Community participation is highly appreciated.

The most valuable contributions at this stage are:

- Bug reports
- Feature requests
- Design discussions
- API design feedback
- Documentation improvements
- Localization/i18n (internationalization) behavior observations
- Security observations
- Real-world usage feedback

Please use GitHub Issues or Discussions to share ideas and suggestions.

## Pull Requests

At the current stage of the project:

- Pull requests containing source-code changes may not be accepted.
- Pull requests that improve documentation may be considered on a case-by-case basis.
- Large design proposals should begin as an Issue or Discussion before implementation.

Once the project reaches a stable baseline, external code contributions will be opened publicly.

## Documentation Contributions

Documentation changes are welcome within the current contribution policy. Read
[Contributing to the Documentation](docs/CONTRIBUTING.md) before opening a documentation pull
request. It covers where a change belongs, chapter structure, code-example verification, package
README rules, the required checks, and the review checklist.

## Project Structure

```text
src/
    Jeninnet.Localization.Abstractions
    Jeninnet.Localization.Core
    Jeninnet.Localization.Storage
    providers/          (Memory, Json, Xml, ResX, PO, Satellite, Pseudo, Database.*, EntityFrameworkCore)
    translation/        (Azure, Google)
    interchange/        (Abstractions, Csv, Po, ResX, Xliff)
    generators/         (Keys source generator)
    tooling/            (Keys extraction/parity CLI)
    analyzers/          (Localizability analyzer + code fix)

integrations/
    DataAnnotations, Mvc.Validation, Blazor, JavaScript, Diagnostics, RequestLocalization

tests/
    Jeninnet.Localization.Tests

samples/
    Jeninnet.Localization.Reference.*

consumers/
    Jeninnet.Localization.Consumer
    Jeninnet.Localization.Consumer.Tests

docs/
scripts/
```

## Design Philosophy

The architecture emphasizes:

- A canonical provider contract with exactly one provider per pipeline (no chaining, no merging)
- A single owner for culture fallback; providers are exact-match only
- Explicit contracts: cross-assembly contracts live in `Abstractions`
- Deterministic behavior: message key plus culture exact-match lookups
- Separation of concerns: behavior, data contracts, source adapters, and optional translation
- Minimal dependencies: BCL and approved `Microsoft.Extensions.*` packages first
- Trimming and Native AOT compatibility for every runtime library
- Long-term API stability enforced by committed public-API baselines

When evaluating proposals, maintainability, determinism, and long-term API stability are generally
preferred over short-term convenience.

## Development Standards

The project follows modern .NET engineering practices including:

- Automated CI validation
- Static analysis and quality gates
- Package validation
- Public API surface baselines
- Cross-platform testing
- Native AOT smoke verification
- Release verification procedures

Any future code contributions will be expected to satisfy these requirements.

## Future Contributor Program

External source-code contributions are planned for a future phase of the project.

Once the architecture, release workflow, and public APIs are considered stable, contributor
guidelines will be expanded to include:

- Coding standards
- Pull request requirements
- Review process
- Testing requirements
- Release compatibility expectations

Thank you for helping improve Jeninnet.Localization.
