//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// SARIF 2.1.0 log model for the parity report. Property declaration order is the
/// JSON key order: golden tests depend on it. Only the subset the tool emits is
/// modeled: one run, one driver, one rule per finding kind, one result per finding.
/// </summary>
internal sealed class SarifLog {
    /// <summary>
    /// Gets the SARIF version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.1.0";

    /// <summary>
    /// Gets the SARIF schema URI.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://json.schemastore.org/sarif-2.1.0.json";

    /// <summary>
    /// Gets the runs (exactly one).
    /// </summary>
    [JsonPropertyName("runs")]
    public List<SarifRun> Runs { get; set; } = [];
}

/// <summary>
/// One SARIF run.
/// </summary>
internal sealed class SarifRun {
    /// <summary>
    /// Gets the tool description.
    /// </summary>
    [JsonPropertyName("tool")]
    public SarifTool Tool { get; set; } = new();

    /// <summary>
    /// Gets the results in deterministic (kind, key, culture, path, line) order.
    /// </summary>
    [JsonPropertyName("results")]
    public List<SarifResult> Results { get; set; } = [];
}

/// <summary>
/// The SARIF tool description.
/// </summary>
internal sealed class SarifTool {
    /// <summary>
    /// Gets the driver description.
    /// </summary>
    [JsonPropertyName("driver")]
    public SarifDriver Driver { get; set; } = new();
}

/// <summary>
/// The SARIF driver description.
/// </summary>
internal sealed class SarifDriver {
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "jeninnet-localization-keys";

    /// <summary>
    /// Gets the tool assembly version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets the tool repository.
    /// </summary>
    [JsonPropertyName("informationUri")]
    public string InformationUri { get; set; } = "https://github.com/TarekNajem04/Jeninnet.Localization";

    /// <summary>
    /// Gets one rule per finding kind, in ordinal kind order.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<SarifRule> Rules { get; set; } = [];
}

/// <summary>
/// One SARIF rule: a stable finding kind.
/// </summary>
internal sealed class SarifRule {
    /// <summary>
    /// Gets the rule id (the finding kind).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets the human-readable rule name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the rule description.
    /// </summary>
    [JsonPropertyName("shortDescription")]
    public SarifMessage ShortDescription { get; set; } = new();

    /// <summary>
    /// Gets the default severity for results of this rule.
    /// </summary>
    [JsonPropertyName("defaultConfiguration")]
    public SarifRuleConfiguration DefaultConfiguration { get; set; } = new();
}

/// <summary>
/// The SARIF default rule configuration.
/// </summary>
internal sealed class SarifRuleConfiguration {
    /// <summary>
    /// Gets the default level (<c>error</c> or <c>warning</c>; see the level map).
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "warning";
}

/// <summary>
/// One SARIF result.
/// </summary>
internal sealed class SarifResult {
    /// <summary>
    /// Gets the rule id (the finding kind).
    /// </summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the severity (<c>error</c> or <c>warning</c>; see the level map).
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "warning";

    /// <summary>
    /// Gets the human-readable detail.
    /// </summary>
    [JsonPropertyName("message")]
    public SarifMessage Message { get; set; } = new();

    /// <summary>
    /// Gets the locations in deterministic order.
    /// </summary>
    [JsonPropertyName("locations")]
    public List<SarifLocation> Locations { get; set; } = [];

    /// <summary>
    /// Gets the stable fingerprints (one entry: the parity identity hash).
    /// </summary>
    [JsonPropertyName("partialFingerprints")]
    public Dictionary<string, string> PartialFingerprints { get; set; } = [];
}

/// <summary>
/// One SARIF result location.
/// </summary>
internal sealed class SarifLocation {
    /// <summary>
    /// Gets the physical location.
    /// </summary>
    [JsonPropertyName("physicalLocation")]
    public SarifPhysicalLocation PhysicalLocation { get; set; } = new();
}

/// <summary>
/// One SARIF physical location.
/// </summary>
internal sealed class SarifPhysicalLocation {
    /// <summary>
    /// Gets the artifact location.
    /// </summary>
    [JsonPropertyName("artifactLocation")]
    public SarifArtifactLocation ArtifactLocation { get; set; } = new();

    /// <summary>
    /// Gets the region, or <see langword="null"/> when the location has no line.
    /// </summary>
    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SarifRegion? Region { get; set; }
}

/// <summary>
/// One SARIF artifact location.
/// </summary>
internal sealed class SarifArtifactLocation {
    /// <summary>
    /// Gets the URI: the repo-relative path with forward slashes, or the absolute path.
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

/// <summary>
/// One SARIF region.
/// </summary>
internal sealed class SarifRegion {
    /// <summary>
    /// Gets the 1-based start line.
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; } = 1;

    /// <summary>
    /// Gets the 1-based start column.
    /// </summary>
    [JsonPropertyName("startColumn")]
    public int StartColumn { get; set; } = 1;
}

