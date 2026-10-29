# Choosing a Provider

A provider is the single source of message templates for a composition. Choosing one is therefore
a decision about where the catalog lives, who may change it, how changes reach production, and
what runtime cost each lookup carries. It is not a minor package choice.

This chapter starts from scenarios, compares the trade-offs, and explains how to plan a migration
when more than one source exists today. Registration mechanics are in
[Configuring providers](02-configuring-providers.md).

## Start from the workflow, not the file format

Ask these questions before comparing packages:

1. **Who edits the catalog?** Developers in the repository, translators in a CAT tool, or
   administrators in a running system?
2. **How do changes reach production?** A build and deployment, a content sync, or an immediate
   database write?
3. **Which culture features are required?** Flat templates only, contexts, plural forms, or
   runtime machine translation?
4. **What is the runtime budget?** In-process dictionary lookup, file read at startup, database
   query per lookup, or network call per cache miss?
5. **What must be auditable?** Nothing, a deployment log, or per-row actor/timestamp history?
6. **What is the hosting constraint?** Server, desktop, browser WebAssembly, trimmed, or Native
   AOT (Ahead-of-Time)?

The answers usually select one family immediately.

## Scenario guide

### Tests, prototypes, and small static catalogs

Use **`Jeninnet.Localization.Providers.Memory`**. It copies the supplied dictionary into an
immutable construction snapshot, performs no I/O, and declares itself compatible with the browser
WASM (WebAssembly) guard. The provider serves one flat map and ignores the culture argument; multi-culture
compositions use a `CultureAwareMessageProvider` with one source map per culture. Memory is also
the right choice for deterministic unit tests of the pipeline.

Do not use memory as a production system of record. Changes require a process restart, and there is
no external editing workflow.

### Simple file catalogs owned by the repository

Use **JSON** or **XML**. Both have the same lifecycle shape: one file per culture for multi-culture
registration, lazy loading on first lookup, exact ordinal key matching, and opt-in hot reload with
`WatchForChanges` and `ReloadDebounce`. JSON is convenient for application teams and JavaScript
tooling; XML fits existing XML content pipelines.

These providers are a good default when the catalog is versioned with the code and deployed as
content. They are a poor fit for non-developer editors who cannot commit and deploy.

### Existing ResX files or compiled resource packs

Use **`Providers.ResX`** when the `.resx` files are embedded in your assembly and you want
`ResourceManager` to resolve them. Use **`Providers.Satellite`** when the strings arrive as compiled
neutral resources plus `{culture}/{AssemblyName}.resources.dll` satellite packs, including packs
authored outside your repository.

Both providers disable `ResourceManager` parent probing and leave culture fallback to the localizer.
That prevents two fallback algorithms from disagreeing. Use satellite when you consume precompiled
packs; use ResX when you author `.resx` in the same project.

### Translator tooling, contexts, and plural forms

Use **`Providers.Po`** when translators work in Poedit, Weblate, Crowdin, or another gettext-aware
tool. It reads PO (Portable Object) text and GNU MO (Machine Object) binaries, honors `msgctxt` through the same context encoding as
`LocalizeWithContextAsync`, and implements `IPluralFormMessageProvider` so `LocalizePluralAsync`
can select the count-aware form from a `Plural-Forms` header.

Choose PO for the workflow, not because it is fashionable. If you need a single JSON file consumed
directly by a front end, PO adds a conversion step. The interchange packages can convert catalogs
without changing the runtime provider.

### Central, runtime-editable catalogs

Use one of the **database message providers** when several application instances must read the same
message table and content can change without a deployment. SQLite is a good fit for a single host
or an embedded scenario. SQL Server, MySQL, and PostgreSQL fit a shared server catalog.

These providers query `LocalizationMessages` for each lookup; they do not maintain an in-box message
cache. Expect a database round trip per fallback step unless you add caching at another layer. The
governed administration APIs and history live in the EF Core **store**, not in the ADO.NET message
providers, and the store uses a different schema (`LocalizedStrings`, resource sets). If you need
both runtime message lookup and audited administration, plan the data flow explicitly rather than
assuming the store feeds the pipeline.

