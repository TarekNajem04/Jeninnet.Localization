## Summary

Describe the change and why it is needed.

## Motivation / Context

Why is this change necessary? What problem does it solve?

## Type of Change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Chore (refactoring, build, or infrastructure updates)

## Tests

- [ ] `dotnet build Jeninnet.Localization.slnx -c Release`
- [ ] `dotnet test tests/Jeninnet.Localization.Tests/Jeninnet.Localization.Tests.csproj -c Release`
- [ ] New test cases added (if applicable)
- [ ] All existing tests passed
- [ ] Other: 

## Security and Repository Controls

Confirm every item before requesting review:

- [ ] No secrets, credentials, tokens, private keys, or generated secret material were added.
- [ ] No GitHub Actions workflow permission escalation was added.
- [ ] No repository, organization, branch protection, environment, or security setting was weakened.
- [ ] No CODEOWNERS, security policy, Dependabot, or issue/PR governance change was made without owner approval.
- [ ] No direct commit to a protected branch was used; this change is submitted through a pull request.

## Review Requirements

- [ ] Repository owner review is requested.
- [ ] All conversations are resolved before merge.
- [ ] Required status checks are passing before merge.

## Breaking Changes

State any compatibility impact. Write `None` if not applicable.
