# Contributing to the Documentation

The documentation is part of the product. A chapter that teaches the wrong registration, a package
README that promises automatic schema creation, or a code example that no longer compiles is a defect.
This guide explains where a change belongs, how to write it, and which checks must pass.

The repository-level [CONTRIBUTING.md](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CONTRIBUTING.md) covers the project's contribution policy.
Documentation pull requests are welcome within that policy.

## Before you write

- **Verify against the source tree.** Read the implementation, tests, and package README before
  describing behavior. Do not document an intended API from memory.
- **Prefer one canonical explanation.** If a concept is already covered, link to it instead of
  restating it in a second place. Duplicated explanations drift.
- **State the limits.** A feature page is not complete until it says what the feature does not do,
  which exceptions it throws, and which host responsibility remains outside the library.
- **Do not use git-ignored working material as a source or a link target.** It is not part of the
  published repository.

## Where a change belongs

| The change is about... | Put it in... |
|---|---|
| A concept, model, or end-to-end workflow | A chapter under `docs/en/01-getting-started/` or `docs/en/02-guides/` |
| A recurring task or integration | The relevant guide under `docs/en/02-guides/` |
| An API, option, diagnostic name, or behavior contract | `docs/en/03-reference/` and the package README |
| Deployment, security, performance, or operations | `docs/en/04-operations/` |
| Release status, support posture, or known limits | `docs/en/03-reference/09-release-notes.md` or `10-known-limitations.md` |
| A package's installation, registration, or options | That package's `README.md` |
| A reference-host behavior or run command | The host README and `samples/README.md` |
| A repository script | The script's `README.md` and, when user-facing, the tooling reference |
| A schema or database operation | `docs/database/README.md` and the matching provider/deployment page |

When a change affects more than one place, update each place with its own level of detail and link
between them. Do not paste the same paragraphs into several chapters.

## The book's structure

`docs/README.md` is the table of contents and the entry point. It has four parts and an appendix:

1. **Getting started** — the problem space, installation, quick start, and mental model.
2. **Guides** — recurring jobs: providers, culture, formatting, keys, store, audit, hosts,
   translation, quality, diagnostics, testing, and RTL (Right-to-Left).
3. **Reference** — API, storage, providers, interchange, tooling, diagnostics, compatibility, release
   notes, limitations, and migration.
4. **Operations** — deployment, security, performance, observability, troubleshooting, FAQ, and the
   EF store schema.
5. **Appendix** — glossary and package map.

A chapter should follow the same arc as the existing ones:

1. **Problem.** What goes wrong without this feature or decision?
2. **Model.** What does the library do, and who owns which responsibility?
3. **Workflow.** A complete, real example.
4. **Limits and pitfalls.** What is not supported, what throws, what the host must do.
5. **Where to go next.** Two to five links that continue the journey.

Avoid a page that is only a list of links, a class-by-class dump, or marketing prose. The reference
chapters are still explanation-first; generated API pages, when added, handle member lookup.

## File naming and table-of-contents rules

- Chapters use `NN-topic.md` inside their part, for example `docs/en/02-guides/16-rtl-and-bidi.md`.
- Do not renumber existing chapters casually. Renumbering changes every inbound link. If you must,
  update the table of contents, all relative links, and the package READMEs in the same change.
- Add or update the entry in `docs/README.md` in the same change. A chapter that is not in the table
  of contents is invisible.
- Keep the part summaries in `docs/README.md` accurate when a chapter is added.
- Use lower-case, hyphen-separated file names.
- Do not create a placeholder page for a planned chapter. Add the row only when the chapter exists,
  or mark the plan explicitly outside the table of contents.

## Writing style

- Write in the present tense and the active voice. "The provider reads the file at registration",
  not "the file will be read".
- Prefer "the library", "the host", and "the consumer" over "we" or "you" when assigning a
  responsibility.
- Be direct about limits. "There is no provider cache" is better than "caching is handled elsewhere".
- Use sentence case for headings, except canonical type names and product names.
- Expand an acronym or abbreviation on first use in each page, for example
  ICU (International Components for Unicode). The [glossary](en/appendix/01-glossary.md) holds the
  canonical expansion for every short form used in the book.
