//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Renders the parity report: a deterministic console table for humans and the
/// versioned JSON report for machines. Both orderings are ordinal and stable, so
/// golden tests can assert them byte for byte.
/// </summary>
internal static class ReportWriter {
    /// <summary>
    /// Writes the console table.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <param name="output">The output writer.</param>
    public static void WriteConsole(ParityReport report, TextWriter output) {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine($"jeninnet-localization-keys {report.ToolVersion}");
        output.WriteLine($"catalogs: {report.Catalogs.Count} file(s), {report.Catalogs.Select(static catalog => catalog.Culture).Distinct(StringComparer.Ordinal).Count()} culture(s), {report.Catalogs.Select(static catalog => catalog.Format).Distinct(StringComparer.Ordinal).Count()} format(s)");
        var references = report.Sources.Sum(static source => source.References);
        output.WriteLine($"sources: {report.Sources.Count} file(s), {references} reference(s), {report.Matrix.Count} distinct key(s)");
        output.WriteLine($"definitions: {report.KeyDefinitions.Count} constant(s)");
        output.WriteLine($"excluded: {report.ExcludedKeys.Count} key(s)");
        output.WriteLine($"findings: {report.Summary.Total}");
        foreach(var finding in report.Findings) {
            var subject = finding.Key.Length > 0 ? finding.Key : finding.Locations.FirstOrDefault()?.Path ?? string.Empty;
            var culture = finding.Culture.Length > 0 ? finding.Culture : "*";
            var first = finding.Locations.FirstOrDefault();
            var location = first is null ? "-" : $"{first.Path}:{first.Line}";
            var more = finding.Locations.Count > 1 ? $" (+{finding.Locations.Count - 1} more)" : string.Empty;
            output.WriteLine($"  [{finding.Kind}] {subject} [{culture}] {location}{more} - {finding.Message}");
        }

        output.WriteLine($"summary: missing-key={report.Summary.MissingKeys}, unused-key={report.Summary.UnusedKeys}, template-drift={report.Summary.TemplateDrifts}, format-gap={report.Summary.FormatGaps}, placeholder-drift={report.Summary.PlaceholderDrifts}, unresolved-reference={report.Summary.UnresolvedReferences}, definition-conflict={report.Summary.DefinitionConflicts}, catalog-error={report.Summary.CatalogErrors}");
        output.WriteLine(report.Summary.Total == 0
            ? $"result: PASS (exit {ExitCodes.CLEAN})"
            : $"result: FAIL (exit {ExitCodes.FINDINGS})");
    }

    /// <summary>
    /// Serializes the versioned JSON report through the source-generated contract.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <returns>The JSON text.</returns>
    public static string ToJson(ParityReport report) {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, ParityReportJsonContext.Default.ParityReport);
    }

    /// <summary>
    /// Writes the JSON report file, creating its directory when needed.
    /// </summary>
    /// <param name="report">The parity report.</param>
    /// <param name="path">The report path.</param>
    public static void WriteJsonFile(ParityReport report, string path) {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if(directory is not null) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson(report) + Environment.NewLine);
    }
}
