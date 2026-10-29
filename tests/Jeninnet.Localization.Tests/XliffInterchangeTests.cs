//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
///  interchange tests: export subset conformance, byte-identical round-trips,
/// changed-target application, per-key conflicts with source/target/state, dry-run
/// isolation, malformed rejection with diagnostics, and plural/context metadata.
/// </summary>
[TestClass]
public class XliffInterchangeTests {
    /// <summary>
    /// Performs the source operation.
    /// </summary>
    /// <returns>The source result.</returns>
    private static Dictionary<string, string?> Source() => new(StringComparer.Ordinal) {
        ["Hello"] = "Hello",
        ["Bye"] = "Goodbye",
        ["Open"] = "Open!",
    };

    /// <summary>
    /// Performs the target operation.
    /// </summary>
    /// <returns>The target result.</returns>
    private static Dictionary<string, string?> Target() => new(StringComparer.Ordinal) {
        ["Hello"] = "Bonjour",
        ["Bye"] = "Au revoir",
    };

    /// <summary>
    /// Performs the temp path operation.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <returns>The temp path text.</returns>
    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"xliff-{Guid.NewGuid():N}{extension}");

    /// <summary>
    /// Writes catalog.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>A task whose result is catalog.</returns>
    private async Task<string> WriteCatalogAsync(string extension, IReadOnlyDictionary<string, string?> entries) {
        var path = TempPath(extension);
        if(string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)) {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<messages>\r\n");
            foreach(var kvp in entries) {
                builder.Append(CultureInfo.InvariantCulture, $"  <{kvp.Key}>{kvp.Value}</{kvp.Key}>\r\n");
            }

            builder.Append("</messages>");
            await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), TestContext?.CancellationToken ?? CancellationToken.None);
        } else {
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
            await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), TestContext?.CancellationToken ?? CancellationToken.None);
        }

        return path;
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
    /// Verifies that export validates against subset.
    /// </summary>
    [TestMethod]
    public void Export_ValidatesAgainstSubset() {
        var text = XliffCatalogInterchange.ExportCatalog(Source(), Target(), "en", "fr", "messages");

        var document = XliffCatalogInterchange.ParseDocument(text, "export.xliff");

        document.SourceLanguage.Should().Be("en");
        document.TargetLanguage.Should().Be("fr");
        document.FileId.Should().Be("messages");
        document.Units.Should().HaveCount(3);
        document.Units.Should().OnlyContain(static u => u.State is "translated" or "initial");
        document.Units.First(static u => u.Id == "Hello").Target.Should().Be("Bonjour");
        document.Units.First(static u => u.Id == "Open").Target.Should().BeNull("a missing target exports untranslated");
    }

    /// <summary>
    /// Verifies that round trip with no changes is byte identical.
    /// </summary>
    [TestMethod]
    public async Task RoundTrip_WithNoChanges_IsByteIdenticalAsync() {
        foreach(var extension in new[] { ".json", ".xml" }) {
            var sourcePath = await WriteCatalogAsync(extension, Source());
            var targetPath = await WriteCatalogAsync(extension, Target());
            var xliffPath = TempPath(".xliff");
            try {
                var beforeSource = await File.ReadAllBytesAsync(sourcePath, TestContext?.CancellationToken ?? CancellationToken.None);
                var beforeTarget = await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None);

                XliffCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, xliffPath, "en", "fr");
                var report = XliffCatalogInterchange.ImportCatalogFiles(xliffPath, sourcePath, targetPath);

                report.UpdatedCount.Should().Be(0);
                report.ConflictCount.Should().Be(0);
                report.Keys.Should().OnlyContain(static k => k.Status is CatalogKeyStatus.Unchanged or CatalogKeyStatus.Skipped);
                (await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None)).Should().BeEquivalentTo(beforeTarget);
                (await File.ReadAllBytesAsync(sourcePath, TestContext?.CancellationToken ?? CancellationToken.None)).Should().BeEquivalentTo(beforeSource);
            }
            finally {
                File.Delete(sourcePath);
                File.Delete(targetPath);
                File.Delete(xliffPath);
            }
        }
    }

    /// <summary>
    /// Verifies that round trip sample catalogs are byte identical.
    /// </summary>
    [TestMethod]
    public async Task RoundTrip_SampleCatalogs_AreByteIdenticalAsync() {
        var root = RepositoryRoot();
        foreach(var culture in new[] { "ar" }) {
            foreach(var extension in new[] { ".json", ".xml" }) {
                var work = Path.Combine(Path.GetTempPath(), $"xliff-sample-{Guid.NewGuid():N}");
                Directory.CreateDirectory(work);
                try {
                    var sourcePath = Path.Combine(work, $"en{extension}");
                    var targetPath = Path.Combine(work, culture + extension);
                    File.Copy(Path.Combine(root, "samples", "Content", "Localization", $"en{extension}"), sourcePath);
                    File.Copy(Path.Combine(root, "samples", "Content", "Localization", culture + extension), targetPath);
                    var xliffPath = Path.Combine(work, $"{culture}.xliff");
                    var before = await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None);

                    XliffCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, xliffPath, "en", culture);
                    var reparsed = XliffCatalogInterchange.ParseDocument(
                        await File.ReadAllTextAsync(xliffPath, TestContext?.CancellationToken ?? CancellationToken.None), xliffPath);
                    reparsed.Units.Should().NotBeEmpty("the sample catalog must export units");
                    var report = XliffCatalogInterchange.ImportCatalogFiles(xliffPath, sourcePath, targetPath);

                    report.UpdatedCount.Should().Be(0);
                    (await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None)).Should().BeEquivalentTo(before);
                }
                finally {
                    Directory.Delete(work, true);
                }
            }
        }
    }

    /// <summary>
    /// Verifies that import applies only changed targets.
    /// </summary>
    [TestMethod]
    public async Task Import_AppliesOnlyChangedTargetsAsync() {
        var sourcePath = await WriteCatalogAsync(".json", Source());
        var targetPath = await WriteCatalogAsync(".json", Target());
        var xliffPath = TempPath(".xliff");
        try {
            XliffCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, xliffPath, "en", "fr");

            var xliff = await File.ReadAllTextAsync(xliffPath, TestContext?.CancellationToken ?? CancellationToken.None);
            xliff = xliff.Replace("Bonjour", "Salut", StringComparison.Ordinal);
            await File.WriteAllTextAsync(xliffPath, xliff, new UTF8Encoding(false), TestContext?.CancellationToken ?? CancellationToken.None);

            var report = XliffCatalogInterchange.ImportCatalogFiles(xliffPath, sourcePath, targetPath);

            report.UpdatedCount.Should().Be(1);
            report.Keys.First(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Updated);
            report.Keys.First(static k => k.Key == "Bye").Status.Should().Be(CatalogKeyStatus.Unchanged);
            report.Keys.First(static k => k.Key == "Open").Status.Should().Be(CatalogKeyStatus.Skipped);
            var rewritten = await File.ReadAllTextAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None);
            rewritten.Should().Contain("\"Hello\": \"Salut\"");
            rewritten.Should().Contain("\"Bye\": \"Au revoir\"");
        }
        finally {
            File.Delete(sourcePath);
            File.Delete(targetPath);
            File.Delete(xliffPath);
        }
    }

    /// <summary>
    /// Verifies that import reports conflicts per key with source target state.
    /// </summary>
    [TestMethod]
    public void Import_ReportsConflictsPerKeyWithSourceTargetState() {
        var source = Source();
        var target = Target();
        var text = XliffCatalogInterchange.ExportCatalog(source, target, "en", "fr", "messages")
            .Replace("<source>Hello</source>", "<source>CHANGED</source>", StringComparison.Ordinal)
            .Replace("</file>", "    <unit id=\"Ghost\"><segment><source>x</source><target>y</target></segment></unit>\n  </file>", StringComparison.Ordinal);
        var document = XliffCatalogInterchange.ParseDocument(text, "vendor.xliff");

        var result = XliffCatalogInterchange.ImportDocument(document, source, target);

        var stale = result.Report.Keys.First(static k => k.Key == "Hello");
        stale.Status.Should().Be(CatalogKeyStatus.StaleSource);
        stale.Source.Should().Be("CHANGED");
        stale.Target.Should().Be("Bonjour");
        stale.State.Should().Be("translated");
        var unknown = result.Report.Keys.First(static k => k.Key == "Ghost");
        unknown.Status.Should().Be(CatalogKeyStatus.UnknownKey);
        result.Report.ConflictCount.Should().Be(2);
        result.UpdatedTarget.Should().BeNull("conflicts are never applied");
    }

    /// <summary>
    /// Verifies that import dry run changes nothing.
    /// </summary>
    [TestMethod]
    public async Task Import_DryRun_ChangesNothingAsync() {
        var sourcePath = await WriteCatalogAsync(".json", Source());
        var targetPath = await WriteCatalogAsync(".json", Target());
        var xliffPath = TempPath(".xliff");
        try {
            XliffCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, xliffPath, "en", "fr");
            var xliff = await File.ReadAllTextAsync(xliffPath, TestContext?.CancellationToken ?? CancellationToken.None);
            xliff = xliff.Replace("Bonjour", "Salut", StringComparison.Ordinal);
            await File.WriteAllTextAsync(xliffPath, xliff, new UTF8Encoding(false), TestContext?.CancellationToken ?? CancellationToken.None);
            var before = await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None);

            var report = XliffCatalogInterchange.ImportCatalogFiles(
                xliffPath, sourcePath, targetPath, new XliffImportOptions { DryRun = true });

            report.UpdatedCount.Should().Be(1, "dry-run still computes the plan");
            (await File.ReadAllBytesAsync(targetPath, TestContext?.CancellationToken ?? CancellationToken.None)).Should().BeEquivalentTo(before);
        }
        finally {
            File.Delete(sourcePath);
            File.Delete(targetPath);
            File.Delete(xliffPath);
        }
    }

    /// <summary>
    /// Verifies that malformed xliff is rejected with diagnostics.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="truncated">The truncated.</param>
    [TestMethod]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\"><file id=\"m\"></file>", true)]
    [DataRow("<xliff version=\"1.2\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"></file></xliff>", false)]
    [DataRow("<xliff srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" trgLang=\"fr\"><file id=\"m\"></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><unit id=\"a\"><segment><source>x</source></segment></unit><unit id=\"a\"><segment><source>y</source></segment></unit></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><unit id=\"a\"><segment state=\"bogus\"><source>x</source></segment></unit></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><unit id=\"a\"><segment><target>y</target></segment></unit></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><unit id=\"a\"><segment><source>x</source></segment><segment><source>y</source></segment></unit></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><unit id=\"a\"><segment><source>x<pc id=\"1\">y</pc></source></segment></unit></file></xliff>", false)]
    [DataRow("<xliff version=\"2.0\" srcLang=\"en\" trgLang=\"fr\"><file id=\"m\"><group id=\"g\"></group></file></xliff>", false)]
    public void MalformedXliff_IsRejectedWithDiagnostics(string fragment, bool truncated) {
        var text = truncated
            ? fragment
            : "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + fragment;

        Func<XliffDocument> act = () => XliffCatalogInterchange.ParseDocument(text, "bad.xliff");

        var exception = act.Should().Throw<XliffInterchangeException>();
        exception.Which.FilePath.Should().Be("bad.xliff");
        exception.Which.Message.Should().Contain("bad.xliff", "diagnostics always name the file");
    }

    /// <summary>
    /// Verifies that missing inputs are rejected with diagnostics.
    /// </summary>
    [TestMethod]
    public async Task MissingInputs_AreRejectedWithDiagnosticsAsync() {
        Func<XliffImportReport> missingXliff = () => XliffCatalogInterchange.ImportCatalogFiles(
            TempPath(".xliff"), TempPath(".json"), TempPath(".json"));

        missingXliff.Should().Throw<XliffInterchangeException>().Which.FilePath.Should().EndWith(".xliff");

        var poPath = TempPath(".po");
        var sourcePath = await WriteCatalogAsync(".json", Source());
        var targetPath = await WriteCatalogAsync(".json", Target());
        var xliffPath = TempPath(".xliff");
        try {
            await File.WriteAllTextAsync(poPath, "msgid \"a\"\nmsgstr \"b\"\n", TestContext?.CancellationToken ?? CancellationToken.None);
            XliffCatalogInterchange.ExportCatalogFiles(sourcePath, targetPath, xliffPath, "en", "fr");

            Func<XliffImportReport> unsupportedFormat = () => XliffCatalogInterchange.ImportCatalogFiles(
                xliffPath, poPath, targetPath);

            unsupportedFormat.Should().Throw<XliffInterchangeException>().Which.Message.Should().Contain(".po");
        }
        finally {
            File.Delete(poPath);
            File.Delete(sourcePath);
            File.Delete(targetPath);
            File.Delete(xliffPath);
        }
    }

    /// <summary>
    /// Verifies that plural metadata round trips through units.
    /// </summary>
    [TestMethod]
    public void PluralMetadata_RoundTripsThroughUnits() {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["book"] = "book",
            ["books"] = "books",
        };
        var tables = new[] {
            new XliffPluralEntry {
                SingularKey = "book",
                PluralKey = "books",
                TargetForms = ["kniha", "knihy", "knih"],
            },
        };

        var text = XliffCatalogInterchange.ExportCatalog(source, new Dictionary<string, string?>(), "en", "cs", "messages", tables);
        var document = XliffCatalogInterchange.ParseDocument(text, "plural.xliff");

        document.Units.Should().HaveCount(3, "grouped pair keys export as form units only");
        document.Units.Select(static u => u.Plural!.Index).Should().BeEquivalentTo(0, 1, 2);
        document.Units.Should().OnlyContain(static u => u.Plural!.Count == 3);

        // Without managed tables the pair slots carry forms 0/1 and higher forms are unsupported.
        var empty = new Dictionary<string, string?>(StringComparer.Ordinal);
        var flat = XliffCatalogInterchange.ImportDocument(document, source, empty);

        flat.UpdatedTarget.Should().NotBeNull();
        flat.UpdatedTarget!["book"].Should().Be("kniha");
        flat.UpdatedTarget!["books"].Should().Be("knihy");
        flat.Report.Keys.First(static k => k.Key == "book").Status.Should().Be(CatalogKeyStatus.Updated);
        flat.Report.Keys.First(static k => k.Key == "books").Status.Should().Be(CatalogKeyStatus.Updated);
        flat.Report.Keys.First(static k => k.Key == "book[2]").Status.Should().Be(CatalogKeyStatus.UnsupportedKey);
        flat.UpdatedPlurals.Should().BeNull("no tables were supplied");

        // With managed tables every form resolves inside the table.
        var tabled = XliffCatalogInterchange.ImportDocument(document, source, empty, currentPlurals: tables);

        tabled.UpdatedPlurals.Should().BeNull("imported tables already match");
        tabled.UpdatedTarget.Should().BeNull("pair slots stay untouched when tables are managed");
        tabled.Report.Keys.Should().OnlyContain(static k => k.Status == CatalogKeyStatus.Unchanged);
    }

    /// <summary>
    /// Verifies that plural metadata changed form updates table only.
    /// </summary>
    [TestMethod]
    public void PluralMetadata_ChangedForm_UpdatesTableOnly() {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["book"] = "book",
            ["books"] = "books",
        };
        var current = new[] {
            new XliffPluralEntry {
                SingularKey = "book",
                PluralKey = "books",
                TargetForms = ["kniha", "knihy", "knih"],
            },
        };
        var text = XliffCatalogInterchange.ExportCatalog(source, new Dictionary<string, string?>(), "en", "cs", "messages", current);
        text = text.Replace("knihy", "knizky", StringComparison.Ordinal);
        var document = XliffCatalogInterchange.ParseDocument(text, "plural.xliff");
        var empty = new Dictionary<string, string?>(StringComparer.Ordinal);

        var result = XliffCatalogInterchange.ImportDocument(document, source, empty, currentPlurals: current);

        result.Report.Keys.First(static k => k.Key == "book[1]").Status.Should().Be(CatalogKeyStatus.Updated);
        var forms = result.UpdatedPlurals!.Single().TargetForms;
        forms.Count.Should().Be(3);
        forms[0].Should().Be("kniha");
        forms[1].Should().Be("knizky");
        forms[2].Should().Be("knih");
        result.UpdatedTarget.Should().BeNull("pair slots stay untouched when tables are managed");
    }

    /// <summary>
    /// Verifies that context metadata round trips with encoded separator.
    /// </summary>
    [TestMethod]
    public void ContextMetadata_RoundTripsWithEncodedSeparator() {
        var key = "menu" + '\u0004' + "open";
        var source = new Dictionary<string, string?>(StringComparer.Ordinal) {
            [key] = "Open menu",
            ["open"] = "Open!",
        };

        var translated = new Dictionary<string, string?>(StringComparer.Ordinal) {
            [key] = "Ouvrir menu",
            ["open"] = "Ouvrir!",
        };
        var text = XliffCatalogInterchange.ExportCatalog(source, translated, "en", "fr", "messages");
        var document = XliffCatalogInterchange.ParseDocument(text, "ctx.xliff");

        var contextual = document.Units.First(static u => u.Context is not null);
        contextual.Context.Should().Be("menu");
        contextual.Id.Should().Be("menu" + '\u2404' + "open");
        var result = XliffCatalogInterchange.ImportDocument(document, source, new Dictionary<string, string?>());

        result.UpdatedTarget.Should().NotBeNull();
        result.UpdatedTarget![key].Should().Be("Ouvrir menu");
        result.UpdatedTarget!["open"].Should().Be("Ouvrir!");
        result.Report.Keys.Should().OnlyContain(static k => k.Status == CatalogKeyStatus.Updated);
    }

    /// <summary>
    /// Verifies that skipped units never clear existing templates.
    /// </summary>
    [TestMethod]
    public void SkippedUnits_NeverClearExistingTemplates() {
        var source = Source();
        var target = Target();
        var text = XliffCatalogInterchange.ExportCatalog(source, target, "en", "fr", "messages")
            .Replace("<target>Bonjour</target>", "<target></target>", StringComparison.Ordinal)
            .Replace("<unit id=\"Bye\" xml:space=\"preserve\">", "<unit id=\"Bye\" xml:space=\"preserve\" translate=\"no\">", StringComparison.Ordinal);
        var document = XliffCatalogInterchange.ParseDocument(text, "skip.xliff");

        var result = XliffCatalogInterchange.ImportDocument(document, source, target);

        result.Report.Keys.First(static k => k.Key == "Hello").Status.Should().Be(CatalogKeyStatus.Skipped);
        result.Report.Keys.First(static k => k.Key == "Bye").Status.Should().Be(CatalogKeyStatus.Skipped);
        result.UpdatedTarget.Should().BeNull("skips never touch the catalog");
    }

    /// <summary>
    /// Verifies that contextual keys against xml catalogs report is unsupported.
    /// </summary>
    [TestMethod]
    public async Task ContextualKeys_AgainstXmlCatalogs_ReportUnsupportedAsync() {
        var sourcePath = await WriteCatalogAsync(".xml", new Dictionary<string, string?> { ["open"] = "Open!" });
        var targetPath = await WriteCatalogAsync(".xml", new Dictionary<string, string?> { ["open"] = "Ouvrir!" });
        try {
            var key = "menu" + '\u0004' + "open";
            var source = new Dictionary<string, string?>(StringComparer.Ordinal) {
                [key] = "Open menu",
                ["open"] = "Open!",
            };
            var translated = new Dictionary<string, string?>(StringComparer.Ordinal) {
                [key] = "Ouvrir menu",
                ["open"] = "Ouvrir!",
            };
            var text = XliffCatalogInterchange.ExportCatalog(source, translated, "en", "fr", "m");
            var xliffPath = TempPath(".xliff");
            try {
                await File.WriteAllTextAsync(xliffPath, text, new UTF8Encoding(false), TestContext?.CancellationToken ?? CancellationToken.None);

                var report = XliffCatalogInterchange.ImportCatalogFiles(xliffPath, sourcePath, targetPath);

                report.Keys.First(static k => k.Key == "open").Status.Should().Be(CatalogKeyStatus.Unchanged);
                report.Keys.First(static k => k.Key.Contains("menu")).Status.Should().Be(CatalogKeyStatus.UnsupportedKey);
            }
            finally {
                File.Delete(xliffPath);
            }
        }
        finally {
            File.Delete(sourcePath);
            File.Delete(targetPath);
        }
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
