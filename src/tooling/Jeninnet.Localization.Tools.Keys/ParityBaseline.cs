//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// One committed baseline finding: the identity a current finding is compared against.
/// Identity is (kind, key, culture, path-or-empty, line-or-zero): paths and lines are
/// recorded for readability but only (kind, key, culture) participate in matching,
/// with the file path as a tiebreaker for key-less file findings.
/// </summary>
internal sealed class BaselineFinding {
    /// <summary>
    /// Gets the finding kind.
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
    /// Gets the first location path, or empty.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets the first location line, or 0.
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }
}

/// <summary>
/// A committed parity baseline: the findings a clean gate run produced. A later run
/// fails only on findings absent from the baseline; findings fixed since the baseline
/// was recorded are reported informationally and never fail the run.
/// </summary>
internal sealed class ParityBaseline {
    /// <summary>
    /// Gets the baseline format version (1).
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets the recorded findings in deterministic order.
    /// </summary>
    [JsonPropertyName("findings")]
    public List<BaselineFinding> Findings { get; set; } = [];
}

/// <summary>
/// Compares a current parity report against a committed baseline. Findings are
/// matched by (kind, key, culture), with the file path as a tiebreaker for
/// key-less file findings.
/// </summary>
internal static class BaselineComparer {
    /// <summary>
    /// Loads a committed baseline file.
    /// </summary>
    /// <param name="path">The baseline path.</param>
    /// <returns>The baseline.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be read or parsed, or the version is unsupported.</exception>
    public static ParityBaseline Load(string path) {
        ArgumentNullException.ThrowIfNull(path);

        ParityBaseline? baseline;
        try {
            baseline = JsonSerializer.Deserialize(File.ReadAllText(path), ParityReportJsonContext.Default.ParityBaseline);
        }
        catch(Exception exception) when(exception is IOException or JsonException or UnauthorizedAccessException) {
            throw new IOException($"Baseline file '{path}' cannot be read or parsed: {exception.Message}", exception);
        }

        if(baseline is null || baseline.Version != 1) {
            throw new IOException($"Baseline file '{path}' has an unsupported version.");
        }

        return baseline;
    }

    /// <summary>
    /// Writes the current report findings as a new baseline file, creating its
    /// directory when needed.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <param name="path">The baseline path.</param>
    public static void Write(ParityReport report, string path) {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(path);

        var baseline = new ParityBaseline();
        foreach(var finding in report.Findings) {
            var first = finding.Locations.FirstOrDefault();
            baseline.Findings.Add(new BaselineFinding {
                Kind = finding.Kind,
                Key = finding.Key,
                Culture = finding.Culture,
                Path = first?.Path ?? string.Empty,
                Line = first?.Line ?? 0,
            });
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if(directory is not null) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(baseline, ParityReportJsonContext.Default.ParityBaseline) + Environment.NewLine);
    }

    /// <summary>
    /// Compares the current findings against the baseline.
    /// </summary>
    /// <param name="report">The current parity report.</param>
    /// <param name="baseline">The committed baseline.</param>
    /// <returns>The new findings (fail the gate) and the fixed findings (informational).</returns>
    public static (IReadOnlyList<ParityFinding> New, IReadOnlyList<BaselineFinding> Fixed) Compare(ParityReport report, ParityBaseline baseline) {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(baseline);

        var committed = new HashSet<string>(StringComparer.Ordinal);
        foreach(var finding in baseline.Findings) {
            committed.Add(MatchKey(finding.Kind, finding.Key, finding.Culture, finding.Path));
        }

        var current = new HashSet<string>(StringComparer.Ordinal);
        var fresh = new List<ParityFinding>();
        foreach(var finding in report.Findings) {
            var key = MatchKey(finding.Kind, finding.Key, finding.Culture, finding.Locations.FirstOrDefault()?.Path ?? string.Empty);
            current.Add(key);
            if(!committed.Contains(key)) {
                fresh.Add(finding);
            }
        }

        var fixedSince = baseline.Findings
            .Where(finding => !current.Contains(MatchKey(finding.Kind, finding.Key, finding.Culture, finding.Path)))
            .ToList();

        return (fresh, fixedSince);
    }

    /// <summary>
    /// Builds the match key for a finding identity.
    /// </summary>
    /// <param name="kind">The finding kind.</param>
    /// <param name="key">The localization key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="path">The first location path.</param>
    /// <returns>The match key.</returns>
    private static string MatchKey(string kind, string key, string culture, string path) =>
        key.Length == 0 ? string.Join("\0", kind, path) : string.Join("\0", kind, key, culture);
}
