# Release Checklist

This checklist gates every preview release. It mirrors the ROADMAP exit criteria and the
publish workflow (`.github/workflows/nuget-publish.yml`); a release ships only when every box
is checked with evidence.

## Version and tag

- [ ] `Directory.Build.props` `<Version>` equals the release version (e.g. `1.0.0-preview.3`).
- [ ] The tag is `v<Version>` (e.g. `v1.0.0-preview.3`) and points at the release commit.
- [ ] `CHANGELOG.md` has an entry for the release; preview scope still allows surface adjustments.
- [ ] A notes file exists at `.github/release-notes/v<Version>.md`; otherwise GitHub generates
  notes from the commit range.
- [ ] The GitHub release is created with `--prerelease` while the version carries a prerelease
  label, so nuget.org and GitHub both mark it as a preview.

## Build and test

- [ ] `dotnet build Jeninnet.Localization.slnx -c Release` succeeds with zero warnings/errors.
- [ ] `dotnet build Jeninnet.Localization.Windows.slnx -c Release` succeeds on Windows.
- [ ] `dotnet test Jeninnet.Localization.Tests -c Release -f net10.0` passes (library suite, net10 leg).
- [ ] `dotnet test Jeninnet.Localization.Tests -c Release -f net11.0` passes (library suite, net11 leg).
- [ ] Reference suites pass: `Reference.Shared.Tests`, `Reference.Hosts.Tests`.
- [ ] Windows host suite passes: `HostIntegrations.Tests` (MAUI/WinUI).
- [ ] `dotnet format --verify-no-changes` is clean for the main and consumers solutions.

## API and packages

- [ ] `PublicApiSurface_MatchesCommittedBaselines` passes; any intended surface change regenerated
  its `.Api.txt` baseline in the same change with a changelog note.
- [ ] ApiCompat check passes: the surface diff against the previous release tag is fully listed in
  `.github/api-compat-acknowledged.txt`.
- [ ] `dotnet pack` with `EnablePackageValidation=true` succeeds for both solutions.
- [ ] The packed package count equals the API baseline count (44 packable: 42 cross-platform plus
  MAUI and WinUI); the consumer-feed count check in the workflow passes.
- [ ] Every package carries the icon, README, license, third-party notices, SourceLink commit,
  and per-package description/tags (see the packed `.nuspec`).

## Consumers and smoke

- [ ] The consumers solution restores, builds, tests, and runs from the packed local feed.
- [ ] Clean-install smoke: install the metapackage plus one provider from the feed into an empty
  console host and resolve one key per served culture plus one deliberate miss.

## Docs, AOT, security

- [ ] DocFX builds; the link, README-claims, Arabic-text, and API-coverage gates pass.
- [ ] The package map, provider reference, compatibility page, and release notes cover the release.
- [ ] The NativeAOT smoke consumer publishes and runs (`SMOKE OK`) with trim analysis clean.
- [ ] `NuGetAudit` passes; `THIRD-PARTY-NOTICES.md` covers every redistributed dependency.
- [ ] No secret or personal data in catalogs, logs, or diagnostics (see the security chapter).

## Publish

- [ ] The publish workflow ran on the tag: version verification, tests, pack, consumer
  validation, artifact upload (30-day retention), OIDC push with `--skip-duplicate`, and the
  GitHub release with attached `.nupkg`/`.snupkg`.
- [ ] The production environment approval completed before the push step.
- [ ] nuget.org lists the version as a prerelease with all packages visible.

## Where to go next

- [Compatibility and versions](../03-reference/07-compatibility-and-versions.md) for the support posture.
- [Deployment](01-deployment.md) for the release order and rollback pairing.
- [Security](02-security.md) for the threat model behind the scan and redaction checks.
