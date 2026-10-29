//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// ResX catalog interchange: canonical-shape export, BCL-reader
/// compatibility, round-trip fidelity (XML entities, newlines, Unicode), the status
/// subset (Skipped/UnknownKey/UnsupportedKey/Unchanged/Updated), translator-note
/// comments, malformed/foreign diagnostics, dry-run purity, and byte-identical
/// no-change round-trips (including the sample catalogs).
/// </summary>
[TestClass]
public sealed class ResXInterchangeTests {
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
    /// Verifies that export produces the canonical resx shape.
    /// </summary>
    [TestMethod]
    public void Export_ProducesTheCanonicalResxShape() {
        var catalog = new Dictionary<string, string?> {
            ["B"] = "Beta",
            ["A"] = "Alpha",
        };
        var notes = new Dictionary<string, string> { ["A"] = "Note A." };

        var text = ResXCatalogInterchange.ExportCatalog(catalog, notes);

        var expected = string.Join(
            "\n",
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "<root>",
            "  <resheader name=\"resmimetype\">",
            "    <value>text/microsoft-resx</value>",
            "  </resheader>",
            "  <resheader name=\"version\">",
            "    <value>2.0</value>",
            "  </resheader>",
            "  <resheader name=\"reader\">",
            "    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>",
            "  </resheader>",
            "  <resheader name=\"writer\">",
            "    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>",
            "  </resheader>",
            "  <data name=\"A\" xml:space=\"preserve\">",
            "    <value>Alpha</value>",
            "    <comment>Note A.</comment>",
            "  </data>",
            "  <data name=\"B\" xml:space=\"preserve\">",
            "    <value>Beta</value>",
            "  </data>",
            "</root>");
        text.Should().Be(expected, "deterministic VS-canonical header, ordinal key order, 2-space indent, LF, no trailing newline");
    }

    /// <summary>
    /// Verifies that export opens cleanly through the bcl xml stack.
    /// </summary>
    [TestMethod]
    public void Export_OpensCleanlyThroughTheBclXmlStack() {
        var notes = new Dictionary<string, string> { ["Hello"] = "Friendly tone." };

        var text = ResXCatalogInterchange.ExportCatalog(_target, notes);

        // The BCL (Base Class Library) resx TEXT reader is Windows-Desktop-only as well (ResXResourceReader);
        // the available cross-platform verification is the BCL XML stack plus the
        // canonical header/structure contract.
        var document = XDocument.Parse(text);
        var root = document.Root!;
        root.Name.LocalName.Should().Be("root");

        var headers = root.Elements("resheader")
            .ToDictionary(
                static header => (string)header.Attribute("name")!,
                static header => header.Element("value")!.Value,
                StringComparer.Ordinal);
        headers["resmimetype"].Should().Be("text/microsoft-resx");
        headers["version"].Should().Be("2.0");
        headers["reader"].Should().StartWith("System.Resources.ResXResourceReader, System.Windows.Forms,");
        headers["writer"].Should().StartWith("System.Resources.ResXResourceWriter, System.Windows.Forms,");

        var data = root.Elements("data")
            .ToDictionary(
                static entry => (string)entry.Attribute("name")!,
                static entry => entry.Element("value")!.Value,
                StringComparer.Ordinal);
        data.Should().HaveCount(_target.Count);
        foreach(var pair in _target) {
            data[pair.Key].Should().Be(pair.Value, $"value of '{pair.Key}'");
        }

        root.Elements("data").Single(static entry => (string)entry.Attribute("name")! == "Hello")
            .Element("comment")!.Value.Should().Be("Friendly tone.");
    }

    /// <summary>
    /// Verifies that round trip preserves values and comments.
    /// </summary>
    [TestMethod]
    public void RoundTrip_PreservesValuesAndComments() {
        var catalog = new Dictionary<string, string?> {
            ["Markup"] = "<b>&amp;</b>",
            ["Multiline"] = "line one\r\nline two\nline three",
            ["Unicode"] = "أهلاً — 你好 ␄",
            ["Spaces"] = "  padded  ",
            ["Dot.Key"] = "dotted",
        };
        var notes = new Dictionary<string, string> { ["Markup"] = "Use <b>bold</b> & \"quotes\".\r\nSecond line." };

        var text = ResXCatalogInterchange.ExportCatalog(catalog, notes);
        var document = ResXCatalogInterchange.ParseDocument(text, "catalog.resx");

        foreach(var pair in catalog) {
            document.Entries.Single(entry => entry.Name == pair.Key).Value.Should().Be(pair.Value, $"value of '{pair.Key}'");
        }

        document.Entries.Single(static entry => entry.Name == "Markup").Comment.Should().Be("Use <b>bold</b> & \"quotes\".\r\nSecond line.");
        document.Entries.Single(static entry => entry.Name == "Spaces").Comment.Should().BeNull();
    }

