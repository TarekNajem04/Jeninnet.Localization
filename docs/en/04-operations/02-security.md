# Security

Localization sits on a trust boundary that is easy to miss. Translator credentials, catalog files,
interchange uploads, and the administration store are all inputs to a production system, and some
of them are supplied by people or services outside the deployment team. This chapter describes the
threat surface as the code actually implements it, and the controls that belong to the host
application rather than to the library.

## Trust boundaries

Four inputs deserve separate treatment:

1. **Catalog content** — templates, keys, and translator notes. File catalogs are usually trusted
   deployment artifacts; a database catalog or an administration API may accept content from
   non-developers.
2. **Interchange files** — PO (Portable Object), XLIFF (XML Localization Interchange File Format), CSV, and ResX documents produced by translators or vendors.
   These are untrusted inputs even when the sender is trusted.
3. **Translator credentials and endpoints** — API keys and base URLs for Azure Translator or
   Google Translate.
4. **The administration store** — the governed write path, its audit trail, and the database
   account that backs it.

The library provides parsing, validation, and capability checks. It does not authenticate users,
authorize endpoints, encrypt storage, or decide whether a translation vendor is trustworthy. Those
are host responsibilities, and they must be designed explicitly.

## Translator credentials

`AzureTranslationOptions.ApiKey` is sent in the `Ocp-Apim-Subscription-Key` header through
`HttpClient`; the optional region is sent in `Ocp-Apim-Subscription-Region`. Google's
`GoogleTranslationOptions.ApiKey` is sent as the documented `key` query parameter on the v2
endpoint. That difference matters operationally: query strings are more likely to appear in proxy
logs, gateway access logs, and crash telemetry. Do not enable request logging that captures full
URLs for the Google provider.

Both options validate the endpoint as an absolute `http` or `https` URI. The provider does not pin
a host, so configuration is part of the security boundary. Pin the endpoint to the expected
service in production, reject overrides from untrusted configuration sources, and require HTTPS.
An attacker who can rewrite configuration can redirect the API key to a host they control.

Treat keys as environment-scoped secrets:

- Use .NET User Secrets during local development; never commit keys to `appsettings.json`.
- Use the platform secret store or protected environment variables in production.
- Give each environment its own key and rotate on a schedule.
- Restrict outbound network access to the translation endpoint.
- Do not log the options object or dump configuration at startup.

The translation providers send the lookup key as the source text. Never encode personal data,
access tokens, or other secrets into message keys: the key is transmitted to the remote service
and may be retained by it. If the content is sensitive, translate catalogs offline and serve the
result from a file or database provider.

## Untrusted interchange files

All four interchange packages follow a validate-then-apply model: the whole document is parsed and
validated before anything is computed, and the file-level import rewrites the target catalog at
most once. A dry run changes nothing. That protects the target from partial writes, but it does
not make an untrusted document safe to process without resource limits.

### XML-based formats

The XML provider uses `XDocument.Load`, and the XLIFF and ResX codecs use `XDocument.Parse`,
without custom settings
([XML provider](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Xml/XmlMessageProvider.cs),
[XLIFF parser](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.Xliff/XliffDocumentParser.cs),
[ResX codec](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/interchange/Jeninnet.Localization.Interchange.ResX/ResXTextCodec.cs)).
No `XmlResolver` is supplied, so external entity resolution is not enabled: a declared external
entity does not read a file or URL. That blocks classic XXE disclosure, but DTDs are not rejected:
LINQ to XML parses internal DTD declarations and expands internal entities within the runtime's
entity-expansion limit. If your policy forbids DTDs, reject documents containing `<!DOCTYPE>`
before parsing or use a reader configured with `DtdProcessing.Prohibit`; the file-level APIs do
not expose reader settings.

No explicit size, character-count, or depth limit is configured. A large but well-formed document
is read into memory, and internal entity expansion can consume CPU before the runtime limit is
reached. Enforce a byte-size limit before calling the parser, reject unexpected DTDs, and run
imports with a time budget in a worker that can be terminated.

### ResX without type activation

