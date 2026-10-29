//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tools.Keys;

/// <summary>
/// Builds the parity report from scanned catalogs and extracted references:
/// resolves constant references through harvested definitions, computes the
/// per-culture fallback-aware completeness matrix (a key missing locally but served
/// by a parent culture or the default culture is fallback, not missing), and emits
/// the eight deterministic finding kinds.
/// Pure function of its inputs.
/// </summary>
internal static class ParityAnalyzer {
    private static readonly string[] _formatOrder = ["json", "xml", "resx", "po"];

    /// <summary>
    /// Analyzes parity and builds the report.
    /// </summary>
    /// <param name="scan">The catalog scan.</param>
    /// <param name="extraction">The key extraction.</param>
    /// <param name="options">The CLI options.</param>
    /// <param name="toolVersion">The tool version stamped into the report.</param>
    /// <returns>The parity report.</returns>
    public static ParityReport Analyze(CatalogScan scan, ExtractionResult extraction, CliOptions options, string toolVersion) {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(toolVersion);

        var report = CreateReport(scan, options, toolVersion);
        AddSources(report, extraction);

        // Definitions: first ordinal-by-scan-order value wins; differing values conflict.
        var (definitions, definitionConflicts) = CollectDefinitions(extraction);
        AddKeyDefinitions(report, definitions);

        report.ExcludedKeys.AddRange(options.Excludes.Distinct(StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal));

        var (served, cultures) = BuildServedMap(scan);
        var (keyReferences, findings) = ResolveReferences(extraction, definitions);
        AddDefinitionConflictFindings(findings, definitionConflicts);

        AddMatrixFindings(report, findings, served, [.. cultures], keyReferences, options);
        AddCatalogFindings(findings, served, keyReferences, scan, options);
        AddPlaceholderFindings(findings, served, options);
        AddCatalogErrorFindings(findings, scan);

        SortFindings(findings);
        report.Findings.AddRange(findings);
        AddSummary(report, findings);

        return report;
    }

    /// <summary>
    /// Creates the report shell with the catalog summaries and the ignored files.
    /// </summary>
    /// <param name="scan">The catalog scan.</param>
    /// <param name="options">The CLI options.</param>
    /// <param name="toolVersion">The tool version stamped into the report.</param>
    /// <returns>The report shell.</returns>
    private static ParityReport CreateReport(CatalogScan scan, CliOptions options, string toolVersion) {
        var report = new ParityReport {
            ToolVersion = toolVersion,
            DefaultCulture = options.DefaultCulture,
        };

        foreach(var catalog in scan.Catalogs) {
            report.Catalogs.Add(new CatalogSummary {
                Path = catalog.Path,
                Format = catalog.Format,
                Culture = catalog.Culture,
                Keys = catalog.Entries.Count,
                Error = catalog.Error,
            });
        }

        report.IgnoredFiles.AddRange(scan.IgnoredFiles.OrderBy(static path => path, StringComparer.Ordinal));
        return report;
    }

    /// <summary>
    /// Adds one source summary per scanned file with its reference count.
    /// </summary>
    /// <param name="report">The report receiving the source summaries.</param>
    /// <param name="extraction">The key extraction.</param>
    private static void AddSources(ParityReport report, ExtractionResult extraction) {
        var referencesByFile = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach(var path in extraction.References.Select(static reference => reference.Path)) {
            referencesByFile.TryGetValue(path, out var count);
            referencesByFile[path] = count + 1;
        }

        foreach(var file in extraction.ScannedFiles) {
            referencesByFile.TryGetValue(file, out var count);
            report.Sources.Add(new SourceSummary { Path = file, References = count });
        }
    }

    /// <summary>
    /// Collects the first definition per name and the conflicting values for names defined
    /// more than once with differing values.
    /// </summary>
    /// <param name="extraction">The key extraction.</param>
    /// <returns>The winning definitions by name and the conflicting definitions by name.</returns>
    private static (Dictionary<string, KeyDefinition> Definitions, Dictionary<string, List<KeyDefinition>> Conflicts) CollectDefinitions(ExtractionResult extraction) {
        var definitions = new Dictionary<string, KeyDefinition>(StringComparer.Ordinal);
        var definitionConflicts = new Dictionary<string, List<KeyDefinition>>(StringComparer.Ordinal);
        foreach(var definition in extraction.Definitions) {
            if(!definitions.TryGetValue(definition.Name, out var existing)) {
                definitions[definition.Name] = definition;
                continue;
            }

            if(existing.Value.Equals(definition.Value, StringComparison.Ordinal)) {
                continue;
            }

            RegisterDefinitionConflict(definition, existing, definitionConflicts);
        }

        return (definitions, definitionConflicts);
    }

