//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// CSV catalog interchange: RFC 4180 export/import round-trip
/// fidelity (commas, quotes, newlines, Unicode, BOM (Byte Order Mark)/newline tolerance), the shared
/// conflict-policy status matrix, translator-note round-trip, dry-run purity,
/// byte-identical no-change file round-trips (including the sample catalogs), and
/// malformed-input diagnostics. Each test owns isolated temp files.
/// </summary>
[TestClass]
public sealed class CsvInterchangeTests {
    private static readonly Dictionary<string, string?> _source = new() {
        ["Hello"] = "Hello",
        ["Bye"] = "Bye",
        ["Open"] = "Open",
    };

    private static readonly Dictionary<string, string?> _target = new() {
        ["Hello"] = "Bonjour",
        ["Bye"] = "Au revoir",
    };

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the cancellation.
    /// </summary>
    private CancellationToken Cancellation => TestContext?.CancellationToken ?? CancellationToken.None;

    // ------------------------------------------------------------------- export

    /// <summary>
    /// Verifies that export produces deterministic header rows and quoting.
    /// </summary>
    [TestMethod]
    public void Export_ProducesDeterministicHeaderRowsAndQuoting() {
        var notes = new Dictionary<string, string> { ["Hello"] = "Friendly tone." };
        var source = new Dictionary<string, string?> {
            ["B"] = "Comma, here",
            ["A"] = "Plain",
        };
        var target = new Dictionary<string, string?> { ["B"] = "Quote \"x\"" };

        var text = CsvCatalogInterchange.ExportCatalog(source, target, "en", "ar", notes);

        var expected = string.Join(
            "\r\n",
            "key,culture,value,note",
            "A,en,Plain,",
            "A,ar,,",
            "B,en,\"Comma, here\",",
            "B,ar,\"Quote \"\"x\"\"\",");
        text.Should().Be(expected, "deterministic header, ordinal key order, source-then-target rows, minimal quoting, no trailing newline");
    }

    /// <summary>
    /// Verifies that export notes travel on every row of the key.
    /// </summary>
    [TestMethod]
    public void Export_Notes_TravelOnEveryRowOfTheKey() {
        var notes = new Dictionary<string, string> { ["Hello"] = "Shown on the home page." };

        var text = CsvCatalogInterchange.ExportCatalog(_source, _target, "en", "ar", notes);
        var document = CsvCatalogInterchange.ParseDocument(text, "catalog.csv");

        document.Rows.Where(static row => row.Key == "Hello").Should().HaveCount(2);
        document.Rows.Where(static row => row.Key == "Hello").Should().OnlyContain(static row => row.Note == "Shown on the home page.");
        document.Rows.Where(static row => row.Key == "Bye").Should().OnlyContain(static row => row.Note is null);
    }

    /// <summary>
    /// Verifies that export validates arguments.
    /// </summary>
    [TestMethod]
    public void Export_ValidatesArguments() {
        Action nullSource = () => CsvCatalogInterchange.ExportCatalog(null!, _target, "en", "ar");
        Action emptySourceLang = () => CsvCatalogInterchange.ExportCatalog(_source, _target, string.Empty, "ar");
        Action equalLanguages = () => CsvCatalogInterchange.ExportCatalog(_source, _target, "en", "en");

        nullSource.Should().Throw<ArgumentNullException>();
        emptySourceLang.Should().Throw<ArgumentException>();
        equalLanguages.Should().Throw<ArgumentException>("a source-only pair would duplicate every (key, culture) row");
    }

    // ---------------------------------------------------------------- round-trip

    /// <summary>
    /// Verifies that round trip preserves values exactly.
    /// </summary>
    [TestMethod]
    public void RoundTrip_PreservesValuesExactly() {
        var source = new Dictionary<string, string?> {
            ["Tricky"] = "a,b",
            ["Quoted"] = "say \"hi\"",
            ["Multiline"] = "line one\r\nline two\nline three",
            ["Unicode"] = "أهلاً — 你好 ␄",
            ["Spaces"] = "  padded  ",
        };

        var text = CsvCatalogInterchange.ExportCatalog(source, null, "en", "ar");
        var document = CsvCatalogInterchange.ParseDocument(text, "catalog.csv");

        foreach(var pair in source) {
            document.Rows.Single(row => row.Key == pair.Key && row.Culture == "en").Value.Should().Be(pair.Value, $"value of '{pair.Key}'");
        }
    }

