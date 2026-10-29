# Roadmap

This is the public delivery plan for Jeninnet.Localization. It is organized by release, not by
internal planning structures. The rule is simple:

- the highest-impact, adoption-critical work ships first;
- no release ships with an open release blocker;
- a feature moves to a later release only when the release scope is explicitly re-cut here.

Legend: **MUST** ships with the release; **SHOULD** is planned but can move at the scope gate;
**LATER** is intentionally deferred.

## Release principles

- **SemVer.** Previews use `1.0.0-preview.N`; stable follows `1.0.0`; additive features use minor
  versions; breaking changes require a major version.
- **One public API baseline per TFM and package.** A surface change is never silent.
- **Packages, consumers, docs, AOT, and security are release gates**, not afterthoughts.
- **No dependency without a written reason.** BCL and approved `Microsoft.Extensions.*` packages
  come first.
- **No public artifact links to an internal, unpublished document.**

---

# 1.0.0-preview.1 — Adoption and release readiness

**Goal:** make the library installable, understandable, and trustworthy for a new consumer on the
platforms they actually use, while shipping the highest-value differentiators that do not require a
second ecosystem cycle.

## Adoption and platform support

- [x] **MUST — Multi-target runtime libraries.** Ship `net8.0`, `net9.0`, and `net10.0` assets for
  the runtime libraries; keep Roslyn tooling on `netstandard2.0`; multi-target the CLI tool where it
  adds value. Use conditional package versions per target.
  - Acceptance: every runtime package contains all three `lib/<tfm>` folders; build, test, pack,
    package validation, and consumer validation pass on each target.
  - Note: document the real support windows. .NET 9 is already out of support and .NET 8 reaches end
    of support in November 2026; the compatibility page must say so.
- [x] **MUST — Multi-target package and validation matrix.** Conditional dependency versions,
  TFM-specific package assets, package validation, consumer validation, and TFM-independent API
  baseline checks are in place for `net8.0`, `net9.0`, and `net10.0`.
- [x] **MUST — Multi-target CI, tests, and documentation.** The library test suite runs on all three
  runtime targets; the CLI tool ships `net8.0` and `net10.0`; README, compatibility, installation,
  release notes, AOT guidance, package READMEs, and badges state the supported matrix.
- [x] **MUST — .NET MAUI integration.** Add a MAUI package with DI registration, culture persistence,
  current-culture application, RTL guidance, a sample, and composition tests.
- [x] **MUST — Avalonia integration.** Add an Avalonia package with the same culture contract, a
  sample, and Windows/Linux build and culture-switch tests.
  - [x] Shared host-culture contract, file-backed persistence, culture-switch tests, RTL direction
    helper, DI registration, and public-API baseline.
  - [x] Reference sample and cross-platform culture-switch tests.
- [x] **MUST — WinUI 3 integration.** Add a Windows-only WinUI 3 package with DI, persistence,
  XAML usage, a sample, and a smoke test.
- [x] **MUST — Cross-platform CI for the new hosts.** Isolate MAUI, Avalonia, and WinUI workload
  jobs so they do not slow the core build unnecessarily; a failing host blocks the release.
- [x] **MUST — Package map and baselines.** Add the new integration packages to the package map,
  public-API baselines, package validation, and the consumer matrix.
  - [x] Avalonia package is in the package map, public-API baseline, package-validation inventory,
    and local consumer feed.
  - [x] MAUI and WinUI package entries, baselines, and local consumer feed.

## Release blockers

- [x] **MUST — Remove the dead ASP.NET Core API and legacy dependency.** Delete the public
  application-builder extension and the static service-provider accessor that has no consumer;
  remove the old ASP.NET Core HTTP-abstractions dependency from Core; update the public-API
  baseline, architecture tests, README, and package documentation.
  - Acceptance: Core has no ASP.NET Core dependency; no public API exists without a real behavior;
    the store-backed localizer bridge still works.
  - **Sequence:** do this before the MAUI/Avalonia/WinUI work so the dead API is not ported into the
    new packages.
- [x] **MUST — Third-party notices and license review.** Add a public third-party notice covering
  Unicode/CLDR data, ICU attribution where applicable, and every redistributed dependency
  (including the MySQL connector); include the notice in packages and the documentation site.
- [x] **MUST — Public architecture documentation.** Publish the authoritative architecture overview
  and remove every public reference to internal, unpublished architecture files.
  - Acceptance: no public document links to or names an internal workspace file.
