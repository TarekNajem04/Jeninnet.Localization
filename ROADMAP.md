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

### Recorded decisions (1.0.0-preview.1 engineering review)

- **MySql.Data retained for preview.1.** The MySQL provider uses `MySql.Data` (GPL-2.0-only with
  Universal FOSS Exception, disclosed in `THIRD-PARTY-NOTICES.md` and `CHANGELOG.md`). A move to
  `MySqlConnector` (MIT) is deferred to the preview.2 scope gate, batched with the multi-vendor
  EF Core store work — the swap is dependency-only with no behavior gap, and churning it alone
  buys nothing.
- **EFCore store bypass stands.** `SqlDbLocalizationStore` implements the store contracts directly
  instead of extending `DatabaseMessageProviderBase` because the resource-set store and the
  message-provider hierarchies are deliberately separate. Unification is a preview.2 design item,
  not a defect.
- **PO parsing stays duplicated for now.** Four implementations serve four different layers
  (provider runtime, interchange import, shared engine, parity tool) with different error
  contracts; consolidating them risks behavior drift for no runtime gain. Revisit alongside the
  analyzer-expansion work.

---

# 1.0.0-preview.1 — Adoption and release readiness

**Goal:** make the library installable, understandable, and trustworthy for a new consumer on the
platforms they actually use, while shipping the highest-value differentiators that do not require a
second ecosystem cycle.

## Adoption and platform support

- [x] **MUST — Multi-target runtime libraries (superseded).** Shipped `net8.0`, `net9.0`, and `net10.0`
  assets for preview.1; Roslyn tooling stayed on `netstandard2.0`; CLI tool multi-targeted where it
  added value. Conditional package versions per target were used.
  - Acceptance (met for preview.1): every runtime package contained all three `lib/<tfm>` folders; build, test, pack,
    package validation, and consumer validation passed on each target.
  - Note: net8.0/net9.0 reached end of support in November 2026. Starting with the preview.2 tree,
    all runtime libraries target `net10.0` only (see compatibility chapter); preview.1 remains
    available for older runtimes.
- [x] **MUST — Multi-target package and validation matrix (superseded for net8/net9).** Conditional dependency versions,
  TFM-specific package assets, package validation, consumer validation, and TFM-independent API
  baseline checks were in place for `net8.0`, `net9.0`, and `net10.0`. The matrix is now `net10.0`
  only; the validation machinery is unchanged.
- [x] **MUST — Multi-target CI, tests, and documentation (superseded for net8/net9).** The library test suite ran on all three
  runtime targets; the CLI tool shipped `net8.0` and `net10.0`; README, compatibility, installation,
  release notes, AOT guidance, package READMEs, and badges stated the supported matrix. CI now tests
  `net10.0`; the tool ships `net10.0`.
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
- [x] **MUST — Repository and security posture.** The repository is public at the canonical
  URL; `.github/SECURITY.md` (private advisories) and `.github/SUPPORT.md` define the
  reachable security and issue-reporting paths, linked from the root README.
- [x] **MUST — Dependency and vulnerability gate.** Add `NuGetAudit`/vulnerability scanning for
  direct and transitive packages to CI and document the response process.
- [x] **MUST — Code of conduct and support policy.** `CODE_OF_CONDUCT.md` and
  `.github/SUPPORT.md` are published and match the preview release status.

## NuGet and release engineering

- [x] **MUST — Visual and package identity.** The package icon ships in every package
  (`PackageIcon` + packed asset), repository/project URLs are centralized, and the
  generated README badges state the matrix. Per-package descriptions/tags are tracked
  separately below.
- [x] **MUST — Per-package descriptions and tags.** Every packable package carries a meaningful
  `Description` and targeted `PackageTags` (centralized conditions in `Directory.Build.props`
  keyed on the project identity rule); the metapackage description is duplicated in its csproj
  for single-source clarity. Verified in the packed `.nuspec` for the INI provider.
- [x] **MUST — Package READMEs.** All 37 packable package READMEs use absolute links (GitHub blob
  for repository files, `tareknajem04.github.io` for the book); no `](../` relative link remains
  in a packed README. The non-packable code-fix README and the Core design note keep working
  relative links by design. The docs link gate stays green (absolute URLs are out of its scope).
- [x] **MUST — SourceLink and deterministic builds.** SourceLink (`Microsoft.SourceLink.GitHub`),
  deterministic builds, and CI-only `ContinuousIntegrationBuild` are enabled; the packed `.nuspec`
  embeds the repository commit and `.snupkg` symbols ship per package.