    /// <summary>
    /// Verifies that import unchanged reports unchanged and rewrites nothing.
    /// </summary>
    [TestMethod]
    public async Task Import_Unchanged_ReportsUnchangedAndRewritesNothingAsync() {
        var sourcePath = await WriteCatalogAsync(_source);
        var targetPath = await WriteCatalogAsync(_target);
        var csvPath = TempPath(".csv");
        try {
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);
            CsvCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, csvPath, "en", "ar");

            var report = CsvCatalogInterchange.ImportCatalogFiles(csvPath, sourcePath, targetPath, "en", "ar");

            report.UpdatedCount.Should().Be(0);
            report.ConflictCount.Should().Be(0);
            report.Keys.Should().OnlyContain(static k => k.Status is CatalogKeyStatus.Unchanged or CatalogKeyStatus.Skipped);
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(before, "an unchanged import rewrites nothing");
        }
        finally {
            Delete(sourcePath, targetPath, csvPath);
        }
    }

    /// <summary>
    /// Verifies that import applies only changed targets preserving catalog formatting.
    /// </summary>
    [TestMethod]
    public async Task Import_AppliesOnlyChangedTargets_PreservingCatalogFormattingAsync() {
        var sourcePath = await WriteCatalogAsync(_source);
        var targetPath = await WriteCatalogAsync(_target);
        var csvPath = TempPath(".csv");
        try {
            CsvCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, csvPath, "en", "ar");
            var csv = await File.ReadAllTextAsync(csvPath, Cancellation);
            csv = csv.Replace("Hello,ar,Bonjour", "Hello,ar,Marhaba", StringComparison.Ordinal);
            await File.WriteAllTextAsync(csvPath, csv, new UTF8Encoding(true), Cancellation);

            var report = CsvCatalogInterchange.ImportCatalogFiles(csvPath, sourcePath, targetPath, "en", "ar");

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated);
            report.Keys.Single(static k => k.Key == "Bye").Status.Should().Be(CatalogKeyStatus.Unchanged);
            var rewritten = await File.ReadAllTextAsync(targetPath, Cancellation);
            rewritten.Should().Contain("\"Hello\": \"Marhaba\"");
            rewritten.Should().Contain("\"Bye\": \"Au revoir\"");
            rewritten.IndexOf("\"Hello\"", StringComparison.Ordinal).Should().BeLessThan(rewritten.IndexOf("\"Bye\"", StringComparison.Ordinal),
                "the catalog key order is preserved");
        }
        finally {
            Delete(sourcePath, targetPath, csvPath);
        }
    }

    /// <summary>
    /// Verifies that file round trip sample catalogs are byte identical.
    /// </summary>
    [TestMethod]
    public async Task FileRoundTrip_SampleCatalogs_AreByteIdenticalAsync() {
        var root = RepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), $"csv-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try {
            var sourcePath = Path.Combine(work, "en.json");
            var targetPath = Path.Combine(work, "ar.json");
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "en.json"), sourcePath);
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "ar.json"), targetPath);
            var csvPath = Path.Combine(work, "ar.csv");
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);

            CsvCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, csvPath, "en", "ar");
            var report = CsvCatalogInterchange.ImportCatalogFiles(csvPath, sourcePath, targetPath, "en", "ar");

            report.UpdatedCount.Should().Be(0);
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(before);
            var csvBytes = await File.ReadAllBytesAsync(csvPath, Cancellation);
            (csvBytes[0] == 0xEF && csvBytes[1] == 0xBB && csvBytes[2] == 0xBF).Should().BeTrue("CSV files are written with a UTF-8 BOM for spreadsheets");
        }
        finally {
            Directory.Delete(work, true);
        }
    }

    /// <summary>
    /// Verifies that import dry run changes nothing.
    /// </summary>
    [TestMethod]
    public void Import_DryRun_ChangesNothing() {
        var sourcePath = WriteCatalog(_source);
        var targetPath = WriteCatalog(_target);
        var csvPath = TempPath(".csv");
        try {
            CsvCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, csvPath, "en", "ar");
            var csv = File.ReadAllText(csvPath).Replace("Hello,ar,Bonjour", "Hello,ar,Marhaba", StringComparison.Ordinal);
            File.WriteAllText(csvPath, csv, new UTF8Encoding(true));
            var before = File.ReadAllBytes(targetPath);

            var report = CsvCatalogInterchange.ImportCatalogFiles(csvPath, sourcePath, targetPath, "en", "ar", new CsvImportOptions { DryRun = true });

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated, "the preview reports what would apply");
            File.ReadAllBytes(targetPath).Should().BeEquivalentTo(before, "dry-run changes nothing");
        }
        finally {
            Delete(sourcePath, targetPath, csvPath);
        }
    }

    // ------------------------------------------------------------ status matrix

    /// <summary>
    /// Verifies that import empty target is skipped and never clears existing translations.
    /// </summary>
    [TestMethod]
    public void Import_EmptyTarget_IsSkipped_AndNeverClearsExistingTranslations() {
        var text = CsvRows(("Hello", "Hello", string.Empty), ("Bye", "Bye", "Au revoir"));

        var result = Import(text, xmlTarget: false);

        result.Report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Skipped);
        result.UpdatedTarget.Should().BeNull("existing templates are never cleared by an empty target");
    }

    /// <summary>
    /// Verifies that import unknown key is reported never is applied.
    /// </summary>
    [TestMethod]
    public void Import_UnknownKey_IsReportedNeverApplied() {
        var text = CsvRows(("Hello", "Hello", "Bonjour"), ("Ghost", "Ghost", "Boo"));

        var result = Import(text, xmlTarget: false);

        var ghost = result.Report.Keys.Single(static k => k.Key == "Ghost");
        ghost.Status.Should().Be(CatalogKeyStatus.UnknownKey);
        result.Report.ConflictCount.Should().Be(1);
        result.UpdatedTarget.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import stale source when the source row differs or is missing.
    /// </summary>
    [TestMethod]
    public void Import_StaleSource_WhenTheSourceRowDiffersOrIsMissing() {
        var differing = Import(CsvRows(("Hello", "Hej", "Bonjour")), xmlTarget: false);
        differing.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.StaleSource, "the source row differs from the source catalog");

        var missingSourceRow = string.Join("\r\n", "key,culture,value,note", "Bye,ar,Vaarwel,");
        var missing = Import(missingSourceRow, xmlTarget: false);
        missing.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.StaleSource, "a missing source row cannot be verified");
    }

    /// <summary>
    /// Verifies that import unsupported key for AContextual key against an xml target.
    /// </summary>
    [TestMethod]
    public void Import_UnsupportedKey_ForAContextualKeyAgainstAnXmlTarget() {
        var source = new Dictionary<string, string?> { ["ctx\u0004Hello"] = "Hello" };
        var text = CsvRows(("ctx\u0004Hello", "Hello", "Bonjour"));

        var result = CsvCatalogInterchange.ImportDocument(
            CsvCatalogInterchange.ParseDocument(text, "catalog.csv"),
            source,
            new Dictionary<string, string?>(),
            "en",
            "ar",
            xmlTarget: true);

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.UnsupportedKey, "contextual keys cannot be XML element names");
    }

    /// <summary>
    /// Verifies that import unknown culture row fails validation.
    /// </summary>
    [TestMethod]
    public void Import_UnknownCultureRow_FailsValidation() {
        var text = string.Join("\r\n", "key,culture,value,note", "Hello,en,Hello,", "Hello,ar,Bonjour,", "Bye,de,Tschüss,");

        Action act = () => Import(text, xmlTarget: false);

        act.Should().Throw<CsvInterchangeException>().WithMessage("*'de'*");
    }

    /// <summary>
    /// Verifies that import validates arguments.
    /// </summary>
    [TestMethod]
    public void Import_ValidatesArguments() {
        var document = CsvCatalogInterchange.ParseDocument(CsvRows(("Hello", "Hello", "Bonjour")), "catalog.csv");

        Action nullDocument = () => CsvCatalogInterchange.ImportDocument(null!, _source, _target, "en", "ar");
        Action nullSource = () => CsvCatalogInterchange.ImportDocument(document, null!, _target, "en", "ar");
        Action equalLanguages = () => CsvCatalogInterchange.ImportDocument(document, _source, _target, "en", "en");

        nullDocument.Should().Throw<ArgumentNullException>();
        nullSource.Should().Throw<ArgumentNullException>();
        equalLanguages.Should().Throw<ArgumentException>();
    }

    // ------------------------------------------------------------------- notes

    /// <summary>
    /// Verifies that import surfaces translator notes per key and rejects conflicts.
    /// </summary>
    [TestMethod]
    public void Import_SurfacesTranslatorNotesPerKey_AndRejectsConflicts() {
        var withNote = CsvRows(("Hello", "Hello", "Bonjour"), ("Bye", "Bye", "Au revoir"))
            .Replace("Hello,en,Hello,", "Hello,en,Hello,Vendor hint.", StringComparison.Ordinal);

        var result = Import(withNote, xmlTarget: false);
        result.Report.Keys.Single(static k => k.Key == "Hello").TranslatorNote.Should().Be("Vendor hint.");
        result.Report.Keys.Single(static k => k.Key == "Bye").TranslatorNote.Should().BeNull();

        var conflicting = CsvRows(("Hello", "Hello", "Bonjour"), ("Bye", "Bye", "Au revoir"))
            .Replace("Hello,en,Hello,", "Hello,en,Hello,First.", StringComparison.Ordinal)
            .Replace("Hello,ar,Bonjour,", "Hello,ar,Bonjour,Second.", StringComparison.Ordinal);
        Action act = () => CsvCatalogInterchange.ParseDocument(conflicting, "catalog.csv");
        act.Should().Throw<CsvInterchangeException>().WithMessage("*Conflicting translator notes*");
    }

    // -------------------------------------------------------------- diagnostics

    /// <summary>
    /// Verifies that parse malformed csv fails with file line diagnostics.
    /// </summary>
    [TestMethod]
    public void Parse_MalformedCsv_FailsWithFileLineDiagnostics() {
        var cases = new (string Text, string Expected)[]
        {
            ("value,culture,key,note", "header must be exactly"),
            ("key,culture,value,note\r\nA,en,Hello", "Expected 4 fields"),
            ("key,culture,value,note\r\nA,en,\"unterminated", "Unterminated quoted field"),
            ("key,culture,value,note\r\nA,en,He\"llo,", "Unexpected quote in an unquoted field"),
            ("key,culture,value,note\r\nA,en,\"Hello\"x,", "Unexpected character after a closing quote"),
            ("key,culture,value,note\r\n,en,Hello,", "key must not be empty"),
            ("key,culture,value,note\r\nA,,Hello,", "culture must not be empty"),
            ("key,culture,value,note\r\nA,en,One,\r\nA,en,Two,", "Duplicate row"),
        };

        foreach(var (text, expected) in cases) {
            var exception = Assert.ThrowsExactly<CsvInterchangeException>(
                () => CsvCatalogInterchange.ParseDocument(text, "broken.csv"));
            exception.Message.Should().Contain(expected);
            exception.FilePath.Should().Be("broken.csv");
        }
    }

    /// <summary>
    /// Verifies that parse empty document fails.
    /// </summary>
    [TestMethod]
    public void Parse_EmptyDocument_Fails() {
        Action act = () => CsvCatalogInterchange.ParseDocument(string.Empty, "empty.csv");

        act.Should().Throw<CsvInterchangeException>().WithMessage("*header row is required*");
    }

    /// <summary>
    /// Verifies that parse tolerates bom lf and blank lines.
    /// </summary>
    [TestMethod]
    public void Parse_ToleratesBomLfAndBlankLines() {
        const string text = "\uFEFFkey,culture,value,note\nHello,en,Hello,\n\nHello,ar,Bonjour,\nBye,en,Bye,\n";

        var document = CsvCatalogInterchange.ParseDocument(text, "catalog.csv");

        document.Rows.Should().HaveCount(3);
        document.Rows[0].Should().BeEquivalentTo(new CsvRow { Key = "Hello", Culture = "en", Value = "Hello", Note = null });
        document.Rows[1].Culture.Should().Be("ar");
    }

    /// <summary>
    /// Verifies that import malformed catalog file throws csv interchange exception.
    /// </summary>
    [TestMethod]
    public void Import_MalformedCatalogFile_ThrowsCsvInterchangeException() {
        var sourcePath = TempPath(".json");
        var targetPath = WriteCatalog(_target);
        var csvPath = TempPath(".csv");
        try {
            File.WriteAllText(sourcePath, "{ not json", new UTF8Encoding(false));
            File.WriteAllText(csvPath, CsvRows(("Hello", "Hello", "Bonjour")), new UTF8Encoding(true));

            var exception = Assert.ThrowsExactly<CsvInterchangeException>(
                () => CsvCatalogInterchange.ImportCatalogFiles(csvPath, sourcePath, targetPath, "en", "ar"));

            exception.FilePath.Should().Be(sourcePath, "the shared codec failure is wrapped into the public CSV exception");
            exception.Message.Should().Contain("Malformed JSON catalog");
        }
        finally {
            Delete(sourcePath, targetPath, csvPath);
        }
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Performs the import operation.
    /// </summary>
    /// <param name="csvText">The csv text.</param>
    /// <param name="xmlTarget">The xml target.</param>
    /// <returns>The import result.</returns>
    private static CsvCatalogImportResult Import(string csvText, bool xmlTarget) =>
        CsvCatalogInterchange.ImportDocument(
            CsvCatalogInterchange.ParseDocument(csvText, "catalog.csv"),
            _source,
            _target,
            "en",
            "ar",
            xmlTarget);

    /// <summary>
    /// Performs the csv rows operation.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <returns>The csv rows text.</returns>
    private static string CsvRows(params (string Key, string Source, string Target)[] keys) {
        var lines = new List<string> { "key,culture,value,note" };
        foreach(var (key, source, target) in keys) {
            lines.Add($"{key},en,{source},");
            lines.Add($"{key},ar,{target},");
        }

        return string.Join("\r\n", lines);
    }

    /// <summary>
    /// Performs the temp path operation.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <returns>The temp path text.</returns>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"csv-{Guid.NewGuid():N}{extension}");

    /// <summary>
    /// Writes catalog.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The catalog text.</returns>
    private static string WriteCatalog(IReadOnlyDictionary<string, string?> entries) {
        var path = TempPath(".json");
        File.WriteAllText(path, BuildJson(entries), new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Writes catalog.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>A task whose result is catalog.</returns>
    private async Task<string> WriteCatalogAsync(IReadOnlyDictionary<string, string?> entries) {
        var path = TempPath(".json");
        await File.WriteAllTextAsync(path, BuildJson(entries), new UTF8Encoding(false), Cancellation);
        return path;
    }

    /// <summary>
    /// Builds json.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The json text.</returns>
    private static string BuildJson(IReadOnlyDictionary<string, string?> entries) {
        var builder = new StringBuilder();
        builder.Append("{\r\n");
        var first = true;
        foreach(var kvp in entries) {
            if(!first) {
                builder.Insert(builder.Length - 2, ",");
            }

            first = false;
            builder.Append(CultureInfo.InvariantCulture, $"  \"{kvp.Key}\": \"{kvp.Value}\"\r\n");
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Performs the repository root operation.
    /// </summary>
    /// <returns>The repository root text.</returns>
    private static string RepositoryRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"))) {
            directory = directory.Parent;
        }

        if(directory is null) {
            throw new InvalidOperationException("repository root could not be located from the test output directory");
        }

        return directory.FullName;
    }

    /// <summary>
    /// Performs the delete operation.
    /// </summary>
    /// <param name="paths">The paths.</param>
    private static void Delete(params string[] paths) {
        foreach(var path in paths) {
            File.Delete(path);
        }
    }
}
