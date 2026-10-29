# Languages / اللغات

The documentation is written in English. The Arabic edition covers the complete book: forty-one
chapters from the introduction through the API reference guide and appendices, plus the AOT (Ahead-of-Time) and
trimming project page. It has been reviewed by a native speaker; the English source remains
authoritative if the two disagree.

| Language | Scope | Entry point |
|---|---|---|
| English | The complete book, reference, operations, and API guide | [Documentation book](en/README.md) |
| العربية | Complete edition: book and AOT/trimming page (native review completed) | [التوثيق العربي](ar/README.md) |

## Translation policy

- English is the source of truth. A translated page that disagrees with the English source is stale.
- Arabic is drafted from the argument in contemporary engineering Arabic, not as a sentence-level
  translation of the English text. A translation that reads as translation is not final.
- Code, API names, package IDs, configuration keys, and CLI commands are not translated. Arabic pages
  use explicit RTL (Right-to-Left)/LTR (Left-to-Right) direction wrappers for prose and code.
- A translated page links to the English chapter when the translated target does not exist yet.
- Translations are reviewed by a native speaker before they are listed as complete.
- When an English chapter changes, the translation is either updated or marked stale; it is not left
  silently divergent.

## Contributing a translation

Read [Contributing to the Documentation](CONTRIBUTING.md), then open an issue or pull request with:

- the target language and the chapters you want to translate;
- a native reviewer for the translation;
- the English revision the translation is based on.

The first translated pages live under `docs/<culture>/`. The Arabic pilot uses `docs/ar/` and its own
localized navigation at `docs/ar/toc.yml`.