- [x] **MUST — Metadata package.** `Jeninnet.Localization` discoverability metapackage ships the
  core trio (`Abstractions` + `Core` + `Storage`) with no code and no provider registration;
  the one-provider rule is unchanged. It participates in the solution, architecture inventory,
  API baselines (empty surface), package map, DocFX, and AOT posture matrix.
- [ ] **MUST — NuGet prefix and ownership.** Reserve the package prefix, verify owners, and document
  the publishing account and approval environment.
- [x] **MUST — Preview versioning/tag verification.** The workflow resolves the version from the
  `v*` tag, rejects a mismatch with the solution-wide `<Version>`, and normalizes four-part
  numeric tags; prerelease labels must match exactly.
- [x] **MUST — Preview publication workflow.** The workflow builds, tests, packs with package
  validation, validates consumers from the packed feed, retains artifacts (30 days), requires
  the production environment approval, pushes via OIDC with `--skip-duplicate` (symbols ride
  along, never pushed separately), and creates the GitHub release from the versioned notes file
  (or generated notes) with `--prerelease` while the version carries a prerelease label.
  A separate dry-run publish job is intentionally out of scope: the tag-verification step and
  `--skip-duplicate` make re-runs safe.
- [x] **MUST — Package count and validation.** The publish workflow derives the expected package
  count from the committed API-baseline inventory (no fixed literal) and compares it against the
  packed feed; pack runs with `EnablePackageValidation=true`, and the consumers solution
  restores, builds, tests, and runs from that feed before any push.
- [x] **MUST — Release checklist.** `docs/en/04-operations/09-release-checklist.md` (and the Arabic
  edition) gates every release: version/tag, build/test matrices, API/baseline gates, package and
  consumer validation, clean-install smoke, docs/AOT/security, and the publish evidence.
- [ ] **SHOULD — SBOM and checksums.** Publish an SBOM and checksums with each release.
- [ ] **SHOULD — Package signing decision.** Decide whether preview packages are signed and record
  the rationale.

## Core differentiators

- [ ] **MUST — Composite provider (moved to preview.2).** Tracked under preview.2
  "Provider composition" below; the exactly-one-provider selector boundary is unchanged
  until then.
- [ ] **MUST — Source-aware translation (moved to preview.2).** Tracked under preview.2
  "Translation source templates" below.
- [ ] **MUST — Provider cache decorators (moved to preview.2).** Store-level caching exists;
  message-provider-level decorators are tracked under preview.2 "Provider caching layer" below.
- [ ] **MUST — Missing and unused key analyzer (moved to preview.2).** `JLNLOC001` flags
  hardcoded user-facing strings today; catalog-backed missing/unused reporting lives in the
  parity tool. The unified compile-time check is tracked under preview.2 "Analyzer expansion"
  below.
- [x] **MUST — Documentation and samples.** The book, package READMEs, sample host matrix,
  performance notes, and release notes cover every preview.1 feature.

## Quality and release evidence

- [x] **MUST — Coverage gate.** A 65% total line-coverage ratchet runs in the SonarCloud
  coverage step (measured ~70%); generated code, samples, and tooling are excluded from the
  collection where appropriate.
- [x] **MUST — AOT and trimming matrix (net10-only).** The smoke consumer publishes and runs
  localize/format/cache/translate paths NativeAOT with trim analysis clean; with a single
  supported TFM the matrix is the smoke run plus the Windows host coverage. Multi-OS AOT
  evidence stays under the preview.3 compatibility matrix.
- [x] **MUST — Benchmark suite (core paths).** BenchmarkDotNet coverage for localizer lookup
  (hit/miss/plural) and default/ICU formatter paths with memory diagnostics lives in
  `benchmarks/` and runs locally. Caching-path and translation-batching measurements ride with
  the preview.2 benchmark item below.
- [x] **MUST — Threat model.** File ingestion, translation endpoints, database access, audit
  redaction, secret handling, and untrusted catalog input are reviewed in the security chapter
  (`docs/en/04-operations/02-security.md`); XML hardening, handoff size caps, redacted audit
  hashes, and the no-secrets rule are enforced in code and tests.
- [x] **MUST — Fuzz smoke tests (scope moved to preview.2).** Parser/formatter property coverage
  without thresholds ships as part of the preview.2 "Full fuzz/property suite" item below,
  together with malformed-catalog regression corpora; this line tracks that item, not a
  separate preview.1 deliverable.
- [x] **MUST — Documentation gates.** DocFX, repository links, README claims, Arabic text, and API
  coverage stay green.

## Identity consistency