### Runtime machine translation

Use **`Translation.Azure`** or **`Translation.Google`** only when translating at request time is an
acceptable product decision. These providers are ordinary message providers: the key you pass is
the source text, and the target culture is the translation target. They replace the file or
database provider; they cannot be layered behind one because exactly one provider is resolved.

Azure offers a TTL (Time To Live) cache, single-flight coalescing, batching, preload, refresh, glossary and
translation-memory hooks, retries, a circuit breaker, and rate limiting. Google exposes the minimal
single-key v2 path with no provider cache, batching, or refresh lifecycle. Both add network latency,
cost, and failure modes to the lookup path. For most products, translate catalogs offline and serve
the result from a file or database provider; use a translation provider for genuinely dynamic
content or as a backfill source.

### Quality assurance and pseudo-localization

Use **`Providers.Pseudo`** in test or staging environments to catch layout overflow and untranslated
leaks. It serves a synthetic culture (`qps-ploc` by default) by transforming a source template:
accented letters, `[!! ... !!]` markers, and percentage-based padding. Placeholders survive, so
formatting still runs.

Pseudo-localization is not a translation. It is a deliberately ugly variant used to make hardcoded
strings and short layouts visible. Because it is a provider, enabling it means replacing the normal
provider in that environment.

### Strong typing and build-time validation

The key generator and analyzer are not providers, but they influence the choice. JSON catalogs are
the generator's input format (`*.loc.json`). If compile-time keys are a hard requirement, JSON
becomes the natural canonical catalog even if another format is used for translation exchange.
See [Strongly typed keys](05-strongly-typed-keys.md).

## Trade-off summary

| Provider | Culture model | Change model | Runtime cost | Best fit | Main constraint |
|---|---|---|---|---|---|
| Memory | One flat map; compose for cultures | Restart | In-process dictionary | Tests, prototypes, WASM | No external editing |
| JSON | One file per culture in multi-culture mode | Redeploy, or opt-in file watch | One file load, then dictionary | Repository-owned catalogs | Flat format; no contexts/plurals |
| XML | One file per culture in multi-culture mode | Redeploy, or opt-in file watch | One file load, then dictionary | Existing XML pipelines | Same format limits as JSON |
| ResX | Embedded resource sets per culture | Rebuild/redeploy | `ResourceManager` lookup | Existing ResX in one assembly | Missing base name fails at first lookup |
| Satellite | Compiled neutral + satellite packs | Rebuild/redeploy packs | `ResourceManager` lookup | Third-party/precompiled packs | Authoring is external |
| PO/MO | One catalog per culture | Redeploy, or opt-in watch | Catalog parse, then dictionary | Translator workflow, plural/context | Tooling and file discipline required |
| SQLite | Exact key + culture rows | Write to database | DB round trip per step | Single-host shared storage | No built-in message cache |
| SQL Server / MySQL / PostgreSQL | Exact key + culture rows | Write to database | DB round trip per step | Shared central catalog | Operational dependency |
| EF Core store | Resource-set name + culture | Governed API | DB round trip | Admin, audit, synchronization | Not a message provider |
| Pseudo | Synthetic culture only | Restart or reconfigure | In-process transform | QA and demos | Not a translation |
| Azure Translator | Target culture, key is source text | Remote service | Network on cache miss | Dynamic content | Cost, latency, one-provider rule |
| Google Translate | Target culture, key is source text | Remote service | Network per lookup | Simple dynamic translation | No cache/batch/refresh |

- **The EF Core store is not in the message-pipeline column.** It implements `ILocalizationStore`,
  not `IInnerMessageProvider`. It is the right choice for administered resource sets, audit history,
  and code-first synchronization. It does not make `IMessageLocalizer` return strings.
- **Translation providers are not a fallback layer.** They are the single provider. If a key is not
  translatable or the service fails, there is no file catalog behind them.

## Combining sources when only one provider is allowed

The architectural rule is per composition, not per repository. Different hosts can register
different providers: an API can serve a database catalog, a desktop build can embed JSON, and a QA
host can use pseudo-localization. Within one host, exactly one provider is resolved.

