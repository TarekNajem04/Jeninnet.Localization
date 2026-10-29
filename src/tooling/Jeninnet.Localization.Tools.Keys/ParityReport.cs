//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Stable finding-kind identifiers. These strings are part of the versioned
/// JSON report schema and the console output: never rename, only add.
/// </summary>
internal static class FindingKinds {
    /// <summary>A source-referenced key not served for a culture (nor its fallback chain).</summary>
    public const string MISSING_KEY = "missing-key";

    /// <summary>A catalog key never referenced in source.</summary>
    public const string UNUSED_KEY = "unused-key";

    /// <summary>One (culture, key) served with differing templates across formats.</summary>
    public const string TEMPLATE_DRIFT = "template-drift";

    /// <summary>A key served for a culture but absent from one format's catalog.</summary>
    public const string FORMAT_GAP = "format-gap";

    /// <summary>One (key, culture) whose placeholder identifier set differs from the reference culture.</summary>
    public const string PLACEHOLDER_DRIFT = "placeholder-drift";

    /// <summary>A <c>*Keys.Name</c> reference with no constant definition.</summary>
    public const string UNRESOLVED_REFERENCE = "unresolved-reference";

    /// <summary>One constant name defined with differing values.</summary>
    public const string DEFINITION_CONFLICT = "definition-conflict";

    /// <summary>A catalog file that could not be read or parsed.</summary>
    public const string CATALOG_ERROR = "catalog-error";
}

/// <summary>
/// How a referenced key resolves for one culture: served locally, served through
/// the fallback chain, or missing everywhere in the chain.
/// </summary>
internal static class MatrixStates {
    /// <summary>Served by the culture's own catalogs.</summary>
    public const string SERVED = "served";

    /// <summary>Missing locally but served by a parent culture or the default culture.</summary>
    public const string FALLBACK = "fallback";

    /// <summary>Missing in the whole fallback chain.</summary>
    public const string MISSING = "missing";
}

/// <summary>
/// One file/line location in a finding.
/// </summary>
internal sealed class FindingLocation {
    /// <summary>
    /// Gets the path relative to the working directory (forward slashes), or an
    /// absolute path when outside of it.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; } = 1;

    /// <summary>
    /// Gets the 1-based column number.
    /// </summary>
    [JsonPropertyName("column")]
    public int Column { get; set; } = 1;
}

/// <summary>
/// One parity finding: a stable kind, an optional key and culture, a human message,
/// and deterministic file/line locations.
/// </summary>
internal sealed class ParityFinding {
    /// <summary>
    /// Gets the finding kind (see <see cref="FindingKinds"/>).
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets the localization key, or empty for file-level findings.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the culture, or empty for key-global findings.
    /// </summary>
    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the human-readable detail.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the locations in deterministic order.
    /// </summary>
    [JsonPropertyName("locations")]
    public List<FindingLocation> Locations { get; set; } = [];
}

/// <summary>
/// One scanned catalog file as reported.
/// </summary>
internal sealed class CatalogSummary {
    /// <summary>
    /// Gets the path relative to the working directory (forward slashes).
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the format (<c>json</c>, <c>xml</c>, <c>resx</c>, <c>po</c>).
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Gets the culture the file serves.
    /// </summary>
    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the number of served keys (0 when the file failed to parse).
    /// </summary>
    [JsonPropertyName("keys")]
    public int Keys { get; set; }

    /// <summary>
    /// Gets the parse failure, or <see langword="null"/> when the file parsed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

/// <summary>
/// One scanned source file as reported.
/// </summary>
internal sealed class SourceSummary {
    /// <summary>
    /// Gets the path relative to the working directory (forward slashes).
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the number of key references extracted.
    /// </summary>
    [JsonPropertyName("references")]
    public int References { get; set; }
}

/// <summary>
/// One resolved constant definition as reported.
/// </summary>
internal sealed class DefinitionSummary {
    /// <summary>
    /// Gets the constant name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the key value.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the defining file relative to the working directory (forward slashes).
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the 1-based defining line.
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; } = 1;
}