- [x] **MUST — Source headers.** Every hand-authored source file carries the canonical header
  (`Copyright (c) Jeninnet`, `Part of the Jeninnet Platform`,
  `Repository: https://github.com/TarekNajem04/Jeninnet.Localization`, MIT). The sweep replaced
  the legacy `Jeninnet.Platform` repository URL in 25 files and the legacy license-only variant
  in one test file. Generated `*.Designer.cs` files keep their tool headers, and header-less
  shared/fixture inputs carry no legacy header to replace.
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

- [ ] **MUST — Translation source templates (moved from preview.1).** Let translation providers
  translate a source template instead of an opaque key, with key-as-source kept for gettext-style
  catalogs. Include the source template in cache identity and validate placeholder parity.

- [ ] **MUST — First TMS connector.** Add a connector for one translation management system
  (Crowdin or Weblate) with pull, push, dry-run, conflict reporting, and translator-note support.
- [ ] **MUST — Connector contract.** Define a reusable connector contract so additional systems do
  not fork the workflow.
- [ ] **SHOULD — Additional TMS connectors.** Add Lokalise and Phrase if the first connector proves
  the model.
- [x] **MUST — DeepL provider.** `Jeninnet.Localization.Translation.DeepL` ships behind the
  canonical contract with the Azure-grade capability model (batching, cache, preload, refresh,
  events, translation memory, glossary, resilience) and DeepL wire details documented in its
  README and the machine-translation guide. All 49 tests run offline against a fake HTTP
  handler (no live key exists or is required); the provider participates in the solution,
  architecture inventory, API baselines, package map, DocFX metadata, AOT posture matrix,
  and per-package descriptions/tags.
- [ ] **SHOULD — AWS Translate provider.** Add an AWS translation provider with the same contract and
  resilience policy.
- [ ] **SHOULD — Glossary and translation-memory exchange.** Import/export glossary and memory data
  through the existing interchange packages.
- [ ] **SHOULD — Batch job persistence.** Allow long-running translation jobs to resume from a
  ledger instead of restarting from scratch.

## Providers and storage

- [ ] **MUST — Provider composition (moved from preview.1).** Add an opt-in ordered provider
  composition: exact match wins, no implicit translation, no hidden fallback, capability
  aggregation, and clear validation errors. It remains one registered provider at the selector
  boundary.
- [x] **MUST — Provider caching layer (moved from preview.1, extends the store decorators).**
  `Jeninnet.Localization.Providers.Caching` ships `CachedMessageProvider` and
  `DistributedCachedMessageProvider`: TTL caching with single-flight coalescing, honest
  capability reporting/forwarding, preload, refresh (+forwarding), owned acquisition events,
  generation-based distributed invalidation, and SHA-256 key namespacing, with 24 tests,
  baselines, and full docs sync. AOT-safe; neither decorator implements `IDisposable`.

- [x] **MUST — Additional catalog formats.** INI and TOML ship as first-class providers with
  contract + file-behavior tests, baselines, package map, provider reference, and AOT posture
  entries; the YAML/TOML-beyond-flat question is answered by documenting the flat string-only
  subset as the supported scope.
- [x] **MUST — Source generation from more formats.** The key generator reads `*.loc.json`, `*.resx`, `*.po`, and XLIFF 2.0 `*.xliff`/`*.xlf` additional files (ResX data names, PO msgids with fuzzy/obsolete/header skips and context-deduped plurals, XLIFF 2.0 unit ids), with format-specific malformed-content diagnostics (`JLNKEY007`–`JLNKEY009`) and 21 new unit tests plus fixture end-to-end coverage; documented in the strongly-typed-keys guide, tooling reference, and generator README.
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

- [x] **MUST — Parity CLI enhancements.** `--sarif` writes SARIF 2.1.0 (one rule per finding
  kind, file/line locations, `parity/v1` fingerprints; `error` for catalog-error, missing-key,
  unresolved-reference, and definition-conflict); `--write-baseline` records a versioned baseline
  (always exit 0) and `--baseline` fails only on new findings (fixed ones informational; missing
  baseline file is a usage error). The reusable `.github/actions/parity` composite action installs
  the tool, runs the gate, and uploads SARIF to code scanning; a `parity-action` CI job dogfoods
  it against the clean fixture. JSON report stays at `schemaVersion: 2` (no format change).
- [ ] **MUST — Analyzer expansion (includes the preview.1 catalog-backed check).** Add rules
  for common localization defects: concatenated fragments, non-localized interpolated strings,
  unsafe placeholder usage, and the catalog-backed missing/unused-key check moved from preview.1
  (generated key constants + source references vs catalog inputs, with a safe code fix).
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
