//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Generators.Keys;

/// <summary>
/// Declared catalog input: one <c>*.loc.json</c> additional file.
/// </summary>
/// <param name="path">The additional-file path (used for diagnostics and deterministic ordering).</param>
/// <param name="text">The file content.</param>
internal sealed class CatalogInput(string path, string text) {

    /// <summary>
    /// Gets the additional-file path (used for diagnostics and deterministic ordering).
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public string Text { get; } = text;
}

/// <summary>
/// A generator-computed diagnostic with an explicit file/line location.
/// </summary>
/// <param name="descriptor">The descriptor.</param>
/// <param name="path">The file path, or empty for location-less diagnostics.</param>
/// <param name="offset">The raw offset in <paramref name="text"/> (ignored when <paramref name="path"/> is empty).</param>
/// <param name="length">The raw length (ignored when <paramref name="path"/> is empty).</param>
/// <param name="text">The file content the offset refers to.</param>
/// <param name="arguments">The message arguments.</param>
internal sealed class ReportedDiagnostic(DiagnosticDescriptor descriptor, string path, int offset, int length, string text, params object?[] arguments) {

    /// <summary>
    /// Gets the descriptor.
    /// </summary>
    public DiagnosticDescriptor Descriptor { get; } = descriptor;

    /// <summary>
    /// Gets the file path, or empty for location-less diagnostics.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the raw offset in <see cref="Text"/> (ignored when <see cref="Path"/> is empty).
    /// </summary>
    public int Offset { get; } = offset;

    /// <summary>
    /// Gets the raw length (ignored when <see cref="Path"/> is empty).
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// Gets the file content the offset refers to.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// Gets the message arguments.
    /// </summary>
    public object?[] Arguments { get; } = arguments;
}

/// <summary>
/// The pure outcome of one generation pass: either source text or diagnostics, never both.
/// </summary>
/// <param name="source">The generated compilation unit, or <see langword="null"/> when diagnostics were reported.</param>
/// <param name="diagnostics">The reported diagnostics in deterministic order.</param>
internal sealed class GenerationOutcome(string? source, List<ReportedDiagnostic> diagnostics) {

    /// <summary>
    /// Gets the generated compilation unit, or <see langword="null"/> when diagnostics were reported.
    /// </summary>
    public string? Source { get; } = source;

    /// <summary>
    /// Gets the reported diagnostics in deterministic order.
    /// </summary>
    public List<ReportedDiagnostic> Diagnostics { get; } = diagnostics;
}

/// <summary>
/// Roslyn incremental source generator that industrializes the hand-maintained
/// <c>ReferenceKeys</c> pattern: compile-time key constants generated from the
/// canonical JSON content root, so typos become build errors, renames propagate through
/// compilation, and content/key drift is caught at compile time.
/// </summary>
/// <remarks>
/// Architecture decision:
/// <list type="bullet">
/// <item>Identity: <c>Jeninnet.Localization.Generators.Keys</c> (project == assembly ==
/// root namespace == package, the project identity rule) under <c>src/generators/</c>,
/// listed in the solution under <c>/src/Tooling/</c>.
/// Ships as its own analyzer package (<c>analyzers/dotnet/cs</c>,
/// <c>DevelopmentDependency</c>), never as a library dependency.
/// </item>
/// <item>Framework: <c>netstandard2.0</c> is a deliberate, documented exception to the
/// net10.0 runtime rule (the target-framework rule): a generator is build-time-only and must load
/// in every Roslyn host (dotnet CLI, test harness, Visual Studio). BCL (Base Class Library) + compiler APIs
/// only; JSON parsing is a hand-rolled strict flat-map parser because the
/// System.Text.Json netstandard2.0 closure (7 transitive packages) is rejected per
/// dependency minimalism (the dependency-minimalism rule).
/// </item>
/// <item>Input contract: <c>*.loc.json</c> additional files holding flat string maps.
/// Anything outside that subset is malformed content: an error diagnostic, no emission.
/// </item>
/// <item>Emission: keys only (<c>const string</c> per key plus the keys-only <c>All</c>
/// table; templates stay in providers), ordinal-sorted, no timestamps — byte-identical
/// across rebuilds for fixed inputs. XML docs per constant keep consuming builds with
/// warnings-as-errors green.
/// </item>
/// <item>Incrementality: <c>AdditionalTextsProvider</c> plus <c>Collect</c> plus a single
/// tracked <c>RegisterSourceOutput</c> step, so unchanged catalogs reuse cached results.
/// </item>
/// </list>
/// </remarks>
[Generator]
public sealed class LocalizationKeysGenerator : IIncrementalGenerator {
    /// <summary>
    /// The additional-file suffix declaring the canonical content root.
    /// </summary>
    internal const string CATALOG_SUFFIX = ".loc.json";

