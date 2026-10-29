//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests;

/// <summary>
/// Code-fix end-to-end tests through a real workspace: a flagged literal
/// matching a <c>*Keys</c> constant is rewritten to that constant, and literals
/// with no match get no fix. This is the  synergy made mechanical.
/// </summary>
[TestClass]
public sealed class LocalizabilityCodeFixTests {
    /// <summary>
    /// Verifies that fix replaces literal with matching keys constant.
    /// </summary>
    [TestMethod]
    public async Task Fix_ReplacesLiteral_WithMatchingKeysConstantAsync() {
        const string source = """
            namespace App.Strings {
                public static class AppKeys {
                    public const string SAVE = "Save";
                }
            }
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                }
            }
            public static class Consumer {
                public static void Run(System.Windows.Forms.Control control) {
                    control.Text = "Save";
                }
            }
            """;

        var changed = await ApplyFixAsync(source);

        changed.Should().NotBeNull("a matching constant offers the fix");
        changed.Should().Contain("control.Text = App.Strings.AppKeys.SAVE;");
    }

    /// <summary>
    /// Verifies that fix offers nothing without matching constant.
    /// </summary>
    [TestMethod]
    public async Task Fix_OffersNothing_WithoutMatchingConstantAsync() {
        const string source = """
            namespace System.Windows.Forms {
                public class Control {
                    public string Text { get; set; } = "";
                }
            }
            public static class Consumer {
                public static void Run(System.Windows.Forms.Control control) {
                    control.Text = "Unmatched";
                }
            }
            """;

        var changed = await ApplyFixAsync(source);

        changed.Should().BeNull("literals with no matching constant keep their diagnostic");
    }

    /// <summary>
    /// Verifies that find constant picks first ordinal match.
    /// </summary>
    [TestMethod]
    public void FindConstant_PicksFirstOrdinalMatch() {
        var tree = CSharpSyntaxTree.ParseText(
            """
            public static class BKeys {
                public const string SAVE = "Save";
            }
            public static class AKeys {
                public const string SAVE = "Save";
                public const string OTHER = "Save";
            }
            """,
            path: "Keys.cs", cancellationToken: TestContext.CancellationToken);
        var compilation = CSharpCompilation.Create(
            "FixLookup",
            [tree],
            TestReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var match = LocalizabilityCodeFixProvider.FindConstant(compilation, "Save");

        match.Should().NotBeNull();
        match!.QualifiedName.Should().Be("AKeys.OTHER", "ordinal-first match is deterministic");
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
    /// Runs the localizability analyzer and its code fix over the supplied source, returning the fixed text.
    /// </summary>
    /// <param name="source">The C# source to analyze and fix.</param>
    /// <returns>The fixed source, or <see langword="null"/> when the analyzer or fix produces no change.</returns>
    private static async Task<string?> ApplyFixAsync(string source) {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("FixTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(TestReferences());
        var document = project.AddDocument("Test.cs", SourceText.From(source, Encoding.UTF8));

        var compilation = await document.Project.GetCompilationAsync() ?? throw new InvalidOperationException("the fix workspace must produce a compilation");
        var analyzer = new Jeninnet.Localization.Analyzers.Localizability.LocalizabilityAnalyzer();
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.FirstOrDefault(static candidate => candidate.Id == "JLNLOC001");
        if(diagnostic is null) {
            return null;
        }

        var fix = new LocalizabilityCodeFixProvider();
        var actions = new List<CodeAction>();
        await fix.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        if(actions.Count == 0) {
            return null;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(document.Id) ?? throw new InvalidOperationException("the fix must preserve the document");
        return (await changedDocument.GetTextAsync()).ToString();
    }

    /// <summary>
    /// Gets or sets the MSTest execution context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;
}