    /// <summary>
    /// Records a differing definition as a conflict for its name, ignoring duplicates of an
    /// already recorded value.
    /// </summary>
    /// <param name="definition">The differing definition.</param>
    /// <param name="existing">The first definition recorded for the name.</param>
    /// <param name="definitionConflicts">The conflicting definitions by name.</param>
    private static void RegisterDefinitionConflict(KeyDefinition definition, KeyDefinition existing, Dictionary<string, List<KeyDefinition>> definitionConflicts) {
        if(!definitionConflicts.TryGetValue(definition.Name, out var clash)) {
            clash = [existing];
            definitionConflicts[definition.Name] = clash;
        }

        if(clash.All(previous => !previous.Value.Equals(definition.Value, StringComparison.Ordinal))) {
            clash.Add(definition);
        }
    }

    /// <summary>
    /// Adds the winning constant definitions to the report in ordinal name order.
    /// </summary>
    /// <param name="report">The report receiving the definitions.</param>
    /// <param name="definitions">The winning definitions by name.</param>
    private static void AddKeyDefinitions(ParityReport report, Dictionary<string, KeyDefinition> definitions) {
        foreach(var name in definitions.Keys.OrderBy(static name => name, StringComparer.Ordinal)) {
            var definition = definitions[name];
            report.KeyDefinitions.Add(new DefinitionSummary {
                Name = definition.Name,
                Key = definition.Value,
                Path = definition.Path,
                Line = definition.Line,
            });
        }
    }

    /// <summary>
    /// Builds the served map ((culture, key) to served entries) and the sorted set of cultures
    /// that have at least one readable catalog.
    /// </summary>
    /// <param name="scan">The catalog scan.</param>
    /// <returns>The served entries by cell and the cultures with readable catalogs.</returns>
    private static (Dictionary<(string Culture, string Key), List<ServedEntry>> Served, SortedSet<string> Cultures) BuildServedMap(CatalogScan scan) {
        // Served map: (culture, key) -> served entries (format, template, path, line).
        var served = new Dictionary<(string Culture, string Key), List<ServedEntry>>(new CultureKeyComparer());
        var cultures = new SortedSet<string>(StringComparer.Ordinal);
        foreach(var catalog in scan.Catalogs) {
            if(catalog.Error is not null) {
                continue;
            }

            cultures.Add(catalog.Culture);
            foreach(var entry in catalog.Entries) {
                var cell = (catalog.Culture, entry.Key);
                if(!served.TryGetValue(cell, out var entries)) {
                    entries = [];
                    served[cell] = entries;
                }

                entries.Add(new ServedEntry(catalog.Format, entry.Template, catalog.Path, entry.Line));
            }
        }

        return (served, cultures);
    }

    /// <summary>
    /// Resolves member accesses through the harvested definitions, keeps literals, and records
    /// unresolved references as findings.
    /// </summary>
    /// <param name="extraction">The key extraction.</param>
    /// <param name="definitions">The winning definitions by name.</param>
    /// <returns>The reference spots by resolved key and the findings produced during resolution.</returns>
    private static (Dictionary<string, List<ReferenceSpot>> KeyReferences, List<ParityFinding> Findings) ResolveReferences(ExtractionResult extraction, Dictionary<string, KeyDefinition> definitions) {
        // Referenced keys: resolve member accesses, keep literals.
        var keyReferences = new Dictionary<string, List<ReferenceSpot>>(StringComparer.Ordinal);
        var findings = new List<ParityFinding>();
        foreach(var reference in extraction.References) {
            var key = reference.LiteralValue;
            if(reference.MemberName is not null) {
                if(!definitions.TryGetValue(reference.MemberName, out var definition)) {
                    findings.Add(new ParityFinding {
                        Kind = FindingKinds.UNRESOLVED_REFERENCE,
                        Message = $"Reference '{reference.MemberName}' has no constant definition.",
                        Locations = [ToLocation(Spot(reference))],
                    });
                    continue;
                }

                key = definition.Value;
            }

            if(key is null) {
                continue;
            }

            if(!keyReferences.TryGetValue(key, out var spots)) {
                spots = [];
                keyReferences[key] = spots;
            }

            spots.Add(Spot(reference));
        }

        return (keyReferences, findings);
    }