/// <summary>
/// One SARIF message.
/// </summary>
internal sealed class SarifMessage {
    /// <summary>
    /// Gets the text.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Renders the parity report as SARIF 2.1.0. Rule ids are the stable finding
/// kinds; levels map catalog errors, missing keys, unresolved references, and
/// definition conflicts to <c>error</c> and every other kind to <c>warning</c>.
/// Fingerprints hash the (kind, key, culture, path, line) identity so code
/// scanning can track a finding across runs.
/// </summary>
internal static class SarifWriter {
    /// <summary>The SARIF level for catalog errors, missing keys, unresolved references, and definition conflicts.</summary>
    private const string SarifErrorLevel = "error";

    /// <summary>
    /// Serializes the SARIF log for a parity report through the source-generated contract.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <returns>The SARIF text.</returns>
    public static string ToSarif(ParityReport report) {
        ArgumentNullException.ThrowIfNull(report);

        var log = new SarifLog();
        var run = new SarifRun();
        log.Runs.Add(run);
        run.Tool.Driver.Version = report.ToolVersion;
        run.Tool.Driver.Rules.AddRange(RuleDescriptions().Select(static pair => new SarifRule {
            Id = pair.Kind,
            Name = pair.Name,
            ShortDescription = new SarifMessage { Text = pair.Description },
            DefaultConfiguration = new SarifRuleConfiguration { Level = LevelFor(pair.Kind) },
        }));

        foreach(var finding in report.Findings) {
            var result = new SarifResult {
                RuleId = finding.Kind,
                Level = LevelFor(finding.Kind),
                Message = new SarifMessage { Text = finding.Message },
            };
            result.PartialFingerprints["parity/v1"] = Fingerprint(finding);
            foreach(var location in finding.Locations) {
                result.Locations.Add(new SarifLocation {
                    PhysicalLocation = new SarifPhysicalLocation {
                        ArtifactLocation = new SarifArtifactLocation { Uri = location.Path },
                        Region = new SarifRegion { StartLine = location.Line, StartColumn = location.Column },
                    },
                });
            }

            run.Results.Add(result);
        }

        return JsonSerializer.Serialize(log, ParityReportJsonContext.Default.SarifLog);
    }

    /// <summary>
    /// Writes the SARIF log file, creating its directory when needed.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <param name="path">The SARIF path.</param>
    public static void WriteSarifFile(ParityReport report, string path) {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if(directory is not null) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToSarif(report) + Environment.NewLine);
    }

    /// <summary>
    /// Maps a finding kind to its SARIF level.
    /// </summary>
    /// <param name="kind">The finding kind.</param>
    /// <returns><c>error</c> or <c>warning</c>.</returns>
    internal static string LevelFor(string kind) => kind switch {
        FindingKinds.CATALOG_ERROR => SarifErrorLevel,
        FindingKinds.MISSING_KEY => SarifErrorLevel,
        FindingKinds.UNRESOLVED_REFERENCE => SarifErrorLevel,
        FindingKinds.DEFINITION_CONFLICT => SarifErrorLevel,
        _ => "warning",
    };

    /// <summary>
    /// Computes the stable fingerprint for a finding identity.
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <returns>The lowercase hex SHA-256 of kind, key, culture, path, and line.</returns>
    private static string Fingerprint(ParityFinding finding) {
        var first = finding.Locations.FirstOrDefault();
        var identity = string.Join(
            "\n",
            finding.Kind,
            finding.Key,
            finding.Culture,
            first?.Path ?? string.Empty,
            (first?.Line ?? 0).ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    /// <summary>
    /// Describes the rule.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Name">The name.</param>
    /// <param name="Description">The description.</param>
    private sealed record RuleDescription(string Kind, string Name, string Description);

    /// <summary>
    /// Describes the rule.
    /// </summary>
    /// <returns>The rule descriptions.</returns>
    private static IReadOnlyList<RuleDescription> RuleDescriptions() => [
        new(FindingKinds.CATALOG_ERROR, "Catalog error", "A catalog file that could not be read or parsed."),
        new(FindingKinds.DEFINITION_CONFLICT, "Definition conflict", "One constant name defined with differing values."),
        new(FindingKinds.FORMAT_GAP, "Format gap", "A key served for a culture but absent from one format's catalog."),
        new(FindingKinds.MISSING_KEY, "Missing key", "A source-referenced key not served for a culture (nor its fallback chain)."),
        new(FindingKinds.PLACEHOLDER_DRIFT, "Placeholder drift", "A (key, culture) whose placeholder identifier set differs from the reference culture."),
        new(FindingKinds.TEMPLATE_DRIFT, "Template drift", "A (culture, key) served with differing templates across formats."),
        new(FindingKinds.UNRESOLVED_REFERENCE, "Unresolved reference", "A *Keys.Name reference with no constant definition."),
        new(FindingKinds.UNUSED_KEY, "Unused key", "A catalog key never referenced in source."),
    ];
}