The ResX interchange codec is hand-rolled and reads only `data` entries with a `name`, `value`, and
`comment`. It ignores `assembly`/`metadata` root elements and marks typed entries as
`UnsupportedKey` rather than instantiating them. This avoids the type-activation surface of a
general-purpose resource reader. Keep it that way: do not route untrusted ResX through a different
reader that deserializes arbitrary types.

### JSON, CSV, and PO

The JSON catalog codec reads the whole file with `File.ReadAllBytes` and parses it with
`JsonDocument`; the default maximum depth is 64. CSV and PO readers are hand-rolled and also read
the whole document as text. PO and MO (Machine Object) parsing validate structure and bounds: the MO reader checks
table offsets and string lengths, and decodes strict UTF-8 (Unicode Transformation Format, 8-bit) with invalid bytes rejected rather than
replaced ([MO parser](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Po/MoParser.cs)).

These are correctness and memory-safety measures, not size limits. A multi-gigabyte CSV or PO file
will still consume memory proportional to its size. Enforce upload size, row count, and processing
time at the ingestion boundary.

### Archives and paths

The library does not unpack ZIP or other archives. A "zip bomb" is therefore not a parser feature;
it is a risk in whatever workflow accepts a zipped bundle of XLIFF or PO files. If your intake
accepts archives, apply decompression limits (entry count, uncompressed bytes, compression ratio)
before extracting, and never trust an entry name as a filesystem path.

The file-level interchange APIs take caller-supplied paths and read or write exactly those paths.
They do not derive a path from an uploaded filename. If an HTTP endpoint accepts an upload, the
host must generate a safe temporary name, keep the file inside a dedicated directory, and never
pass a user-controlled path directly to `ImportCatalogFiles` or `ExportCatalogFiles`.

### Applying untrusted content

Use `DryRun` first and inspect the report. Interchange never invents keys: unknown keys are
reported as `UnknownKey`, unsupported entries as `UnsupportedKey`, and stale sources as
`StaleSource` where the format can represent them. Apply only after validation, and apply through
the governed store path when the target is a resource set. ResX, CSV, and PO file imports write
catalog files; they do not authenticate the translator or approve the content.

## Store administration authorization

The library has no authentication or authorization layer. `IResourceAdminStore` is a capability
contract: `SupportsAdministration` says whether the store can mutate, and `RequireAdmin()` throws
`StoreReadOnlyException` when it cannot. That is not an access-control decision. The host must
authorize the caller before invoking any admin method.

Practical rules:

- Do not expose the admin store through a public endpoint. Put it behind the application's
  authentication, role checks, and CSRF protections.
- Treat the `Actor` value in `ResourceAdminOptions` as an audit label, not as proof of identity.
  Populate it from the authenticated principal, never from a request body.
- Use a read-only database account for message lookups and a separate write-capable account only
  for the administration process.
- Keep the administration surface on the primary database; do not point it at a read replica.
- Log authorization decisions and admin mutations, but never log template values from the audit
  event unless the log store is approved for catalog content.

SQL injection is addressed at the provider boundary. Database message queries parameterize
`MessageKey` and `Culture`; identifiers are escaped per vendor and the table name is validated by
`DatabaseLocalizationOptions.TableName`
([database base](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/src/providers/Jeninnet.Localization.Providers.Database.Abstractions/DatabaseMessageProviderBase.cs)).
That does not protect a custom store implementation or a hand-written admin endpoint.

## Audit integrity and confidentiality

The persisted audit trail is a hash chain: each `LocalizationAuditRecord` carries `PreviousHash`
and `ChainHash`, and `VerifyHistoryAsync` recomputes the chain. Any modification, insertion, or
removal breaks it unless every later record is rewritten. The chain is **unanchored**: an attacker
who can rewrite the entire trail can recompute every hash. Treat verification as tamper evidence
under normal operational controls, not as proof against a privileged database administrator. If
the threat model requires stronger evidence, export chain heads or periodic verification results
to an external, append-only store.

Template text is not stored in audit records. `OldValueHash` and `NewValueHash` are lowercase
hexadecimal SHA-256 (Secure Hash Algorithm, 256-bit) hashes of the UTF-8 template bytes, unsalted by design so equality and change
detection work across records. Unsalted hashes of short, predictable strings can be guessed
offline; they are not a confidentiality mechanism.