When several sources must contribute to one host, use one of these strategies:

1. **Consolidate at build time.** Export every source into one canonical catalog and register one
   provider. This is the cleanest option for JSON, XML, and PO catalogs and preserves deterministic
   deployments.
2. **Make one source authoritative and migrate the others into it.** A database can be seeded from
   files; files can be generated from a database export. The runtime sees only the winner.
3. **Use translation batching to materialize results.** If machine translation is part of the
   workflow, run it ahead of time and write the results into the runtime catalog. The runtime
   provider stays a file or database provider, and lookups do not depend on the network.
4. **Use the store for administered resource sets and keep the message pipeline separate.** This is
   a deliberate two-path design, not a merge. Document which path each consumer uses.

Do not try to simulate chaining with a custom `IMessageProvider` that queries two providers. It
would violate the single-fallback-owner rule, duplicate culture logic, and make the exactly-one
guarantee meaningless. If a custom composite is truly required, it must present itself as one
provider and own its acquisition semantics, but it is outside the supported model.

## Worked example: moving a product catalog to PO

A product team starts with a JSON catalog and one developer-maintained English file. The translator
uses Poedit, needs plural forms for Slavic languages, and needs `msgctxt` to distinguish "Open" as a
button from "Open" as an order state.

The migration plan:

1. **Inventory keys and cultures.** Export the JSON keys, identify duplicates across screens, and
   assign contexts where the same key has different meanings. Context is part of the key; changing
   it later is a catalog migration.
2. **Generate PO catalogs per culture.** The PO interchange codec can write `msgid`/`msgstr` pairs.
   The runtime provider expects one catalog per culture, for example `Localization/en.po` and
   `Localization/ar.po`.
3. **Switch registration in one release.** Replace the JSON registration with
   `AddPoLocalizationProviders(AppContext.BaseDirectory, ["en", "ar"])`. Do not register both.
4. **Move context lookups to `LocalizeWithContextAsync`.** Existing plain keys keep working; the
   context-scoped keys use the composite encoding automatically.
5. **Use `LocalizePluralAsync` for count-bearing messages.** The PO provider serves per-form
   entries when a `Plural-Forms` header is present; otherwise the pair mapping applies.
6. **Keep JSON for the key generator if needed.** The generator reads `*.loc.json`; you can keep
   the canonical key files as generator inputs while the runtime serves PO, as long as the key sets
   are checked by the parity tool.

This plan changes the source of messages without changing application lookup code. The localizer
API, fallback algorithm, and formatter stay the same.

## Pitfalls

**Pitfall:** choosing a translation provider as a drop-in replacement for a catalog and discovering
that offline startup, tests, and air-gapped deployments now depend on a remote service. Keep a
file or database provider for normal operation and use translation as a batch or backfill step
unless request-time translation is an explicit product requirement.

**Pitfall:** registering the EF Core store and expecting `IMessageLocalizer` to use it. It is a
store. See [Resource sets and the store](06-resource-sets-and-store.md) for the bridge that does use
it.

**Pitfall:** treating a file provider's culture argument as a selector. A single-culture JSON, XML,
or PO provider is a flat map and ignores the culture for exact-match lookup. Use the multi-culture
registration overloads (which compose `CultureAwareMessageProvider`) or one provider per culture
inside a dispatcher. Do not add multiple providers to DI (Dependency Injection).

**Pitfall:** enabling file watching in production without an operational plan. Hot reload is
opt-in, keeps the last-good table on failure, and raises events, but it is not a deployment
mechanism. If your content is baked into an image, watching adds overhead for no benefit.

## Where to go next

- [Configuring providers](02-configuring-providers.md) for the exact option types and validation.
- [The mental model](../01-getting-started/04-mental-model.md) for the fallback and selection
  rules that constrain every choice here.
- [Provider reference](../03-reference/03-provider-reference.md) for package-by-package details.
- [Migration from Microsoft.Extensions.Localization](../03-reference/08-migration-from-ms-extensions.md)
  if the application already uses `IStringLocalizer` and ResX.
