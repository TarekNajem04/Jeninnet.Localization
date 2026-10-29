//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Exit codes: 0 is clean, 1 is parity findings, 2 is tool misuse.
/// </summary>
internal static class ExitCodes {
    /// <summary>No findings: catalogs and sources agree.</summary>
    public const int CLEAN = 0;

    /// <summary>One or more parity findings were reported.</summary>
    public const int FINDINGS = 1;

    /// <summary>Bad arguments or an unreadable environment (missing root, no inputs).</summary>
    public const int USAGE_ERROR = 2;
}

/// <summary>
/// Parsed command line. The tool is a single check command: catalog roots plus
/// source roots in, console table plus optional JSON report out.
/// </summary>
internal sealed class CliOptions {
    /// <summary>
    /// Gets the catalog roots to scan recursively.
    /// </summary>
    public List<string> CatalogRoots { get; } = [];

    /// <summary>
    /// Gets the source roots to scan recursively.
    /// </summary>
    public List<string> SourceRoots { get; } = [];

    /// <summary>
    /// Gets the explicit C# files parsed for constant definitions.
    /// </summary>
    public List<string> KeysFiles { get; } = [];

    /// <summary>
    /// Gets the exclusions: exact keys (<c>Demo.Missing</c>) or culture-scoped
    /// (<c>ar:Demo.Partial</c>).
    /// </summary>
    public List<string> Excludes { get; } = [];

    /// <summary>
    /// Gets the default culture closing every fallback chain.
    /// </summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>
    /// Gets the JSON report path, or <see langword="null"/> for console only.
    /// </summary>
    public string? ReportPath { get; set; }

    /// <summary>
    /// Returns whether parsing succeeded; otherwise <paramref name="error"/> explains why.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="options">The parsed options on success.</param>
    /// <param name="error">The usage error on failure.</param>
    /// <returns><see langword="true"/> on success.</returns>
    public static bool TryParse(string[] args, [NotNullWhen(true)] out CliOptions? options, out string error) {
        options = null;
        error = string.Empty;

        if(args is null) {
            error = "No arguments were provided.";
            return false;
        }

        var parsed = new CliOptions();
        var index = 0;
        while(index < args.Length) {
            var current = args[index];
            if(current is "--help" or "-h" or "-?") {
                parsed.ShowHelp = true;
                options = parsed;
                return true;
            }

            if(current == "--version") {
                parsed.ShowVersion = true;
                options = parsed;
                return true;
            }

            if(!TryApplyOption(args, ref index, parsed, out error)) {
                return false;
            }
        }

        return Validate(parsed, out options, out error);
    }

    /// <summary>
    /// Consumes one <c>--option value</c> pair from the argument list.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The current position; advanced past the consumed pair.</param>
    /// <param name="parsed">The options being populated.</param>
    /// <param name="error">The usage error on failure.</param>
    /// <returns><see langword="true"/> when the pair was consumed.</returns>
    private static bool TryApplyOption(string[] args, ref int index, CliOptions parsed, out string error) {
        error = string.Empty;
        var current = args[index];

        if(!current.StartsWith("--", StringComparison.Ordinal)) {
            error = $"Unexpected argument '{current}'.";
            return false;
        }

        if(index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) {
            error = $"Option '{current}' requires a value.";
            return false;
        }

        var value = args[index + 1];
        index += 2;

        switch(current) {
            case "--catalog":
                parsed.CatalogRoots.Add(value);
                return true;
            case "--source":
                parsed.SourceRoots.Add(value);
                return true;
            case "--keys-file":
                parsed.KeysFiles.Add(value);
                return true;
            case "--exclude":
                parsed.Excludes.Add(value);
                return true;
            case "--default-culture":
                parsed.DefaultCulture = value;
                return true;
            case "--report":
                parsed.ReportPath = value;
                return true;
            default:
                error = $"Unknown option '{current}'.";
                return false;
        }
    }

    /// <summary>
    /// Validates the parsed options against the filesystem and the culture invariant.
    /// </summary>
    /// <param name="parsed">The parsed options.</param>
    /// <param name="options">The validated options on success.</param>
    /// <param name="error">The usage error on failure.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool Validate(CliOptions parsed, [NotNullWhen(true)] out CliOptions? options, out string error) {
        options = null;
        error = string.Empty;

        if(parsed.CatalogRoots.Count == 0) {
            error = "At least one --catalog root is required.";
            return false;
        }

        if(parsed.SourceRoots.Count == 0) {
            error = "At least one --source root is required.";
            return false;
        }

        var missingRoot = parsed.CatalogRoots.Concat(parsed.SourceRoots).FirstOrDefault(static root => !Directory.Exists(root));
        if(missingRoot is not null) {
            error = $"Directory '{missingRoot}' does not exist.";
            return false;
        }

        var missingKeysFile = parsed.KeysFiles.FirstOrDefault(static file => !File.Exists(file));
        if(missingKeysFile is not null) {
            error = $"Keys file '{missingKeysFile}' does not exist.";
            return false;
        }

        try {
            _ = CultureInfo.GetCultureInfo(parsed.DefaultCulture);
        }
        catch(ArgumentException) {
            error = $"Default culture '{parsed.DefaultCulture}' is not a known culture.";
            return false;
        }

        options = parsed;
        return true;
    }

    /// <summary>
    /// Gets whether usage help was requested.
    /// </summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Gets whether the tool version was requested.
    /// </summary>
    public bool ShowVersion { get; set; }

    /// <summary>
    /// Gets the usage text.
    /// </summary>
    public static string Usage { get; } = """
        jeninnet-localization-keys — key-extraction + catalog parity check.

        Usage:
          jeninnet-localization-keys --catalog <dir>... --source <dir>... [options]

        Options:
          --catalog <dir>         Catalog root scanned recursively (repeatable, required).
          --source <dir>          Source root scanned recursively for .cs/.razor/.cshtml (repeatable, required).
          --keys-file <file>      Extra C# file parsed for key-constant definitions (repeatable).
          --exclude <key>         Excluded key, or culture-scoped 'culture:key' (repeatable).
          --default-culture <tag> Default culture closing fallback chains (default: en).
          --report <path>         Write the versioned JSON report to <path>.
          --help                  Show this help.
          --version               Show the tool version.

        Exit codes: 0 clean, 1 parity findings, 2 usage or environment error.
        Write-back is out of scope: the tool reports only.
        """;
}
