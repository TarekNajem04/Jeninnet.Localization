//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Roslyn-harness unit tests for the multi-format key generation inputs: ResX data names,
/// PO msgids, and XLIFF 2.0 unit ids generate the same constants as the canonical JSON
/// root, with format-specific malformed-content diagnostics and shared duplicate-key,
/// non-string, identifier, and namespace rules.
/// </summary>
[TestClass]
public sealed class LocalizationKeysGeneratorFormatsTests {
    // ─── ResX ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that generates keys from res x data names.
    /// </summary>
    [TestMethod]
    public void GeneratesKeys_FromResXDataNames() {
        const string resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
              <metadata name="author" />
              <data name="App.Title" xml:space="preserve"><value>Title</value></data>
              <data name="Cart.Items" xml:space="preserve"><value>Items</value><comment>note</comment></data>
            </root>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", resx)]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        sources[0].Should().Contain("""public const string APP_TITLE = "App.Title";""");
        sources[0].Should().Contain("""public const string CART_ITEMS = "Cart.Items";""");
    }

    /// <summary>
    /// Verifies that malformed res x reports JLNKEY007 and emits nothing.
    /// </summary>
    [TestMethod]
    public void MalformedResX_ReportsJLNKEY007_AndEmitsNothing() {
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", "<root><data name=\"A\">")]);

        sources.Should().BeEmpty("the generator never emits on malformed content");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY007");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.GetLineSpan().Path.Should().Be("/catalogs/strings.resx");
    }

    /// <summary>
    /// Verifies that res x missing name reports JLNKEY007.
    /// </summary>
    [TestMethod]
    public void ResXMissingName_ReportsJLNKEY007() {
        const string resx = """
            <root>
              <data xml:space="preserve"><value>Title</value></data>
            </root>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", resx)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY007");
    }

    /// <summary>
    /// Verifies that res x typed entry reports non string error.
    /// </summary>
    [TestMethod]
    public void ResXTypedEntry_ReportsNonStringError() {
        const string resx = """
            <root>
              <data name="Count" type="System.Int32, mscorlib"><value>5</value></data>
            </root>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", resx)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY002");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Count");
    }

    /// <summary>
    /// Verifies that res x missing value resolves to empty.
    /// </summary>
    [TestMethod]
    public void ResXMissingValue_ResolvesToEmpty() {
        const string resx = """
            <root>
              <data name="Empty" xml:space="preserve" />
            </root>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", resx)]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("""public const string EMPTY = "Empty";""");
    }

    /// <summary>
    /// Verifies that res x duplicate key reports error.
    /// </summary>
    [TestMethod]
    public void ResXDuplicateKey_ReportsError() {
        const string resx = """
            <root>
              <data name="A" xml:space="preserve"><value>first</value></data>
              <data name="A" xml:space="preserve"><value>second</value></data>
            </root>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.resx", resx)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY003");
    }

    // ─── PO ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that generates keys from po msgids.
    /// </summary>
    [TestMethod]
    public void GeneratesKeys_FromPoMsgids() {
        const string po = """
            msgid "App.Title"
            msgstr "Title"

            msgid "Cart.Items"
            msgstr "Items"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        sources[0].Should().Contain("""public const string APP_TITLE = "App.Title";""");
        sources[0].Should().Contain("""public const string CART_ITEMS = "Cart.Items";""");
    }

    /// <summary>
    /// Verifies that po fuzzy entries are skipped.
    /// </summary>
    [TestMethod]
    public void PoFuzzyEntries_AreSkipped() {
        const string po = """
            #, fuzzy
            msgid "Draft"
            msgstr "Brouillon"

            msgid "Final"
            msgstr "Final-fr"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty("skipped fuzzy entries are not errors");
        sources[0].Should().Contain("""public const string FINAL = "Final";""");
        sources[0].Should().NotContain("DRAFT");
    }

    /// <summary>
    /// Verifies that po header and obsolete entries are skipped.
    /// </summary>
    [TestMethod]
    public void PoHeaderAndObsoleteEntries_AreSkipped() {
        const string po = """
            msgid ""
            msgstr "Project-Id-Version: x\n"

            #~ msgid "Old"
            #~ msgstr "Vieux"

            msgid "Live"
            msgstr "En vie"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("""public const string LIVE = "Live";""");
        sources[0].Should().NotContain("Old");
    }

    /// <summary>
    /// Verifies that po plural entry contributes singular once.
    /// </summary>
    [TestMethod]
    public void PoPluralEntry_ContributesSingularOnce() {
        const string po = """
            msgid "Book"
            msgid_plural "Books"
            msgstr[0] "Livre"
            msgstr[1] "Livres"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("""public const string BOOK = "Book";""");
        sources[0].Should().NotContain("Books");
    }

    /// <summary>
    /// Verifies that po context variants share msgid constant.
    /// </summary>
    [TestMethod]
    public void PoContextVariants_ShareMsgidConstant() {
        const string po = """
            msgctxt "menu"
            msgid "Open"
            msgstr "Ouvrir"

            msgctxt "order"
            msgid "Open"
            msgstr "Ouvert"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty("contextual variants share one constant without a duplicate error");
        sources[0].Should().Contain("""public const string OPEN = "Open";""");
    }

    /// <summary>
    /// Verifies that po continuation lines concatenate.
    /// </summary>
    [TestMethod]
    public void PoContinuationLines_Concatenate() {
        const string po = """
            msgid "Lon"
            "g Key"
            msgstr "Longue"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("Long Key");
    }

    /// <summary>
    /// Verifies that malformed po reports JLNKEY008 and emits nothing.
    /// </summary>
    [TestMethod]
    public void MalformedPo_ReportsJLNKEY008_AndEmitsNothing() {
        const string po = """
            msgstr "orphan"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        sources.Should().BeEmpty("the generator never emits on malformed content");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY008");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.GetLineSpan().Path.Should().Be("/catalogs/strings.po");
    }

    /// <summary>
    /// Verifies that po duplicate msgid reports error.
    /// </summary>
    [TestMethod]
    public void PoDuplicateMsgid_ReportsError() {
        const string po = """
            msgid "A"
            msgstr "first"

            msgid "A"
            msgstr "second"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.po", po)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY003");
    }

    // ─── XLIFF ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that generates keys from xliff unit ids.
    /// </summary>
    [TestMethod]
    public void GeneratesKeys_FromXliffUnitIds() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="2.0" xmlns="urn:oasis:names:tc:xliff:document:2.0" srcLang="en" trgLang="fr">
              <file id="f1">
                <unit id="App.Title">
                  <segment><source>Title</source><target>Titre</target></segment>
                </unit>
                <unit id="Cart.Items">
                  <segment><source>Items</source></segment>
                </unit>
              </file>
            </xliff>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.xliff", xliff)]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        sources[0].Should().Contain("""public const string APP_TITLE = "App.Title";""");
        sources[0].Should().Contain("""public const string CART_ITEMS = "Cart.Items";""");
    }

    /// <summary>
    /// Verifies that xliff missing id reports JLNKEY009.
    /// </summary>
    [TestMethod]
    public void XliffMissingId_ReportsJLNKEY009() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="2.0" xmlns="urn:oasis:names:tc:xliff:document:2.0" srcLang="en">
              <file id="f1">
                <unit>
                  <segment><source>Title</source></segment>
                </unit>
              </file>
            </xliff>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.xliff", xliff)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY009");
    }

    /// <summary>
    /// Verifies that xliff 1x reports JLNKEY009.
    /// </summary>
    [TestMethod]
    public void Xliff1x_ReportsJLNKEY009() {
        const string xliff = """
            <?xml version="1.0" encoding="utf-8"?>
            <xliff version="1.2">
              <file><body><trans-unit id="A"><source>a</source></trans-unit></body></file>
            </xliff>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.xliff", xliff)]);

        sources.Should().BeEmpty("a 1.x document must not silently yield an empty class");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY009");
    }

    /// <summary>
    /// Verifies that malformed xliff reports JLNKEY009 and emits nothing.
    /// </summary>
    [TestMethod]
    public void MalformedXliff_ReportsJLNKEY009_AndEmitsNothing() {
        var (sources, diagnostics) = Drive([("/catalogs/strings.xlf", "<xliff><unit id=\"A\">")]);

        sources.Should().BeEmpty("the generator never emits on malformed content");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY009");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.GetLineSpan().Path.Should().Be("/catalogs/strings.xlf");
    }

    /// <summary>
    /// Verifies that xliff duplicate id reports error.
    /// </summary>
    [TestMethod]
    public void XliffDuplicateId_ReportsError() {
        const string xliff = """
            <xliff version="2.0" xmlns="urn:oasis:names:tc:xliff:document:2.0">
              <file id="f1">
                <unit id="A"><segment><source>a</source></segment></unit>
                <unit id="A"><segment><source>b</source></segment></unit>
              </file>
            </xliff>
            """;
        var (sources, diagnostics) = Drive([("/catalogs/strings.xliff", xliff)]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY003");
    }

    // ─── Cross-format ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies that union across formats emits one constant per key.
    /// </summary>
    [TestMethod]
    public void Union_AcrossFormats_EmitsOneConstantPerKey() {
        const string resx = """
            <root>
              <data name="Shared" xml:space="preserve"><value>shared</value></data>
              <data name="ResXOnly" xml:space="preserve"><value>x</value></data>
            </root>
            """;
        const string po = """
            msgid "Shared"
            msgstr "partagé"

            msgid "PoOnly"
            msgstr "y"
            """;
        var (sources, diagnostics) = Drive([
            ("/catalogs/en.loc.json", """{ "Shared": "en", "JsonOnly": "z" }"""),
            ("/catalogs/strings.resx", resx),
            ("/catalogs/strings.po", po),
            ("/catalogs/strings.xliff", """<xliff version="2.0"><file id="f"><unit id="XliffOnly"><segment><source>w</source></segment></unit></file></xliff>"""),
        ]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        sources[0].Should().Contain("""public const string SHARED = "Shared";""");
        sources[0].Should().Contain("JSON_ONLY");
        sources[0].Should().Contain("""public const string RES_X_ONLY = "ResXOnly";""");
        sources[0].Should().Contain("PO_ONLY");
        sources[0].Should().Contain("XLIFF_ONLY");
    }

    /// <summary>
    /// Verifies that suffix matching is case insensitive.
    /// </summary>
    [TestMethod]
    public void SuffixMatching_IsCaseInsensitive() {
        const string po = """
            msgid "Upper"
            msgstr "u"
            """;
        var (sources, diagnostics) = Drive([("/catalogs/STRINGS.PO", po)]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("""public const string UPPER = "Upper";""");
    }

    /// <summary>
    /// Drives the generator over in-memory additional files.
    /// </summary>
    /// <param name="files">The catalog files.</param>
    /// <returns>The drive result.</returns>
    private static (IReadOnlyList<string> Sources, IReadOnlyList<Diagnostic> Diagnostics) Drive(
        IReadOnlyList<(string Path, string Text)> files) {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("public sealed class Anchor {}", path: "Anchor.cs")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = files.Select(static file => (AdditionalText)new TestAdditionalText(file.Path, file.Text)).ToImmutableArray();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LocalizationKeysGenerator().AsSourceGenerator()],
            texts);

        driver = driver.RunGenerators(compilation);
        var run = driver.GetRunResult();
        var sources = run.Results
            .SelectMany(static result => result.GeneratedSources)
            .Select(static source => source.SourceText.ToString())
            .ToList();
        return (sources, [.. run.Diagnostics]);
    }

    /// <summary>
    /// Represents a test additional text.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="text">The text.</param>
    private sealed class TestAdditionalText(string path, string text) : AdditionalText {
        /// <summary>
        /// Gets the path.
        /// </summary>
        public override string Path => path;

        /// <inheritdoc/>
        public override SourceText? GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text, Encoding.UTF8);
    }
}