- [ ] **MUST — Repository and security posture.** Confirm the repository is public, document the
  canonical URL, and provide reachable security and issue-reporting paths.
- [x] **MUST — Dependency and vulnerability gate.** Add `NuGetAudit`/vulnerability scanning for
  direct and transitive packages to CI and document the response process.
- [ ] **MUST — Code of conduct and support policy.** Publish a code of conduct and a support policy
  that matches the actual release status.

## NuGet and release engineering

- [ ] **MUST — Visual and package identity.** Add a package icon, project URL, repository URL, and
  a consistent visual identity for all packages.
- [ ] **MUST — Per-package descriptions and tags.** Replace the generic package description with a
  meaningful description and targeted search tags for every package (providers, gettext, ICU,
  Blazor, EF Core, analyzer, tool, and so on).
- [ ] **MUST — Package READMEs.** Convert repository-relative links in package READMEs to absolute
  links; verify rendering on nuget.org for representative packages.
- [ ] **MUST — SourceLink and deterministic builds.** Enable source linking and CI deterministic
  builds; verify the source link inside symbol packages.
- [ ] **MUST — Metadata package.** Add a `Jeninnet.Localization` discoverability package for the
  core trio without changing the one-provider rule.
- [ ] **MUST — NuGet prefix and ownership.** Reserve the package prefix, verify owners, and document
  the publishing account and approval environment.
- [ ] **MUST — Preview versioning.** Support `1.0.0-preview.N` in the central version, tag
  verification, package version, and publication workflow; reject mismatched tags.
- [ ] **MUST — Preview publication workflow.** Add a dry-run publish job, a GitHub prerelease,
  artifact retention, and an explicit production environment approval.
- [ ] **MUST — Package count and validation.** Replace the fixed package-count invariant with the
  actual inventory and validate every package against the consumer feed.
- [ ] **MUST — Release checklist.** Add an automated or documented checklist covering build/test
  matrices, docs, AOT, package validation, consumer validation, vulnerability scan, notices,
  metadata, and installation smoke tests.
- [ ] **SHOULD — SBOM and checksums.** Publish an SBOM and checksums with each release.
- [ ] **SHOULD — Package signing decision.** Decide whether preview packages are signed and record
  the rationale.

## Core differentiators

- [ ] **MUST — Composite provider.** Add an opt-in ordered provider composition: exact match wins,
  no implicit translation, no hidden fallback, capability aggregation, and clear validation errors.
  It remains one registered provider at the selector boundary.
- [ ] **MUST — Source-aware translation.** Let translation providers translate a source template
  instead of an opaque key, with key-as-source kept for gettext-style catalogs. Include the source
  template in cache identity and validate placeholder parity.
- [ ] **MUST — Provider cache decorators.** Add memory and distributed cache decorators for message
  providers with TTL, invalidation, capability preservation, and AOT-safe behavior.
- [ ] **MUST — Missing and unused key analyzer.** Add compile-time diagnostics that compare generated
  key constants and source references with catalog inputs, with a safe code fix where possible.
- [ ] **MUST — Documentation and samples.** Update the book, package READMEs, sample host matrix,
  performance notes, and release notes for every preview.1 feature.

## Quality and release evidence

- [ ] **MUST — Coverage gate.** Enforce a meaningful coverage threshold in CI, excluding generated
  code, samples, and tooling where appropriate.
- [ ] **MUST — AOT and trimming matrix.** Run the smoke consumer and trim analysis for every
  supported runtime target and for the new platform integrations where supported.
- [ ] **MUST — Threat model.** Review file ingestion, translation endpoints, database access, audit
  redaction, secret handling, and untrusted catalog input; record threats and mitigations.
- [ ] **MUST — Performance baseline.** Add a reproducible benchmark baseline for lookup, fallback,
  formatting, caching, and translation batching; publish the measured conditions.
- [ ] **MUST — Fuzz smoke tests.** Add property/fuzz coverage for the catalog parsers and message
  formatter before the preview.
- [x] **MUST — Documentation gates.** DocFX, repository links, README claims, Arabic text, and API
  coverage stay green.

## Identity consistency

- [ ] **MUST — Source headers.** Replace legacy repository headers with the canonical project URL in
  every source file.
- [ ] **MUST — Public references.** Align README, package READMEs, package metadata, docs, badges,
  issue templates, and security policy with the canonical repository and NuGet identity.
- [ ] **MUST — Final link audit.** Confirm no public link points to an internal workspace, a stale
  path, or an unpublished document.
- [ ] **SHOULD — Branding polish.** Finalize the icon, README hero, package-page examples, and
  GitHub description/topics.