- Wrap prose at roughly 100 characters. Tables and code blocks are exempt.
- Use fenced code blocks with a language identifier. Use `text` for diagrams and pseudo-code.
- Use relative links for repository targets. Do not link to git-ignored paths.
- Avoid emojis and exclamation marks. The repository's documentation voice is technical and calm.
- Do not add a "Summary" or "Conclusion" section; end with the practical next step.

## Code examples

The examples are the part of the documentation most likely to be copied. Treat them as code:

- Use the real public API and the canonical namespaces. Do not invent `Add*` methods, options, or
  extension names.
- Include the `using` directives needed to compile the example.
- Show a complete registration and a complete lookup, not a fragment that cannot run.
- If an example is illustrative or partial, label it and explain what is missing.
- Use `AppContext.BaseDirectory` for deployed content paths, not the current working directory.
- Never include credentials, connection strings with real values, machine-specific paths, or
  personal data.
- Show the failure behavior when it matters: a miss, a cancellation, a missing file, an invalid
  option, or a fallback step.
- Keep the snippet short; link to the full tests or reference chapter for exhaustive coverage.

### Verify the snippet

Add runnable or compile-only snippets to
[`samples/Jeninnet.Localization.Docs`](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Docs/README.md):

- Put a hermetic, deterministic example in `Program.cs` when it can run without network access,
  credentials, a GUI, or a real database.
- Put a registration-only example in `CompileOnlySnippets.cs` when running it would require external
  state.
- Name the source chapter in the method comment (`docs: ...`) so the pair is discoverable.
- Run the project after changing either side:

  ```powershell
  dotnet run --project samples/Jeninnet.Localization.Docs -c Release
  ```

If a snippet cannot be compiled at all, say why in the chapter and mark it as illustrative.

## Package READMEs

A package README is both repository documentation and the NuGet package page. Keep it useful on both:

- Start with one paragraph that says what the package is and what it is not. Do not open with
  marketing language.
- Include: **Installation**, **Registration**, **Configuration**, **Usage**, **Behavior and limits**,
  **AOT (Ahead-of-Time) and trimming**, **Related packages**, **Documentation**, and **License**.
- Use the exact package ID in install commands. Package IDs are case-sensitive in prose even when
  NuGet is not.
- Document every public option, its default, and its validation. Options are construction snapshots
  unless the package says otherwise.
- Document cancellation, thread safety, error propagation, and misses where they apply.
- Link to the canonical AOT contract and state the package's posture: compatible, compatible with
  documented annotations, excluded, or build-time only.
- Database providers must link to [`docs/database/README.md`](database/README.md) and must not imply
  automatic schema creation or seeding.
- Build-time packages must state their asset path (`analyzers/dotnet/cs`, `tools/...`) and that they
  are not runtime libraries.
- End with the license link. The package validation tests expect `README.md` and `LICENSE` in the
  packed package.
- Relative links must resolve in the repository. Review the rendered package page after the first
  publish; if a link does not survive the NuGet renderer, use a stable absolute repository URL for
  that target.

The full package README template is visible in any existing package; copy the nearest package
README and adapt it rather than inventing a new structure.

## Documentation site

The site is generated by DocFX from the Markdown in this repository. The configuration lives in
[`docfx.json`](docfx.json), the landing page in [`index.md`](index.md), and the navigation in
[`toc.yml`](toc.yml). The generated output goes to `docs/_site/`, which is git-ignored.

Restore the pinned tool, generate the API metadata, and build locally:

```powershell
dotnet tool restore
dotnet docfx metadata docs/docfx.json --warningsAsErrors
pwsh scripts/docs/check-api-reference.ps1
dotnet docfx build docs/docfx.json --warningsAsErrors
```

Preview the built site:

```powershell
dotnet docfx docs/docfx.json --serve
```

The `docs/api/` output is generated from the packable projects and is git-ignored. Always run the
metadata command before a clean site build; a missing `docs/api/` directory produces an incomplete site.
The API pages are linked from the site navigation and from the narrative reference chapters.

