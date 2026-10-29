# Troubleshooting

Localization failures usually present as one of four symptoms: text is missing, text is in the
wrong language, the application fails at startup, or a request fails while looking up a string.
The first job is to identify which layer produced the symptom. `IMessageLocalizer` returns `null`
for a miss and never fabricates a marker. The store bridge returns `[MISSING:key]` with
`ResourceNotFound = true`. A provider exception is neither of those. Start by deciding which one
you are looking at, then follow the relevant section.

## Missing translation or `[MISSING:key]`

| Likely cause | Diagnosis | Fix |
|---|---|---|
| Key typo, case difference, or stray whitespace | Search the catalogs for the exact key; dictionary-backed providers compare ordinally and case-sensitively. | Correct the key or the catalog entry. |
| Wrong culture or fallback path | Log the requested and ambient cultures; compare with the catalog file names. | Pass the intended culture explicitly or fix request-culture selection. |
| Provider not registered, or the wrong provider registered | Resolve `IMessageProvider`; with zero providers every lookup returns `null`. | Register exactly one provider. |
| File not deployed | Check the path derived from `AppContext.BaseDirectory`; file providers validate existence at registration for JSON, XML, and PO (Portable Object). | Add the content copy rule and redeploy. |
| Store lookup rather than message pipeline | `[MISSING:key]` comes from `LocalizedStringEntry.Missing`; the message pipeline returns `null`. | Fix the resource set, culture, or key; or use `IMessageLocalizer` if that was intended. |
| Empty stored value | The governed store permits an empty value, and an empty value is a hit that suppresses fallback. | Store the real template or delete the empty entry. |

Enable the missing-key sink if you need production evidence. `LoggerMissingKeySink` logs one
structured warning per miss with the key, culture, and call site; the sink is consulted only after
the full fallback chain misses. For a broad audit, run `jeninnet-localization-keys`; it reports
missing keys, fallback-served keys, and unused keys with file/line locations.

For a single lookup, reproduce it outside the UI:

```csharp
var value = await localizer.LocalizeAsync("Cart.Items", parameters, new CultureInfo("ar"));
Console.WriteLine(value is null ? "(miss)" : value);
```

If this returns a value but the page does not, the page is using a different localizer, a
different culture, or the store bridge rather than the message pipeline.

## Wrong culture

`CurrentCulture` controls number and date formatting; `CurrentUICulture` selects UI resources. The
convenience overloads use `CurrentUICulture`. A background job that changes `CurrentCulture` but
not `CurrentUICulture` will keep resolving the old language.

Other common causes:

- The default culture is captured when `DefaultMessageLocalizer` is constructed. Mutating
  `LocalizationOptions.DefaultCulture` afterward does not change an existing localizer.
- A long-lived `IStringLocalizer` captures the UI culture at creation. Create a new localizer when
  the culture changes.
- In ASP.NET Core, request cultures come from the middleware. Without the request-localization
  bridge, a provider's Arabic files do not make `ar` a served culture. With no store and no
  explicit cultures, the served set is the default culture only.
- The culture name passed to the provider is exact. A `de` request does not read `de-CH.json`; the
  parent walk goes up, not down.

Diagnose by logging `CultureInfo.CurrentUICulture.Name`, the culture passed to the localizer, and
the `localization.culture` tag on the lookup activity. Fix request-culture configuration rather
than adding duplicate catalogs.

## Fallback surprises

Fallback is working as designed when:

- a missing German key renders English, because the default culture is the last step;
- `de-CH` resolves from `de`, because the parent chain walks up;
- a lookup for invariant culture queries only the default culture, when one is configured;
- the default culture is queried twice on a miss when it is also the requested culture;
- a `de` request does not read `de-CH.json`;
- an empty target value stops fallback because it is a hit.

A successful fallback is not translation coverage. Use the parity tool, pseudo-localization, and a
recording provider test to assert the exact sequence of queried cultures. Do not add a second
provider to "fill gaps"; two providers throw.

## Multiple-provider registration error

Resolution fails with an `InvalidOperationException` whose message begins:

```text
Exactly one Localization Provider must be registered, but 2 Providers were resolved: ...
```

The selector lists the resolved provider type names. This is not a merge conflict; provider
chaining is not supported. Common causes are registering JSON and a database provider "for
fallback", registering a translation provider alongside a file provider, or registering a
provider twice through two composition paths.

Diagnose by resolving `IEnumerable<IInnerMessageProvider>` and inspecting the instances, or by
reviewing the composition root for `Add*Localization` calls. Fix by choosing one system of record
and consolidating the other content into it. The EF Core store is not a provider and does not
trigger this error; adding it does not give `IMessageLocalizer` a source.

## Store conflicts and optimistic concurrency

Governed mutations report expected domain conflicts instead of throwing. Check
`ResourceAdminResult.Applied` and `Conflict.Kind` (`AlreadyExists`, `NotFound`, or
`TargetExists`). A create for an existing key, an update for a missing key, or a rename whose
target already exists is a normal result, not an exception.

Validation failures throw in the `ArgumentException` family: empty keys, unknown cultures, or a
null value. A read-only store throws `StoreReadOnlyException` before producing a result; use
`store.RequireAdmin()` to enter through the capability check. History reads against a store
without history throw `StoreHistoryUnsupportedException`.

Cross-process writer contention can exhaust the version-counter retries and throw
`DbUpdateConcurrencyException` with this message:

```text
The catalog version counter could not be advanced because another writer is mutating the catalog
concurrently. Retry the mutation.
```