## Exit criteria

All MUST items are green; the full build/test matrix, pack, package validation, consumer validation,
docs build, AOT matrix, vulnerability scan, and clean-project install from the preview feed pass.

---

# 1.0.0-preview.2 — Ecosystem depth

**Goal:** close the largest capability gaps against mature localization stacks without destabilizing
the preview.1 surface.

## Translation and translation management

- [ ] **MUST — First TMS connector.** Add a connector for one translation management system
  (Crowdin or Weblate) with pull, push, dry-run, conflict reporting, and translator-note support.
- [ ] **MUST — Connector contract.** Define a reusable connector contract so additional systems do
  not fork the workflow.
- [ ] **SHOULD — Additional TMS connectors.** Add Lokalise and Phrase if the first connector proves
  the model.
- [ ] **MUST — DeepL provider.** Add a DeepL translation provider behind the canonical contract.
- [ ] **SHOULD — AWS Translate provider.** Add an AWS translation provider with the same contract and
  resilience policy.
- [ ] **SHOULD — Glossary and translation-memory exchange.** Import/export glossary and memory data
  through the existing interchange packages.
- [ ] **SHOULD — Batch job persistence.** Allow long-running translation jobs to resume from a
  ledger instead of restarting from scratch.

## Providers and storage

- [ ] **MUST — Additional catalog formats.** Add at least two of INI, YAML, or TOML as first-class
  providers, or document why they remain out of scope.
- [ ] **MUST — Source generation from more formats.** Generate typed keys from ResX, PO, and XLIFF,
  not only the project catalog format.
- [ ] **SHOULD — Multi-vendor EF Core store.** Extend the resource-set store beyond SQL Server to
  PostgreSQL, MySQL, or SQLite with isolated provider packages.
- [ ] **SHOULD — MAUI resource provider.** Serve MAUI resource files through the canonical provider
  contract where the platform exposes them.
- [ ] **SHOULD — XAML resource provider.** Serve XAML resource dictionaries for desktop hosts.
- [ ] **SHOULD — Remote catalog provider.** Add an HTTP provider with ETag, conditional requests,
  retry, and cache semantics.
- [ ] **SHOULD — Provider hot-reload improvements.** Per-culture reload and a public reload API for
  file-backed providers.

## Core and formatting

- [ ] **MUST — Full CLDR plural coverage.** Expand plural rules to all supported CLDR languages,
  ordinals beyond English, and the documented compact-notation cases.
- [ ] **MUST — ICU conformance expansion.** Cover the remaining documented date/time skeletons and
  conformance vectors; keep the subset explicit.
- [ ] **SHOULD — Root `IStringLocalizer` support.** Provide a root localizer registration and a
  documented culture-capture model for framework integrations.
- [ ] **SHOULD — Provider diagnostics.** Add per-provider cache, reload, and failure metrics without
  changing the canonical pipeline.
- [ ] **SHOULD — Migration adapter.** Provide an adapter path for applications moving from the
  built-in Microsoft localization abstractions.

## Tooling and developer experience

- [ ] **MUST — Parity CLI enhancements.** Add SARIF output, baseline mode, and a reusable CI action
  for missing/unused key checks.
- [ ] **MUST — Analyzer expansion.** Add rules for common localization defects: concatenated
  fragments, non-localized interpolated strings, and unsafe placeholder usage.
- [ ] **SHOULD — MSBuild integration.** Add opt-in MSBuild targets for key generation and parity
  checks in build pipelines.
- [ ] **SHOULD — Project templates.** Provide `dotnet new` templates for a minimal localized app
  and for provider-specific setups.
- [ ] **SHOULD — Test helpers package.** Publish reusable localization test assertions and fixtures.

## Quality and documentation

- [ ] **MUST — Full fuzz/property suite.** Expand parser and formatter fuzzing; add regression
  corpora for malformed catalogs.
- [ ] **MUST — Benchmark suite.** Add BenchmarkDotNet coverage for the new providers and caching
  paths; publish reproducible results.
- [ ] **MUST — Documentation completion.** Finish Arabic coverage for the samples, database guide,
  and remaining repository pages.
- [ ] **SHOULD — Load and resilience tests.** Add deterministic load and failure-injection tests for
  translation and caching.

## Exit criteria

The preview.2 feature set is complete; no preview.1 API is broken without a documented migration;
all release gates remain green.

---

# 1.0.0-preview.3 — Hardening and release candidate

**Goal:** freeze the surface and remove risk; this release should add no new public API unless a
preview blocker requires it.