    /// <summary>
    /// Verifies that export rejects xml illegal characters.
    /// </summary>
    [TestMethod]
    public void Export_RejectsXmlIllegalCharacters() {
        const string eot = "ctx\u0004Hello";
        var badKey = new Dictionary<string, string?> { [eot] = "Value" };
        var badValue = new Dictionary<string, string?> { ["Key"] = "Value\u0004" };

        Action key = () => ResXCatalogInterchange.ExportCatalog(badKey);
        Action value = () => ResXCatalogInterchange.ExportCatalog(badValue);

        key.Should().Throw<ArgumentException>().WithMessage("*XML 1.0*");
        value.Should().Throw<ArgumentException>().WithMessage("*XML 1.0*");
    }

    /// <summary>
    /// Verifies that export validates arguments.
    /// </summary>
    [TestMethod]
    public void Export_ValidatesArguments() {
        Action nullCatalog = () => ResXCatalogInterchange.ExportCatalog(null!);
        Action emptyKey = () => ResXCatalogInterchange.ExportCatalog(new Dictionary<string, string?> { [string.Empty] = "x" });
        Action nullValue = () => ResXCatalogInterchange.ExportCatalog(new Dictionary<string, string?> { ["Key"] = null });

        nullCatalog.Should().Throw<ArgumentNullException>();
        emptyKey.Should().Throw<ArgumentException>();
        nullValue.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------- round-trip

    /// <summary>
    /// Verifies that import unchanged reports unchanged and rewrites nothing.
    /// </summary>
    [TestMethod]
    public async Task Import_Unchanged_ReportsUnchangedAndRewritesNothingAsync() {
        var sourcePath = await WriteCatalogAsync(_source);
        var targetPath = await WriteCatalogAsync(_target);
        var resxPath = TempPath(".resx");
        try {
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);
            ResXCatalogInterchange.ExportCatalogFiles(targetPath, resxPath);

            var report = ResXCatalogInterchange.ImportCatalogFiles(resxPath, sourcePath, targetPath);

            report.UpdatedCount.Should().Be(0);
            report.ConflictCount.Should().Be(0);
            report.Keys.Should().OnlyContain(static k => k.Status == CatalogKeyStatus.Unchanged);
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(before, "an unchanged import rewrites nothing");
        }
        finally {
            Delete(sourcePath, targetPath, resxPath);
        }
    }