Retry the whole mutation, add database constraints or serialize writers externally, and keep raw
`ILocalizationResourceSet` CRUD (Create, Read, Update, Delete) out of the governed path. Raw CRUD is not versioned, not audited,
and not gated. If a trail looks incomplete, check whether a tool used raw CRUD.

## Database connectivity

The database providers do not catch ADO.NET exceptions and do not convert them into misses. You
will see the vendor exception: `SqlException`, `MySqlException`, `NpgsqlException`, or
`SqliteException`. Connection failures, logins, TLS, timeouts, and missing tables all surface this
way.

Check, in order:

1. the connection string and the account's permissions;
2. that the table and columns exist (`Id`, `MessageKey`, `Culture`, `Template`);
3. that the configured `TableName` matches the actual schema;
4. that the database is reachable from the application host;
5. that the schema migration actually committed.

An invalid table name fails earlier, when `DatabaseLocalizationOptions.TableName` is set, with an
`ArgumentException`. Registration validates that the connection string and table name are
non-empty and shaped correctly; it deliberately does not connect. Use the provider health check or
a direct scalar query to test connectivity.

## Translation provider authentication and rate limits

Missing configuration fails at registration or at provider construction with
`InvalidOperationException` (`Azure Translator ApiKey is not configured.`,
`Google Translation ApiKey is not configured.`, or an endpoint validation message). That is a
configuration problem, not a remote failure.

Remote failures surface as `HttpRequestException` with the status in the message:

```text
Azure Translator returned 401 Unauthorized.
Google Translation returned 429 Too Many Requests.
```

Azure retries network failures, timeouts, HTTP 408, 429, and 5xx up to `RetryMaxAttempts` with
exponential backoff. When the circuit breaker is open, the call fails fast with
`BrokenCircuitException` and no HTTP request. `RequestTimeout` expiry surfaces as
`OperationCanceledException`. Google has no retry, cache, preload, or refresh lifecycle; a 429 is
returned to the caller immediately.

Diagnose with the response status, the `localization.translation.*` metrics, and Azure's
`AcquisitionFailed` event. Check the key, region, endpoint, subscription quota, and outbound
network. For Google, remember the key is in the query string; check gateway logs for redaction
before assuming the key is invalid.

## Parity tool failures

`jeninnet-localization-keys` exits `0` for clean, `1` for findings, and `2` for usage or
environment errors. Exit `1` is actionable data, not a crash. Read the JSON report's `findings`
array: the kinds are `missing-key`, `unused-key`, `template-drift`, `format-gap`,
`placeholder-drift`, `unresolved-reference`, `definition-conflict`, and `catalog-error`.

Common causes of unexpected findings:

- a key is referenced through reflection or a string shape the tool does not recognize;
- a catalog file is outside the scanned root;
- the default culture is wrong, so fallback states are computed against the wrong chain;
- `.loc.json` generator files were placed in an auto-discovered runtime directory, where the
  file stem becomes a culture code;
- an intentional gap is not excluded with `--exclude key` or `--exclude culture:key`.

Exit `2` usually means a missing `--catalog` or `--source` root, a path that does not exist, or an
unknown default culture. Fix the invocation before reading the findings.

## AOT and trimming issues

Trim and Native AOT (Ahead-of-Time) are covered by [AOT and trimming](../aot-and-trimming.md). The common issues:

- **EF Core store in an AOT build.** It is excluded by design (`IsAotCompatible=false`). Use a
  non-EF database provider or do not trim that path.
- **Reflection-based resource discovery.** `LocalizedResourceDiscovery.FromAssembly` and
  `FromType` are annotated `RequiresUnreferencedCode`; trimmed consumers pass explicit
  `ResourceDeclaration` values to `ResourceSynchronizer`.
- **Validation attributes with reflected members.** The type-based constructors of the localized
  validation attributes propagate the BCL (Base Class Library)'s trim annotations. The integer/string constructors
  remain trim-safe.
- **Embedded resources.** Verify that the resource base name survived trimming and publishing;
  a missing base still surfaces as `MissingManifestResourceException` at the first lookup.

Run the smoke consumer's publish and execution path on the target runtime identifier. A build that
compiles is not an AOT verification; the native link and the smoke binary are the gate.

## Formatting and reload surprises

`IcuMessageFormatter` throws `FormatException` for malformed templates, unknown argument types,
missing selector arguments, type mismatches, a missing `other` branch, and duplicate selectors;
the message includes the pattern offset. Number and date constructs require numeric and date/time
values, not strings. The default formatter is more forgiving: an unknown placeholder stays literal,
and its `ToString()` conversions use the ambient culture, so pass preformatted strings or use the
ICU (International Components for Unicode) formatter when culture-correct numbers matter.

Hot reload exists only for JSON, XML, and PO, and only when `WatchForChanges` is enabled with a
positive `ReloadDebounce`. A failed reload keeps the last-good table and raises
`AcquisitionFailed`; `RefreshAsync` loads immediately and throws on failure. Provider options are
construction snapshots, so `services.Configure<JsonLocalizationOptions>(...)` does nothing for
`AddJsonLocalization`, and changing a path or connection string requires a restart. Calling
`AddLocalization()` and `AddLocalization(configure)` together throws `InvalidOperationException`.

## Where to go next

- [Deployment](01-deployment.md), [Security](02-security.md), and [Performance](03-performance.md).
- [AOT and trimming](../aot-and-trimming.md) for publish-time failures.
- [Cultures and fallback](../02-guides/03-cultures-and-fallback.md) for the fallback model.