The replay journal is different. `StringVersions` stores the actual template values because
as-of-version reads must reproduce the catalog. Protect that table with the same confidentiality
class as the live `LocalizedStrings` table, including backups and read access. The audit trail is
redacted; the version journal is not.

The live `Audited` event is synchronous and in-process. A handler that throws propagates an
exception after the mutation has already committed. Keep handlers fast, non-throwing, and free of
secrets; persist accountability through `IResourceHistoryStore`, not through a log line.

## Least privilege for database accounts

Separate the roles that read messages from the roles that administer them.

| Role | Needed access |
|---|---|
| Message provider | `SELECT` on `LocalizationMessages` (or the configured table). No writes, no DDL. |
| EF store reader | `SELECT` on `LocalizedStrings` and the supporting read tables used by the application. |
| Governed administrator | `SELECT`, `INSERT`, `UPDATE`, `DELETE` on the store tables, plus the version counter and audit/journal tables. |
| Migration identity | DDL rights used only during deployment, not by the running application. |

Grant schema rights narrowly, avoid `db_owner` for the runtime account, require TLS on the
connection, and keep connection strings in the secret store. If the database supports row-level
security or separate schemas per tenant, use them; the library does not partition tenants for you.
Backups inherit the confidentiality of the catalog and the replay journal, so encrypt them and
restrict restore access.

## Dependency and vulnerability response

Restore-time auditing is enabled for every project: `NuGetAudit` is on, `NuGetAuditMode` is `all`
(direct and transitive packages), and the audit level is `low`. `NU1901`–`NU1904` are treated as
errors, so a vulnerable dependency blocks restore and CI instead of producing a warning that can be
ignored.

Dependabot and maintainer review cover package updates. The response process is:

1. Acknowledge a private vulnerability report within two business days.
2. Triage the affected package, version, and supported target frameworks; publish a mitigated
   version or documented workaround for critical/high findings within seven days.
3. Fix moderate findings within 30 days; low findings are handled in the next scheduled update.
4. Re-run restore, package validation, consumer validation, and the full test matrix before
   publishing the fix.
5. Document any temporary exception with the advisory identifier, affected packages, compensating
   control, and expiration date. Do not suppress an advisory without an expiration.

Report suspected vulnerabilities privately through GitHub Security Advisories, not a public issue.
The canonical repository and reporting links are maintained in the repository README and the
[SECURITY.md](https://github.com/TarekNajem04/Jeninnet.Localization/blob/main/.github/SECURITY.md)
policy.
## Recommendation checklist

- [ ] Translation keys are stored in a secret store, scoped per environment, and rotated.
- [ ] Translation endpoints are pinned to the expected host and HTTPS is required.
- [ ] Message keys never contain personal data or secrets sent to a translation service.
- [ ] Interchange intake enforces file size, row count, and processing-time limits.
- [ ] Archive intake (if any) enforces entry count, uncompressed size, and compression ratio
  limits.
- [ ] Uploaded files are written to a generated path inside a dedicated directory.
- [ ] Interchange imports run with `DryRun` first and apply only validated content.
- [ ] Admin endpoints require authentication and authorization; `Actor` comes from the principal.
- [ ] Runtime database accounts are read-only; migration rights are separate.
- [ ] The audit chain is verified on a schedule and verification evidence is retained externally.
- [ ] Access to `StringVersions`, `LocalizedStrings`, and backups is restricted as catalog data.
- [ ] Logs do not contain templates, keys with embedded personal data, or API keys.

## Pitfalls

**Pitfall:** treating `SupportsAdministration` as authorization. It only says the store can mutate.

**Pitfall:** putting the Google API key in a URL and enabling full-URL request logging. Query
strings are commonly captured.

**Pitfall:** accepting a vendor XLIFF file without a size limit because "the parser validates the
schema". Validation is not resource control.

**Pitfall:** assuming the hash chain proves authenticity against a database administrator. It is
unanchored tamper evidence.

## Where to go next

- [Deployment](01-deployment.md) for schema, secrets, and release sequencing.
- [Audit and history](../02-guides/07-audit-and-history.md) for the event and history contracts.
- [Resource sets and the store](../02-guides/06-resource-sets-and-store.md) for the governed path.
- [Troubleshooting](04-troubleshooting.md) for authentication and rate-limit symptoms.