When the packable inventory changes, add the new project to the `metadata.src.files` list in
[`docfx.json`](docfx.json), add its API baseline, and run
`pwsh scripts/docs/check-api-reference.ps1`. The check verifies that every baseline assembly is
represented in the generated metadata.

The `Documentation` workflow rebuilds the site when documentation changes or when source or
integration projects change (their XML documentation feeds the API reference), and publishes it
automatically on `main` through the `github-pages` environment. It can also be run manually with
`workflow_dispatch`. Enable Pages for the repository before the first deployment. The workflow uses
the standard Pages actions.

When you add a chapter, update [`docs/README.md`](en/README.md) and [`toc.yml`](toc.yml).

## Localized documentation

English is the source of truth. A translation is a reviewed copy, not a fork:

- Put translated pages under `docs/<culture>/`, keeping the source file name where possible.
- Arabic pages are written in contemporary engineering Arabic from the argument itself, not as a
  sentence-level translation of the English text. A draft that reads as translation is marked as a
  draft until it passes editorial and native review.
- Keep code blocks, API names, package IDs, configuration keys, and CLI commands unchanged.
- Arabic pages declare direction explicitly: the page is wrapped in RTL, fenced code blocks are LTR (Left-to-Right)
  islands, and inline code uses `<code dir="ltr">`. The site-level direction script is a fallback,
  not a substitute for the document wrappers.
- Link to the translated page when it exists; otherwise link to the English chapter and say so.
- Add a localized `toc.yml` for the translated subset. The Arabic pilot uses `docs/ar/toc.yml`.
- Register the language in [`docs/languages.md`](languages.md) only after a native reviewer has
  approved the pilot.
- When the English source changes, update the translation or mark it stale. Do not leave a silently
  divergent translation.
- The language selector is [`docs/languages.md`](languages.md), linked from the root landing page and
  the book's introductory page.

A new language needs a named native reviewer and at least two translated chapters before it is listed
as available. Machine translation without review is not accepted as a completed translation.

## Required checks

Run the documentation gates from the repository root:

```powershell
pwsh scripts/docs/check-doc-links.ps1
pwsh scripts/docs/check-readme-claims.ps1
pwsh scripts/docs/check-arabic-text.ps1
```

The Arabic check rejects letters from other scripts, replacement characters, and hidden direction
controls in `docs/ar/`.

If you changed code-bearing snippets or sample projects, also run:

```powershell
dotnet build Jeninnet.Localization.slnx -c Release
dotnet run --project samples/Jeninnet.Localization.Docs -c Release
dotnet format Jeninnet.Localization.slnx --verify-no-changes --no-restore
```

If you changed a package README, the consumer package-validation suite is the final check that the
README and license still ship in the package:

```powershell
dotnet pack Jeninnet.Localization.slnx -c Release
dotnet test consumers/Jeninnet.Localization.Consumers.slnx -c Release
```

## Review checklist

- [ ] The claim is verified against source, tests, or a real run.
- [ ] Installation commands and API names match the current packages.
- [ ] The page has a problem/model/workflow/limits/next-step arc.
- [ ] The table of contents is updated.
- [ ] Relative links resolve; no link targets a git-ignored path.
- [ ] No secrets, credentials, machine-specific paths, or personal data.
- [ ] Code examples are compiled or explicitly marked illustrative.
- [ ] Package READMEs include installation, options, behavior, AOT, related packages, docs, and
  license sections.
- [ ] Limits and failure behavior are stated, not implied.
- [ ] The change does not duplicate an existing chapter without a reason.
- [ ] The documentation checks and, when relevant, the solution build pass.

## Chapter template

```markdown
# Chapter Title

One paragraph: the problem this chapter solves and who should read it.

## The model

What the library does and which responsibility stays with the host.

## Workflow

A complete, compiled example with the registration and the lookup.

## Limits and pitfalls

What is not supported, what throws, and what to check.

## Where to go next

- [Related chapter](related.md) for the next step.
```

## Related documentation

- [Documentation book](en/README.md)
- [Repository contribution policy](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/CONTRIBUTING.md)
- [Documentation snippet verification](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/samples/Jeninnet.Localization.Docs/README.md)
- [AOT and trimming](en/aot-and-trimming.md)
- [License](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/LICENSE)
