# TMS Connector Round Trip

The TMS connector moves catalogs between the repository and a translation management
system without forking the governed workflow. `Translation.Tms` is the vendor-neutral
contract (`ITmsConnector`: pull a translated snapshot, push translated catalogs with a
dry-run preview and per-key conflict reporting); `Translation.Tms.Crowdin` implements
it against the Crowdin REST API and `Translation.Tms.Weblate` against the Weblate REST
API (self-hosted or hosted). A connector is workflow tooling, not a message
provider: it never serves `IMessageLocalizer` and never writes catalog files or store
entries. Applying an approved push stays the caller's composition through
`IResourceAdminStore`.

## Mapping

| Catalog concept | Crowdin | Weblate | Notes |
|---|---|---|---|
| Catalog key | String `identifier` when present, else the source `text` | Unit `context` when present, else the first `source` form | Key-based projects carry explicit keys; otherwise the source text is the key. |
| Template | Translation `content` | First `target` form | `null` when the vendor has the key but no translation yet. |
| Translator note | String `context` | Unit `note` | Surfaced on pull; uploaded from the request notes on push. |
| Plural tables | Plural-form translation rows | Units with more than one source or target form | Outside the flat catalog subset; skipped on pull, `UnsupportedKey` on push. |

Plural-form content is the one documented boundary: a string whose translations carry
plural forms (Crowdin) or a unit with several source/target forms (Weblate) never
enters the flat pull map, and pushing a flat target for one reports
`UnsupportedKey`.

## The round trip

1. **Pull.** `PullAsync(culture)` returns the vendor snapshot: templates plus notes
   keyed by catalog key, with the vendor source language recorded on the result.
2. **Export to an interchange file.** Write the pulled (or current) catalog with the
   PO/XLIFF interchange codecs and hand the file to translators, exactly as in the
   [translator workflow](11-translator-workflow.md).
3. **Preview the push.** `PushAsync` with `DryRun: true` compares the proposed target
   against the live vendor snapshot and reports per key without uploading anything.
4. **Push.** The same request with `DryRun: false` uploads one translated file
   (`messages.{culture}.po` or `.xliff`) when at least one key changed. Empty
   proposals never clear vendor translations.
5. **Apply.** Import the approved translations through the governed store
   (`IResourceAdminStore`), the same write path the interchange imports use.
6. **Gate.** Run the [parity check](13-parity-and-quality-gates.md) before the pull
   (clean baseline) and after the apply (no new missing keys, no placeholder drift).

## Registration

```csharp
services.AddCrowdinTmsConnector(options => {
    options.ApiToken = "<Crowdin personal access token>";
    options.ProjectId = 123456;
    options.FileId = 789;
});
```

`ProjectId` selects the Crowdin project; `FileId` is the project file the translated
uploads attach to (pull never needs it, a live push requires it). Enterprise
organizations set their own `Endpoint`. The options object is a construction snapshot;
mutating it after registration changes nothing.

```csharp
services.AddWeblateTmsConnector(options => {
    options.ApiToken = "<Weblate API token>";
    options.Endpoint = "https://hosted.weblate.org";
    options.Project = "my-project";
    options.Component = "messages";
});
```

`Endpoint` is the Weblate server origin (the hosted service or a self-hosted
instance — there is no fixed cloud host); `Project` and `Component` are the vendor
slugs that select the translation set. The same construction-snapshot rule applies.

## Pull example

```csharp
ITmsConnector connector = /* from DI */;

TmsPullResult pulled = await connector.PullAsync(
    CultureInfo.GetCultureInfo("ar"), cancellationToken);

// Templates keyed by catalog key; null means carried but untranslated.
string? template = pulled.Templates["App.Title"];
string? note = pulled.Notes.GetValueOrDefault("App.Title");
```

## Push example

```csharp
TmsPushRequest request = new() {
    Culture = CultureInfo.GetCultureInfo("ar"),
    Source = sourceTemplates,      // governance: unknown-key and stale-source detection
    Target = translatedTemplates,  // proposed translations
    Notes = translatorNotes,       // carried in the uploaded file
    Format = TmsCatalogFormat.Po,
    DryRun = true,                 // preview first
};

TmsPushReport preview = await connector.PushAsync(request, cancellationToken);
foreach (TmsPushEntry entry in preview.Entries) {
    Console.WriteLine($"{entry.Key}: {entry.Status} — {entry.Detail}");
}

// The same request with DryRun = false uploads messages.ar.po when keys changed.
```

## Conflict outcomes

The push comparison reuses the interchange `CatalogKeyStatus` vocabulary, so TMS
conflicts read exactly like import reports:

| Status | Meaning |
|---|---|
| `Skipped` | The proposed value is empty; existing vendor translations are never cleared. |
| `UnknownKey` | The key is not in the vendor project. |
| `StaleSource` | The vendor source text differs from the request source; the proposal was made against old content. |
| `Unchanged` | The vendor translation already matches. |
| `UnsupportedKey` | A plural-form string, or (XLIFF uploads) a key whose source template is missing from the request. |
| `Updated` | The translation was (or, on dry-run, would be) uploaded. |

`ConflictCount` counts `UnknownKey` plus `StaleSource`. `UploadedFileName` is set only
when a live push uploaded a file.

## Notes round trip

Translator notes ride both directions where the format carries them: the vendor string
`context` on pull, and PO `#.` comments (or XLIFF translator notes) in the uploaded
file on push. Notes never gate application and are never written to catalog files by
the connector; apply them through `IResourceAdminStore.SetNoteAsync` like any
interchange import.

## Operations

- **Auth.** The personal access token travels in the `Authorization` header
  (`Bearer` for Crowdin, `Token` for Weblate).
  It is never logged and never appears in exception messages. Treat it like any
  machine-translation key: environment-scoped secrets, no request logging that
  captures headers.
- **Calls.** One attempt per HTTP exchange with a per-operation `Timeout` budget
  (default 30 seconds); a breach surfaces as `OperationCanceledException`. Vendor
  failures surface as `TmsConnectorException` with the status code and a body
  snippet. Pagination follows the vendor cursor (Crowdin: 500 rows per page;
  Weblate: `next` page links, 1000 rows per page). Weblate uploads run with
  `method=translate` so only the compared keys change.
- **Pre/post checks.** The connector does not run parity itself. Run the parity tool
  before the pull and after the apply; see [Parity and quality gates](13-parity-and-quality-gates.md).

## Pitfalls

**Pitfall:** pushing without a dry run first. The dry run is the review surface for
stale sources and unknown keys; a live push with a stale source uploads a translation
of old content.

**Pitfall:** pointing Weblate at the wrong server or component. `Endpoint` is the
server origin, not a full API path, and the component must accept the pushed
format (a PO upload to an XLIFF-only component fails server-side with a
`TmsConnectorException`, not a per-key report).

**Pitfall:** expecting plural tables to round trip. They do not: plural-form strings
are skipped on pull and rejected on push. Translate plurals through the interchange
files, where the tables are explicit.

**Pitfall:** treating the connector as a localizer source. Registering it as an
`IInnerMessageProvider` is a category error; the single-provider rule is unchanged,
and the connector package does not implement the provider interface.

## Where to go next

- [Translator workflow](11-translator-workflow.md) for the file-based translation loop.
- [Interchange formats](../03-reference/04-interchange-formats.md) for the PO/XLIFF
  subsets the uploads are built from.
- [Parity and quality gates](13-parity-and-quality-gates.md) for the CI enforcement
  around the round trip.
- [Machine translation](../02-guides/12-machine-translation.md) for the runtime
  (request-time) translation alternative.
