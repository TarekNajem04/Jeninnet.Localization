//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Analyzer rule tests for JLNLOC002–JLNLOC006: concatenated fragments (with the
/// interpolation code fix), non-localized interpolations, catalog-backed placeholder
/// mismatches, missing keys, and unused keys. Catalog-backed rules read
/// in-memory AdditionalFiles through the same compilation path as real catalogs.
/// </summary>
[TestClass]
public sealed class LocalizabilityNewRulesTests {
    /// <summary>
    /// Verifies that concatenated fragments in sink are flagged with fix.
    /// </summary>
    [TestMethod]
    public async Task ConcatenatedFragments_InSink_FlaggedWithFixAsync() {
        const string source = """
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                }
            }
            public static class Consumer {
                public static void Run(System.Windows.Forms.Control control, string name) {
                    control.Text = "Hello " + name + "!";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().BeEquivalentTo("JLNLOC001", "JLNLOC002");
        var concat = diagnostics.Single(static diagnostic => diagnostic.Id == "JLNLOC002");
        concat.Severity.Should().Be(DiagnosticSeverity.Warning);
        concat.GetMessage(CultureInfo.InvariantCulture).Should().Contain("interpolated");
    }

    /// <summary>
    /// Verifies that concatenated local is quiet.
    /// </summary>
    [TestMethod]
    public async Task ConcatenatedLocal_IsQuietAsync() {
        const string source = """
            public static class Consumer {
                public static string Run(string name) {
                    var text = "Hello " + name;
                    return text;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("locals are not user-facing sinks");
    }

    /// <summary>
    /// Verifies that concatenated throw is flagged.
    /// </summary>
    [TestMethod]
    public async Task ConcatenatedThrow_IsFlaggedAsync() {
        const string source = """
            using System;
            public static class Consumer {
                public static void Run(bool fail, string name) {
                    if(fail) {
                        throw new InvalidOperationException("Bad " + name);
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("JLNLOC002");
    }

    /// <summary>
    /// Verifies that interpolated sink is flagged.
    /// </summary>
    [TestMethod]
    public async Task InterpolatedSink_IsFlaggedAsync() {
        const string source = """
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                }
            }
            public static class Consumer {
                public static void Run(System.Windows.Forms.Control control, string name) {
                    control.Text = $"Hello {name}!";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().BeEquivalentTo("JLNLOC001", "JLNLOC003");
        diagnostics.Single(static diagnostic => diagnostic.Id == "JLNLOC003").Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Verifies that interpolated local is quiet.
    /// </summary>
    [TestMethod]
    public async Task InterpolatedLocal_IsQuietAsync() {
        const string source = """
            public static class Consumer {
                public static string Run(string name) {
                    return $"Hello {name}!";
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().BeEmpty("locals are not user-facing sinks");
    }

    /// <summary>
    /// Verifies that placeholder mismatch is flagged.
    /// </summary>
    [TestMethod]
    public async Task PlaceholderMismatch_IsFlaggedAsync() {
        const string source = """
            public static class SampleKeys {
                public const string CART = "Cart.Add";
            }
            public static class Consumer {
                public static string Run(string name, int count) {
                    return string.Format(SampleKeys.CART, name);
                }
            }
            """;
        const string catalog = """{"Cart.Add": "Add {0} to {1}."}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        var mismatch = diagnostics.Single(static diagnostic => diagnostic.Id == "JLNLOC004");
        mismatch.Severity.Should().Be(DiagnosticSeverity.Warning);
        mismatch.GetMessage(CultureInfo.InvariantCulture).Should().Contain("{1}");
        mismatch.GetMessage(CultureInfo.InvariantCulture).Should().Contain("Cart.Add");
    }

    /// <summary>
    /// Verifies that named placeholder mismatch is flagged.
    /// </summary>
    [TestMethod]
    public async Task NamedPlaceholderMismatch_IsFlaggedAsync() {
        const string source = """
            public static class SampleKeys {
                public const string GREET = "App.Greet";
            }
            public static class Consumer {
                public static string Run(string name) {
                    return string.Format(SampleKeys.GREET, name);
                }
            }
            """;
        const string catalog = """{"App.Greet": "Hello {name}!"}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("JLNLOC004");
    }

    /// <summary>
    /// Verifies that matching placeholders are quiet.
    /// </summary>
    [TestMethod]
    public async Task MatchingPlaceholders_AreQuietAsync() {
        const string source = """
            public static class SampleKeys {
                public const string GREET = "App.Greet";
            }
            public static class Consumer {
                public static string Run(string name) {
                    return string.Format(SampleKeys.GREET, name);
                }
            }
            """;
        const string catalog = """{"App.Greet": "Hello {0}!"}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().NotContain("JLNLOC004");
    }

    /// <summary>
    /// Verifies that placeholder check needs catalog visibility.
    /// </summary>
    [TestMethod]
    public async Task PlaceholderCheck_WithoutCatalog_IsSilentAsync() {
        const string source = """
            public static class Consumer {
                public static string Run(string name) {
                    return string.Format("Hello {0} {1}", name);
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().NotContain("JLNLOC004");
    }

    /// <summary>
    /// Verifies that missing key reference is flagged.
    /// </summary>
    [TestMethod]
    public async Task MissingKeyReference_IsFlaggedAsync() {
        const string source = """
            public static class SampleKeys {
                public const string GHOST = "Ghost.Key";
                public const string KNOWN = "App.Known";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.GHOST + SampleKeys.KNOWN;
                }
            }
            """;
        const string catalog = """{"App.Known": "Known"}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        var missing = diagnostics.Where(static diagnostic => diagnostic.Id == "JLNLOC005").ToArray();
        missing.Should().HaveCount(1);
        missing[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        missing[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("Ghost.Key");
    }

    /// <summary>
    /// Verifies that missing key needs catalog visibility.
    /// </summary>
    [TestMethod]
    public async Task MissingKey_WithoutCatalog_IsSilentAsync() {
        const string source = """
            public static class SampleKeys {
                public const string GHOST = "Ghost.Key";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.GHOST;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().NotContain("JLNLOC005");
    }

    /// <summary>
    /// Verifies that key position literal missing is flagged.
    /// </summary>
    [TestMethod]
    public async Task KeyPositionLiteralMissing_IsFlaggedAsync() {
        const string source = """
            using System.Globalization;
            using Jeninnet.Localization.Abstractions;
            public static class Consumer {
                public static string Run(IInnerMessageProvider provider) {
                    return provider.GetMessageTemplateAsync("Ghost.Key", CultureInfo.InvariantCulture).AsTask().GetAwaiter().GetResult() ?? "";
                }
            }
            """;
        const string catalog = """{"App.Known": "Known"}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("JLNLOC005");
    }

    /// <summary>
    /// Verifies that unused keys are flagged on catalog lines.
    /// </summary>
    [TestMethod]
    public async Task UnusedKeys_AreFlaggedOnCatalogLinesAsync() {
        const string source = """
            public static class SampleKeys {
                public const string USED = "App.Used";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.USED;
                }
            }
            """;
        const string catalog = """
            {"App.Unused": "Unused",
            "App.Used": "Used"}
            """;

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        var unused = diagnostics.Single(static diagnostic => diagnostic.Id == "JLNLOC006");
        unused.Severity.Should().Be(DiagnosticSeverity.Warning);
        unused.GetMessage(CultureInfo.InvariantCulture).Should().Contain("App.Unused");
        unused.Location.GetLineSpan().StartLinePosition.Line.Should().Be(0, "the unused key sits on catalog line 1");
    }

    /// <summary>
    /// Verifies that fully referenced catalog is quiet for unused.
    /// </summary>
    [TestMethod]
    public async Task FullyReferencedCatalog_IsQuietForUnusedAsync() {
        const string source = """
            public static class SampleKeys {
                public const string USED = "App.Used";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.USED;
                }
            }
            """;
        const string catalog = """{"App.Used": "Used"}""";

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", catalog));

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().NotContain("JLNLOC006");
    }

    /// <summary>
    /// Verifies that malformed catalog is silent.
    /// </summary>
    [TestMethod]
    public async Task MalformedCatalog_IsSilentAsync() {
        const string source = """
            public static class SampleKeys {
                public const string GHOST = "Ghost.Key";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.GHOST;
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source, ("en.loc.json", "{oops"));

        diagnostics.Should().BeEmpty("a malformed catalog contributes no findings");
    }

    /// <summary>
    /// Verifies that resx catalog keys participate.
    /// </summary>
    [TestMethod]
    public async Task ResxCatalog_KeysParticipateAsync() {
        const string source = """
            public static class SampleKeys {
                public const string TITLE = "App.Title";
                public const string GHOST = "Ghost.Key";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.TITLE + SampleKeys.GHOST;
                }
            }
            """;
        const string catalog = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="App.Title" xml:space="preserve">
                <value>Title</value>
              </data>
            </root>
            """;

        var diagnostics = await AnalyzeAsync(source, ("strings.resx", catalog));

        diagnostics.Select(static diagnostic => diagnostic.Id).Should().Contain("JLNLOC005");
        diagnostics.Select(static diagnostic => diagnostic.Id).Should().NotContain("JLNLOC006", "the used key is referenced");
    }

    /// <summary>
    /// Verifies that po catalog keys participate.
    /// </summary>
    [TestMethod]
    public async Task PoCatalog_KeysParticipateAsync() {
        const string source = """
            public static class SampleKeys {
                public const string ADD = "Cart.Add";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.ADD;
                }
            }
            """;
        const string catalog = """
            msgid "Cart.Add"
            msgstr "Add"
            """;

        var diagnostics = await AnalyzeAsync(source, ("messages.po", catalog));

        diagnostics.Should().BeEmpty("the key is served and referenced, and the catalog has no unused keys");
    }

    /// <summary>
    /// Verifies that xliff catalog keys participate.
    /// </summary>
    [TestMethod]
    public async Task XliffCatalog_KeysParticipateAsync() {
        const string source = """
            public static class SampleKeys {
                public const string TITLE = "App.Title";
            }
            public static class Consumer {
                public static string Run() {
                    return SampleKeys.TITLE;
                }
            }
            """;
        const string catalog = """
            <xliff version="2.0" srcLang="en"><file id="f"><unit id="App.Title"><segment><source>Title</source></segment></unit><unit id="App.Orphan"><segment><source>Orphan</source></segment></unit></file></xliff>
            """;

        var diagnostics = await AnalyzeAsync(source, ("messages.xliff", catalog));

        var unused = diagnostics.Single(static diagnostic => diagnostic.Id == "JLNLOC006");
        unused.GetMessage(CultureInfo.InvariantCulture).Should().Contain("App.Orphan");
    }

    /// <summary>
    /// Performs the analyze operation.
    /// </summary>
    /// <param name="source">The C# source.</param>
    /// <param name="catalogs">The catalog files.</param>
    /// <returns>A task whose result is analyze.</returns>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params (string Path, string Text)[] catalogs) {
        var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "LocalizabilityReview",
            [tree],
            TestReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new LocalizabilityAnalyzer();
        var additionalFiles = catalogs
            .Select(static catalog => (AdditionalText)new TestAdditionalText(catalog.Path, catalog.Text))
            .ToImmutableArray<AdditionalText>();
        var withAnalyzers = compilation.WithAnalyzers([analyzer], new AnalyzerOptions(additionalFiles));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Performs the test references operation.
    /// </summary>
    /// <returns>The test references result.</returns>
    private static ImmutableArray<MetadataReference> TestReferences() {
        var assemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?.Split(Path.PathSeparator);
        if(assemblies is null) {
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        return [.. assemblies
            .Where(static path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(static path => MetadataReference.CreateFromFile(path))];
    }

    /// <summary>
    /// Test additional text.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="text">The text.</param>
    private sealed class TestAdditionalText(string path, string text) : AdditionalText {
        /// <inheritdoc/>
        public override string Path { get; } = path;

        /// <inheritdoc/>
        public override SourceText? GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text, Encoding.UTF8);
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