    /// <summary>
    /// Verifies that import applies only changed targets preserving catalog formatting.
    /// </summary>
    [TestMethod]
    public async Task Import_AppliesOnlyChangedTargets_PreservingCatalogFormattingAsync() {
        var sourcePath = await WriteCatalogAsync(_source);
        var targetPath = await WriteCatalogAsync(_target);
        var resxPath = TempPath(".resx");
        try {
            ResXCatalogInterchange.ExportCatalogFiles(targetPath, resxPath);
            var resx = await File.ReadAllTextAsync(resxPath, Cancellation);
            resx = resx.Replace("<value>Bonjour</value>", "<value>Marhaba</value>", StringComparison.Ordinal);
            await File.WriteAllTextAsync(resxPath, resx, new UTF8Encoding(true), Cancellation);

            var report = ResXCatalogInterchange.ImportCatalogFiles(resxPath, sourcePath, targetPath);

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated);
            report.Keys.Single(static k => k.Key == "Bye").Status.Should().Be(CatalogKeyStatus.Unchanged);
            var rewritten = await File.ReadAllTextAsync(targetPath, Cancellation);
            rewritten.Should().Contain("\"Hello\": \"Marhaba\"");
            rewritten.IndexOf("\"Hello\"", StringComparison.Ordinal).Should().BeLessThan(rewritten.IndexOf("\"Bye\"", StringComparison.Ordinal),
                "the catalog key order is preserved");
        }
        finally {
            Delete(sourcePath, targetPath, resxPath);
        }
    }

    /// <summary>
    /// Verifies that file round trip sample catalogs are byte identical.
    /// </summary>
    [TestMethod]
    public async Task FileRoundTrip_SampleCatalogs_AreByteIdenticalAsync() {
        var root = RepositoryRoot();
        var work = Path.Combine(Path.GetTempPath(), $"resx-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try {
            var sourcePath = Path.Combine(work, "en.json");
            var targetPath = Path.Combine(work, "ar.json");
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "en.json"), sourcePath);
            File.Copy(Path.Combine(root, "samples", "Content", "Localization", "ar.json"), targetPath);
            var resxPath = Path.Combine(work, "ar.resx");
            var before = await File.ReadAllBytesAsync(targetPath, Cancellation);

            ResXCatalogInterchange.ExportCatalogFiles(targetPath, resxPath);
            var report = ResXCatalogInterchange.ImportCatalogFiles(resxPath, sourcePath, targetPath);

            report.UpdatedCount.Should().Be(0);
            (await File.ReadAllBytesAsync(targetPath, Cancellation)).Should().BeEquivalentTo(before);
            var resxBytes = await File.ReadAllBytesAsync(resxPath, Cancellation);
            (resxBytes[0] == 0xEF && resxBytes[1] == 0xBB && resxBytes[2] == 0xBF).Should().BeTrue("ResX files are written with a UTF-8 BOM for editors");
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
        var resxPath = TempPath(".resx");
        try {
            ResXCatalogInterchange.ExportCatalogFiles(targetPath, resxPath);
            var resx = File.ReadAllText(resxPath).Replace("<value>Bonjour</value>", "<value>Marhaba</value>", StringComparison.Ordinal);
            File.WriteAllText(resxPath, resx, new UTF8Encoding(true));
            var before = File.ReadAllBytes(targetPath);

            var report = ResXCatalogInterchange.ImportCatalogFiles(resxPath, sourcePath, targetPath, new ResXImportOptions { DryRun = true });

            report.Keys.Single(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated, "the preview reports what would apply");
            File.ReadAllBytes(targetPath).Should().BeEquivalentTo(before, "dry-run changes nothing");
        }
        finally {
            Delete(sourcePath, targetPath, resxPath);
        }
    }

    // ------------------------------------------------------------ status subset

    /// <summary>
    /// Verifies that import empty value is skipped and never clears existing translations.
    /// </summary>
    [TestMethod]
    public void Import_EmptyValue_IsSkipped_AndNeverClearsExistingTranslations() {
        var resx = ResxData("Hello", string.Empty);

        var result = Import(resx);

        result.Report.Keys.Single().Status.Should().Be(CatalogKeyStatus.Skipped);
        result.UpdatedTarget.Should().BeNull("existing templates are never cleared by an empty value");
    }

    /// <summary>
    /// Verifies that import unknown key is reported never is applied.
    /// </summary>
    [TestMethod]
    public void Import_UnknownKey_IsReportedNeverApplied() {
        var resx = ResxData("Ghost", "Boo");

        var result = Import(resx);

        var ghost = result.Report.Keys.Single();
        ghost.Status.Should().Be(CatalogKeyStatus.UnknownKey);
        result.Report.ConflictCount.Should().Be(1);
        result.UpdatedTarget.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import non string entries are unsupported key.
    /// </summary>
    [TestMethod]
    public void Import_NonStringEntries_AreUnsupportedKey() {
        var resx = string.Join(
            "\n",
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "<root>",
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>",
            "  <resheader name=\"version\"><value>2.0</value></resheader>",
            "  <data name=\"Image\" type=\"System.Drawing.Bitmap, System.Drawing\"><value>AAEAAQ==</value></data>",
            "  <data name=\"Blob\" mimetype=\"application/octet-stream\"><value>AAEAAQ==</value></data>",
            "  <data name=\"Valueless\" />",
            "</root>");

        var result = Import(resx);

        result.Report.Keys.Should().HaveCount(3);
        result.Report.Keys.Should().OnlyContain(static k => k.Status == CatalogKeyStatus.UnsupportedKey);
        result.Report.ConflictCount.Should().Be(3);
        result.UpdatedTarget.Should().BeNull();
    }

    /// <summary>
    /// Verifies that import surfaces comments as translator notes.
    /// </summary>
    [TestMethod]
    public void Import_SurfacesCommentsAsTranslatorNotes() {
        var resx = ResxData("Hello", "Bonjour", "Vendor hint.");

        var result = Import(resx);

        result.Report.Keys.Single().TranslatorNote.Should().Be("Vendor hint.");
    }

    // -------------------------------------------------------------- diagnostics

    /// <summary>
    /// Verifies that parse malformed or foreign resx fails with file line diagnostics.
    /// </summary>
    [TestMethod]
    public void Parse_MalformedOrForeignResx_FailsWithFileLineDiagnostics() {
        var cases = new (string Text, string Expected)[]
        {
            ("<root><data name=\"A\"><value>x</value></data></root>", "Foreign or malformed ResX"),
            ("<messages><Hello>Hi</Hello></messages>", "must carry a 'root' element"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader></root>", "Foreign or malformed ResX"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>1.3</value></resheader></root>", "Foreign or malformed ResX"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><data><value>x</value></data></root>", "non-empty 'name'"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><data name=\"A\"><value>x</value></data><data name=\"A\"><value>y</value></data></root>", "Duplicate data entry"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><data name=\"A\"><value>x</value><value>y</value></data></root>", "at most one 'value'"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><data name=\"A\"><comment>a</comment><comment>b</comment></data></root>", "at most one 'comment'"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><bogus /></root>", "Unexpected element 'bogus'"),
            ("<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><resheader name=\"version\"><value>2.0</value></resheader><data name=\"A\"><value>x</value><metadata /></data></root>", "unexpected element 'metadata'"),
            ("<root><data name=\"A\">", "Malformed ResX"),
        };

        foreach(var (text, expected) in cases) {
            var exception = Assert.ThrowsExactly<ResXInterchangeException>(
                () => ResXCatalogInterchange.ParseDocument(text, "broken.resx"));
            exception.Message.Should().Contain(expected);
            exception.FilePath.Should().Be("broken.resx");
        }
    }

    /// <summary>
    /// Verifies that parse tolerates bom assembly and metadata.
    /// </summary>
    [TestMethod]
    public void Parse_ToleratesBomAssemblyAndMetadata() {
        var text = string.Join(
            "\n",
            "\uFEFF<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "<root>",
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>",
            "  <resheader name=\"version\"><value>2.0</value></resheader>",
            "  <assembly alias=\"System.Windows.Forms\" name=\"System.Windows.Forms, Version=4.0.0.0\" />",
            "  <metadata name=\"Extra\" type=\"System.String\"><value>ignored</value></metadata>",
            "  <data name=\"Hello\" xml:space=\"preserve\"><value>Bonjour</value></data>",
            "</root>");

        var document = ResXCatalogInterchange.ParseDocument(text, "catalog.resx");

        document.Entries.Should().HaveCount(1);
        document.Entries[0].Name.Should().Be("Hello");
    }

    /// <summary>
    /// Verifies that import malformed catalog file throws res XInterchange exception.
    /// </summary>
    [TestMethod]
    public void Import_MalformedCatalogFile_ThrowsResXInterchangeException() {
        var sourcePath = TempPath(".json");
        var targetPath = WriteCatalog(_target);
        var resxPath = TempPath(".resx");
        try {
            File.WriteAllText(sourcePath, "{ not json", new UTF8Encoding(false));
            File.WriteAllText(resxPath, ResxData("Hello", "Bonjour"), new UTF8Encoding(true));

            var exception = Assert.ThrowsExactly<ResXInterchangeException>(
                () => ResXCatalogInterchange.ImportCatalogFiles(resxPath, sourcePath, targetPath));

            exception.FilePath.Should().Be(sourcePath, "the shared codec failure is wrapped into the public ResX exception");
            exception.Message.Should().Contain("Malformed JSON catalog");
        }
        finally {
            Delete(sourcePath, targetPath, resxPath);
        }
    }

    /// <summary>
    /// Verifies that import validates arguments.
    /// </summary>
    [TestMethod]
    public void Import_ValidatesArguments() {
        var document = ResXCatalogInterchange.ParseDocument(ResxData("Hello", "Bonjour"), "catalog.resx");

        Action nullDocument = () => ResXCatalogInterchange.ImportDocument(null!, _source, _target);
        Action nullSource = () => ResXCatalogInterchange.ImportDocument(document, null!, _target);
        Action nullTarget = () => ResXCatalogInterchange.ImportDocument(document, _source, null!);

        nullDocument.Should().Throw<ArgumentNullException>();
        nullSource.Should().Throw<ArgumentNullException>();
        nullTarget.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------- helpers

    /// <summary>
    /// Performs the import operation.
    /// </summary>
    /// <param name="resxText">The resx text.</param>
    /// <returns>The import result.</returns>
    private static ResXCatalogImportResult Import(string resxText) =>
        ResXCatalogInterchange.ImportDocument(
            ResXCatalogInterchange.ParseDocument(resxText, "catalog.resx"),
            _source,
            _target);

    /// <summary>
    /// Performs the resx data operation.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="comment">The comment.</param>
    /// <returns>The resx data text.</returns>
    private static string ResxData(string name, string value, string? comment = null) {
        var lines = new List<string> {
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "<root>",
            "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>",
            "  <resheader name=\"version\"><value>2.0</value></resheader>",
            $"  <data name=\"{name}\" xml:space=\"preserve\">",
            $"    <value>{value}</value>",
        };
        if(comment is not null) {
            lines.Add($"    <comment>{comment}</comment>");
        }

        lines.Add("  </data>");
        lines.Add("</root>");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Performs the temp path operation.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <returns>The temp path text.</returns>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"resx-{Guid.NewGuid():N}{extension}");

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