/// <summary>
/// One per-culture resolution cell of the completeness matrix.
/// </summary>
internal sealed class MatrixCell {
    /// <summary>
    /// Gets the culture.
    /// </summary>
    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the state (see <see cref="MatrixStates"/>).
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// One matrix row: a referenced key and its per-culture resolution.
/// </summary>
internal sealed class MatrixRow {
    /// <summary>
    /// Gets the referenced key.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets the per-culture cells in ordinal culture order.
    /// </summary>
    [JsonPropertyName("cultures")]
    public List<MatrixCell> Cultures { get; set; } = [];
}

/// <summary>
/// Finding counts by kind (fixed shape: every kind always present).
/// </summary>
internal sealed class ParitySummary {
    /// <summary>
    /// Gets the total number of findings.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Gets the number of <c>missing-key</c> findings.
    /// </summary>
    [JsonPropertyName("missingKeys")]
    public int MissingKeys { get; set; }

    /// <summary>
    /// Gets the number of <c>unused-key</c> findings.
    /// </summary>
    [JsonPropertyName("unusedKeys")]
    public int UnusedKeys { get; set; }

    /// <summary>
    /// Gets the number of <c>template-drift</c> findings.
    /// </summary>
    [JsonPropertyName("templateDrifts")]
    public int TemplateDrifts { get; set; }

    /// <summary>
    /// Gets the number of <c>format-gap</c> findings.
    /// </summary>
    [JsonPropertyName("formatGaps")]
    public int FormatGaps { get; set; }

    /// <summary>
    /// Gets the number of <c>placeholder-drift</c> findings.
    /// </summary>
    [JsonPropertyName("placeholderDrifts")]
    public int PlaceholderDrifts { get; set; }

    /// <summary>
    /// Gets the number of <c>unresolved-reference</c> findings.
    /// </summary>
    [JsonPropertyName("unresolvedReferences")]
    public int UnresolvedReferences { get; set; }

    /// <summary>
    /// Gets the number of <c>definition-conflict</c> findings.
    /// </summary>
    [JsonPropertyName("definitionConflicts")]
    public int DefinitionConflicts { get; set; }

    /// <summary>
    /// Gets the number of <c>catalog-error</c> findings.
    /// </summary>
    [JsonPropertyName("catalogErrors")]
    public int CatalogErrors { get; set; }
}

/// <summary>
/// The versioned parity report root (schema version 2, documented in README).
/// Property declaration order is the JSON key order: golden tests depend on it.
/// </summary>
internal sealed class ParityReport {
    /// <summary>
    /// Gets the report schema version (2).
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// Gets the tool name.
    /// </summary>
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "jeninnet-localization-keys";

    /// <summary>
    /// Gets the tool assembly version.
    /// </summary>
    [JsonPropertyName("toolVersion")]
    public string ToolVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets the default culture used as the final fallback stage.
    /// </summary>
    [JsonPropertyName("defaultCulture")]
    public string DefaultCulture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the scanned catalogs in ordinal path order.
    /// </summary>
    [JsonPropertyName("catalogs")]
    public List<CatalogSummary> Catalogs { get; set; } = [];

    /// <summary>
    /// Gets the catalog files skipped as non-catalogs in ordinal path order.
    /// </summary>
    [JsonPropertyName("ignoredFiles")]
    public List<string> IgnoredFiles { get; set; } = [];

    /// <summary>
    /// Gets the scanned sources in ordinal path order.
    /// </summary>
    [JsonPropertyName("sources")]
    public List<SourceSummary> Sources { get; set; } = [];

    /// <summary>
    /// Gets the resolved constant definitions in ordinal name order.
    /// </summary>
    [JsonPropertyName("keyDefinitions")]
    public List<DefinitionSummary> KeyDefinitions { get; set; } = [];

    /// <summary>
    /// Gets the applied exclusions in ordinal order.
    /// </summary>
    [JsonPropertyName("excludedKeys")]
    public List<string> ExcludedKeys { get; set; } = [];

    /// <summary>
    /// Gets the per-culture completeness matrix in ordinal key order.
    /// </summary>
    [JsonPropertyName("matrix")]
    public List<MatrixRow> Matrix { get; set; } = [];

    /// <summary>
    /// Gets the findings in deterministic (kind, key, culture, path, line) order.
    /// </summary>
    [JsonPropertyName("findings")]
    public List<ParityFinding> Findings { get; set; } = [];

    /// <summary>
    /// Gets the finding counts.
    /// </summary>
    [JsonPropertyName("summary")]
    public ParitySummary Summary { get; set; } = new();
}
