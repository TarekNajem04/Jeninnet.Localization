//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Roslyn-harness unit tests for the localization-keys generator: happy-path
/// emission, byte-identical determinism, incremental tracking, malformed-content
/// errors with file/line diagnostics, identifier rules, and namespace handling.
/// </summary>
[TestClass]
public sealed class LocalizationKeysGeneratorTests {
    /// <summary>
    /// Verifies that generates constants for flat catalog.
    /// </summary>
    [TestMethod]
    public void GeneratesConstants_ForFlatCatalog() {
        var (sources, diagnostics, run) = Drive(
            [("/catalogs/en.loc.json", """{ "App.Title": "Title", "Catalog.Heading": "Heading" }""")]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        run.Results[0].GeneratedSources[0].HintName.Should().Be("LocalizationKeys.g.cs");

        var source = sources[0];
        source.Should().Contain("namespace TestAssembly.Generated;");
        source.Should().Contain("""public const string APP_TITLE = "App.Title";""");
        source.Should().Contain("""public const string CATALOG_HEADING = "Catalog.Heading";""");
        source.Should().Contain("public static readonly string[] All = new string[] {\"App.Title\", \"Catalog.Heading\"};");
    }

    /// <summary>
    /// Verifies that output is byte identical across runs and file order.
    /// </summary>
    [TestMethod]
    public void Output_IsByteIdentical_AcrossRuns_AndFileOrder() {
        var firstFiles = new List<(string Path, string Text)> {
            ("/catalogs/en.loc.json", """{ "B.Key": "b", "A.Key": "a" }"""),
            ("/catalogs/ar.loc.json", """{ "A.Key": "a-ar", "C.Key": "c" }"""),
        };
        var secondFiles = new List<(string Path, string Text)> {
            ("/catalogs/ar.loc.json", """{ "A.Key": "a-ar", "C.Key": "c" }"""),
            ("/catalogs/en.loc.json", """{ "B.Key": "b", "A.Key": "a" }"""),
        };

        var (first, firstDiagnostics, _) = Drive(firstFiles);
        var (second, secondDiagnostics, _) = Drive(secondFiles);

        firstDiagnostics.Should().BeEmpty();
        secondDiagnostics.Should().BeEmpty();
        first.Should().HaveCount(1);
        first[0].Should().Be(second[0], "output is deterministic regardless of file order");
    }

    /// <summary>
    /// Verifies that union across cultures emits one constant per key.
    /// </summary>
    [TestMethod]
    public void Union_AcrossCultures_EmitsOneConstantPerKey() {
        var (sources, diagnostics, _) = Drive([
            ("/catalogs/en.loc.json", """{ "Shared": "en", "EnOnly": "x" }"""),
            ("/catalogs/ar.loc.json", """{ "Shared": "ar", "ArOnly": "y" }"""),
        ]);

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("""public const string SHARED = "Shared";""");
        sources[0].Should().Contain("""public const string EN_ONLY = "EnOnly";""");
        sources[0].Should().Contain("""public const string AR_ONLY = "ArOnly";""");
    }

    /// <summary>
    /// Verifies that malformed json reports error and emits nothing.
    /// </summary>
    [TestMethod]
    public void MalformedJson_ReportsError_AndEmitsNothing() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", "{ \"A\": ")]);

        sources.Should().BeEmpty("the generator never emits on malformed content");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].Location.SourceTree.Should().BeNull();
        diagnostics[0].Location.GetLineSpan().Path.Should().Be("/catalogs/en.loc.json");
    }

    /// <summary>
    /// Verifies that non string value reports error with line.
    /// </summary>
    [TestMethod]
    public void NonStringValue_ReportsErrorWithLine() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", "{\n  \"Count\": 5\n}")]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        var position = diagnostics[0].Location.GetLineSpan().StartLinePosition;
        position.Line.Should().Be(1, "the diagnostic points at the offending value line (0-based 1)");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Count");
    }

    /// <summary>
    /// Verifies that duplicate key reports error.
    /// </summary>
    [TestMethod]
    public void DuplicateKey_ReportsError() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", """{ "A": "1", "A": "2" }""")]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY003");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("A");
    }

    /// <summary>
    /// Verifies that identifier collision reports error.
    /// </summary>
    [TestMethod]
    public void IdentifierCollision_ReportsError() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", """{ "a.b": "x", "a_b": "y" }""")]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY004");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("A_B");
    }

    /// <summary>
    /// Verifies that empty identifier reports error.
    /// </summary>
    [TestMethod]
    public void EmptyIdentifier_ReportsError() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", """{ "!!!": "x" }""")]);

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY005");
    }

    /// <summary>
    /// Verifies that custom namespace is honored.
    /// </summary>
    [TestMethod]
    public void CustomNamespace_Honored() {
        var (sources, diagnostics, _) = Drive(
            [("/catalogs/en.loc.json", """{ "A": "x" }""")],
            requestedNamespace: "My.App.Keys");

        diagnostics.Should().BeEmpty();
        sources[0].Should().Contain("namespace My.App.Keys;");
    }

    /// <summary>
    /// Verifies that invalid namespace reports error without location.
    /// </summary>
    [TestMethod]
    public void InvalidNamespace_ReportsErrorWithoutLocation() {
        var (sources, diagnostics, _) = Drive(
            [("/catalogs/en.loc.json", """{ "A": "x" }""")],
            requestedNamespace: "9bad name");

        sources.Should().BeEmpty();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("JLNKEY006");
        diagnostics[0].Location.IsInSource.Should().BeFalse("namespace misconfiguration has no file location");
    }

    /// <summary>
    /// Verifies that non catalog files is ignored.
    /// </summary>
    [TestMethod]
    public void NonCatalogFiles_Ignored() {
        var (sources, diagnostics, _) = Drive(
            [("/config/appsettings.json", "{ this is not even json")]);

        diagnostics.Should().BeEmpty("undeclared files are not content roots");
        sources.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that empty object emits empty class.
    /// </summary>
    [TestMethod]
    public void EmptyObject_EmitsEmptyClass() {
        var (sources, diagnostics, _) = Drive([("/catalogs/en.loc.json", "{}")]);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(1);
        sources[0].Should().Contain("public static readonly string[] All = new string[] {};");
    }

    /// <summary>
    /// Verifies that incremental pipeline reuses cached results.
    /// </summary>
    [TestMethod]
    public void IncrementalPipeline_ReusesCachedResults() {
        var files = new List<(string Path, string Text)> {
            ("/catalogs/en.loc.json", """{ "A": "x", "B": "y" }"""),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText("public sealed class Anchor {}", path: "Anchor.cs", cancellationToken: TestContext.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LocalizationKeysGenerator().AsSourceGenerator()],
            [.. files.Select(static file => (AdditionalText)new TestAdditionalText(file.Path, file.Text))],
            parseOptions: null,
            optionsProvider: new TestOptionsProvider(null));

        driver = driver.RunGenerators(compilation, TestContext.CancellationToken);
        var firstSources = driver.GetRunResult().Results[0].GeneratedSources
            .Select(static source => source.SourceText.ToString())
            .ToList();

        // Identical rerun on the same driver: cached results, byte-identical output.
        driver = driver.RunGenerators(compilation, TestContext.CancellationToken);
        var secondSources = driver.GetRunResult().Results[0].GeneratedSources
            .Select(static source => source.SourceText.ToString())
            .ToList();
        secondSources.Should().BeEquivalentTo(firstSources);

        // Unrelated syntax change with unchanged catalogs: output is unaffected.
        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public sealed class Bystander {}", path: "Bystander.cs", cancellationToken: TestContext.CancellationToken));
        driver = driver.RunGenerators(edited, TestContext.CancellationToken);
        var thirdSources = driver.GetRunResult().Results[0].GeneratedSources
            .Select(static source => source.SourceText.ToString())
            .ToList();
        thirdSources.Should().BeEquivalentTo(firstSources);
        thirdSources.Should().HaveCount(1);
    }

    /// <summary>
    /// Verifies that naming to identifier cases.
    /// </summary>
    [TestMethod]
    public void Naming_ToIdentifier_Cases() {
        LocalizationKeyNaming.ToIdentifier("App.Title").Should().Be("APP_TITLE");
        LocalizationKeyNaming.ToIdentifier("BookId").Should().Be("BOOK_ID");
        LocalizationKeyNaming.ToIdentifier("OrderID").Should().Be("ORDER_ID");
        LocalizationKeyNaming.ToIdentifier("CairoTrilogy").Should().Be("CAIRO_TRILOGY");
        LocalizationKeyNaming.ToIdentifier("9lives").Should().Be("_9LIVES");
        LocalizationKeyNaming.ToIdentifier("a-b_c").Should().Be("A_B_C");
        LocalizationKeyNaming.ToIdentifier(string.Empty).Should().Be(string.Empty);
        LocalizationKeyNaming.ToIdentifier("!!!").Should().Be(string.Empty);
    }

    /// <summary>
    /// Verifies that naming escape literal cases.
    /// </summary>
    [TestMethod]
    public void Naming_EscapeLiteral_Cases() {
        LocalizationKeyNaming.EscapeLiteral("a\"b\\c").Should().Be("a\\\"b\\\\c");
        LocalizationKeyNaming.EscapeLiteral("line\nbreak").Should().Be("line\\nbreak");
        LocalizationKeyNaming.EscapeLiteral("plain").Should().Be("plain");
    }

    /// <summary>
    /// Verifies that naming is valid namespace cases.
    /// </summary>
    [TestMethod]
    public void Naming_IsValidNamespace_Cases() {
        LocalizationKeyNaming.IsValidNamespace("My.App.Keys").Should().BeTrue();
        LocalizationKeyNaming.IsValidNamespace("9bad").Should().BeFalse();
        LocalizationKeyNaming.IsValidNamespace("has space").Should().BeFalse();
        LocalizationKeyNaming.IsValidNamespace(string.Empty).Should().BeFalse();
        LocalizationKeyNaming.IsValidNamespace(null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that parser unescapes and tracks lines.
    /// </summary>
    [TestMethod]
    public void Parser_Unescapes_AndTracksLines() {
        var parsed = LocJsonParser.Parse("{\n  \"A\\u0041\": \"x\\n😀\"\n}");

        parsed.SyntaxError.Should().BeNull();
        parsed.Entries.Should().HaveCount(1);
        parsed.Entries[0].Key.Should().Be("AA");
        parsed.Entries[0].Value.Should().Be("x\n😀");

        LocJsonParser.GetLineAndColumn("{\n  \"A", 4, out var line, out var column);
        line.Should().Be(2);
        column.Should().Be(3);
    }

    /// <summary>
    /// Verifies that parser rejects trailing comma and nested values.
    /// </summary>
    [TestMethod]
    public void Parser_RejectsTrailingComma_AndNestedValues() {
        var trailing = LocJsonParser.Parse("""{ "A": "x", }""");
        trailing.SyntaxError.Should().NotBeNull();

        var nested = LocJsonParser.Parse("""{ "A": { "B": "x" } }""");
        nested.SyntaxError.Should().BeNull();
        nested.Entries.Should().HaveCount(1);
        nested.Entries[0].IsStringValue.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that resolve namespace cases.
    /// </summary>
    [TestMethod]
    public void ResolveNamespace_Cases() {
        LocalizationKeysGenerator.ResolveNamespace("Custom.Ns", "Anything").Should().Be("Custom.Ns");
        LocalizationKeysGenerator.ResolveNamespace("  ", "My.App").Should().Be("My.App.Generated");
        LocalizationKeysGenerator.ResolveNamespace(string.Empty, "my-app").Should().Be("my_app.Generated");
        LocalizationKeysGenerator.ResolveNamespace(null, null).Should().Be("Localization.Generated");
        LocalizationKeysGenerator.ResolveNamespace("9bad", "My.App").Should().BeNull();
    }

    /// <summary>
    /// Performs the drive operation.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <param name="requestedNamespace">The requested namespace.</param>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The drive sequence.</returns>
    private static (IReadOnlyList<string> Sources, IReadOnlyList<Diagnostic> Diagnostics, GeneratorDriverRunResult Run) Drive(
        IReadOnlyList<(string Path, string Text)> files,
        string? requestedNamespace = null,
        string assemblyName = "TestAssembly") {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText("public sealed class Anchor {}", path: "Anchor.cs")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = files.Select(static file => (AdditionalText)new TestAdditionalText(file.Path, file.Text)).ToImmutableArray();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new LocalizationKeysGenerator().AsSourceGenerator()],
            texts,
            parseOptions: null,
            optionsProvider: new TestOptionsProvider(requestedNamespace));

        driver = driver.RunGenerators(compilation);
        var run = driver.GetRunResult();
        var reported = run.Diagnostics;
        var sources = run.Results
            .SelectMany(static result => result.GeneratedSources)
            .Select(static source => source.SourceText.ToString())
            .ToList();
        return (sources, [.. reported], run);
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

    /// <summary>
    /// Specifies the options for test global.
    /// </summary>
    /// <param name="requestedNamespace">The requested namespace.</param>
    private sealed class TestGlobalOptions(string? requestedNamespace) : AnalyzerConfigOptions {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) {
            if(string.Equals(key, LocalizationKeysGenerator.NAMESPACE_PROPERTY, StringComparison.Ordinal) && requestedNamespace is not null) {
                value = requestedNamespace;
                return true;
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// Provides test options.
    /// </summary>
    /// <param name="requestedNamespace">The requested namespace.</param>
    private sealed class TestOptionsProvider(string? requestedNamespace) : AnalyzerConfigOptionsProvider {
        /// <summary>
        /// Gets the global options.
        /// </summary>
        public override AnalyzerConfigOptions GlobalOptions => new TestGlobalOptions(requestedNamespace);

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestGlobalOptions(null);

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestGlobalOptions(null);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