    /// <summary>
    /// The MSBuild property (via analyzer config) overriding the generated namespace.
    /// </summary>
    internal const string NAMESPACE_PROPERTY = "build_property.LocalizationKeysNamespace";

    /// <summary>
    /// The tracking name of the per-file parse step.
    /// </summary>
    internal const string PARSE_TRACKING_NAME = "LocalizationKeysParse";

    /// <summary>
    /// The tracking name of the single emit step.
    /// </summary>
    internal const string EMIT_TRACKING_NAME = "LocalizationKeysEmit";

    /// <summary>
    /// The hint name of the generated compilation unit (stable: part of determinism).
    /// </summary>
    internal const string HINT_NAME = "LocalizationKeys.g.cs";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // Step 1 (per file, cached by content): parse one catalog into an
        // equatable model. Unchanged files never re-parse.
        var parsed = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(CATALOG_SUFFIX, StringComparison.OrdinalIgnoreCase))
            .Select(static (text, cancellationToken) => ParseCatalogFile(new CatalogInput(
                text.Path,
                text.GetText(cancellationToken)?.ToString() ?? string.Empty)))
            .WithTrackingName(PARSE_TRACKING_NAME);

        var requestedNamespace = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => options.GlobalOptions.TryGetValue(NAMESPACE_PROPERTY, out var value)
                ? value
                : string.Empty);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? string.Empty);

        // Step 2 (single emit): combine the parsed models and produce the unit.
        var inputs = parsed.Collect().Combine(requestedNamespace).Combine(assemblyName);
        context.RegisterSourceOutput(
            inputs.WithTrackingName(EMIT_TRACKING_NAME),
            static (production, pair) => Execute(production, pair.Left.Left, pair.Left.Right, pair.Right));
    }

    /// <summary>
    /// Performs the execute operation.
    /// </summary>
    /// <param name="production">The production.</param>
    /// <param name="catalogs">The catalogs.</param>
    /// <param name="requestedNamespace">The requested namespace.</param>
    /// <param name="assemblyName">The assembly name.</param>
    internal static void Execute(
        SourceProductionContext production,
        ImmutableArray<ParsedCatalogFile> catalogs,
        string requestedNamespace,
        string assemblyName) {
        production.CancellationToken.ThrowIfCancellationRequested();
        var outcome = BuildModel(SortedCatalogs(catalogs), requestedNamespace, assemblyName);
        foreach(var reported in outcome.Diagnostics) {
            production.ReportDiagnostic(ToDiagnostic(reported));
        }

        if(outcome.Source is not null) {
            production.AddSource(HINT_NAME, outcome.Source);
        }
    }

    /// <summary>
    /// Parses catalog file.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>The catalog file result.</returns>
    internal static ParsedCatalogFile ParseCatalogFile(CatalogInput input) {
        if(input is null) {
            throw new ArgumentNullException(nameof(input));
        }

        var parsed = LocJsonParser.Parse(input.Text);
        var entries = new ParsedCatalogEntry[parsed.Entries.Count];
        for(var index = 0; index < parsed.Entries.Count; index++) {
            var entry = parsed.Entries[index];
            entries[index] = new ParsedCatalogEntry(
                entry.Key,
                entry.KeyOffset,
                entry.KeyLength,
                entry.Value,
                entry.IsStringValue,
                entry.ValueOffset);
        }

        return new ParsedCatalogFile(
            input.Path,
            input.Text,
            ImmutableArray.Create(entries),
            parsed.SyntaxError?.Message,
            parsed.SyntaxError?.Offset ?? 0);
    }

    /// <summary>
    /// Builds model.
    /// </summary>
    /// <param name="catalogs">The catalogs.</param>
    /// <param name="requestedNamespace">The requested namespace.</param>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The model result.</returns>
    internal static GenerationOutcome BuildModel(
        IReadOnlyList<ParsedCatalogFile> catalogs,
        string requestedNamespace,
        string? assemblyName) {
        if(catalogs is null) {
            throw new ArgumentNullException(nameof(catalogs));
        }

        // No declared content root: not participating means no emission.
        // (A declared-but-empty catalog still emits the empty class.)
        if(catalogs.Count == 0) {
            return new GenerationOutcome(source: null, []);
        }

        var diagnostics = new List<ReportedDiagnostic>();
        var firstLocation = new Dictionary<string, KeyLocation>(StringComparer.Ordinal);
        foreach(var catalog in catalogs) {
            CollectCatalogEntries(catalog, diagnostics, firstLocation);
        }

        var namespaceName = ResolveNamespace(requestedNamespace, assemblyName);
        if(namespaceName is null) {
            diagnostics.Add(new ReportedDiagnostic(
                GeneratorDiagnostics.InvalidNamespace,
                path: string.Empty,
                offset: 0,
                length: 0,
                text: string.Empty,
                (requestedNamespace ?? string.Empty).Trim()));
        }

        if(diagnostics.Count > 0) {
            return new GenerationOutcome(source: null, diagnostics);
        }

        var emitted = BuildEmittedKeys(firstLocation, diagnostics);
        if(diagnostics.Count > 0) {
            return new GenerationOutcome(source: null, diagnostics);
        }

        return new GenerationOutcome(KeysSourceBuilder.Build(namespaceName!, emitted), []);
    }

    /// <summary>
    /// Collects the entries of one catalog, reporting syntax errors, non-string values, and
    /// duplicate keys, and records the first location of every accepted key.
    /// </summary>
    /// <param name="catalog">The catalog to inspect.</param>
    /// <param name="diagnostics">The diagnostics collected so far.</param>
    /// <param name="firstLocation">The first location per accepted key, filled in place.</param>
    private static void CollectCatalogEntries(
        ParsedCatalogFile catalog,
        List<ReportedDiagnostic> diagnostics,
        Dictionary<string, KeyLocation> firstLocation) {
        if(catalog.SyntaxError is not null) {
            diagnostics.Add(new ReportedDiagnostic(
                GeneratorDiagnostics.MalformedContent,
                catalog.Path,
                catalog.SyntaxOffset,
                1,
                catalog.Text,
                catalog.SyntaxError));
            return;
        }

        var seenInFile = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach(var entry in catalog.Entries) {
            CollectEntry(catalog, entry, seenInFile, diagnostics, firstLocation);
        }
    }

    /// <summary>
    /// Validates one catalog entry and records its first location when accepted.
    /// </summary>
    /// <param name="catalog">The catalog owning the entry.</param>
    /// <param name="entry">The entry to validate.</param>
    /// <param name="seenInFile">The first line per key seen in the current catalog.</param>
    /// <param name="diagnostics">The diagnostics collected so far.</param>
    /// <param name="firstLocation">The first location per accepted key, filled in place.</param>
    private static void CollectEntry(
        ParsedCatalogFile catalog,
        ParsedCatalogEntry entry,
        Dictionary<string, int> seenInFile,
        List<ReportedDiagnostic> diagnostics,
        Dictionary<string, KeyLocation> firstLocation) {
        if(!entry.IsStringValue) {
            diagnostics.Add(new ReportedDiagnostic(
                GeneratorDiagnostics.NonStringEntry,
                catalog.Path,
                entry.ValueOffset,
                1,
                catalog.Text,
                entry.Key));
            return;
        }

        if(seenInFile.TryGetValue(entry.Key, out var firstLine)) {
            diagnostics.Add(new ReportedDiagnostic(
                GeneratorDiagnostics.DuplicateKey,
                catalog.Path,
                entry.KeyOffset,
                entry.KeyLength,
                catalog.Text,
                entry.Key,
                firstLine));
            return;
        }

        LocJsonParser.GetLineAndColumn(catalog.Text, entry.KeyOffset, out var entryLine, out _);
        seenInFile[entry.Key] = entryLine;
        if(!firstLocation.ContainsKey(entry.Key)) {
            firstLocation[entry.Key] = new KeyLocation(catalog.Path, catalog.Text, entry.KeyOffset, entry.KeyLength);
        }
    }

    /// <summary>
    /// Maps the accepted keys to identifiers, reporting empty identifiers and collisions.
    /// </summary>
    /// <param name="firstLocation">The first location per accepted key.</param>
    /// <param name="diagnostics">The diagnostics collected so far.</param>
    /// <returns>The emitted keys in ordinal key order, excluding reported failures.</returns>
    private static List<EmittedKey> BuildEmittedKeys(
        Dictionary<string, KeyLocation> firstLocation,
        List<ReportedDiagnostic> diagnostics) {
        var orderedKeys = new List<string>(firstLocation.Keys);
        orderedKeys.Sort(StringComparer.Ordinal);

        var emitted = new List<EmittedKey>(orderedKeys.Count);
        var identifierOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach(var key in orderedKeys) {
            var identifier = LocalizationKeyNaming.ToIdentifier(key);
            var location = firstLocation[key];
            if(identifier.Length == 0) {
                diagnostics.Add(new ReportedDiagnostic(
                    GeneratorDiagnostics.EmptyIdentifier,
                    location.Path,
                    location.Offset,
                    location.Length,
                    location.Text,
                    key));
                continue;
            }

            if(identifierOwner.TryGetValue(identifier, out var owner)) {
                diagnostics.Add(new ReportedDiagnostic(
                    GeneratorDiagnostics.IdentifierCollision,
                    location.Path,
                    location.Offset,
                    location.Length,
                    location.Text,
                    owner,
                    key,
                    identifier));
                continue;
            }

            identifierOwner[identifier] = key;
            emitted.Add(new EmittedKey(key, identifier));
        }

        return emitted;
    }

    /// <summary>
    /// Resolves namespace.
    /// </summary>
    /// <param name="requested">The requested.</param>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The namespace result.</returns>
    internal static string? ResolveNamespace(string? requested, string? assemblyName) {
        var trimmed = (requested ?? string.Empty).Trim();
        if(trimmed.Length > 0) {
            return LocalizationKeyNaming.IsValidNamespace(trimmed) ? trimmed : null;
        }

        var assembly = string.IsNullOrEmpty(assemblyName) ? "Localization" : assemblyName;
        var parts = assembly!.Split('.');
        for(var index = 0; index < parts.Length; index++) {
            parts[index] = SanitizeNamespacePart(parts[index]);
        }

        return string.Join(".", parts) + ".Generated";
    }

    /// <summary>
    /// Converts to diagnostic.
    /// </summary>
    /// <param name="reported">The reported.</param>
    /// <returns>The to diagnostic result.</returns>
    internal static Diagnostic ToDiagnostic(ReportedDiagnostic reported) {
        if(reported is null) {
            throw new ArgumentNullException(nameof(reported));
        }

        if(reported.Path.Length == 0) {
            return Diagnostic.Create(reported.Descriptor, Location.None, reported.Arguments);
        }

        var length = reported.Length < 0 ? 0 : reported.Length;
        var offset = reported.Offset < 0 ? 0 : reported.Offset;
        if(offset > reported.Text.Length) {
            offset = reported.Text.Length;
        }

        if(offset + length > reported.Text.Length) {
            length = reported.Text.Length - offset;
        }

        LocJsonParser.GetLineAndColumn(reported.Text, offset, out var startLine, out var startColumn);
        LocJsonParser.GetLineAndColumn(reported.Text, offset + length, out var endLine, out var endColumn);
        var span = new LinePositionSpan(
            new LinePosition(startLine - 1, startColumn - 1),
            new LinePosition(endLine - 1, endColumn - 1));
        return Diagnostic.Create(
            reported.Descriptor,
            Location.Create(reported.Path, new TextSpan(offset, length), span),
            reported.Arguments);
    }

    /// <summary>
    /// Performs the sanitize namespace part operation.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>The sanitize namespace part text.</returns>
    private static string SanitizeNamespacePart(string part) {
        if(part is null) {
            throw new ArgumentNullException(nameof(part));
        }

        var builder = new StringBuilder(part.Length);
        for(var index = 0; index < part.Length; index++) {
            var current = part[index];
            builder.Append(IsNamespaceChar(current) ? current : '_');
        }

        if(builder.Length == 0) {
            return "_";
        }

        var first = builder[0];
        if(first is >= '0' and <= '9') {
            return "_" + builder;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether the value is namespace char.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsNamespaceChar(char value) =>
        (value >= 'a' && value <= 'z') ||
        (value >= 'A' && value <= 'Z') ||
        (value >= '0' && value <= '9') ||
        value == '_' ||
        (value > 0x7F && char.IsLetterOrDigit(value));

    /// <summary>
    /// Performs the sorted catalogs operation.
    /// </summary>
    /// <param name="catalogs">The catalogs.</param>
    /// <returns>The sorted catalogs sequence.</returns>
    private static List<ParsedCatalogFile> SortedCatalogs(ImmutableArray<ParsedCatalogFile> catalogs) {
        var sorted = new List<ParsedCatalogFile>(catalogs.Length);
        sorted.AddRange(catalogs);
        sorted.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return sorted;
    }

    /// <summary>
    /// Represents a key location.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    private sealed class KeyLocation(string path, string text, int offset, int length) {
        /// <summary>
        /// Gets the path.
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// Gets the text.
        /// </summary>
        public string Text { get; } = text;

        /// <summary>
        /// Gets the offset.
        /// </summary>
        public int Offset { get; } = offset;

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get; } = length;
    }
}