    /// <summary>
    /// Adds one finding per constant defined with differing values.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="definitionConflicts">The conflicting definitions by name.</param>
    private static void AddDefinitionConflictFindings(List<ParityFinding> findings, Dictionary<string, List<KeyDefinition>> definitionConflicts) {
        foreach(var conflict in definitionConflicts) {
            var values = conflict.Value
                .Select(static definition => definition.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray();
            findings.Add(new ParityFinding {
                Kind = FindingKinds.DEFINITION_CONFLICT,
                Message = $"Constant '{conflict.Key}' is defined with differing values ('{string.Join("' vs '", values)}').",
                Locations = [.. conflict.Value
                    .OrderBy(static definition => definition.Path, StringComparer.Ordinal)
                    .ThenBy(static definition => definition.Line)
                    .Select(static definition => new FindingLocation { Path = definition.Path, Line = definition.Line, Column = 1 })],
            });
        }
    }

    /// <summary>
    /// Builds the completeness matrix rows and the missing-key findings.
    /// </summary>
    /// <param name="report">The report receiving the matrix rows.</param>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="cultures">The cultures with readable catalogs, in ordinal order.</param>
    /// <param name="keyReferences">The reference spots by resolved key.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddMatrixFindings(
        ParityReport report,
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        List<string> cultures,
        Dictionary<string, List<ReferenceSpot>> keyReferences,
        CliOptions options) {
        // Matrix + missing keys.
        var referencedKeys = keyReferences.Keys
            .Where(key => !IsExcludedEverywhere(key, options.Excludes))
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToList();
        foreach(var key in referencedKeys) {
            var row = new MatrixRow { Key = key };
            var spots = keyReferences[key]
                .OrderBy(static spot => spot.Path, StringComparer.Ordinal)
                .ThenBy(static spot => spot.Line)
                .Select(ToLocation)
                .ToList();

            foreach(var culture in cultures) {
                var (chain, state) = ResolveCell(culture, key, served, options.DefaultCulture);
                row.Cultures.Add(new MatrixCell { Culture = culture, State = state });

                if(state == MatrixStates.MISSING && !IsExcluded(key, culture, options.Excludes)) {
                    findings.Add(new ParityFinding {
                        Kind = FindingKinds.MISSING_KEY,
                        Key = key,
                        Culture = culture,
                        Message = $"Key '{key}' is referenced in source but not served for culture '{culture}' (chain: {string.Join(", ", chain)}).",
                        Locations = spots,
                    });
                }
            }

            report.Matrix.Add(row);
        }
    }

    /// <summary>
    /// Computes the fallback chain and completeness state for one matrix cell.
    /// </summary>
    /// <param name="culture">The cell culture.</param>
    /// <param name="key">The matrix key.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="defaultCulture">The default culture appended to the fallback chain.</param>
    /// <returns>The fallback chain and the completeness state.</returns>
    private static (List<string> Chain, string State) ResolveCell(string culture, string key, Dictionary<(string Culture, string Key), List<ServedEntry>> served, string defaultCulture) {
        var chain = FallbackChain(culture, defaultCulture);
        var state = MatrixStates.MISSING;
        if(served.ContainsKey((culture, key))) {
            state = MatrixStates.SERVED;
        } else if(chain.Skip(1).Any(parent => served.ContainsKey((parent, key)))) {
            state = MatrixStates.FALLBACK;
        }

        return (chain, state);
    }

