//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

if(!CliOptions.TryParse(args, out var options, out var error)) {
    await Console.Error.WriteLineAsync($"error: {error}");
    await Console.Error.WriteLineAsync(CliOptions.Usage);
    return ExitCodes.USAGE_ERROR;
}

if(options.ShowHelp) {
    Console.WriteLine(CliOptions.Usage);
    return ExitCodes.CLEAN;
}

if(options.ShowVersion) {
    Console.WriteLine(ToolVersion());
    return ExitCodes.CLEAN;
}

try {
    var extraction = KeyExtractor.Extract(options.SourceRoots, options.KeysFiles);
    var scan = CatalogReader.Read(options.CatalogRoots, options.DefaultCulture);
    var report = ParityAnalyzer.Analyze(scan, extraction, options, ToolVersion());
    ReportWriter.WriteConsole(report, Console.Out);
    if(options.ReportPath is not null) {
        ReportWriter.WriteJsonFile(report, options.ReportPath);
    }

    return report.Summary.Total == 0 ? ExitCodes.CLEAN : ExitCodes.FINDINGS;
}
catch(Exception exception) when(exception is IOException or UnauthorizedAccessException) {
    await Console.Error.WriteLineAsync($"error: {exception.Message}");
    return ExitCodes.USAGE_ERROR;
}

static string ToolVersion() =>
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
