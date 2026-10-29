# Security Policy

## Reporting Vulnerabilities

Do not open a public issue for suspected vulnerabilities. Report security issues privately through GitHub Security Advisories for this repository, or contact the repository owner directly if advisories are unavailable.

Include a clear description, affected versions or commits, reproduction steps, impact, and any known workaround. The owner will acknowledge the report within two business days, triage it, coordinate a fix, and disclose publicly only after a patched release or mitigation is available.

## Dependency and vulnerability gate

`NuGetAudit` runs on restore for direct and transitive packages with audit level `low`; `NU1901`–`NU1904` are treated as errors. Dependabot and maintainer review keep dependencies current. Critical and high findings are mitigated within seven days, moderate findings within 30 days, and low findings in the next scheduled update. Temporary exceptions must record the advisory ID, affected packages, compensating control, and expiration date.

## Repository Security Rules

All changes must be submitted through pull requests. Direct pushes to protected branches are prohibited.

Infrastructure, GitHub Actions, branch protection, CODEOWNERS, Dependabot, security policy, release, deployment, and repository-setting changes require explicit repository owner approval.

Workflow files must use least privilege. The default permission model is `permissions: read-all`; write permissions may be granted only at the job level and only when required.

No contributor may disable, weaken, bypass, or remove security protections, required reviews, required status checks, secret scanning, dependency scanning, code scanning, or deployment approvals.