- [ ] **MUST — Resolve all preview feedback.** Triage every preview.1/preview.2 issue; ship
  documented fixes or explicit non-goals.
- [ ] **MUST — API freeze review.** Review the complete public surface, naming, nullability,
  cancellation, and XML documentation; regenerate all baselines.
- [ ] **MUST — Compatibility matrix.** Run the full OS, TFM, AOT, and host matrix; document the
  supported combinations.
- [ ] **MUST — Performance evidence.** Publish final benchmark evidence and the conditions under
  which it was measured.
- [ ] **MUST — Security review.** Re-run the threat model, vulnerability scan, and dependency
  review; confirm notices and license metadata.
- [ ] **MUST — Package re-validation.** Validate every package, consumer flow, and clean-install
  smoke test from the preview feed.
- [ ] **MUST — Release documentation.** Finalize release notes, known limitations, migration notes,
  and the compatibility page.
- [ ] **SHOULD — Signing and SBOM.** Complete package signing and SBOM publication if not already
  done.

---

# 1.0.0 — First stable release

**Goal:** publish the stable contract with no new feature scope.

- [ ] **MUST — Stable API gate.** No breaking changes after preview.1 without a major-version
  decision recorded before the preview cut.
- [ ] **MUST — Package validation baseline.** Set `PackageValidationBaselineVersion` to `1.0.0` for
  every package after publication.
- [ ] **MUST — Stable installation smoke.** Install from nuget.org into net8, net9, and net10
  console, ASP.NET Core, and platform samples.
- [ ] **MUST — Final docs and release notes.** Publish the stable book, package pages, support
  policy, and release record.
- [ ] **MUST — Post-release support plan.** Define the issue triage, security response, and
  patch-release process.
- [ ] **LATER — Any feature that missed preview.3.** Move explicitly to `1.1.0`; do not hold 1.0
  stable indefinitely for it.

---

# 1.1.0 — Ecosystem expansion

- [ ] Fluent provider decision and implementation if user demand is demonstrated.
- [ ] Additional translation backends (AWS, OpenAI, or others) driven by demand.
- [ ] Additional TMS connectors and two-way glossary/translation-memory sync.
- [ ] Unity, Android, and iOS host integrations where the platform lifecycle justifies the package.
- [ ] Tenant-scoped stores and multi-tenant administration.
- [ ] Approval workflows for translations and governed content.
- [ ] Audit-retention and cleanup policies.
- [ ] Bulk import/export and audit-query APIs for the admin store.
- [ ] `dotnet new` templates, project recipes, and a migration toolkit.
- [ ] Expanded analyzer rules and code fixes.
- [ ] Performance SLOs and published benchmark trends.
- [ ] Full Arabic documentation and additional localized documentation editions.

# 1.2.0 — Operations and governance

- [ ] Translation analytics and quality dashboards as documented recipes.
- [ ] Advanced glossary management and terminology workflows.
- [ ] Multi-vendor EF Core store for the remaining database vendors.
- [ ] Schema migration tooling for the store.
- [ ] OpenTelemetry dashboards and alert packs.
- [ ] Admin API pagination, filtering, and bulk operations.
- [ ] Source-generator support for additional catalog formats.
- [ ] Extended pseudo-localization and screenshot-based overflow checks.

# 2.0.0 — Next major contract

- [ ] Remove pre-1.0 compatibility shims and obsolete surface.
- [ ] Revisit the one-provider/composite boundary if real-world demand requires a different default.
- [ ] Drop end-of-life target frameworks.
- [ ] Publish a complete migration guide from 1.x.
- [ ] Re-cut the package inventory and the API baselines.

## Not planned

- A full translation-management UI, billing, or vendor portal.
- Support for end-of-life frameworks and platforms.
- A provider that hides fallback or translation semantics from the consumer.
- Breaking changes without a major version and a migration note.

## Public release gate

| Gate | Evidence |
|---|---|
| Build | Every supported TFM and OS |
| Tests | Library, reference hosts, consumers, architecture |
| API | Committed baselines for every package and TFM |
| Packages | Pack, package validation, nuspec audit |
| Consumers | Restore, build, and test from the packed feed |
| Docs | DocFX, links, README claims, Arabic text, API coverage |
| AOT | Native AOT smoke on the supported matrix |
| Security | NuGet audit, threat model, notices, no secrets |
| Metadata | Icon, descriptions, tags, SourceLink, repository/project URL |
| Release | Preview tag, dry run, clean-install smoke test |