    /// <summary>
    /// Adds the unused-key and per-cell (template drift, format gap) catalog-side findings.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="keyReferences">The reference spots by resolved key.</param>
    /// <param name="scan">The catalog scan.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddCatalogFindings(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        Dictionary<string, List<ReferenceSpot>> keyReferences,
        CatalogScan scan,
        CliOptions options) {
        AddUnusedKeyFindings(findings, served, keyReferences, options);
        AddServedCellFindings(findings, served, scan, options);
    }

    /// <summary>
    /// Adds one finding per catalog key that no source references.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="keyReferences">The reference spots by resolved key.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddUnusedKeyFindings(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        Dictionary<string, List<ReferenceSpot>> keyReferences,
        CliOptions options) {
        // Catalog-side checks over served (culture, key) cells.
        var catalogKeys = served.Keys
            .Select(static cell => cell.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToList();
        foreach(var key in catalogKeys) {
            if(keyReferences.ContainsKey(key) || IsExcludedEverywhere(key, options.Excludes)) {
                continue;
            }

            findings.Add(new ParityFinding {
                Kind = FindingKinds.UNUSED_KEY,
                Key = key,
                Message = $"Key '{key}' is defined in {served.Keys.Count(cell => cell.Key.Equals(key, StringComparison.Ordinal))} culture(s) but never referenced in source.",
                Locations = [.. served
                    .Where(cell => cell.Key.Key.Equals(key, StringComparison.Ordinal))
                    .SelectMany(cell => cell.Value.Select(entry => new FindingLocation { Path = entry.Path, Line = entry.Line, Column = 1 }))
                    .OrderBy(static location => location.Path, StringComparer.Ordinal)
                    .ThenBy(static location => location.Line)],
            });
        }
    }

    /// <summary>
    /// Adds the template-drift and format-gap findings for every served cell.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="scan">The catalog scan.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddServedCellFindings(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        CatalogScan scan,
        CliOptions options) {
        foreach(var cell in served.Keys.OrderBy(static cell => cell.Culture, StringComparer.Ordinal).ThenBy(static cell => cell.Key, StringComparer.Ordinal)) {
            if(IsExcluded(cell.Key, cell.Culture, options.Excludes)) {
                continue;
            }

            var entries = served[cell]
                .OrderBy(static entry => Array.IndexOf(_formatOrder, entry.Format))
                .ThenBy(static entry => entry.Path, StringComparer.Ordinal)
                .ToList();
            var distinctTemplates = entries.Select(static entry => entry.Template).Distinct(StringComparer.Ordinal).ToList();
            if(distinctTemplates.Count > 1) {
                AddTemplateDriftFinding(findings, cell.Key, cell.Culture, entries);
            }

            AddFormatGapFindings(findings, cell.Key, cell.Culture, entries, scan);
        }
    }

    /// <summary>
    /// Adds the template-drift finding for one cell whose served templates differ.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="key">The cell key.</param>
    /// <param name="culture">The cell culture.</param>
    /// <param name="entries">The served entries for the cell, in format order.</param>
    private static void AddTemplateDriftFinding(List<ParityFinding> findings, string key, string culture, List<ServedEntry> entries) {
        var formats = entries.Select(static entry => entry.Format).Distinct(StringComparer.Ordinal).OrderBy(static format => format, StringComparer.Ordinal);
        findings.Add(new ParityFinding {
            Kind = FindingKinds.TEMPLATE_DRIFT,
            Key = key,
            Culture = culture,
            Message = $"Key '{key}' has differing templates for culture '{culture}' ({string.Join(" vs ", formats)}).",
            Locations = [.. entries.Select(static entry => new FindingLocation { Path = entry.Path, Line = entry.Line, Column = 1 })],
        });
    }

    /// <summary>
    /// Adds one format-gap finding per format that serves the culture but not the key.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="key">The cell key.</param>
    /// <param name="culture">The cell culture.</param>
    /// <param name="entries">The served entries for the cell.</param>
    /// <param name="scan">The catalog scan.</param>
    private static void AddFormatGapFindings(
        List<ParityFinding> findings,
        string key,
        string culture,
        List<ServedEntry> entries,
        CatalogScan scan) {
        var formatsServing = entries.Select(static entry => entry.Format).ToHashSet(StringComparer.Ordinal);
        foreach(var format in FormatsServingCulture(scan, culture).Where(format => !formatsServing.Contains(format))) {
            var gapCatalog = scan.Catalogs.First(catalog =>
                catalog.Error is null &&
                catalog.Format.Equals(format, StringComparison.Ordinal) &&
                catalog.Culture.Equals(culture, StringComparison.Ordinal));
            findings.Add(new ParityFinding {
                Kind = FindingKinds.FORMAT_GAP,
                Key = key,
                Culture = culture,
                Message = $"Key '{key}' is served for culture '{culture}' but missing from the {format} catalog.",
                Locations = [new FindingLocation { Path = gapCatalog.Path, Line = 1, Column = 1 }],
            });
        }
    }

    /// <summary>
    /// Adds the placeholder-drift findings for keys served locally by at least two cultures.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddPlaceholderFindings(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        CliOptions options) {
        // Placeholder parity: catalog-internal comparison for keys
        // served locally by at least two cultures. The reference set is the default
        // culture's when it serves the key locally, otherwise the ordinal-first
        // serving culture's; empty templates are untranslated and not compared.
        var servingCulturesByKey = BuildServingCulturesByKey(served);
        foreach(var key in servingCulturesByKey.Keys) {
            var servingCultures = servingCulturesByKey[key];
            if(servingCultures.Count < 2) {
                continue;
            }

            var placeholderSets = BuildPlaceholderSets(served, key, servingCultures);
            if(placeholderSets.Count < 2) {
                continue;
            }

            AddPlaceholderDriftFindings(findings, served, key, placeholderSets, options);
        }
    }

    /// <summary>
    /// Groups the serving cultures by catalog key.
    /// </summary>
    /// <param name="served">The served entries by cell.</param>
    /// <returns>The serving cultures by key, in ordinal order.</returns>
    private static SortedDictionary<string, SortedSet<string>> BuildServingCulturesByKey(Dictionary<(string Culture, string Key), List<ServedEntry>> served) {
        var servingCulturesByKey = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach(var (Culture, Key) in served.Keys) {
            if(!servingCulturesByKey.TryGetValue(Key, out var servingCultures)) {
                servingCultures = new SortedSet<string>(StringComparer.Ordinal);
                servingCulturesByKey[Key] = servingCultures;
            }

            servingCultures.Add(Culture);
        }

        return servingCulturesByKey;
    }

    /// <summary>
    /// Builds the placeholder identifier set per culture for one key, skipping cultures whose
    /// templates are all empty.
    /// </summary>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="servingCultures">The cultures serving the key.</param>
    /// <returns>The placeholder identifiers by culture.</returns>
    private static SortedDictionary<string, SortedSet<string>> BuildPlaceholderSets(
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        string key,
        SortedSet<string> servingCultures) {
        var placeholderSets = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach(var culture in servingCultures) {
            var templates = served[(culture, key)]
                .Select(static entry => entry.Template)
                .Where(static template => !string.IsNullOrWhiteSpace(template))
                .ToList();
            if(templates.Count == 0) {
                continue;
            }

            var identifiers = new SortedSet<string>(StringComparer.Ordinal);
            foreach(var template in templates) {
                identifiers.UnionWith(PlaceholderScanner.ExtractIdentifiers(template));
            }

            placeholderSets[culture] = identifiers;
        }

        return placeholderSets;
    }

    /// <summary>
    /// Adds the placeholder-drift findings for every culture compared against the reference set.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="placeholderSets">The placeholder identifiers by culture.</param>
    /// <param name="options">The CLI options.</param>
    private static void AddPlaceholderDriftFindings(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        string key,
        SortedDictionary<string, SortedSet<string>> placeholderSets,
        CliOptions options) {
        var referenceCulture = placeholderSets.ContainsKey(options.DefaultCulture)
            ? options.DefaultCulture
            : placeholderSets.Keys.First();
        var reference = placeholderSets[referenceCulture];
        foreach(var culture in placeholderSets.Keys) {
            if(culture.Equals(referenceCulture, StringComparison.Ordinal) || IsExcluded(key, culture, options.Excludes)) {
                continue;
            }

            AddPlaceholderDriftFinding(findings, served, key, culture, referenceCulture, reference, placeholderSets[culture]);
        }
    }

    /// <summary>
    /// Adds one placeholder-drift finding for a culture whose placeholder set differs from the
    /// reference set.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="served">The served entries by cell.</param>
    /// <param name="key">The catalog key.</param>
    /// <param name="culture">The compared culture.</param>
    /// <param name="referenceCulture">The culture whose placeholder set is the reference.</param>
    /// <param name="reference">The reference placeholder identifiers.</param>
    /// <param name="identifiers">The compared culture's placeholder identifiers.</param>
    private static void AddPlaceholderDriftFinding(
        List<ParityFinding> findings,
        Dictionary<(string Culture, string Key), List<ServedEntry>> served,
        string key,
        string culture,
        string referenceCulture,
        SortedSet<string> reference,
        SortedSet<string> identifiers) {
        var missing = reference.Except(identifiers).ToList();
        var extra = identifiers.Except(reference).ToList();
        if(missing.Count == 0 && extra.Count == 0) {
            return;
        }

        var details = new List<string>();
        if(missing.Count > 0) {
            details.Add($"missing {{{string.Join(", ", missing)}}}");
        }

        if(extra.Count > 0) {
            details.Add($"extra {{{string.Join(", ", extra)}}}");
        }

        var referenceList = reference.Count == 0
            ? "no placeholders"
            : $"placeholders {{{string.Join(", ", reference)}}}";
        findings.Add(new ParityFinding {
            Kind = FindingKinds.PLACEHOLDER_DRIFT,
            Key = key,
            Culture = culture,
            Message = $"Key '{key}' has placeholder drift for culture '{culture}': {string.Join(", ", details)} (reference culture '{referenceCulture}' uses {referenceList}).",
            Locations = [.. served[(culture, key)]
                .OrderBy(static entry => Array.IndexOf(_formatOrder, entry.Format))
                .ThenBy(static entry => entry.Path, StringComparer.Ordinal)
                .Select(static entry => new FindingLocation { Path = entry.Path, Line = entry.Line, Column = 1 })],
        });
    }

    /// <summary>
    /// Adds one finding per catalog that could not be read.
    /// </summary>
    /// <param name="findings">The findings being accumulated.</param>
    /// <param name="scan">The catalog scan.</param>
    private static void AddCatalogErrorFindings(List<ParityFinding> findings, CatalogScan scan) {
        foreach(var catalog in scan.Catalogs) {
            if(catalog.Error is null) {
                continue;
            }

            findings.Add(new ParityFinding {
                Kind = FindingKinds.CATALOG_ERROR,
                Culture = catalog.Culture,
                Message = $"Catalog '{catalog.Path}' could not be read: {catalog.Error}",
                Locations = [new FindingLocation { Path = catalog.Path, Line = catalog.ErrorLine, Column = 1 }],
            });
        }
    }

    /// <summary>
    /// Sorts the findings by kind, key, culture, and first location.
    /// </summary>
    /// <param name="findings">The findings to sort.</param>
    private static void SortFindings(List<ParityFinding> findings) => findings.Sort(CompareFindings);

    /// <summary>
    /// Compares two findings by kind, key, culture, and first location for the report order.
    /// </summary>
    /// <param name="left">The left finding.</param>
    /// <param name="right">The right finding.</param>
    /// <returns>The comparison result.</returns>
    private static int CompareFindings(ParityFinding left, ParityFinding right) {
        var kind = left.Kind.CompareTo(right.Kind, StringComparison.Ordinal);
        if(kind != 0) {
            return kind;
        }

        var key = left.Key.CompareTo(right.Key, StringComparison.Ordinal);
        if(key != 0) {
            return key;
        }

        var culture = left.Culture.CompareTo(right.Culture, StringComparison.Ordinal);
        if(culture != 0) {
            return culture;
        }

        var leftFirst = left.Locations.FirstOrDefault();
        var rightFirst = right.Locations.FirstOrDefault();
        var path = leftFirst?.Path.CompareTo(rightFirst?.Path, StringComparison.Ordinal) ?? 0;
        return path != 0 ? path : (leftFirst?.Line ?? 0).CompareTo(rightFirst?.Line ?? 0);
    }

    /// <summary>
    /// Accumulates the per-kind summary counts over the sorted findings.
    /// </summary>
    /// <param name="report">The report receiving the summary counts.</param>
    /// <param name="findings">The sorted findings.</param>
    private static void AddSummary(ParityReport report, List<ParityFinding> findings) {
        foreach(var finding in findings) {
            report.Summary.Total++;
            switch(finding.Kind) {
                case FindingKinds.MISSING_KEY: report.Summary.MissingKeys++; break;
                case FindingKinds.UNUSED_KEY: report.Summary.UnusedKeys++; break;
                case FindingKinds.TEMPLATE_DRIFT: report.Summary.TemplateDrifts++; break;
                case FindingKinds.FORMAT_GAP: report.Summary.FormatGaps++; break;
                case FindingKinds.PLACEHOLDER_DRIFT: report.Summary.PlaceholderDrifts++; break;
                case FindingKinds.UNRESOLVED_REFERENCE: report.Summary.UnresolvedReferences++; break;
                case FindingKinds.DEFINITION_CONFLICT: report.Summary.DefinitionConflicts++; break;
                case FindingKinds.CATALOG_ERROR: report.Summary.CatalogErrors++; break;
            }
        }
    }

    /// <summary>
    /// Determines whether the value is excluded everywhere.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="excludes">The excludes.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsExcludedEverywhere(string key, List<string> excludes) =>
        excludes.Any(exclude => exclude.Equals(key, StringComparison.Ordinal));

    /// <summary>
    /// Determines whether the value is excluded.
    /// </summary>
    /// <param name="key">The message key.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="excludes">The excludes.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsExcluded(string key, string culture, List<string> excludes) =>
        IsExcludedEverywhere(key, excludes) ||
        excludes.Any(exclude => exclude.Equals($"{culture}:{key}", StringComparison.Ordinal));

    /// <summary>
    /// Performs the fallback chain operation.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="defaultCulture">The default culture.</param>
    /// <returns>The fallback chain sequence.</returns>
    private static List<string> FallbackChain(string culture, string defaultCulture) {
        var chain = new List<string> { culture };
        try {
            var current = CultureInfo.GetCultureInfo(culture).Parent;
            while(current != null && !string.IsNullOrEmpty(current.Name) && !current.Equals(CultureInfo.InvariantCulture)) {
                if(!chain.Contains(current.Name, StringComparer.Ordinal)) {
                    chain.Add(current.Name);
                }

                current = current.Parent;
            }
        }
        catch(ArgumentException) {
            // Culture validated at scan time; a defensive miss keeps the chain exact-only.
        }

        if(!chain.Contains(defaultCulture, StringComparer.Ordinal)) {
            chain.Add(defaultCulture);
        }

        return chain;
    }

    /// <summary>
    /// Performs the formats serving culture operation.
    /// </summary>
    /// <param name="scan">The scan.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The s serving culture sequence.</returns>
    private static List<string> FormatsServingCulture(CatalogScan scan, string culture) =>
        [.. scan.Catalogs
            .Where(catalog => catalog.Error is null && catalog.Culture.Equals(culture, StringComparison.Ordinal))
            .Select(static catalog => catalog.Format)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static format => Array.IndexOf(_formatOrder, format))];

    /// <summary>
    /// Performs the spot operation.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The spot result.</returns>
    private static ReferenceSpot Spot(KeyReference reference) => new(reference.Path, reference.Line, reference.Column);

    /// <summary>
    /// Converts to location.
    /// </summary>
    /// <param name="spot">The spot.</param>
    /// <returns>The to location result.</returns>
    private static FindingLocation ToLocation(ReferenceSpot spot) =>
        new() { Path = spot.Path, Line = spot.Line, Column = spot.Column };

    /// <summary>
    /// Represents a reference spot.
    /// </summary>
    /// <param name="Path">The path.</param>
    /// <param name="Line">The line.</param>
    /// <param name="Column">The column.</param>
    private sealed record ReferenceSpot(string Path, int Line, int Column);

    /// <summary>
    /// Represents a served entry.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="template">The message template.</param>
    /// <param name="path">The file path.</param>
    /// <param name="line">The line.</param>
    private sealed class ServedEntry(string format, string template, string path, int line) {
        /// <summary>
        /// Gets the format.
        /// </summary>
        public string Format { get; } = format;

        /// <summary>
        /// Gets the template.
        /// </summary>
        public string Template { get; } = template;

        /// <summary>
        /// Gets the path.
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// Gets the line.
        /// </summary>
        public int Line { get; } = line;
    }

    /// <summary>
    /// Represents a culture key comparer.
    /// </summary>
    private sealed class CultureKeyComparer : IEqualityComparer<(string Culture, string Key)> {
        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>true when the condition holds; otherwise, false.</returns>
        public bool Equals((string Culture, string Key) x, (string Culture, string Key) y) =>
            x.Culture.Equals(y.Culture, StringComparison.Ordinal) &&
            x.Key.Equals(y.Key, StringComparison.Ordinal);

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>The hash code value.</returns>
        public int GetHashCode((string Culture, string Key) obj) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.Culture),
                StringComparer.Ordinal.GetHashCode(obj.Key));
    }
}
